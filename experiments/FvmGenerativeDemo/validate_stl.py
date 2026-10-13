#!/usr/bin/env python3
"""Check an exported two-sphere ASCII STL without third-party dependencies.

An FVM/FEM volume constraint alone does not imply a closed/printable STL.
These tests reject nonmanifold edges, inconsistent triangle orientation,
degenerate facets, and unexpected material gain after optimization.
"""
from __future__ import annotations
from collections import Counter, defaultdict, deque
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
    # Exporter writes G17. Earlier rounding to 1e-4 mm falsely merged
    # distinct tiny mesh vertices (and invented degenerate facets).
    # Use the same 1e-9 mm topological vertex quantization as the exporter.
    def key(p: Vec):
        return tuple(round(v,9) for v in p)
    edge_count=Counter()
    edge_orient=Counter()
    edge_owners=defaultdict(list)
    vol=0.0
    for triangle_id,(a,b,c) in enumerate(triangles):
        qa,qb,qc=key(a),key(b),key(c)
        assert len({qa,qb,qc})==3, "Degenerate triangle"
        for u,v in ((qa,qb),(qb,qc),(qc,qa)):
            edge=tuple(sorted((u,v)))
            edge_count[edge]+=1
            edge_owners[edge].append(triangle_id)
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
    # A closed STL can still contain disjoint components; a two-sphere
    # connector must export one printable connected shell.
    adjacency=[set() for _ in triangles]
    for first,second in edge_owners.values():
        adjacency[first].add(second)
        adjacency[second].add(first)
    reachable={0}
    queue=deque([0])
    while queue:
        i=queue.popleft()
        for other in adjacency[i]:
            if other not in reachable:
                reachable.add(other)
                queue.append(other)
    assert len(reachable)==len(triangles), (
        f"Disconnected STL shells: {len(reachable)} of {len(triangles)} "
        f"triangles attached to primary shell"
    )
    assert vol>1000, f"Unphysical signed solid volume: {vol}"
    print(f"PASS {file}: triangles={len(triangles)}, unique_edges={len(edge_count)}, "
          f"watertight=true, oriented=true, connected=true, mesh_volume={vol:.3f} mm3")
    return vol

if __name__=="__main__":
    assert len(sys.argv)==3, "Usage: validate_stl.py initial.stl optimized.stl"
    initial=analyze(Path(sys.argv[1]))
    final=analyze(Path(sys.argv[2]))
    assert final<initial, "STL has not decreased in geometric volume"
    print(f"PASS STL volume reduction = {(1-final/initial)*100:.3f}%")
