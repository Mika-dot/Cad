#!/usr/bin/env python3
"""Prove that pressure-driven designs with unsteady Hex8 stresses cannot be
exported under the strict three-level mesh verification switch.
"""
from pathlib import Path
import json
import subprocess
import sys

if len(sys.argv) != 4:
    raise SystemExit("Usage: test_pressure_strict_convergence.py solver.dll scene.json output_dir")

solver = Path(sys.argv[1]).resolve()
scene = Path(sys.argv[2]).resolve()
out = Path(sys.argv[3]).resolve()
assert solver.is_file() and scene.is_file()
assert not (out/"optimized.stl").exists(), "Output already contains stale optimized STL"
command = [
    "dotnet", str(solver),
    "--free-topology", str(scene),
    "--iterations", "2",
    "--refinement-report",
    "--require-mesh-convergence",
    "--out", str(out),
]
run = subprocess.run(command, stdout=subprocess.PIPE,
                     stderr=subprocess.STDOUT, text=True, timeout=180)
assert run.returncode != 0, (
    "Strict mesh verification unexpectedly accepted unstable pressure FEM:\n"
    + run.stdout[-2000:]
)
assert "MESH CONVERGENCE NOT DEMONSTRATED" in run.stdout, (
    "Wrong reason for rejecting non-converged pressure FEM:\n"
    + run.stdout[-2200:]
)
assert not (out/"optimized.stl").exists(), (
    "Unsafe non-converged pressure-loaded output was exported"
)
report = json.loads((out/"mesh_refinement.json").read_text())
assert report["convergenceScreenPassed"] is False
assert len(report["transitions"]) == 2
assert abs(report["transitions"][0]["relativeStressPercent"]) > 10
assert abs(report["transitions"][1]["relativeStressPercent"]) < 10
print("PASS PRESSURE MESH GATE: no optimized.stl exported; "
      "large first stress drift cannot be hidden by small last drift")
