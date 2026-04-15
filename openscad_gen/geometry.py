
from __future__ import annotations

from collections import deque
from typing import Iterable

import numpy as np

from .models import Entity, GridSpec, Primitive, Scene, VoxelScene


def _rotation_matrix_xyz(angles_deg: Iterable[float]) -> np.ndarray:
    ax, ay, az = [np.deg2rad(float(a)) for a in angles_deg]
    cx, sx = np.cos(ax), np.sin(ax)
    cy, sy = np.cos(ay), np.sin(ay)
    cz, sz = np.cos(az), np.sin(az)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]], dtype=float)
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]], dtype=float)
    rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]], dtype=float)
    return rz @ ry @ rx


def _bbox_corners(mn: np.ndarray, mx: np.ndarray) -> np.ndarray:
    return np.array(
        [
            [mn[0], mn[1], mn[2]],
            [mn[0], mn[1], mx[2]],
            [mn[0], mx[1], mn[2]],
            [mn[0], mx[1], mx[2]],
            [mx[0], mn[1], mn[2]],
            [mx[0], mn[1], mx[2]],
            [mx[0], mx[1], mn[2]],
            [mx[0], mx[1], mx[2]],
        ],
        dtype=float,
    )


def _primitive_bbox_local(kind: str, params: dict) -> tuple[np.ndarray, np.ndarray]:
    if kind == "cube":
        size = params.get("size", 1.0)
        if isinstance(size, (int, float)):
            sx = sy = sz = float(size)
        else:
            sx, sy, sz = [float(v) for v in size]
        center = bool(params.get("center", False))
        half = np.array([sx, sy, sz], dtype=float) / 2.0
        if center:
            return -half, half
        return np.array([0.0, 0.0, 0.0]), np.array([sx, sy, sz], dtype=float)
    if kind == "sphere":
        r = float(params.get("r", 1.0))
        c = np.array([r, r, r], dtype=float)
        return -c, c
    if kind == "cylinder":
        r = float(params.get("r", params.get("r1", 1.0)))
        h = float(params.get("h", 1.0))
        center = bool(params.get("center", False))
        z0 = -h / 2.0 if center else 0.0
        z1 = h / 2.0 if center else h
        return np.array([-r, -r, z0], dtype=float), np.array([r, r, z1], dtype=float)
    raise ValueError(f"Unsupported primitive kind: {kind}")


def node_bbox(node: dict) -> tuple[np.ndarray, np.ndarray]:
    node_type = node.get("type")
    if node_type == "empty":
        z = np.zeros(3, dtype=float)
        return z.copy(), z.copy()
    if node_type in {"cube", "sphere", "cylinder"}:
        return _primitive_bbox_local(node_type, node.get("params", {}))
    if node_type == "translate":
        mn, mx = node_bbox(node["child"])
        vec = np.array(node.get("vec", [0.0, 0.0, 0.0]), dtype=float)
        return mn + vec, mx + vec
    if node_type == "rotate":
        mn, mx = node_bbox(node["child"])
        corners = _bbox_corners(mn, mx)
        rot = _rotation_matrix_xyz(node.get("angles", [0.0, 0.0, 0.0]))
        rotated = corners @ rot.T
        return rotated.min(axis=0), rotated.max(axis=0)
    if node_type == "union":
        children = node.get("children", [])
        mins = []
        maxs = []
        for child in children:
            mn, mx = node_bbox(child)
            mins.append(mn)
            maxs.append(mx)
        return np.min(np.vstack(mins), axis=0), np.max(np.vstack(maxs), axis=0)
    if node_type == "difference":
        return node_bbox(node["base"])
    raise ValueError(f"Unsupported node type: {node_type}")


def entity_bbox(entity: Entity) -> tuple[np.ndarray, np.ndarray]:
    p = entity.primitive
    if p.kind == "compound":
        return node_bbox(p.params["tree"])
    tx, ty, tz = p.translate
    if p.kind == "cube":
        size = p.params.get("size", 1.0)
        if isinstance(size, (int, float)):
            sx = sy = sz = float(size)
        else:
            sx, sy, sz = [float(v) for v in size]
        center = bool(p.params.get("center", False))
        half = np.array([sx, sy, sz], dtype=float) / 2.0
        if center:
            mn = np.array([tx, ty, tz], dtype=float) - half
            mx = np.array([tx, ty, tz], dtype=float) + half
        else:
            mn = np.array([tx, ty, tz], dtype=float)
            mx = np.array([tx, ty, tz], dtype=float) + np.array([sx, sy, sz], dtype=float)
        return mn, mx
    if p.kind == "sphere":
        r = float(p.params["r"])
        c = np.array([tx, ty, tz], dtype=float)
        return c - r, c + r
    if p.kind == "cylinder":
        r = float(p.params.get("r", p.params.get("r1", 1.0)))
        h = float(p.params["h"])
        center = bool(p.params.get("center", False))
        z0 = tz - h / 2.0 if center else tz
        z1 = tz + h / 2.0 if center else tz + h
        mn = np.array([tx - r, ty - r, z0], dtype=float)
        mx = np.array([tx + r, ty + r, z1], dtype=float)
        return mn, mx
    raise ValueError(f"Unsupported primitive kind: {p.kind}")


def node_mask(points: np.ndarray, node: dict) -> np.ndarray:
    node_type = node.get("type")
    if node_type == "empty":
        return np.zeros(points.shape[:-1], dtype=bool)
    if node_type == "union":
        result = np.zeros(points.shape[:-1], dtype=bool)
        for child in node.get("children", []):
            result |= node_mask(points, child)
        return result
    if node_type == "difference":
        result = node_mask(points, node["base"])
        for child in node.get("subtract", []):
            result &= ~node_mask(points, child)
        return result
    if node_type == "translate":
        vec = np.array(node.get("vec", [0.0, 0.0, 0.0]), dtype=float)
        return node_mask(points - vec, node["child"])
    if node_type == "rotate":
        rot = _rotation_matrix_xyz(node.get("angles", [0.0, 0.0, 0.0]))
        inv_points = points @ rot
        return node_mask(inv_points, node["child"])
    if node_type == "cube":
        primitive = Primitive(kind="cube", params=node.get("params", {}), translate=(0.0, 0.0, 0.0))
        return point_mask_for_primitive(points, primitive)
    if node_type == "sphere":
        primitive = Primitive(kind="sphere", params=node.get("params", {}), translate=(0.0, 0.0, 0.0))
        return point_mask_for_primitive(points, primitive)
    if node_type == "cylinder":
        primitive = Primitive(kind="cylinder", params=node.get("params", {}), translate=(0.0, 0.0, 0.0))
        return point_mask_for_primitive(points, primitive)
    raise ValueError(f"Unsupported node type: {node_type}")


def point_mask_for_primitive(points: np.ndarray, primitive: Primitive) -> np.ndarray:
    if primitive.kind == "compound":
        return node_mask(points, primitive.params["tree"])

    tx, ty, tz = primitive.translate
    px = points[..., 0] - tx
    py = points[..., 1] - ty
    pz = points[..., 2] - tz

    if primitive.kind == "cube":
        size = primitive.params.get("size", 1.0)
        if isinstance(size, (int, float)):
            sx = sy = sz = float(size)
        else:
            sx, sy, sz = [float(v) for v in size]
        center = bool(primitive.params.get("center", False))
        if center:
            return (
                (np.abs(px) <= sx / 2.0)
                & (np.abs(py) <= sy / 2.0)
                & (np.abs(pz) <= sz / 2.0)
            )
        return (
            (px >= 0.0)
            & (py >= 0.0)
            & (pz >= 0.0)
            & (px <= sx)
            & (py <= sy)
            & (pz <= sz)
        )

    if primitive.kind == "sphere":
        r = float(primitive.params["r"])
        return px * px + py * py + pz * pz <= r * r

    if primitive.kind == "cylinder":
        r = float(primitive.params.get("r", primitive.params.get("r1", 1.0)))
        h = float(primitive.params["h"])
        center = bool(primitive.params.get("center", False))
        radial = px * px + py * py <= r * r
        if center:
            axial = np.abs(pz) <= h / 2.0
        else:
            axial = (pz >= 0.0) & (pz <= h)
        return radial & axial

    raise ValueError(f"Unsupported primitive kind: {primitive.kind}")


def dilate_mask(mask: np.ndarray, steps: int = 1) -> np.ndarray:
    result = mask.copy()
    for _ in range(steps):
        expanded = result.copy()
        expanded[1:, :, :] |= result[:-1, :, :]
        expanded[:-1, :, :] |= result[1:, :, :]
        expanded[:, 1:, :] |= result[:, :-1, :]
        expanded[:, :-1, :] |= result[:, 1:, :]
        expanded[:, :, 1:] |= result[:, :, :-1]
        expanded[:, :, :-1] |= result[:, :, 1:]
        result = expanded
    return result


def neighbors6() -> list[tuple[int, int, int]]:
    return [
        (-1, 0, 0),
        (1, 0, 0),
        (0, -1, 0),
        (0, 1, 0),
        (0, 0, -1),
        (0, 0, 1),
    ]


def build_voxel_scene(scene: Scene) -> VoxelScene:
    cfg = scene.config
    connect_entities = [e for e in scene.entities if e.connect]
    if not connect_entities:
        raise ValueError("At least one entity with connect: true is required.")

    all_mins: list[np.ndarray] = []
    all_maxs: list[np.ndarray] = []
    for entity in connect_entities:
        mn, mx = entity_bbox(entity)
        all_mins.append(mn)
        all_maxs.append(mx)
    scene_min = np.min(np.vstack(all_mins), axis=0) - cfg.bbox_margin
    scene_max = np.max(np.vstack(all_maxs), axis=0) + cfg.bbox_margin

    voxel = float(cfg.voxel_size)
    dims = np.maximum(np.ceil((scene_max - scene_min) / voxel).astype(int), 1)
    nx, ny, nz = int(dims[0]), int(dims[1]), int(dims[2])
    origin = tuple(scene_min.tolist())
    grid = GridSpec(origin=origin, voxel_size=voxel, shape=(nx, ny, nz))

    xs = scene_min[0] + (np.arange(nx) + 0.5) * voxel
    ys = scene_min[1] + (np.arange(ny) + 0.5) * voxel
    zs = scene_min[2] + (np.arange(nz) + 0.5) * voxel
    xx, yy, zz = np.meshgrid(xs, ys, zs, indexing="ij")
    centers = np.stack([xx, yy, zz], axis=-1)

    entity_masks: dict[str, np.ndarray] = {}
    anchor_mask = np.zeros((nx, ny, nz), dtype=bool)
    load_mask = np.zeros((nx, ny, nz), dtype=bool)
    obstacle_mask = np.zeros((nx, ny, nz), dtype=bool)
    preserve_mask = np.zeros((nx, ny, nz), dtype=bool)

    for entity in scene.entities:
        mask = point_mask_for_primitive(centers, entity.primitive)
        entity_masks[entity.name] = mask
        role = entity.role.lower()
        if role == "anchor":
            anchor_mask |= mask
        elif role == "load":
            load_mask |= mask
        elif role == "obstacle" or entity.avoid:
            obstacle_mask |= mask
        elif role == "preserve" or entity.preserve:
            preserve_mask |= mask
        else:
            preserve_mask |= mask if entity.structural else False

    domain_mask = np.ones((nx, ny, nz), dtype=bool)
    solids_mask = anchor_mask | load_mask | preserve_mask
    design_mask = domain_mask & ~obstacle_mask & ~solids_mask
    mandatory_design_mask = design_mask & dilate_mask(anchor_mask | load_mask, steps=1)
    connector_mask = design_mask.copy()

    return VoxelScene(
        grid=grid,
        anchor_mask=anchor_mask,
        load_mask=load_mask,
        obstacle_mask=obstacle_mask,
        preserve_mask=preserve_mask,
        design_mask=design_mask,
        mandatory_design_mask=mandatory_design_mask,
        connector_mask=connector_mask,
        domain_mask=domain_mask,
        entity_masks=entity_masks,
        scene=scene,
    )


def cell_centers_from_grid(grid: GridSpec) -> np.ndarray:
    ox, oy, oz = grid.origin
    v = grid.voxel_size
    xs = ox + (np.arange(grid.nx) + 0.5) * v
    ys = oy + (np.arange(grid.ny) + 0.5) * v
    zs = oz + (np.arange(grid.nz) + 0.5) * v
    xx, yy, zz = np.meshgrid(xs, ys, zs, indexing="ij")
    return np.stack([xx, yy, zz], axis=-1)


def voxel_index_to_element_id(i: int, j: int, k: int, shape: tuple[int, int, int]) -> int:
    nx, ny, _ = shape
    return k * nx * ny + i * ny + j


def mask_to_element_ids(mask: np.ndarray) -> np.ndarray:
    ids = []
    nx, ny, nz = mask.shape
    for i, j, k in np.argwhere(mask):
        ids.append(voxel_index_to_element_id(int(i), int(j), int(k), (nx, ny, nz)))
    return np.array(ids, dtype=int)


def mask_components(mask: np.ndarray) -> list[list[tuple[int, int, int]]]:
    visited = np.zeros_like(mask, dtype=bool)
    components: list[list[tuple[int, int, int]]] = []
    nx, ny, nz = mask.shape

    for seed in np.argwhere(mask & ~visited):
        sx, sy, sz = [int(v) for v in seed]
        queue = deque([(sx, sy, sz)])
        visited[sx, sy, sz] = True
        comp: list[tuple[int, int, int]] = []
        while queue:
            x, y, z = queue.popleft()
            comp.append((x, y, z))
            for dx, dy, dz in neighbors6():
                nx_, ny_, nz_ = x + dx, y + dy, z + dz
                if 0 <= nx_ < nx and 0 <= ny_ < ny and 0 <= nz_ < nz:
                    if mask[nx_, ny_, nz_] and not visited[nx_, ny_, nz_]:
                        visited[nx_, ny_, nz_] = True
                        queue.append((nx_, ny_, nz_))
        components.append(comp)
    return components


def connector_is_connected(anchor_mask: np.ndarray, load_mask: np.ndarray, connector_mask: np.ndarray, preserve_mask: np.ndarray) -> bool:
    structural = anchor_mask | load_mask | preserve_mask | connector_mask
    if not anchor_mask.any() or not load_mask.any():
        return False
    seed = tuple(int(v) for v in np.argwhere(anchor_mask)[0])
    target = load_mask

    visited = np.zeros_like(structural, dtype=bool)
    queue = deque([seed])
    visited[seed] = True

    nx, ny, nz = structural.shape
    while queue:
        x, y, z = queue.popleft()
        if target[x, y, z]:
            return True
        for dx, dy, dz in neighbors6():
            nx_, ny_, nz_ = x + dx, y + dy, z + dz
            if 0 <= nx_ < nx and 0 <= ny_ < ny and 0 <= nz_ < nz:
                if structural[nx_, ny_, nz_] and not visited[nx_, ny_, nz_]:
                    visited[nx_, ny_, nz_] = True
                    queue.append((nx_, ny_, nz_))
    return False
