#!/usr/bin/env python3
"""Independent source-scene and free-topology STL/metric validation.

Requires standard Python only. Does not call C# geometry, FEM or 5-M routines.
"""
from pathlib import Path
import json
import math
import sys
import struct
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
    assert summary["acceptedUpdates"]>=1
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
    # A centre-only test is insufficient: it can pass while a significant
    # fraction of a required input fixture is missing. Sample multiple
    # well-inside locations, independently of FVM and original C# CSG.
    def fixture_samples(body):
        shape=body["shape"]
        kind=shape["type"]
        if kind=="union":
            # Every component of the required CSG union must survive.
            # A single sample at the centroid could miss an entire lobe.
            samples=[]
            for child in shape["children"]:
                samples.extend(fixture_samples({"id":body["id"],"shape":child}))
            return samples
        if kind in ("intersection","difference"):
            # A complex Boolean requires an interior witness; a supplied seed
            # is unambiguous for an arbitrary non-convex/eroded solid.
            seed=body.get("seed")
            if seed is not None:return [seed]
            first=fixture_samples({"id":body["id"],"shape":shape["children"][0]})
            # Analytic signed-geometry checks on simple primitives.
            def primitive_inside(part,p):
                t=part["type"]
                if t=="sphere":
                    return sum((p[k]-part["center"][k])**2 for k in range(3))<part["radius"]**2
                if t=="box":
                    return all(abs(p[k]-part["center"][k])<part["halfSize"][k] for k in range(3))
                if t=="ellipsoid":
                    return sum(((p[k]-part["center"][k])/part["radii"][k])**2 for k in range(3))<1
                raise AssertionError("Complex intersect/difference requires explicit validation seed")
            def inside_csg(p):
                flags=[primitive_inside(c,p) for c in shape["children"]]
                return all(flags) if kind=="intersection" else flags[0] and not any(flags[1:])
            candidates=[p for p in first if inside_csg(p)]
            assert candidates,(
                f"No independent interior witness for CSG {body['id']}; "
                "specify an interior seed in the test fixture")
            return candidates
        centre=body.get("seed",shape.get("center"))
        if centre is None and "a" in shape and "b" in shape:
            centre=[(shape["a"][k]+shape["b"][k])*.5 for k in range(3)]
        if kind=="mesh":
            path=(Path(scene_path).parent/shape["file"]).resolve()
            raw=path.read_bytes()
            if len(raw)>=84 and 84+50*struct.unpack_from("<I",raw,80)[0]==len(raw):
                points=[]
                num=struct.unpack_from("<I",raw,80)[0]
                for i in range(num):
                    off=84+i*50
                    for j in range(3):
                        points.append(struct.unpack_from("<fff",raw,off+12+j*12))
            else:
                points=[tuple(map(float,line.split()[1:])) for line in
                        path.read_text(encoding="utf8").splitlines()
                        if line.strip().startswith("vertex ")]
            assert points,(path,"No vertices")
            bounds_lo=[min(p[k] for p in points) for k in range(3)]
            bounds_hi=[max(p[k] for p in points) for k in range(3)]
            centre=centre or [(a+b)*.5 for a,b in zip(bounds_lo,bounds_hi)]
            # The centroid is not a guaranteed interior point for every concave
            # STL; complex fixtures should supply a known-inside seed.
            offsets=[.13*(hi-lo) for lo,hi in zip(bounds_lo,bounds_hi)]
        elif kind in ("sphere","box","ellipsoid","torus"):
            assert centre is not None,body
            if kind=="sphere":
                offsets=[.22*shape["radius"]]*3
            elif kind=="box":
                offsets=[.22*v for v in shape["halfSize"]]
            elif kind=="ellipsoid":
                offsets=[.22*v for v in shape["radii"]]
            else:
                # Torus uses a point on the ring, not the central empty hole.
                centre=[centre[0]+shape["majorRadius"],centre[1],centre[2]]
                offsets=[.20*shape["minorRadius"]]*3
        elif kind in ("capsule","cylinder"):
            assert centre is not None,body
            a,b=shape["a"],shape["b"]
            return [centre,
                    [(3*a[k]+b[k])*.25 for k in range(3)],
                    [(a[k]+3*b[k])*.25 for k in range(3)]]
        else:
            assert centre is not None,(body["id"],"Explicit seed required to validate complex CSG")
            return [centre]
        result=[centre]
        for axis in range(3):
            for side in (-1,+1):
                p=list(centre)
                p[axis]+=side*offsets[axis]
                result.append(p)
        return result

    checked=0;checked_points=0
    for body in scene["bodies"]:
        samples=fixture_samples(body)
        assert samples,("Empty fixture samples",body["id"])
        for point in samples:
            assert contains(point),(
                f"Prescribed base body '{body['id']}' loses interior point {point} "
                "in optimized STL")
            checked_points+=1
        checked+=1
    assert checked==len(scene["bodies"])
    print("PASS FREE TOPOLOGY: maintained bodies",checked,
          "sampled protected interior points",checked_points,
          "FVM variables",summary["variables"],"voids",summary["nucleatedVoidPatches"],
          "FEM reduction",f"{summary['volumeReductionPercent']:.3f}%",
          "binary VM",f"{summary['binaryMaximumVonMisesMpa']:.4f} MPa")

if __name__=="__main__":
    if len(sys.argv) not in (3,4):raise SystemExit("Usage: validate_free_topology.py scene.json output_dir [--require-void]")
    validate(sys.argv[1],sys.argv[2],"--require-void" in sys.argv[3:])
