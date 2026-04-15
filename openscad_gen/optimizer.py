from __future__ import annotations

import heapq
import logging
from pathlib import Path
from typing import Callable

import numpy as np
from scipy.ndimage import binary_closing, binary_opening, convolve
from scipy.spatial import cKDTree
from scipy.sparse import coo_matrix, csr_matrix

from .fem import FemContext, FemResult, build_fem_context, solve_density_fem
from .geometry import connector_is_connected, dilate_mask
from .models import DEFAULT_MATERIALS, IterationMetrics, VoxelScene
from .visualize import render_scalar_voxel_state, render_voxel_state

ProgressCallback = Callable[[list[IterationMetrics], dict], None]


_NEIGHBORS6 = ((-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1), (0, 0, 1))


def _emit(progress_callback: ProgressCallback | None, metrics: list[IterationMetrics], payload: dict) -> None:
    if progress_callback is not None:
        progress_callback(metrics, payload)


def _build_filter_matrix(ctx: FemContext, voxel_scene: VoxelScene, radius_voxels: float) -> tuple[csr_matrix, np.ndarray]:
    if ctx.design_elem_ids.size == 0:
        return csr_matrix((0, 0), dtype=float), np.zeros(0, dtype=float)
    centers = ctx.element_voxels[ctx.design_elem_ids].astype(float)
    radius = max(1.0, float(radius_voxels))
    tree = cKDTree(centers)
    rows: list[int] = []
    cols: list[int] = []
    data: list[float] = []
    for row, point in enumerate(centers):
        neighbors = tree.query_ball_point(point, radius)
        for col in neighbors:
            dist = float(np.linalg.norm(point - centers[col]))
            weight = max(0.0, radius - dist)
            if weight > 0.0:
                rows.append(row)
                cols.append(col)
                data.append(weight)
    h = coo_matrix((data, (rows, cols)), shape=(ctx.design_elem_ids.size, ctx.design_elem_ids.size)).tocsr()
    hs = np.asarray(h.sum(axis=1)).ravel()
    hs = np.maximum(hs, 1e-9)
    return h, hs


def _project_density(rho_tilde: np.ndarray, beta: float, eta: float) -> tuple[np.ndarray, np.ndarray]:
    beta = float(max(beta, 1e-6))
    eta = float(np.clip(eta, 1e-6, 1.0 - 1e-6))
    denom = np.tanh(beta * eta) + np.tanh(beta * (1.0 - eta))
    rho_phys = (np.tanh(beta * eta) + np.tanh(beta * (rho_tilde - eta))) / denom
    dproj = (beta * (1.0 - np.tanh(beta * (rho_tilde - eta)) ** 2)) / denom
    return rho_phys, dproj


def _rho_grid(voxel_scene: VoxelScene, ctx: FemContext, rho_phys: np.ndarray) -> np.ndarray:
    grid = np.zeros(voxel_scene.grid.shape, dtype=float)
    if rho_phys.size:
        ijk = ctx.element_voxels[ctx.design_elem_ids]
        grid[ijk[:, 0], ijk[:, 1], ijk[:, 2]] = np.clip(rho_phys, 0.0, 1.0)
    return grid


def _frontier_mask(design_mask: np.ndarray, solid_mask: np.ndarray) -> np.ndarray:
    return design_mask & dilate_mask(solid_mask.astype(bool), steps=1) & ~solid_mask


def _shortest_density_path(
    design_mask: np.ndarray,
    rho_grid: np.ndarray,
    source_mask: np.ndarray,
    target_mask: np.ndarray,
) -> np.ndarray:
    sources = [tuple(int(v) for v in idx) for idx in np.argwhere(source_mask)]
    targets = target_mask.astype(bool)
    if not sources or not targets.any():
        return np.zeros_like(design_mask, dtype=bool)

    shape = design_mask.shape
    inf = float("inf")
    dist = np.full(shape, inf, dtype=float)
    prev = np.full(shape + (3,), -1, dtype=int)
    heap: list[tuple[float, int, int, int]] = []

    for i, j, k in sources:
        base_cost = 1.0 / max(float(rho_grid[i, j, k]), 1e-6)
        dist[i, j, k] = base_cost
        heapq.heappush(heap, (base_cost, i, j, k))

    found: tuple[int, int, int] | None = None
    nx, ny, nz = shape
    while heap:
        cur, i, j, k = heapq.heappop(heap)
        if cur != dist[i, j, k]:
            continue
        if targets[i, j, k]:
            found = (i, j, k)
            break
        for di, dj, dk in _NEIGHBORS6:
            ni, nj, nk = i + di, j + dj, k + dk
            if 0 <= ni < nx and 0 <= nj < ny and 0 <= nk < nz and design_mask[ni, nj, nk]:
                step = 1.0 / max(float(rho_grid[ni, nj, nk]), 1e-6)
                nd = cur + step
                if nd < dist[ni, nj, nk]:
                    dist[ni, nj, nk] = nd
                    prev[ni, nj, nk] = (i, j, k)
                    heapq.heappush(heap, (nd, ni, nj, nk))

    mask = np.zeros_like(design_mask, dtype=bool)
    if found is None:
        return mask

    i, j, k = found
    while True:
        mask[i, j, k] = True
        pi, pj, pk = prev[i, j, k]
        if pi < 0:
            break
        i, j, k = int(pi), int(pj), int(pk)
    return mask


def _grow_connected_region(
    design_mask: np.ndarray,
    rho_grid: np.ndarray,
    seed_mask: np.ndarray,
    target_voxels: int,
) -> np.ndarray:
    keep = seed_mask.copy()
    if target_voxels <= int(keep.sum()):
        return keep

    nx, ny, nz = design_mask.shape
    seen = keep.copy()
    heap: list[tuple[float, int, int, int]] = []

    def push_neighbors(i: int, j: int, k: int) -> None:
        for di, dj, dk in _NEIGHBORS6:
            ni, nj, nk = i + di, j + dj, k + dk
            if 0 <= ni < nx and 0 <= nj < ny and 0 <= nk < nz and design_mask[ni, nj, nk] and not seen[ni, nj, nk]:
                seen[ni, nj, nk] = True
                heapq.heappush(heap, (-float(rho_grid[ni, nj, nk]), ni, nj, nk))

    for i, j, k in np.argwhere(keep):
        push_neighbors(int(i), int(j), int(k))

    while int(keep.sum()) < target_voxels and heap:
        _, i, j, k = heapq.heappop(heap)
        if keep[i, j, k]:
            continue
        keep[i, j, k] = True
        push_neighbors(i, j, k)
    return keep


def _prune_sparse_voxels(mask: np.ndarray, mandatory: np.ndarray, min_neighbors: int) -> np.ndarray:
    if min_neighbors <= 0 or not mask.any():
        return mask
    kernel = np.zeros((3, 3, 3), dtype=int)
    kernel[1, 1, 0] = kernel[1, 1, 2] = 1
    kernel[1, 0, 1] = kernel[1, 2, 1] = 1
    kernel[0, 1, 1] = kernel[2, 1, 1] = 1
    out = mask.copy()
    for _ in range(2):
        counts = convolve(out.astype(int), kernel, mode="constant", cval=0)
        keep = (counts >= int(min_neighbors)) | mandatory
        new_out = out & keep
        if np.array_equal(new_out, out):
            break
        out = new_out
    return out


def _densities_to_mask(voxel_scene: VoxelScene, ctx: FemContext, rho_phys: np.ndarray) -> np.ndarray:
    cfg = voxel_scene.scene.config
    design_mask = voxel_scene.design_mask
    mandatory = voxel_scene.mandatory_design_mask
    rho_grid = _rho_grid(voxel_scene, ctx, rho_phys)

    anchor_attachment = mandatory & dilate_mask(voxel_scene.anchor_mask.astype(bool), steps=1)
    load_attachment = mandatory & dilate_mask(voxel_scene.load_mask.astype(bool), steps=1)
    source_frontier = _frontier_mask(design_mask, voxel_scene.anchor_mask) | anchor_attachment
    target_frontier = _frontier_mask(design_mask, voxel_scene.load_mask) | load_attachment
    path_mask = _shortest_density_path(design_mask, rho_grid, source_frontier, target_frontier)

    target_voxels = max(int(mandatory.sum()), int(round(float(rho_phys.sum()))))
    keep = mandatory | path_mask
    keep &= design_mask
    keep = _grow_connected_region(design_mask, rho_grid, keep, target_voxels)

    structure = np.ones((3, 3, 3), dtype=bool)
    if int(cfg.post_smooth_passes) > 0 and keep.any():
        candidate = keep.copy()
        for _ in range(int(cfg.post_smooth_passes)):
            candidate = binary_closing(candidate, structure=structure)
            candidate = binary_opening(candidate, structure=structure)
            candidate |= keep
            candidate &= design_mask
        if connector_is_connected(voxel_scene.anchor_mask, voxel_scene.load_mask, candidate, voxel_scene.preserve_mask):
            keep = candidate

    keep = _prune_sparse_voxels(keep, mandatory | path_mask, int(cfg.min_connector_neighbors))
    keep |= mandatory | path_mask
    keep &= design_mask

    if not connector_is_connected(voxel_scene.anchor_mask, voxel_scene.load_mask, keep, voxel_scene.preserve_mask):
        keep |= path_mask
        keep &= design_mask
    return keep


def _render_iteration(
    path_prefix: str,
    frames_dir: Path,
    voxel_scene: VoxelScene,
    mask: np.ndarray,
    fem_result: FemResult,
    title: str,
    subtitle: str,
) -> tuple[Path, Path]:
    frame = frames_dir / f"{path_prefix}.png"
    stress = frames_dir / f"{path_prefix}_stress.png"
    render_voxel_state(frame, voxel_scene, mask, title, subtitle)
    render_scalar_voxel_state(stress, voxel_scene, mask, fem_result.active_ids, fem_result.connector_vm, f"{title} — stress", subtitle)
    return frame, stress


def optimize_connector(
    voxel_scene: VoxelScene,
    frames_dir: Path,
    logger: logging.Logger,
    progress_callback: ProgressCallback | None = None,
) -> tuple[np.ndarray, FemResult, list[IterationMetrics]]:
    cfg = voxel_scene.scene.config
    ctx = build_fem_context(voxel_scene, logger)
    h, hs = _build_filter_matrix(ctx, voxel_scene, cfg.filter_radius)

    connector_material = DEFAULT_MATERIALS[cfg.connector_material]
    allowable = connector_material.yield_strength / float(cfg.safety_factor)

    n_design = ctx.design_count
    mandatory_mask_design = np.isin(ctx.design_elem_ids, ctx.mandatory_elem_ids)
    free_mask_design = ~mandatory_mask_design
    target_mass = float(cfg.target_volume_ratio) * float(n_design)
    free_count = int(np.count_nonzero(free_mask_design))
    mandatory_count = int(np.count_nonzero(mandatory_mask_design))
    if target_mass < mandatory_count:
        logger.warning(
            "Requested target volume ratio %.3f is below mandatory connector fraction %.3f; clamping to mandatory minimum.",
            float(cfg.target_volume_ratio),
            mandatory_count / max(float(n_design), 1.0),
        )
        target_mass = float(mandatory_count)
    if free_count == 0:
        raise RuntimeError("No free design elements remain after mandatory zones were fixed.")

    min_density = float(cfg.min_density)
    free_initial = (target_mass - mandatory_count) / max(free_count, 1)
    free_initial = float(np.clip(free_initial, min_density, 1.0))
    rho = np.full(n_design, free_initial, dtype=float)
    rho[mandatory_mask_design] = 1.0

    metrics: list[IterationMetrics] = []
    beta = float(cfg.projection_beta_start)
    penal = float(cfg.penal_start)
    best_result: FemResult | None = None
    best_mask = voxel_scene.connector_mask.copy()
    last_frame = frames_dir / "002_initial_connector_box.png"
    last_stress = frames_dir / "002_initial_connector_box.png"

    logger.info(
        "Starting SIMP+OC optimization: design=%d, mandatory=%d, target_mass=%.2f, filter_radius=%.2f voxels.",
        n_design,
        mandatory_count,
        target_mass,
        float(cfg.filter_radius),
    )

    for iteration in range(1, int(cfg.max_iterations) + 1):
        rho_tilde = np.asarray(h @ rho).ravel() / hs
        rho_phys, dproj = _project_density(rho_tilde, beta=beta, eta=float(cfg.projection_eta))
        rho_phys[mandatory_mask_design] = 1.0

        fem_result = solve_density_fem(
            voxel_scene,
            ctx,
            rho_phys,
            penal=penal,
            logger=logger,
            emin_ratio=float(getattr(cfg, "void_stiffness_ratio", 1.0e-6)),
        )
        if not fem_result.success or fem_result.design_ce is None:
            raise RuntimeError(f"Optimization FEM failed on iteration {iteration}: {fem_result.reason}")

        ce = fem_result.design_ce
        e0 = connector_material.young_modulus
        emin = float(getattr(cfg, "void_stiffness_ratio", 1.0e-6)) * e0
        dc_phys = -(penal * (e0 - emin) * np.maximum(rho_phys, min_density) ** (penal - 1.0)) * ce
        dv_phys = np.ones_like(dc_phys, dtype=float)

        dc_tilde = dc_phys * dproj
        dv_tilde = dv_phys * dproj
        dc = np.asarray(h.T @ (dc_tilde / hs)).ravel()
        dv = np.asarray(h.T @ (dv_tilde / hs)).ravel()

        x_old = rho.copy()
        free_current = rho[free_mask_design]
        free_dc = dc[free_mask_design]
        free_dv = np.maximum(dv[free_mask_design], 1.0e-12)

        l1, l2 = 0.0, 1.0e9
        move = float(cfg.move_limit)
        candidate = rho.copy()
        for _ in range(80):
            lmid = 0.5 * (l1 + l2)
            candidate_free = np.clip(
                free_current * np.sqrt(np.maximum(1.0e-18, -free_dc / (free_dv * lmid))),
                np.maximum(min_density, free_current - move),
                np.minimum(1.0, free_current + move),
            )
            candidate = rho.copy()
            candidate[free_mask_design] = candidate_free
            candidate[mandatory_mask_design] = 1.0
            cand_tilde = np.asarray(h @ candidate).ravel() / hs
            cand_phys, _ = _project_density(cand_tilde, beta=beta, eta=float(cfg.projection_eta))
            cand_phys[mandatory_mask_design] = 1.0
            if float(cand_phys.sum()) > target_mass:
                l1 = lmid
            else:
                l2 = lmid
            if (l2 - l1) / max(l2 + l1, 1.0) < 1.0e-4:
                break

        rho = candidate
        rho[mandatory_mask_design] = 1.0
        max_change = float(np.max(np.abs(rho - x_old)))
        binary_mask = _densities_to_mask(voxel_scene, ctx, rho_phys)

        best_result = fem_result
        best_mask = binary_mask
        metrics.append(
            IterationMetrics(
                iteration=iteration,
                removal_fraction=max_change,
                active_connector_voxels=int(binary_mask.sum()),
                connector_volume=float(binary_mask.sum()) * voxel_scene.grid.voxel_size ** 3,
                volume_ratio=float(binary_mask.sum()) / max(float(n_design), 1.0),
                max_connector_vm=float(fem_result.connector_max_vm),
                max_abs_vm=float(fem_result.abs_max_vm),
                max_displacement=float(fem_result.max_displacement),
                compliance=float(fem_result.compliance),
                accepted=True,
                reason=f"penal={penal:.2f}, beta={beta:.2f}, {fem_result.reason}",
            )
        )
        logger.info(
            "iter=%d | solver=%s | max_change=%.4f | rho_vol=%.4f | binary_vox=%d | vm=%.3f MPa | disp=%.4f",
            iteration,
            fem_result.reason,
            max_change,
            float(rho_phys.sum()) / max(float(n_design), 1.0),
            int(binary_mask.sum()),
            float(fem_result.connector_max_vm),
            float(fem_result.max_displacement),
        )

        if iteration == 1 or iteration % max(int(cfg.render_every), 1) == 0 or iteration == int(cfg.max_iterations):
            last_frame, last_stress = _render_iteration(
                f"{iteration:03d}",
                frames_dir,
                voxel_scene,
                binary_mask,
                fem_result,
                "SIMP+OC topology step",
                f"iter={iteration} | change={max_change:.4f} | vox={int(binary_mask.sum())} | rho_vol={rho_phys.sum()/max(n_design,1):.3f} | vm={fem_result.connector_max_vm:.2f} MPa",
            )
        _emit(
            progress_callback,
            metrics,
            {
                "state": "running",
                "phase": "optimize",
                "iteration": iteration,
                "accepted": True,
                "reason": metrics[-1].reason,
                "latest_frame": str(last_frame),
                "latest_stress_frame": str(last_stress),
            },
        )

        if iteration % max(int(cfg.penal_every), 1) == 0:
            penal = min(float(cfg.penal_max), penal + float(cfg.penal_step))
            beta = min(float(cfg.projection_beta_max), beta * float(cfg.projection_beta_scale))

        if max_change < max(0.01, float(cfg.move_limit) * 0.08) and iteration > 10:
            logger.info("Converged after %d iterations with max density change %.5f.", iteration, max_change)
            break

    if best_result is None:
        raise RuntimeError("Optimizer produced no iterations.")

    final_mask = _densities_to_mask(
        voxel_scene,
        ctx,
        best_result.rho_phys if best_result.rho_phys is not None else np.ones(n_design, dtype=float),
    )
    final_rho = np.zeros(n_design, dtype=float)
    if best_result.rho_phys is not None:
        final_rho[:] = best_result.rho_phys
    if final_mask.any():
        mask_design = final_mask[
            ctx.element_voxels[ctx.design_elem_ids][:, 0],
            ctx.element_voxels[ctx.design_elem_ids][:, 1],
            ctx.element_voxels[ctx.design_elem_ids][:, 2],
        ]
        final_rho[mask_design] = 1.0
        final_rho[~mask_design] = np.maximum(final_rho[~mask_design], min_density)
    if ctx.free_dofs.size > int(getattr(cfg, "final_resolve_max_dofs", 250000)):
        logger.warning(
            "Skipping final binary re-solve because the system is too large (%d free DOFs). "
            "Using the latest density solve instead.",
            ctx.free_dofs.size,
        )
        final_result = best_result
    else:
        final_result = solve_density_fem(
            voxel_scene,
            ctx,
            final_rho,
            penal=float(cfg.penal_max),
            logger=logger,
            emin_ratio=float(getattr(cfg, "void_stiffness_ratio", 1.0e-6)),
            binary_mask=final_mask,
        )
        if not final_result.success:
            logger.warning(
                "Final binary solve failed, falling back to latest density result: %s",
                final_result.reason,
            )
            final_result = best_result
    if not final_result.success:
        logger.warning("Final binary solve failed, falling back to latest density result: %s", final_result.reason)
        final_result = best_result
    final_frame, final_stress = _render_iteration(
        "999_final",
        frames_dir,
        voxel_scene,
        final_mask,
        final_result,
        "Final connector",
        f"vox={int(final_mask.sum())} | vm={final_result.connector_max_vm:.2f} MPa | disp={final_result.max_displacement:.4g} mm",
    )
    _emit(
        progress_callback,
        metrics,
        {
            "state": "completed",
            "phase": "final",
            "iteration": metrics[-1].iteration if metrics else 0,
            "accepted": True,
            "reason": "completed",
            "latest_frame": str(final_frame),
            "latest_stress_frame": str(final_stress),
        },
    )
    logger.info(
        "Finished optimization: final voxels=%d, volume_ratio=%.3f, max_vm=%.2f MPa, max_disp=%.4g mm.",
        int(final_mask.sum()),
        float(final_mask.sum()) / max(float(n_design), 1.0),
        final_result.connector_max_vm,
        final_result.max_displacement,
    )
    if final_result.connector_max_vm > allowable:
        logger.warning(
            "Final design exceeds allowable stress: %.2f MPa > %.2f MPa.",
            final_result.connector_max_vm,
            allowable,
        )
    if final_result.max_displacement > float(cfg.max_displacement):
        logger.warning(
            "Final design exceeds displacement target: %.4g > %.4g.",
            final_result.max_displacement,
            float(cfg.max_displacement),
        )
    return final_mask, final_result, metrics
