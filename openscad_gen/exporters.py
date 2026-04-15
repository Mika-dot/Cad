from __future__ import annotations

import csv
import json
from pathlib import Path

import numpy as np

from .models import Entity, IterationMetrics, Scene, VoxelScene


def _repr_scad(value: object) -> str:
    if isinstance(value, (list, tuple)):
        return "[" + ", ".join(_repr_scad(v) for v in value) + "]"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return f"{float(value):.6g}"
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    return repr(value)


def tree_to_scad(node: dict, indent: int = 0) -> str:
    pad = " " * indent
    typ = node.get("type")
    if typ == "empty":
        return pad + "// empty"
    if typ == "union":
        children = node.get("children", [])
        inner = "\n".join(tree_to_scad(child, indent + 2) for child in children)
        return f"{pad}union() {{\n{inner}\n{pad}}}"
    if typ == "difference":
        items = [node.get("base")] + list(node.get("subtract", []))
        inner = "\n".join(tree_to_scad(child, indent + 2) for child in items if child is not None)
        return f"{pad}difference() {{\n{inner}\n{pad}}}"
    if typ == "translate":
        vec = _repr_scad(node.get("vec", [0, 0, 0]))
        child = tree_to_scad(node["child"], indent + 2)
        return f"{pad}translate({vec})\n{child}"
    if typ == "rotate":
        angles = _repr_scad(node.get("angles", [0, 0, 0]))
        child = tree_to_scad(node["child"], indent + 2)
        return f"{pad}rotate({angles})\n{child}"
    if typ == "cube":
        params = dict(node.get("params", {}))
        size = params.get("size", 1)
        center = params.get("center", False)
        return f"{pad}cube({_repr_scad(size)}, center = {'true' if center else 'false'});"
    if typ == "sphere":
        params = dict(node.get("params", {}))
        r = params.get("r", 1)
        return f"{pad}sphere(r = {_repr_scad(r)});"
    if typ == "cylinder":
        params = dict(node.get("params", {}))
        args = [f"h = {_repr_scad(params.get('h', 1))}"]
        if "r" in params:
            args.append(f"r = {_repr_scad(params['r'])}")
        elif "r1" in params:
            args.append(f"r1 = {_repr_scad(params['r1'])}")
            if "r2" in params:
                args.append(f"r2 = {_repr_scad(params['r2'])}")
        if "center" in params:
            args.append(f"center = {'true' if params['center'] else 'false'}")
        if "$fn" in params:
            args.append(f"$fn = {_repr_scad(params['$fn'])}")
        return f"{pad}cylinder({', '.join(args)});"
    raise ValueError(f"Unsupported tree node: {typ}")


def primitive_to_scad(entity: Entity) -> str:
    tx, ty, tz = entity.primitive.translate
    p = entity.primitive
    if p.kind == "cube":
        size = p.params.get("size", 1)
        size_repr = _repr_scad(size)
        center = "true" if p.params.get("center", False) else "false"
        body = f"cube({size_repr}, center = {center});"
    elif p.kind == "sphere":
        body = f"sphere(r = {float(p.params['r']):.6g});"
    elif p.kind == "cylinder":
        center = "true" if p.params.get("center", False) else "false"
        body = f"cylinder(h = {float(p.params['h']):.6g}, r = {float(p.params.get('r', 1.0)):.6g}, center = {center}, $fn = 64);"
    elif p.kind == "compound":
        return tree_to_scad(p.params["tree"])
    else:
        raise ValueError(f"Unsupported primitive: {p.kind}")
    return f"translate([{tx:.6g}, {ty:.6g}, {tz:.6g}]) {body}"


def export_structured_scene(path: Path, scene: Scene) -> None:
    cfg = scene.config
    with path.open("w", encoding="utf-8") as f:
        f.write("/*GD_SCENE\n")
        for key, value in cfg.to_dict().items():
            f.write(f"{key}: {_repr_scad(value)}\n")
        f.write("*/\n\n")
        for entity in scene.entities:
            f.write("/*GD_ENTITY\n")
            f.write(f"name: {_repr_scad(entity.name)}\n")
            f.write(f"role: {_repr_scad(entity.role)}\n")
            f.write(f"material: {_repr_scad(entity.material)}\n")
            f.write(f"fix: {_repr_scad(list(entity.fix))}\n")
            f.write(f"force: {_repr_scad(list(entity.force))}\n")
            f.write(f"connect: {_repr_scad(entity.connect)}\n")
            f.write(f"structural: {_repr_scad(entity.structural)}\n")
            f.write(f"preserve: {_repr_scad(entity.preserve)}\n")
            f.write(f"avoid: {_repr_scad(entity.avoid)}\n")
            f.write("*/\n")
            f.write(primitive_to_scad(entity) + "\n\n")


def export_connector_scad(path: Path, voxel_scene: VoxelScene, connector_mask: np.ndarray, subtract_source: bool = True) -> None:
    ox, oy, oz = voxel_scene.grid.origin
    voxel = voxel_scene.grid.voxel_size
    with path.open("w", encoding="utf-8") as f:
        f.write("// Auto-generated PETG connector\n")
        if subtract_source:
            f.write("difference() {\n")
            f.write("  union() {\n")
            for i, j, k in np.argwhere(connector_mask):
                cx = ox + (i + 0.5) * voxel
                cy = oy + (j + 0.5) * voxel
                cz = oz + (k + 0.5) * voxel
                f.write(f"    translate([{cx:.6g}, {cy:.6g}, {cz:.6g}]) cube([{voxel:.6g}, {voxel:.6g}, {voxel:.6g}], center = true);\n")
            f.write("  }\n")
            f.write("  union() {\n")
            for entity in voxel_scene.scene.entities:
                for line in primitive_to_scad(entity).splitlines() or [primitive_to_scad(entity)]:
                    f.write(f"    {line}\n")
            f.write("  }\n")
            f.write("}\n")
        else:
            f.write("union() {\n")
            for i, j, k in np.argwhere(connector_mask):
                cx = ox + (i + 0.5) * voxel
                cy = oy + (j + 0.5) * voxel
                cz = oz + (k + 0.5) * voxel
                f.write(f"  translate([{cx:.6g}, {cy:.6g}, {cz:.6g}]) cube([{voxel:.6g}, {voxel:.6g}, {voxel:.6g}], center = true);\n")
            f.write("}\n")


def export_scene_preview(path: Path, voxel_scene: VoxelScene) -> None:
    connector_name = "final_connector.scad"
    with path.open("w", encoding="utf-8") as f:
        f.write("// Auto-generated preview scene\n")
        for entity in voxel_scene.scene.entities:
            f.write(primitive_to_scad(entity) + "\n")
        f.write(f"include <{connector_name}>;\n")


def export_metrics_csv(path: Path, metrics: list[IterationMetrics]) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(metrics[0].to_dict().keys()) if metrics else [])
        if metrics:
            writer.writeheader()
            for item in metrics:
                writer.writerow(item.to_dict())


def export_summary_json(path: Path, payload: dict) -> None:
    with path.open("w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
