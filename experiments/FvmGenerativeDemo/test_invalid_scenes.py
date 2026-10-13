#!/usr/bin/env python3
"""Negative integration tests: never report success for undefined FEM boundary conditions."""
from __future__ import annotations
import json
from pathlib import Path
import subprocess
import sys
import tempfile

if len(sys.argv)!=2:
    raise SystemExit("Usage: test_invalid_scenes.py path/to/FvmGenerativeDemo.dll")

exe=Path(sys.argv[1]).resolve()
assert exe.exists(), exe

def run_bad(label, scene, expected):
    with tempfile.TemporaryDirectory(prefix="fvm_invalid_") as td:
        cfg=Path(td)/"invalid.json"
        cfg.write_text(json.dumps(scene),encoding="utf-8")
        cmd=["dotnet",str(exe),"--multi-scene",str(cfg),"--out",str(Path(td)/"output")]
        process=subprocess.run(cmd,capture_output=True,text=True,timeout=60)
        result=process.stdout+"\n"+process.stderr
        assert process.returncode!=0,(label,"invalid scene incorrectly succeeded",result[-800:])
        assert expected.lower() in result.lower(),(label,"wrong rejection",result[-1600:])
        print(f"PASS invalid-scene correctly rejected: {label}")

scene={
    "bounds":{"min":[-17,-17,-17],"max":[49,17,17]},
    "routingStepMm":6,"fvmStepMm":4,"bridgeRadiusMm":4,
    "femCells":[8,6,6],"iterations":1,
    "bodies":[
        {"id":"base","role":"fixed","shape":{"type":"sphere","center":[0,0,0],"radius":10}},
        {"id":"moving","role":"load","shape":{"type":"sphere","center":[3,0,0],"radius":10},"forceN":[100,0,0]}
    ]
}
run_bad("loaded nodes overlap fixed nodes",scene,"shares fixed FEM nodes")
copy=json.loads(json.dumps(scene))
copy["bodies"][0]["role"]="anchor"
copy["bodies"][1]["shape"]["center"]=[30,0,0]
copy["bodies"][1]["seed"]=[30,0,0]
run_bad("no declared physical support",copy,"at least one fixed")
conflict=json.loads(json.dumps(scene))
conflict["bodies"][1]["shape"]["center"]=[30,0,0]
conflict["bodies"][1]["seed"]=[30,0,0]
conflict["obstacles"]=[{
    "id":"forbidden-cuts-fixed-shell",
    "shape":{"type":"sphere","center":[10,0,0],"radius":3},
    "clearanceMm":0.0
}]
run_bad("mandatory fixture intersects forbidden region away from seed",
        conflict,"overlaps prohibited keepout")
# Regression: the real six-body demonstrator originally included a
# geometric contradiction (the protected anchor cylinder penetrated the
# lower forbidden block at a point far away from its supplied seed). A
# simplistic seed-only check accepted it and destroyed protected geometry.
fixture_path=Path(__file__).with_name("multi_scene_6_bodies.json")
fixture=json.loads(fixture_path.read_text(encoding="utf8"))
collision=json.loads(json.dumps(fixture))
old_keepout=next(x for x in collision["obstacles"] if x["id"]=="lower-block")
old_keepout["shape"]["center"]=[38,-6,0]
old_keepout["shape"]["halfSize"]=[6,5,10]
run_bad("real six-body forbidden volume clips protected cylindrical anchor",
        collision,"overlaps prohibited keepout")
print("PASS four invalid geometry and FEM scenarios fail closed")
