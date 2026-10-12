#!/usr/bin/env python3
"""Check an exported two-sphere ASCII STL without third-party dependencies.

An FVM/FEM volume constraint alone does not imply a closed/printable STL.
These tests reject nonmanifold edges, inconsistent triangle orientation,
degenerate facets, and unexpected material gain after optimization.
"""
from __future__ import annotations
from collections import Counter
import math
from pathlib import Path
import sys

Vec = tuple[float,float,float]

def analyze(file: Path):
    vertices=[]
    triangles=[]
    for raw in file.read_text(encoding="utf-8").splitlines():
        line=raw.strip()
        if line.startswith("vertex "):
            pos=tuple(map(float,line.split()[1:]))
            if len(pos)!=3 or not all(math.isfinite(v) for v in pos):
                raise AssertionError(f"Nonfinite/non-3D vertex: {pos}")
            vertices.append(pos)
            if len(vertices)==3:
                triangles.append(tuple(vertices))
                vertices=[]
    assert not vertices, "Incomplete STL triangle"
    assert len(triangles)>1000, f"Too few facets: {len(triangles)}"
    # STL writes G9; round-trip to 1e-4mm is ample for 2mm marching cells.
    def key(p: Vec):
        return tuple(round(v,4) for v in p)
    edge_count=Counter()
    edge_orient=Counter()
    vol=0.0
    for a,b,c in triangles:
        qa,qb,qc=key(a),key(b),key(c)
        assert len({qa,qb,qc})==3, "Degenerate triangle"
        for u,v in ((qa,qb),(qb,qc),(qc,qa)):
            edge_count[tuple(sorted((u,v)))]+=1
            edge_orient[(u,v)]+=1
        vol+=(a[0]*(b[1]*c[2]-b[2]*c[1])+
              a[1]*(b[2]*c[0]-b[0]*c[2])+
              a[2]*(b[0]*c[1]-b[1]*c[0]))/6
    bad=[(e,n) for e,n in edge_count.items() if n!=2]
    assert not bad, f"Boundary or nonmanifold edges: {len(bad)}, sample={bad[:3]}"
    reversed_error=[edge for edge in edge_count
                    if edge_orient[(edge[0],edge[1])]!=1
                    or edge_orient[(edge[1],edge[0])]!=1]
    assert not reversed_error, f"Inconsistent facet winding: {len(reversed_error)}"
    assert vol>1000, f"Unphysical signed solid volume: {vol}"
    print(f"PASS {file}: triangles={len(triangles)}, unique_edges={len(edge_count)}, "
          f"watertight=true, oriented=true, mesh_volume={vol:.3f} mm3")
    return vol

if __name__=="__main__":
    assert len(sys.argv)==3, "Usage: validate_stl.py initial.stl optimized.stl"
    initial=analyze(Path(sys.argv[1]))
    final=analyze(Path(sys.argv[2]))
    assert final<initial, "STL has not decreased in geometric volume"
    print(f"PASS STL volume reduction = {(1-final/initial)*100:.3f}%")
