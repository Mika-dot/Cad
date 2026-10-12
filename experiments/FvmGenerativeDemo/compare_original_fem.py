#!/usr/bin/env python3
"""Numerically compare C# Hex8 element stiffness against *original* FEM_Voxel.
Requires only numpy/scipy/scikit-fem; reads the original branch without editing it.
This checks elasticity and quadrature, NOT final shape optimization equivalence.
"""
from pathlib import Path
import importlib
import sys
import types
import numpy as np
from skfem import MeshHex

source=Path(sys.argv[1]).resolve()
csv=Path(sys.argv[2]).resolve()
if not (source/"openscad_gen"/"fem.py").is_file():
    raise SystemExit("FAIL: missing original FEM_Voxel/openscad_gen/fem.py")
# Skip unrelated original package __init__ imports (Streamlit/visualization).
pkg=types.ModuleType("openscad_gen")
pkg.__path__=[str(source/"openscad_gen")]
sys.modules["openscad_gen"]=pkg
fem=importlib.import_module("openscad_gen.fem")
models=importlib.import_module("openscad_gen.models")
mat=models.Material("steel",200000.0,0.30,7.85e-6,250.0)
ke_py,_=fem.build_local_matrices(10.0,mat)
coords=MeshHex.init_tensor(np.array([0.,10.]),np.array([0.,10.]),np.array([0.,10.]))
local=coords.p[:,coords.t[:,0]].T
ours=np.array([[0,0,0],[10,0,0],[10,10,0],[0,10,0],
               [0,0,10],[10,0,10],[10,10,10],[0,10,10]],dtype=float)
nodes=[]
for pt in ours:
    match=np.flatnonzero(np.linalg.norm(local-pt[None,:],axis=1)<1e-10)
    if len(match)!=1: raise SystemExit("FAIL: unmatched local element node")
    nodes.append(int(match[0]))
idx=[3*n+d for n in nodes for d in range(3)]
expected=ke_py[np.ix_(idx,idx)]
actual=np.loadtxt(csv,delimiter=",",encoding="utf-8-sig")
if actual.shape!=(24,24): raise SystemExit("FAIL: C# stiffness shape is not 24x24")
rel=np.linalg.norm(actual-expected)/np.linalg.norm(expected)
sym=np.linalg.norm(actual-actual.T)/np.linalg.norm(actual)
print(f"Original FEM_Voxel scikit-fem vs C# Hex8: relative_Frobenius_error={rel:.12e} symmetry_error={sym:.12e}")
if rel>2e-10 or sym>2e-12:
    raise SystemExit("FAIL: C# Hex8 local matrix differs from original Python")
print("PASS: 24x24 element stiffness matches original FEM_Voxel")
