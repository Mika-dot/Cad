from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path
from typing import Any

import numpy as np

from .models import VoxelScene


FORMAT_VERSION = 1


def export_dcad_field(
    path: str | Path,
    voxel_scene: VoxelScene,
    *,
    density: np.ndarray | None = None,
    stress: np.ndarray | None = None,
    displacement: np.ndarray | None = None,
    metadata: dict[str, Any] | None = None,
) -> Path:
    """Export a compact interchange field for the future unified DCad application.

    Arrays are stored in compressed NPZ. Geometry is described by origin, voxel size and grid shape;
    masks use the same ijk indexing as the FEM/topology optimizer. Optional scalar/vector fields can
    be consumed by VoxelCAD or a renderer without parsing OpenSCAD.
    """
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)

    payload: dict[str, Any] = {
        "format_version": np.asarray([FORMAT_VERSION], dtype=np.int32),
        "origin": np.asarray(voxel_scene.grid.origin, dtype=np.float64),
        "voxel_size": np.asarray([voxel_scene.grid.voxel_size], dtype=np.float64),
        "shape": np.asarray(voxel_scene.grid.shape, dtype=np.int32),
        "anchor_mask": np.asarray(voxel_scene.anchor_mask, dtype=np.uint8),
        "load_mask": np.asarray(voxel_scene.load_mask, dtype=np.uint8),
        "obstacle_mask": np.asarray(voxel_scene.obstacle_mask, dtype=np.uint8),
        "preserve_mask": np.asarray(voxel_scene.preserve_mask, dtype=np.uint8),
        "design_mask": np.asarray(voxel_scene.design_mask, dtype=np.uint8),
        "connector_mask": np.asarray(voxel_scene.connector_mask, dtype=np.uint8),
    }
    if density is not None:
        payload["density"] = np.asarray(density, dtype=np.float32)
    if stress is not None:
        payload["stress"] = np.asarray(stress, dtype=np.float32)
    if displacement is not None:
        payload["displacement"] = np.asarray(displacement, dtype=np.float32)

    manifest = {
        "format": "dcad-field",
        "version": FORMAT_VERSION,
        "units": {"length": "mm", "force": "N", "stress": "N/mm^2"},
        "grid": {
            "origin": list(voxel_scene.grid.origin),
            "voxel_size": float(voxel_scene.grid.voxel_size),
            "shape": list(voxel_scene.grid.shape),
        },
        "scene_config": voxel_scene.scene.config.to_dict(),
        "metadata": metadata or {},
    }
    payload["manifest_json"] = np.asarray(json.dumps(manifest, ensure_ascii=False))
    np.savez_compressed(target, **payload)
    return target


def load_dcad_field(path: str | Path) -> dict[str, Any]:
    """Load DCad field NPZ and validate the small stable interchange header."""
    with np.load(Path(path), allow_pickle=False) as data:
        result = {name: data[name].copy() for name in data.files if name != "manifest_json"}
        manifest = json.loads(str(data["manifest_json"]))
    if manifest.get("format") != "dcad-field":
        raise ValueError("Not a dcad-field archive")
    if int(manifest.get("version", -1)) != FORMAT_VERSION:
        raise ValueError(f"Unsupported dcad-field version: {manifest.get('version')}")
    result["manifest"] = manifest
    return result


def density_vector_to_grid(voxel_scene: VoxelScene, element_voxels: np.ndarray, design_elem_ids: np.ndarray, rho: np.ndarray) -> np.ndarray:
    """Map optimizer design-element density into the common dense grid indexing."""
    out = np.zeros(voxel_scene.grid.shape, dtype=np.float32)
    if len(design_elem_ids) == 0:
        return out
    ijk = np.asarray(element_voxels)[np.asarray(design_elem_ids)]
    values = np.asarray(rho, dtype=np.float32)
    if ijk.shape[0] != values.shape[0]:
        raise ValueError("rho size does not match design element count")
    out[ijk[:, 0], ijk[:, 1], ijk[:, 2]] = values
    return out
