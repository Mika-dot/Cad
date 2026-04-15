from __future__ import annotations

import logging
from dataclasses import dataclass

import numpy as np
from scipy.sparse import coo_matrix, csr_matrix
from scipy.sparse.linalg import LinearOperator, bicgstab, cg, spilu, spsolve
from skfem import Basis, ElementHex1, ElementVector, MeshHex, asm
from skfem.models.elasticity import lame_parameters, linear_elasticity

from .geometry import entity_bbox, point_mask_for_primitive
from .models import DEFAULT_MATERIALS, GridSpec, Material, VoxelScene


@dataclass(slots=True)
class FemResult:
    success: bool
    reason: str
    displacement: np.ndarray | None = None
    compliance: float = float("inf")
    connector_energy: np.ndarray | None = None
    connector_vm: np.ndarray | None = None
    abs_vm: np.ndarray | None = None
    connector_max_vm: float = float("inf")
    abs_max_vm: float = 0.0
    max_displacement: float = float("inf")
    active_ids: np.ndarray | None = None
    rho_phys: np.ndarray | None = None
    design_ce: np.ndarray | None = None
    binary_mask: np.ndarray | None = None


@dataclass(slots=True)
class FemContext:
    grid: GridSpec
    ndofs: int
    active_node_count: int
    active_element_count: int
    estimated_matrix_gb: float
    node_points: np.ndarray
    element_voxels: np.ndarray
    element_linear_ids: np.ndarray
    edof: np.ndarray
    design_elem_ids: np.ndarray
    mandatory_elem_ids: np.ndarray
    fixed_design_elem_ids: np.ndarray
    solid_elem_ids: np.ndarray
    free_design_elem_ids: np.ndarray
    fixed_dofs: np.ndarray
    free_dofs: np.ndarray
    force_vector: np.ndarray
    ke_connector: np.ndarray
    ke_abs: np.ndarray
    b_connector: np.ndarray
    b_abs: np.ndarray
    d_connector: np.ndarray
    d_abs: np.ndarray
    design_count: int
    mandatory_count: int


def _solve_linear_system(
    kk,
    ff,
    solver: str,
    rtol: float,
    maxiter: int,
    large_system_rtol: float = 1.0e-6,
    large_system_maxiter: int = 12000,
    diagonal_regularization: float = 1.0e-9,
):
    solver_name = str(solver).lower()
    large_system = kk.shape[0] >= 80000

    if solver_name == "auto":
        solver_name = "cg" if large_system else "direct"

    diag = kk.diagonal().astype(float, copy=False)
    safe_diag = np.where(np.abs(diag) > 1.0e-12, np.abs(diag), 1.0)
    max_diag = float(safe_diag.max()) if safe_diag.size else 1.0
    shift = max(float(diagonal_regularization), 0.0) * max(max_diag, 1.0)
    if shift > 0.0:
        kk = kk.tocsr(copy=True)
        kk.setdiag(kk.diagonal() + shift)
        diag = kk.diagonal().astype(float, copy=False)

    diag = np.where(np.abs(diag) > 1.0e-12, diag, 1.0)
    inv_diag = 1.0 / diag
    jacobi = LinearOperator(kk.shape, matvec=lambda x: inv_diag * x, dtype=float)

    if large_system:
        rtol = max(float(rtol), float(large_system_rtol))
        maxiter = max(int(maxiter), int(large_system_maxiter))

    if solver_name == "cg":
        sol, info = cg(kk, ff, rtol=float(rtol), atol=0.0, maxiter=int(maxiter), M=jacobi)
        if info == 0 and np.all(np.isfinite(sol)):
            return np.asarray(sol, dtype=float), "cg"

        ilu_info = None
        if kk.shape[0] <= 450000:
            try:
                ilu = spilu(kk.tocsc(), drop_tol=1.0e-3, fill_factor=6.0)
                ilu_prec = LinearOperator(kk.shape, matvec=lambda x: ilu.solve(x), dtype=float)
                sol_ilu, info_ilu = bicgstab(
                    kk,
                    ff,
                    rtol=max(float(rtol), 5.0e-6),
                    atol=0.0,
                    maxiter=max(int(maxiter), 6000),
                    M=ilu_prec,
                )
                if info_ilu == 0 and np.all(np.isfinite(sol_ilu)):
                    return np.asarray(sol_ilu, dtype=float), "bicgstab+ilu"
                ilu_info = int(info_ilu)
            except Exception as exc:
                ilu_info = str(exc)

        sol2, info2 = bicgstab(
            kk,
            ff,
            rtol=max(float(rtol), 5.0e-6),
            atol=0.0,
            maxiter=max(int(maxiter), 2 * int(maxiter)),
            M=jacobi,
        )
        if info2 == 0 and np.all(np.isfinite(sol2)):
            return np.asarray(sol2, dtype=float), "bicgstab"

        if large_system:
            suffix = f", ilu={ilu_info}" if ilu_info is not None else ""
            raise RuntimeError(f"iterative solver failed (cg={info}, bicgstab={info2}{suffix})")

    sol = spsolve(kk, ff)
    return np.asarray(sol, dtype=float), "direct"


def constitutive_matrix(material: Material) -> np.ndarray:
    lam, mu = lame_parameters(material.young_modulus, material.poisson_ratio)
    return np.array(
        [
            [lam + 2.0 * mu, lam, lam, 0.0, 0.0, 0.0],
            [lam, lam + 2.0 * mu, lam, 0.0, 0.0, 0.0],
            [lam, lam, lam + 2.0 * mu, 0.0, 0.0, 0.0],
            [0.0, 0.0, 0.0, mu, 0.0, 0.0],
            [0.0, 0.0, 0.0, 0.0, mu, 0.0],
            [0.0, 0.0, 0.0, 0.0, 0.0, mu],
        ],
        dtype=float,
    )


# Matches skfem MeshHex.init_tensor numbering for a first-order hex.
_HEX_NODE_OFFSETS = np.array(
    [
        [0, 0, 0],
        [0, 1, 0],
        [1, 0, 0],
        [0, 0, 1],
        [1, 1, 0],
        [0, 1, 1],
        [1, 0, 1],
        [1, 1, 1],
    ],
    dtype=int,
)


def build_local_matrices(voxel_size: float, material: Material) -> tuple[np.ndarray, np.ndarray]:
    mesh = MeshHex.init_tensor(
        np.array([0.0, voxel_size]),
        np.array([0.0, voxel_size]),
        np.array([0.0, voxel_size]),
    )
    element = ElementVector(ElementHex1())
    basis = Basis(mesh, element)
    k_dense = asm(linear_elasticity(*lame_parameters(material.young_modulus, material.poisson_ratio)), basis).toarray()
    dofs = basis.element_dofs[:, 0]
    ke = k_dense[np.ix_(dofs, dofs)]

    local_nodes = mesh.p[:, mesh.t[:, 0]].T
    center = local_nodes.mean(axis=0)
    b = np.zeros((6, 24), dtype=float)
    for node_index, node in enumerate(local_nodes):
        sx = -1.0 if node[0] < center[0] else 1.0
        sy = -1.0 if node[1] < center[1] else 1.0
        sz = -1.0 if node[2] < center[2] else 1.0
        dndx = sx / (4.0 * voxel_size)
        dndy = sy / (4.0 * voxel_size)
        dndz = sz / (4.0 * voxel_size)
        col = 3 * node_index
        b[0, col + 0] = dndx
        b[1, col + 1] = dndy
        b[2, col + 2] = dndz
        b[3, col + 0] = dndy
        b[3, col + 1] = dndx
        b[4, col + 1] = dndz
        b[4, col + 2] = dndy
        b[5, col + 0] = dndz
        b[5, col + 2] = dndx
    return ke, b


def von_mises(stress: np.ndarray) -> np.ndarray:
    sx, sy, sz, txy, tyz, txz = stress.T
    return np.sqrt(0.5 * ((sx - sy) ** 2 + (sy - sz) ** 2 + (sz - sx) ** 2) + 3.0 * (txy ** 2 + tyz ** 2 + txz ** 2))


def _linear_ids_from_ijk(ijk: np.ndarray, grid: GridSpec) -> np.ndarray:
    return ijk[:, 2] * (grid.nx * grid.ny) + ijk[:, 0] * grid.ny + ijk[:, 1]


def _node_ids_from_ijk(ijk: np.ndarray, nx1: int, ny1: int) -> np.ndarray:
    return ijk[:, 1] + ny1 * ijk[:, 0] + (nx1 * ny1) * ijk[:, 2]


def estimate_fem_requirements(voxel_scene: VoxelScene) -> dict[str, float | int]:
    grid = voxel_scene.grid
    active_mask = voxel_scene.design_mask | voxel_scene.anchor_mask | voxel_scene.load_mask | voxel_scene.preserve_mask
    active_voxels = int(np.count_nonzero(active_mask))
    if active_voxels == 0:
        return {
            "grid_voxels": int(np.prod(grid.shape)),
            "active_voxels": 0,
            "active_nodes": 0,
            "ndofs": 0,
            "estimated_matrix_gb": 0.0,
        }

    active_ijk = np.argwhere(active_mask)
    node_mask = np.zeros((grid.nx + 1, grid.ny + 1, grid.nz + 1), dtype=bool)
    for offset in _HEX_NODE_OFFSETS:
        node_mask[
            active_ijk[:, 0] + offset[0],
            active_ijk[:, 1] + offset[1],
            active_ijk[:, 2] + offset[2],
        ] = True
    active_nodes = int(np.count_nonzero(node_mask))
    ndofs = active_nodes * 3
    nnz_guess = active_voxels * 24 * 24
    estimated_matrix_gb = float(nnz_guess * 16) / (1024.0 ** 3)
    return {
        "grid_voxels": int(np.prod(grid.shape)),
        "active_voxels": active_voxels,
        "active_nodes": active_nodes,
        "ndofs": ndofs,
        "estimated_matrix_gb": estimated_matrix_gb,
    }


def build_force_vector(node_points: np.ndarray, voxel_scene: VoxelScene, ndofs: int, logger: logging.Logger) -> np.ndarray:
    f = np.zeros(ndofs, dtype=float)
    voxel_size = voxel_scene.grid.voxel_size

    for entity in voxel_scene.scene.entities:
        if entity.role.lower() != "load":
            continue
        inside = point_mask_for_primitive(node_points, entity.primitive)
        if not inside.any():
            mn, mx = entity_bbox(entity)
            pad = voxel_size * 0.75
            inside = np.all((node_points >= (mn - pad)) & (node_points <= (mx + pad)), axis=1)
        if not inside.any():
            center = np.mean(np.asarray(entity_bbox(entity)), axis=0)
            dists = np.linalg.norm(node_points - center[None, :], axis=1)
            closest = np.argsort(dists)[: max(4, min(24, len(dists)))]
            inside = np.zeros(node_points.shape[0], dtype=bool)
            inside[closest] = True
            logger.warning("Load entity %s had no exact node hit; using %d nearest nodes.", entity.name, len(closest))
        node_ids = np.flatnonzero(inside)
        for axis, force_value in enumerate(entity.force):
            if abs(force_value) < 1e-12:
                continue
            coords = node_points[node_ids, axis]
            extreme = coords.max() if force_value < 0 else coords.min()
            surface_nodes = node_ids[np.isclose(coords, extreme, atol=voxel_size * 0.55)]
            if surface_nodes.size == 0:
                surface_nodes = node_ids
            per_node = float(force_value) / float(surface_nodes.size)
            logger.info(
                "Applying %.3f N of axis %d from entity %s to %d nodes.",
                force_value,
                axis,
                entity.name,
                surface_nodes.size,
            )
            f[3 * surface_nodes + axis] += per_node
    return f


def build_dirichlet_dofs(node_points: np.ndarray, voxel_scene: VoxelScene, logger: logging.Logger) -> np.ndarray:
    fixed: list[int] = []
    for entity in voxel_scene.scene.entities:
        if entity.role.lower() != "anchor":
            continue
        inside = point_mask_for_primitive(node_points, entity.primitive)
        if not inside.any():
            mn, mx = entity_bbox(entity)
            pad = voxel_scene.grid.voxel_size * 0.75
            inside = np.all((node_points >= (mn - pad)) & (node_points <= (mx + pad)), axis=1)
        node_ids = np.flatnonzero(inside)
        logger.info("Anchor entity %s constrains %d nodes.", entity.name, node_ids.size)
        for axis, locked in enumerate(entity.fix):
            if locked:
                fixed.extend((3 * node_ids + axis).tolist())
    if not fixed:
        return np.array([], dtype=int)
    return np.array(sorted(set(fixed)), dtype=int)


def _assemble_stiffness(ctx: FemContext, conn_scale: np.ndarray, abs_scale: np.ndarray, chunk_size: int = 12000) -> csr_matrix:
    stiffness = csr_matrix((ctx.ndofs, ctx.ndofs), dtype=float)
    ke_conn = ctx.ke_connector.reshape(1, -1)
    ke_abs = ctx.ke_abs.reshape(1, -1)

    for start in range(0, ctx.active_element_count, int(chunk_size)):
        stop = min(start + int(chunk_size), ctx.active_element_count)
        edof_chunk = ctx.edof[start:stop]
        rows = np.repeat(edof_chunk, 24, axis=1).ravel()
        cols = np.tile(edof_chunk, (1, 24)).ravel()
        data = (conn_scale[start:stop, None] * ke_conn + abs_scale[start:stop, None] * ke_abs).ravel()
        stiffness += coo_matrix((data, (rows, cols)), shape=(ctx.ndofs, ctx.ndofs)).tocsr()

    return 0.5 * (stiffness + stiffness.T)

def _assemble_diagonal(ctx: FemContext, conn_scale: np.ndarray, abs_scale: np.ndarray, chunk_size: int = 24000) -> np.ndarray:
    diag = np.zeros(ctx.ndofs, dtype=float)
    diag_conn = np.diag(ctx.ke_connector)
    diag_abs = np.diag(ctx.ke_abs)
    for start in range(0, ctx.active_element_count, int(chunk_size)):
        stop = min(start + int(chunk_size), ctx.active_element_count)
        edof_chunk = ctx.edof[start:stop]
        values = conn_scale[start:stop, None] * diag_conn[None, :] + abs_scale[start:stop, None] * diag_abs[None, :]
        np.add.at(diag, edof_chunk.ravel(), values.ravel())
    return diag


def _matrix_free_apply(
    ctx: FemContext,
    x_full: np.ndarray,
    conn_scale: np.ndarray,
    abs_scale: np.ndarray,
    chunk_size: int = 4096,
) -> np.ndarray:
    out = np.zeros_like(x_full)
    ke_conn = ctx.ke_connector
    ke_abs = ctx.ke_abs
    for start in range(0, ctx.active_element_count, int(chunk_size)):
        stop = min(start + int(chunk_size), ctx.active_element_count)
        edof_chunk = ctx.edof[start:stop]
        ue = x_full[edof_chunk]
        local = conn_scale[start:stop, None] * (ue @ ke_conn) + abs_scale[start:stop, None] * (ue @ ke_abs)
        np.add.at(out, edof_chunk.ravel(), local.ravel())
    return out


def _solve_matrix_free_system(
    ctx: FemContext,
    conn_scale: np.ndarray,
    abs_scale: np.ndarray,
    ff: np.ndarray,
    solver: str,
    rtol: float,
    maxiter: int,
    large_system_rtol: float = 1.0e-6,
    large_system_maxiter: int = 12000,
    diagonal_regularization: float = 1.0e-9,
    chunk_size: int = 4096,
):
    solver_name = str(solver).lower()
    if solver_name == 'auto':
        solver_name = 'cg'
    if solver_name == 'direct':
        solver_name = 'cg'

    diag = _assemble_diagonal(ctx, conn_scale, abs_scale, chunk_size=max(int(chunk_size) * 2, 4096))
    safe_diag = np.where(np.abs(diag) > 1.0e-12, np.abs(diag), 1.0)
    max_diag = float(safe_diag.max()) if safe_diag.size else 1.0
    shift = max(float(diagonal_regularization), 0.0) * max(max_diag, 1.0)
    if shift > 0.0:
        diag = diag + shift
    diag = np.where(np.abs(diag) > 1.0e-12, diag, 1.0)
    free_diag = diag[ctx.free_dofs]
    inv_diag = 1.0 / free_diag
    jacobi = LinearOperator((ctx.free_dofs.size, ctx.free_dofs.size), matvec=lambda x: inv_diag * x, dtype=float)

    rtol = max(float(rtol), float(large_system_rtol))
    maxiter = max(int(maxiter), int(large_system_maxiter))

    def matvec(x_free: np.ndarray) -> np.ndarray:
        x_full = np.zeros(ctx.ndofs, dtype=float)
        x_full[ctx.free_dofs] = x_free
        y_full = _matrix_free_apply(ctx, x_full, conn_scale, abs_scale, chunk_size=int(chunk_size))
        if shift > 0.0:
            y_full += shift * x_full
        return y_full[ctx.free_dofs]

    operator = LinearOperator((ctx.free_dofs.size, ctx.free_dofs.size), matvec=matvec, dtype=float)

    sol, info = cg(operator, ff, rtol=rtol, atol=0.0, maxiter=maxiter, M=jacobi)
    if info == 0 and np.all(np.isfinite(sol)):
        return np.asarray(sol, dtype=float), 'matrix-free-cg'

    sol2, info2 = bicgstab(operator, ff, rtol=max(rtol, 5.0e-6), atol=0.0, maxiter=max(maxiter, 2 * maxiter), M=jacobi)
    if info2 == 0 and np.all(np.isfinite(sol2)):
        return np.asarray(sol2, dtype=float), 'matrix-free-bicgstab'

    raise RuntimeError(f'matrix-free iterative solver failed (cg={info}, bicgstab={info2})')


def build_fem_context(voxel_scene: VoxelScene, logger: logging.Logger) -> FemContext:
    grid = voxel_scene.grid
    cfg = voxel_scene.scene.config
    estimates = estimate_fem_requirements(voxel_scene)

    max_active_dofs = int(getattr(cfg, "max_active_dofs", 350000))
    max_estimated_matrix_gb = float(getattr(cfg, "max_estimated_matrix_gb", 6.0))
    practical_iterative_max_dofs = int(getattr(cfg, "practical_iterative_max_dofs", 1200000))
    ndofs_est = int(estimates["ndofs"])
    matrix_est_gb = float(estimates["estimated_matrix_gb"])
    allow_large = bool(getattr(cfg, "force_allow_large_problems", False))
    if ndofs_est > max_active_dofs or matrix_est_gb > max_estimated_matrix_gb:
        message = (
            "Voxel size is very small for the current scene. "
            f"Estimated active DOFs: {ndofs_est:,}; "
            f"estimated sparse stiffness footprint: {matrix_est_gb:.1f} GiB. "
            "This may take a very long time and can still fail inside the solver. Continuing anyway."
        )
        if allow_large:
            logger.warning("OVERRIDE enabled: %s", message)
        else:
            logger.warning("Preflight warning: %s", message)
    solver_name = str(getattr(cfg, "solver", "auto")).lower()
    if solver_name in {"auto", "cg"} and ndofs_est > practical_iterative_max_dofs:
        message = (
            "The scene is larger than the practical iterative-solver limit for this pipeline. "
            f"Estimated active DOFs: {ndofs_est:,}; practical limit: {practical_iterative_max_dofs:,}. "
            "The iterative solver may run for a long time and still fail to converge. Continuing anyway."
        )
        if allow_large:
            logger.warning("OVERRIDE enabled: %s", message)
        else:
            logger.warning("Preflight warning: %s", message)

    active_mask = voxel_scene.design_mask | voxel_scene.anchor_mask | voxel_scene.load_mask | voxel_scene.preserve_mask
    active_voxels = np.argwhere(active_mask)
    if active_voxels.size == 0:
        raise ValueError("No active FEM elements were found.")
    element_linear_ids = _linear_ids_from_ijk(active_voxels, grid)
    order = np.argsort(element_linear_ids)
    active_voxels = active_voxels[order]
    element_linear_ids = element_linear_ids[order]

    nx1 = grid.nx + 1
    ny1 = grid.ny + 1
    nz1 = grid.nz + 1
    total_nodes = nx1 * ny1 * nz1

    node_mask = np.zeros((nx1, ny1, nz1), dtype=bool)
    for offset in _HEX_NODE_OFFSETS:
        node_mask[
            active_voxels[:, 0] + offset[0],
            active_voxels[:, 1] + offset[1],
            active_voxels[:, 2] + offset[2],
        ] = True

    active_node_ijk = np.argwhere(node_mask)
    active_node_full_ids = _node_ids_from_ijk(active_node_ijk, nx1, ny1)
    node_order = np.argsort(active_node_full_ids)
    active_node_ijk = active_node_ijk[node_order]
    active_node_full_ids = active_node_full_ids[node_order]

    node_lut = np.full(total_nodes, -1, dtype=int)
    node_lut[active_node_full_ids] = np.arange(active_node_full_ids.size, dtype=int)

    edof = np.empty((active_voxels.shape[0], 24), dtype=int)
    for node_index, offset in enumerate(_HEX_NODE_OFFSETS):
        node_ijk = active_voxels + offset
        node_full_ids = _node_ids_from_ijk(node_ijk, nx1, ny1)
        local_nodes = node_lut[node_full_ids]
        edof[:, 3 * node_index:3 * node_index + 3] = 3 * local_nodes[:, None] + np.array([0, 1, 2], dtype=int)

    ox, oy, oz = grid.origin
    v = float(grid.voxel_size)
    node_points = np.column_stack(
        (
            ox + active_node_ijk[:, 0] * v,
            oy + active_node_ijk[:, 1] * v,
            oz + active_node_ijk[:, 2] * v,
        )
    )

    design_flags = voxel_scene.design_mask[active_voxels[:, 0], active_voxels[:, 1], active_voxels[:, 2]]
    mandatory_flags = voxel_scene.mandatory_design_mask[active_voxels[:, 0], active_voxels[:, 1], active_voxels[:, 2]]
    solid_flags = (
        voxel_scene.anchor_mask[active_voxels[:, 0], active_voxels[:, 1], active_voxels[:, 2]]
        | voxel_scene.load_mask[active_voxels[:, 0], active_voxels[:, 1], active_voxels[:, 2]]
        | voxel_scene.preserve_mask[active_voxels[:, 0], active_voxels[:, 1], active_voxels[:, 2]]
    )
    fixed_design_flags = mandatory_flags | solid_flags

    design_elem_ids_arr = np.flatnonzero(design_flags)
    mandatory_elem_ids_arr = np.flatnonzero(mandatory_flags)
    solid_elem_ids_arr = np.flatnonzero(solid_flags)
    fixed_design_elem_ids_arr = np.flatnonzero(fixed_design_flags)
    free_design_elem_ids_arr = np.flatnonzero(design_flags & ~mandatory_flags)

    ndofs = node_points.shape[0] * 3
    fixed_dofs = build_dirichlet_dofs(node_points, voxel_scene, logger)
    if fixed_dofs.size == 0:
        raise ValueError("No fixed DOFs were found.")
    all_dofs = np.arange(ndofs, dtype=int)
    free_dofs = np.setdiff1d(all_dofs, fixed_dofs, assume_unique=True)
    force_vector = build_force_vector(node_points, voxel_scene, ndofs, logger)
    if np.allclose(force_vector, 0.0):
        raise ValueError("No external loads were applied.")

    ke_abs, b_abs = build_local_matrices(v, DEFAULT_MATERIALS["abs"])
    ke_connector, b_connector = build_local_matrices(v, DEFAULT_MATERIALS[voxel_scene.scene.config.connector_material])
    d_abs = constitutive_matrix(DEFAULT_MATERIALS["abs"])
    d_connector = constitutive_matrix(DEFAULT_MATERIALS[voxel_scene.scene.config.connector_material])

    logger.info(
        "FEM context built: %d active elements, %d active nodes, %d design voxels, %d solid non-design voxels, %d DOFs, estimated matrix %.2f GiB.",
        active_voxels.shape[0],
        active_node_ijk.shape[0],
        design_elem_ids_arr.size,
        solid_elem_ids_arr.size,
        ndofs,
        float(estimates["estimated_matrix_gb"]),
    )

    return FemContext(
        grid=grid,
        ndofs=ndofs,
        active_node_count=int(active_node_ijk.shape[0]),
        active_element_count=int(active_voxels.shape[0]),
        estimated_matrix_gb=float(estimates["estimated_matrix_gb"]),
        node_points=node_points,
        element_voxels=active_voxels,
        element_linear_ids=element_linear_ids,
        edof=edof,
        design_elem_ids=design_elem_ids_arr,
        mandatory_elem_ids=mandatory_elem_ids_arr,
        fixed_design_elem_ids=fixed_design_elem_ids_arr,
        solid_elem_ids=solid_elem_ids_arr,
        free_design_elem_ids=free_design_elem_ids_arr,
        fixed_dofs=fixed_dofs,
        free_dofs=free_dofs,
        force_vector=force_vector,
        ke_connector=ke_connector,
        ke_abs=ke_abs,
        b_connector=b_connector,
        b_abs=b_abs,
        d_connector=d_connector,
        d_abs=d_abs,
        design_count=int(design_elem_ids_arr.size),
        mandatory_count=int(mandatory_elem_ids_arr.size),
    )


def solve_density_fem(
    voxel_scene: VoxelScene,
    ctx: FemContext,
    rho_phys: np.ndarray,
    penal: float,
    logger: logging.Logger,
    emin_ratio: float = 1.0e-3,
    binary_mask: np.ndarray | None = None,
) -> FemResult:
    connector_material = DEFAULT_MATERIALS[voxel_scene.scene.config.connector_material]
    e0 = connector_material.young_modulus
    emin = float(emin_ratio) * e0

    effective_rho = np.zeros(ctx.active_element_count, dtype=float)
    if ctx.design_elem_ids.size:
        effective_rho[ctx.design_elem_ids] = np.clip(rho_phys, 0.0, 1.0)

    conn_scale = np.full(ctx.active_element_count, emin, dtype=float)
    if ctx.design_elem_ids.size:
        conn_scale[ctx.design_elem_ids] = emin + effective_rho[ctx.design_elem_ids] ** float(penal) * (e0 - emin)
    abs_scale = np.zeros(ctx.active_element_count, dtype=float)
    if ctx.solid_elem_ids.size:
        abs_scale[ctx.solid_elem_ids] = DEFAULT_MATERIALS["abs"].young_modulus

    cfg = voxel_scene.scene.config
    solver_name = str(getattr(cfg, "solver", "auto")).lower()
    matrix_free_large = bool(getattr(cfg, "matrix_free_large_systems", True))
    matrix_free_min_dofs = int(getattr(cfg, "matrix_free_min_dofs", 600000))
    matrix_free_min_estimated_matrix_gb = float(getattr(cfg, "matrix_free_min_estimated_matrix_gb", 2.5))
    matrix_free_chunk_size = int(getattr(cfg, "matrix_free_chunk_size", 4096))
    fallback_to_matrix_free_on_oom = bool(getattr(cfg, "fallback_to_matrix_free_on_oom", True))
    prefer_matrix_free = solver_name == "matrix_free" or (
        matrix_free_large and (
            ctx.ndofs >= matrix_free_min_dofs or ctx.estimated_matrix_gb >= matrix_free_min_estimated_matrix_gb
        )
    )

    try:
        u = np.zeros(ctx.ndofs, dtype=float)
        ff = ctx.force_vector[ctx.free_dofs]
        if prefer_matrix_free and solver_name in {"auto", "cg", "direct", "matrix_free"}:
            if solver_name == "direct":
                logger.warning(
                    "Large problem detected (%d DOFs, %.2f GiB sparse estimate). "
                    "Switching from direct factorization to matrix-free iterative solve to avoid LU memory blow-up.",
                    ctx.ndofs,
                    ctx.estimated_matrix_gb,
                )
            solved, solver_used = _solve_matrix_free_system(
                ctx,
                conn_scale,
                abs_scale,
                ff,
                solver="cg" if solver_name in {"auto", "direct", "matrix_free"} else solver_name,
                rtol=getattr(cfg, "cg_relative_tolerance", 1.0e-8),
                maxiter=getattr(cfg, "cg_max_iterations", 4000),
                large_system_rtol=getattr(cfg, "large_system_relative_tolerance", 1.0e-6),
                large_system_maxiter=getattr(cfg, "large_system_max_iterations", 12000),
                diagonal_regularization=getattr(cfg, "solver_diagonal_regularization", 1.0e-9),
                chunk_size=matrix_free_chunk_size,
            )
        else:
            stiffness = _assemble_stiffness(ctx, conn_scale, abs_scale)
            kk = stiffness[ctx.free_dofs][:, ctx.free_dofs]
            solved, solver_used = _solve_linear_system(
                kk,
                ff,
                solver=solver_name,
                rtol=getattr(cfg, "cg_relative_tolerance", 1.0e-8),
                maxiter=getattr(cfg, "cg_max_iterations", 4000),
                large_system_rtol=getattr(cfg, "large_system_relative_tolerance", 1.0e-6),
                large_system_maxiter=getattr(cfg, "large_system_max_iterations", 12000),
                diagonal_regularization=getattr(cfg, "solver_diagonal_regularization", 1.0e-9),
            )
            del stiffness
        u[ctx.free_dofs] = solved
    except MemoryError as exc:
        if fallback_to_matrix_free_on_oom and not prefer_matrix_free:
            logger.warning("Direct assembled solve ran out of memory (%s). Falling back to matrix-free iterative solve.", exc)
            try:
                solved, solver_used = _solve_matrix_free_system(
                    ctx,
                    conn_scale,
                    abs_scale,
                    ctx.force_vector[ctx.free_dofs],
                    solver="cg",
                    rtol=getattr(cfg, "cg_relative_tolerance", 1.0e-8),
                    maxiter=getattr(cfg, "cg_max_iterations", 4000),
                    large_system_rtol=getattr(cfg, "large_system_relative_tolerance", 1.0e-6),
                    large_system_maxiter=getattr(cfg, "large_system_max_iterations", 12000),
                    diagonal_regularization=getattr(cfg, "solver_diagonal_regularization", 1.0e-9),
                    chunk_size=matrix_free_chunk_size,
                )
                u = np.zeros(ctx.ndofs, dtype=float)
                u[ctx.free_dofs] = solved
            except Exception as fallback_exc:
                return FemResult(success=False, reason=f"FEM solve failed after matrix-free fallback: {fallback_exc}")
        else:
            return FemResult(success=False, reason=f"FEM solve failed: not enough memory ({exc})")
    except Exception as exc:
        if fallback_to_matrix_free_on_oom and ("factorization" in str(exc).lower() or "memory" in str(exc).lower()) and not prefer_matrix_free:
            logger.warning("Assembled solve failed (%s). Falling back to matrix-free iterative solve.", exc)
            try:
                solved, solver_used = _solve_matrix_free_system(
                    ctx,
                    conn_scale,
                    abs_scale,
                    ctx.force_vector[ctx.free_dofs],
                    solver="cg",
                    rtol=getattr(cfg, "cg_relative_tolerance", 1.0e-8),
                    maxiter=getattr(cfg, "cg_max_iterations", 4000),
                    large_system_rtol=getattr(cfg, "large_system_relative_tolerance", 1.0e-6),
                    large_system_maxiter=getattr(cfg, "large_system_max_iterations", 12000),
                    diagonal_regularization=getattr(cfg, "solver_diagonal_regularization", 1.0e-9),
                    chunk_size=matrix_free_chunk_size,
                )
                u = np.zeros(ctx.ndofs, dtype=float)
                u[ctx.free_dofs] = solved
            except Exception as fallback_exc:
                return FemResult(success=False, reason=f"FEM solve failed after matrix-free fallback: {fallback_exc}")
        else:
            return FemResult(success=False, reason=f"FEM solve failed: {exc}")

    if not np.all(np.isfinite(u)):
        return FemResult(success=False, reason="Solver returned non-finite displacements.")

    compliance = float(ctx.force_vector @ u)
    u_nodes = u.reshape((-1, 3))
    max_disp = float(np.linalg.norm(u_nodes, axis=1).max())

    ue = u[ctx.edof]
    connector_energy = np.zeros(ctx.design_elem_ids.size, dtype=float)
    connector_vm = np.zeros(ctx.design_elem_ids.size, dtype=float)
    if ctx.design_elem_ids.size:
        ue_design = ue[ctx.design_elem_ids]
        ce = np.einsum("ni,ij,nj->n", ue_design, ctx.ke_connector, ue_design)
        connector_energy = (emin + rho_phys ** float(penal) * (e0 - emin)) * ce
        strain_design = (ctx.b_connector @ ue_design[:, :, None]).squeeze(-1)
        stress_design = (ctx.d_connector @ strain_design[:, :, None]).squeeze(-1)
        stress_design *= (emin + rho_phys[:, None] ** float(penal) * (e0 - emin)) / e0
        connector_vm = von_mises(stress_design)
    else:
        ce = np.zeros(0, dtype=float)

    abs_vm = np.zeros(ctx.solid_elem_ids.size, dtype=float)
    if ctx.solid_elem_ids.size:
        ue_solid = ue[ctx.solid_elem_ids]
        strain_abs = (ctx.b_abs @ ue_solid[:, :, None]).squeeze(-1)
        stress_abs = (ctx.d_abs @ strain_abs[:, :, None]).squeeze(-1)
        abs_vm = von_mises(stress_abs)

    return FemResult(
        success=True,
        reason=f"ok ({solver_used})",
        displacement=u,
        compliance=compliance,
        connector_energy=connector_energy,
        connector_vm=connector_vm,
        abs_vm=abs_vm,
        connector_max_vm=float(connector_vm.max()) if connector_vm.size else 0.0,
        abs_max_vm=float(abs_vm.max()) if abs_vm.size else 0.0,
        max_displacement=max_disp,
        active_ids=ctx.element_linear_ids[ctx.design_elem_ids],
        rho_phys=rho_phys.copy(),
        design_ce=ce,
        binary_mask=binary_mask,
    )
