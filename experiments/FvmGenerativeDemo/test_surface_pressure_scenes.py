#!/usr/bin/env python3
"""Fail-closed validation of engineering surface traction STL inputs.

Run: python test_surface_pressure_scenes.py FvmGenerativeDemo.dll scene.json
Requires no Python dependencies beyond stdlib.
"""
from pathlib import Path
import json
import subprocess
import sys
import tempfile

if len(sys.argv)!=3:
    raise SystemExit("Usage: test_surface_pressure_scenes.py solver.dll scene.json")

dll=Path(sys.argv[1]).resolve()
scene_file=Path(sys.argv[2]).resolve()
root=json.loads(scene_file.read_text(encoding="utf-8"))
source_stl=scene_file.parent/root["surfacePressurePatches"][0]["stlFile"]
assert dll.is_file() and source_stl.is_file()

def expect_failure(label,mutate,fragment):
    with tempfile.TemporaryDirectory(prefix="fvm_pressure_invalid_") as tmp:
        directory=Path(tmp)
        scene=json.loads(json.dumps(root))
        lines=source_stl.read_text(encoding="ascii").splitlines()
        mutate(scene,lines)
        patch_name=scene["surfacePressurePatches"][0]["stlFile"]
        (directory/patch_name).write_text("\n".join(lines)+"\n",encoding="ascii")
        path=directory/"scene.json"
        path.write_text(json.dumps(scene),encoding="utf-8")
        args=["dotnet",str(dll),"--validate-scene",str(path)]
        run=subprocess.run(args,capture_output=True,text=True,timeout=45)
        output=run.stdout+"\n"+run.stderr
        assert run.returncode!=0,(label,"invalid pressure patch accepted",output[-1500:])
        assert fragment.lower() in output.lower(),(label,"wrong validation error",output[-2200:])
        print("PASS negative surface patch:",label)

def reverse(scene,lines):
    indices=[i for i,v in enumerate(lines) if v.lstrip().startswith("vertex ")]
    for k in range(0,len(indices),3):
        first,second,third=indices[k:k+3]
        lines[second],lines[third]=lines[third],lines[second]

def detached(scene,lines):
    for i,v in enumerate(lines):
        cols=v.strip().split()
        if cols and cols[0]=="vertex":
            cols[1]=str(float(cols[1])+1.5)
            lines[i]="   "+" ".join(cols)

def wrong_case(scene,lines):
    scene["surfacePressurePatches"][0]["loadCase"]="not-a-declared-case"

def fixed(scene,lines):
    scene["surfacePressurePatches"][0]["bodyId"]="fixed-sphere"

def zero_pressure(scene,lines):
    scene["surfacePressurePatches"][0]["pressureMpa"]=0

expect_failure("inward triangle normal",reverse,"not outward")
expect_failure("pressure outside designated body's boundary",detached,"not on body")
expect_failure("unknown pressure load case",wrong_case,"unknown load case")
expect_failure("attempt to pressure-load fixed body",fixed,"nonfixed body")
expect_failure("zero MPa pressure",zero_pressure,"nonzero")
print("PASS five malformed surface-loading scenes rejected")

# Free density starts as a nearly solid filled design region. The exposed
# pressure patch from the SOURCE box can therefore become buried beneath
# extra design material. Such a pressure-only FEM must NOT quietly accept
# an internal artificial load as if it were an external surface traction.
with tempfile.TemporaryDirectory(prefix="fvm_pressure_buried_") as tmp:
    directory=Path(tmp)
    output=directory/"output"
    # Suppress the *new* automatic pressure-contact access cavity on purpose.
    # The formerly incorrect geometry must still be rejected fail-closed.
    legacy=json.loads(json.dumps(root))
    legacy["pressureContactVoid"]=False
    (directory/source_stl.name).write_bytes(source_stl.read_bytes())
    config=directory/"buried.json"
    config.write_text(json.dumps(legacy),encoding="utf8")
    cmd=["dotnet",str(dll),"--free-topology",str(config),
         "--iterations","1","--out",str(output)]
    run=subprocess.run(cmd,capture_output=True,text=True,timeout=90)
    log=run.stdout+"\n"+run.stderr
    assert run.returncode!=0,("Buried pressure surface was incorrectly accepted",log[-2200:])
    assert "pressure face is buried" in log.lower(),log[-2200:]
    assert not (output/"optimized.stl").exists(),"Invalid buried-load STL exported"
    print("PASS free-density rejects a surface-pressure patch buried by design material")

