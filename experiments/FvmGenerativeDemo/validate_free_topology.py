#!/usr/bin/env python3
"""Independent source-scene and free-topology STL/metric validation.

Requires standard Python only. Does not call C# geometry, FEM or 5-M routines.
"""
from pathlib import Path
import json
import math
import sys
from validate_keepouts import inspect_stl

def validate(scene_path,results,require_void=False):
    scene=json.loads(Path(scene_path).read_text(encoding="utf8"))
    out=Path(results)
    summary=json.loads((out/"summary.json").read_text(encoding="utf8"))
    assert summary["status"]=="PASS"
    assert summary["mode"]=="free-topology-fvm-simp-oc"
    assert summary["inputBodies"]==len(scene["bodies"])
    assert summary["obstacles"]==len(scene.get("obstacles",[]))
    assert summary["variables"]>40
    assert summary["acceptedUpdates"]>=2
    if require_void:assert summary["nucleatedVoidPatches"]>=1
    assert summary["volumeReductionPercent"]>5
    assert summary["finalVolumeMm3"]<summary["initialVolumeMm3"]
    lim=scene["limits"]
    for name,limit in [("maximumVonMisesMpa","vonMisesMpa"),
                       ("binaryMaximumVonMisesMpa","vonMisesMpa"),
                       ("maximumDisplacementMm","displacementMm"),
                       ("binaryMaximumDisplacementMm","displacementMm")]:
        assert summary[name]<=lim[limit],(name,summary[name],lim[limit])
    for key,value in summary.items():
        if isinstance(value,float):
            assert math.isfinite(value),(key,value)
    assert summary["binaryFvmVolumeMm3"]>0
    assert summary["binaryComplianceNmm"]>0

    obstacles=scene.get("obstacles",[])
    if obstacles:
        # Independent check at seven points per STL face, not just its vertices.
        inspect_stl(str(out/"initial.stl"),obstacles)
        inspect_stl(str(out/"optimized.stl"),obstacles)

    # Test that the protected *input geometry* centres remain inside the
    # actually exported STL, not merely that the FEM reports connectivity.
    triangles=[]
    tri=[]
    for line in (out/"optimized.stl").read_text(encoding="utf8").splitlines():
        fields=line.strip().split()
        if len(fields)==4 and fields[0]=="vertex":
            tri.append(tuple(map(float,fields[1:])))
            if len(tri)==3:
                triangles.append(tuple(tri));tri=[]
    # Non-axis-aligned ray avoids coplanar triangle faces of box fixtures.
    direction=(1.,0.12345671,0.23456789)
    def dot(a,b): return sum(x*y for x,y in zip(a,b))
    def sub(a,b): return tuple(x-y for x,y in zip(a,b))
    def cross(a,b):
        return (a[1]*b[2]-a[2]*b[1],
                a[2]*b[0]-a[0]*b[2],
                a[0]*b[1]-a[1]*b[0])
    def contains(point):
        hit=0
        for a,b,c in triangles:
            edge1=sub(b,a);edge2=sub(c,a)
            h=cross(direction,edge2)
            det=dot(edge1,h)
            if abs(det)<1e-12:continue
            inv=1/det
            s=sub(point,a)
            u=inv*dot(s,h)
            if not (1e-9<u<1-1e-9):continue
            q=cross(s,edge1)
            v=inv*dot(direction,q)
            if not (1e-9<v and u+v<1-1e-9):continue
            t=inv*dot(edge2,q)
            if t>1e-9:hit+=1
        return hit%2==1
    checked=0
    for body in scene["bodies"]:
        shape=body["shape"]
        point=body.get("seed",shape.get("center"))
        if point is None:continue
        assert contains(point),f"Prescribed base body missing in optimized STL: {body['id']}"
        checked+=1
    assert checked>=2
    print("PASS FREE TOPOLOGY: maintained bodies",checked,
          "FVM variables",summary["variables"],"voids",summary["nucleatedVoidPatches"],
          "FEM reduction",f"{summary['volumeReductionPercent']:.3f}%",
          "binary VM",f"{summary['binaryMaximumVonMisesMpa']:.4f} MPa")

if __name__=="__main__":
    if len(sys.argv) not in (3,4):raise SystemExit("Usage: validate_free_topology.py scene.json output_dir [--require-void]")
    validate(sys.argv[1],sys.argv[2],"--require-void" in sys.argv[3:])
