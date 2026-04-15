from __future__ import annotations

import argparse
from pathlib import Path

from openscad_gen import run_pipeline


def build_argparser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Voxel generative connector from structured OpenSCAD scene")
    parser.add_argument("--scene", type=Path, default=Path("test.scad"), help="Path to structured .scad file")
    parser.add_argument("--output-root", type=Path, default=None, help="Optional output root directory")
    return parser


if __name__ == "__main__":
    args = build_argparser().parse_args()
    run_dir = run_pipeline(args.scene, args.output_root)
    print(f"[DONE] Results saved to: {run_dir}")
