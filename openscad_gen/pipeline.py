from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path
from typing import Callable

from .exporters import export_connector_scad, export_metrics_csv, export_scene_preview, export_summary_json
from .geometry import build_voxel_scene
from .logging_utils import configure_logging
from .optimizer import optimize_connector
from .parser import parse_auto_scene
from .visualize import build_gif, render_domain_only, render_voxel_state

ProgressCallback = Callable[[list, dict], None]


def _write_status(run_dir: Path, metrics: list, payload: dict) -> None:
    if metrics:
        export_metrics_csv(run_dir / "metrics_live.csv", metrics)
    status = {"updated_at": datetime.now().isoformat(timespec="seconds"), "metrics_count": len(metrics), **payload}
    (run_dir / "status.json").write_text(json.dumps(status, ensure_ascii=False, indent=2), encoding="utf-8")


def run_pipeline(scene_path: str | Path, output_root: str | Path | None = None, progress_callback: ProgressCallback | None = None) -> Path:
    scene_path = Path(scene_path)
    output_root = Path(output_root) if output_root else scene_path.parent / "output"
    run_dir = output_root / f"run_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    frames_dir = run_dir / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)

    logger = configure_logging(run_dir)
    metrics: list = []

    try:
        logger.info("Loading scene: %s", scene_path)
        _write_status(run_dir, [], {"state": "loading", "phase": "parse", "scene": str(scene_path)})
        scene = parse_auto_scene(scene_path)
        voxel_scene = build_voxel_scene(scene)

        render_domain_only(frames_dir / "001_objects_and_domain.png", voxel_scene, "Objects + auto-built design domain")
        render_voxel_state(
            frames_dir / "002_initial_connector_box.png",
            voxel_scene,
            voxel_scene.connector_mask,
            "Initial design volume",
            f"connector voxels={int(voxel_scene.connector_mask.sum())}",
        )
        _write_status(
            run_dir,
            [],
            {
                "state": "running",
                "phase": "voxelized",
                "iteration": 0,
                "latest_frame": str(frames_dir / "002_initial_connector_box.png"),
                "latest_stress_frame": None,
            },
        )

        def callback(new_metrics: list, payload: dict) -> None:
            metrics[:] = new_metrics
            _write_status(run_dir, new_metrics, payload)
            if progress_callback is not None:
                progress_callback(new_metrics, payload)

        best_mask, fem_result, metrics = optimize_connector(voxel_scene, frames_dir, logger, progress_callback=callback)

        export_connector_scad(run_dir / "final_connector.scad", voxel_scene, best_mask, subtract_source=True)
        export_scene_preview(run_dir / "final_scene_preview.scad", voxel_scene)
        if metrics:
            export_metrics_csv(run_dir / "metrics.csv", metrics)
        summary = {
            "scene": str(scene_path),
            "config": scene.config.to_dict(),
            "final_connector_voxels": int(best_mask.sum()),
            "final_connector_volume": float(best_mask.sum()) * voxel_scene.grid.voxel_size ** 3,
            "final_max_connector_vm": float(fem_result.connector_max_vm),
            "final_max_abs_vm": float(fem_result.abs_max_vm),
            "final_max_displacement": float(fem_result.max_displacement),
            "final_compliance": float(fem_result.compliance),
            "frames": len(list(frames_dir.glob("*.png"))),
        }
        export_summary_json(run_dir / "summary.json", summary)
        build_gif(frames_dir, run_dir / "animation.gif", fps=2)
        _write_status(
            run_dir,
            metrics,
            {
                "state": "completed",
                "phase": "done",
                "iteration": len(metrics) - 1 if metrics else 0,
                "latest_frame": str(frames_dir / "999_final.png"),
                "latest_stress_frame": str(frames_dir / "999_final_stress.png"),
                "summary": summary,
            },
        )
        logger.info("Finished. Results in: %s", run_dir)
        return run_dir
    except Exception as exc:
        logger.exception("Pipeline failed: %s", exc)
        _write_status(
            run_dir,
            metrics,
            {
                "state": "failed",
                "phase": "error",
                "iteration": len(metrics),
                "reason": str(exc),
            },
        )
        raise
