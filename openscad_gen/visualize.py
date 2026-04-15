from __future__ import annotations

from pathlib import Path

import imageio.v2 as imageio
import matplotlib.cm as cm
import matplotlib.pyplot as plt
import numpy as np

from .models import VoxelScene

COLOR_CONNECTOR = "#f58518"
COLOR_ANCHOR = "#4c78a8"
COLOR_LOAD = "#54a24b"
COLOR_PRESERVE = "#72b7b2"
COLOR_OBSTACLE = "#bab0ac"


def _draw_bbox(ax, shape: tuple[int, int, int]) -> None:
    nx, ny, nz = shape
    pts = np.array([[0, 0, 0], [nx, 0, 0], [nx, ny, 0], [0, ny, 0], [0, 0, nz], [nx, 0, nz], [nx, ny, nz], [0, ny, nz]], dtype=float)
    edges = [(0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7)]
    for a, b in edges:
        ax.plot(*zip(pts[a], pts[b]), color="black", linewidth=0.5, alpha=0.5)


def _element_id_to_ijk(element_id: int, shape: tuple[int, int, int]) -> tuple[int, int, int]:
    nx, ny, _ = shape
    plane = nx * ny
    k = element_id // plane
    rem = element_id % plane
    i = rem // ny
    j = rem % ny
    return int(i), int(j), int(k)


def _scalar_to_hex_colors(values: np.ndarray) -> list[str]:
    cmap = cm.get_cmap("turbo")
    if values.size == 0:
        return []
    vmin = float(np.nanmin(values))
    vmax = float(np.nanmax(values))
    if not np.isfinite(vmin) or not np.isfinite(vmax) or np.isclose(vmin, vmax):
        normed = np.zeros_like(values, dtype=float)
    else:
        normed = (values - vmin) / (vmax - vmin)
    return ["#{:02x}{:02x}{:02x}".format(*(int(round(c * 255.0)) for c in cmap(val)[:3])) for val in normed]


def _mask_to_points(mask: np.ndarray) -> np.ndarray:
    ijk = np.argwhere(mask)
    if ijk.size == 0:
        return np.empty((0, 3), dtype=float)
    return ijk.astype(float) + 0.5


def _scatter_points(ax, points: np.ndarray, color: str | list[str], size: float = 11.0, alpha: float = 0.9) -> None:
    if points.size == 0:
        return
    ax.scatter(points[:, 0], points[:, 1], points[:, 2], s=size, c=color, marker="s", alpha=alpha, edgecolors="none")


def render_voxel_state(path: Path, voxel_scene: VoxelScene, connector_mask: np.ndarray, title: str, subtitle: str | None = None, show_domain: bool = True) -> None:
    shape = voxel_scene.grid.shape
    total_filled = int(
        np.count_nonzero(voxel_scene.obstacle_mask)
        + np.count_nonzero(voxel_scene.preserve_mask)
        + np.count_nonzero(voxel_scene.anchor_mask)
        + np.count_nonzero(voxel_scene.load_mask)
        + np.count_nonzero(connector_mask)
    )
    fig = plt.figure(figsize=(10, 8))
    ax = fig.add_subplot(111, projection="3d")
    if total_filled <= 6500:
        filled = np.zeros(shape, dtype=bool)
        colors = np.full(shape, "#ffffff", dtype=object)
        for mask, color in [
            (voxel_scene.obstacle_mask, COLOR_OBSTACLE),
            (voxel_scene.preserve_mask, COLOR_PRESERVE),
            (voxel_scene.anchor_mask, COLOR_ANCHOR),
            (voxel_scene.load_mask, COLOR_LOAD),
            (connector_mask, COLOR_CONNECTOR),
        ]:
            filled |= mask
            colors[mask] = color
        ax.voxels(filled, facecolors=colors, edgecolor="k", linewidth=0.04)
    else:
        _scatter_points(ax, _mask_to_points(voxel_scene.obstacle_mask), COLOR_OBSTACLE, size=10.0, alpha=0.35)
        _scatter_points(ax, _mask_to_points(voxel_scene.preserve_mask), COLOR_PRESERVE, size=12.0, alpha=0.55)
        _scatter_points(ax, _mask_to_points(voxel_scene.anchor_mask), COLOR_ANCHOR, size=16.0, alpha=0.95)
        _scatter_points(ax, _mask_to_points(voxel_scene.load_mask), COLOR_LOAD, size=16.0, alpha=0.95)
        _scatter_points(ax, _mask_to_points(connector_mask), COLOR_CONNECTOR, size=12.0, alpha=0.85)
    if show_domain:
        _draw_bbox(ax, shape)
    ax.set_title(title + (f"\n{subtitle}" if subtitle else ""))
    ax.set_xlabel("X vox")
    ax.set_ylabel("Y vox")
    ax.set_zlabel("Z vox")
    ax.set_box_aspect(shape)
    ax.view_init(elev=25, azim=35)
    plt.tight_layout()
    fig.savefig(path, dpi=140)
    plt.close(fig)


def render_scalar_voxel_state(path: Path, voxel_scene: VoxelScene, connector_mask: np.ndarray, active_ids: np.ndarray | None, connector_values: np.ndarray | None, title: str, subtitle: str | None = None) -> None:
    shape = voxel_scene.grid.shape
    fig = plt.figure(figsize=(10, 8))
    ax = fig.add_subplot(111, projection="3d")
    base_total = int(np.count_nonzero(connector_mask))
    if base_total <= 6500:
        filled = np.zeros(shape, dtype=bool)
        colors = np.full(shape, "#ffffff", dtype=object)
        for mask, color in [
            (voxel_scene.obstacle_mask, COLOR_OBSTACLE),
            (voxel_scene.preserve_mask, COLOR_PRESERVE),
            (voxel_scene.anchor_mask, COLOR_ANCHOR),
            (voxel_scene.load_mask, COLOR_LOAD),
        ]:
            filled |= mask
            colors[mask] = color
        if connector_mask.any():
            filled |= connector_mask
            colors[connector_mask] = COLOR_CONNECTOR
        if active_ids is not None and connector_values is not None and len(active_ids) == len(connector_values):
            positions = []
            scalars = []
            for local_id, original_element_id in enumerate(active_ids):
                ijk = _element_id_to_ijk(int(original_element_id), shape)
                if connector_mask[ijk]:
                    positions.append(ijk)
                    scalars.append(float(connector_values[local_id]))
            palette = _scalar_to_hex_colors(np.asarray(scalars, dtype=float))
            for ijk, color in zip(positions, palette):
                colors[ijk] = color
        ax.voxels(filled, facecolors=colors, edgecolor="k", linewidth=0.04)
    else:
        _scatter_points(ax, _mask_to_points(voxel_scene.anchor_mask), COLOR_ANCHOR, size=16.0, alpha=0.95)
        _scatter_points(ax, _mask_to_points(voxel_scene.load_mask), COLOR_LOAD, size=16.0, alpha=0.95)
        _scatter_points(ax, _mask_to_points(voxel_scene.preserve_mask), COLOR_PRESERVE, size=12.0, alpha=0.55)
        if active_ids is not None and connector_values is not None and len(active_ids) == len(connector_values):
            positions = []
            scalars = []
            for local_id, original_element_id in enumerate(active_ids):
                ijk = _element_id_to_ijk(int(original_element_id), shape)
                if connector_mask[ijk]:
                    positions.append(np.asarray(ijk, dtype=float) + 0.5)
                    scalars.append(float(connector_values[local_id]))
            if positions:
                points = np.vstack(positions)
                palette = _scalar_to_hex_colors(np.asarray(scalars, dtype=float))
                _scatter_points(ax, points, palette, size=12.0, alpha=0.9)
        else:
            _scatter_points(ax, _mask_to_points(connector_mask), COLOR_CONNECTOR, size=12.0, alpha=0.85)
    _draw_bbox(ax, shape)
    ax.set_title(title + (f"\n{subtitle}" if subtitle else ""))
    ax.set_xlabel("X vox")
    ax.set_ylabel("Y vox")
    ax.set_zlabel("Z vox")
    ax.set_box_aspect(shape)
    ax.view_init(elev=25, azim=35)
    plt.tight_layout()
    fig.savefig(path, dpi=140)
    plt.close(fig)


def render_domain_only(path: Path, voxel_scene: VoxelScene, title: str) -> None:
    shape = voxel_scene.grid.shape
    fig = plt.figure(figsize=(10, 8))
    ax = fig.add_subplot(111, projection="3d")
    _draw_bbox(ax, shape)
    _scatter_points(ax, _mask_to_points(voxel_scene.anchor_mask), COLOR_ANCHOR, size=16.0, alpha=0.95)
    _scatter_points(ax, _mask_to_points(voxel_scene.load_mask), COLOR_LOAD, size=16.0, alpha=0.95)
    _scatter_points(ax, _mask_to_points(voxel_scene.obstacle_mask), COLOR_OBSTACLE, size=10.0, alpha=0.35)
    _scatter_points(ax, _mask_to_points(voxel_scene.preserve_mask), COLOR_PRESERVE, size=12.0, alpha=0.55)
    ax.set_title(title)
    ax.set_xlabel("X vox")
    ax.set_ylabel("Y vox")
    ax.set_zlabel("Z vox")
    ax.set_box_aspect(shape)
    ax.view_init(elev=25, azim=35)
    plt.tight_layout()
    fig.savefig(path, dpi=140)
    plt.close(fig)


def build_gif(frames_dir: Path, output_path: Path, fps: int = 2) -> None:
    pngs = sorted(frames_dir.glob("*.png"))
    if not pngs:
        return
    images = [imageio.imread(png) for png in pngs]
    imageio.mimsave(output_path, images, duration=1.0 / max(fps, 1))
