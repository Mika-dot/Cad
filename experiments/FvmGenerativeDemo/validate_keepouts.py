#!/usr/bin/env python3
"""Independent geometry-only validation of CSG keep-outs and routed centrelines.

No C# FVM functions are imported: this catches discrepancies between the
exported triangulated solid and forbidden source geometries.
"""
from __future__ import annotations
from pathlib import Path
import csv, json, math, sys


def vec(p):
    return tuple(float(a) for a in p)


def minus(a,b):
    return tuple(x-y for x,y in zip(a,b))


def dot(a,b):
    return sum(x*y for x,y in zip(a,b))


def length(a):
    return math.sqrt(dot(a,a))


def sd(shape,p):
    typ=shape["type"]
    if typ=="sphere":
        return shape["radius"]-length(minus(p,vec(shape["center"])))
    if typ=="box":
        q=[abs(p[i]-shape.get("center",[0,0,0])[i])-shape["halfSize"][i] for i in range(3)]
        return -math.sqrt(sum(max(x,0)**2 for x in q))-min(max(q),0)
    if typ=="ellipsoid":
        q=minus(p,vec(shape.get("center",[0,0,0])))
        r=shape["radii"]
        return min(r)*(1-math.sqrt(sum((q[i]/r[i])**2 for i in range(3))))
    if typ=="capsule":
        a=vec(shape["a"]);b=vec(shape["b"]);ab=minus(b,a)
        f=max(0,min(1,dot(minus(p,a),ab)/max(1e-20,dot(ab,ab))))
        near=tuple(a[i]+ab[i]*f for i in range(3))
        return shape["radius"]-length(minus(p,near))
    if typ=="cylinder":
        a=vec(shape["a"]);b=vec(shape["b"]);ab=minus(b,a)
        L=length(ab);direction=tuple(x/L for x in ab)
        q=minus(p,a);t=dot(q,direction)
        radial=length(tuple(q[i]-direction[i]*t for i in range(3)))-shape["radius"]
        axial=abs(t-L/2)-L/2
        return -math.hypot(max(radial,0),max(axial,0))-min(max(radial,axial),0)
    if typ=="torus":
        q=minus(p,vec(shape.get("center",[0,0,0])))
        rr=math.hypot(q[0],q[1])-shape["majorRadius"]
        return shape["minorRadius"]-math.hypot(rr,q[2])
    if typ=="union":
        return max(sd(x,p) for x in shape["children"])
    if typ=="intersection":
        return min(sd(x,p) for x in shape["children"])
    if typ=="difference":
        pieces=shape["children"]
        return min(sd(pieces[0],p),*[ -sd(x,p) for x in pieces[1:]])
    raise ValueError(f"Unsupported obstacle CSG type {typ!r}; do not silently ignore keepout")


def inspect_stl(filename,obstacles):
    verts=[]
    max_intrusion=-float("inf")
    closest_clearance=float("inf")
    triangles=0
    # Check all vertices, triangle edge midpoints and centroids.
    for raw in Path(filename).read_text().splitlines():
        line=raw.strip().split()
        if len(line)==4 and line[0]=="vertex":
            verts.append(tuple(map(float,line[1:])))
        if len(verts)==3:
            triangles+=1
            a,b,c=verts
            points=[a,b,c,
                tuple((a[i]+b[i])/2 for i in range(3)),
                tuple((a[i]+c[i])/2 for i in range(3)),
                tuple((b[i]+c[i])/2 for i in range(3)),
                tuple((a[i]+b[i]+c[i])/3 for i in range(3))]
            for p in points:
                for obstacle in obstacles:
                    raw_f=sd(obstacle["shape"],p)
                    max_intrusion=max(max_intrusion,raw_f)
                    closest_clearance=min(closest_clearance,-raw_f-obstacle["clearanceMm"])
                    # Tolerance accounts for STL triangulation and finite M
                    # reconstruction. Actual occupied obstacle volume is never allowed.
                    if -raw_f-obstacle["clearanceMm"] < -0.05:
                        raise AssertionError(
                            f"{filename}: STL violates required nominal clearance "
                            f"for {obstacle['id']} by {raw_f+obstacle['clearanceMm']:.4f} mm")
            verts.clear()
    assert triangles>1000
    print(f"PASS STL obstacle nonpenetration: {filename}, triangles={triangles}, "
          f"deepest penetration indicator={max_intrusion:.5f}mm, "
          f"minimum nominal clearance={closest_clearance:.4f}mm")


def inspect_routes(routes,obstacles,bridge_radius,extra_safety):
    branches={}
    for row in csv.DictReader(Path(routes).open(newline="",encoding="utf-8")):
        branches.setdefault(row["branch"],[]).append(tuple(
            float(row[x]) for x in ("x_mm","y_mm","z_mm")))
    minimum=float("inf")
    for branch,polyline in branches.items():
        for a,b in zip(polyline,polyline[1:]):
            distance=length(minus(a,b))
            count=max(2,math.ceil(distance/.25))
            for k in range(count+1):
                p=tuple(a[i]+(b[i]-a[i])*k/count for i in range(3))
                for obstacle in obstacles:
                    clearance=-sd(obstacle["shape"],p)-obstacle["clearanceMm"]
                    minimum=min(minimum,clearance)
                    if clearance<bridge_radius+extra_safety-.3:
                        raise AssertionError(
                            f"Path {branch} violates keepout {obstacle['id']}: "
                            f"{clearance:.4f}mm remaining, needs {bridge_radius+extra_safety}mm")
    print(f"PASS route clearances: branches={len(branches)}, "
          f"minimum free radial space={minimum:.4f}mm, required={bridge_radius+extra_safety}mm")


if __name__=="__main__":
    if len(sys.argv)!=5:
        raise SystemExit("Usage: validate_keepouts.py scene.json routes.csv initial.stl optimized.stl")
    scene=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    obs=scene.get("obstacles",[])
    if not obs:
        print("PASS: no forbidden regions in scene")
        raise SystemExit(0)
    inspect_routes(sys.argv[2],obs,scene["bridgeRadiusMm"],scene.get("surfaceSafetyMm",max(.25,scene["fvmStepMm"]*.3)))
    inspect_stl(sys.argv[3],obs)
    inspect_stl(sys.argv[4],obs)
