#!/usr/bin/env python3
"""Independent T4 FEM check of a binary exported STL, not a Hex8 re-run.

Background 6-tetra subdivision, linear displacement T4, exact constant-strain
stiffness, independently ray-classified STL material. Pressure STL patches
are projected by barycentric partition of unity onto T4 nodes. Requires
numpy + scipy; no C# code, 5-M coefficients or Hex8 stiffness routines.

IMPORTANT: Background ersatz tetra is NOT a surface-fitted STL FEM.
Use as an independent discretization cross-check, never as certification.
"""
import argparse
import json
import math
from pathlib import Path
import struct
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components
from scipy.sparse.linalg import spsolve

def stl_faces(path):
    raw=Path(path).read_bytes()
    if len(raw)>=84:
        n=struct.unpack_from("<I",raw,80)[0]
        if n>0 and 84+50*n==len(raw):
            record=np.dtype([("normal","<f4",(3,)),
                             ("vertices","<f4",(3,3)),("tag","<u2")])
            result=np.frombuffer(raw,dtype=record,count=n,offset=84)
            return result["vertices"].astype(np.float64)
    vertices=[]
    for line in raw.decode("ascii").splitlines():
        fields=line.split()
        if len(fields)==4 and fields[0].lower()=="vertex":
            vertices.append(tuple(float(f) for f in fields[1:]))
    if not vertices or len(vertices)%3:
        raise ValueError("STL must contain complete ASCII or binary triangles")
    return np.asarray(vertices,dtype=float).reshape(-1,3,3)

def inside_stl(points,faces,chunk=32):
    """Independent +X parity ray, vectorized across triangles (not trimesh)."""
    f=faces
    y0,z0=f[:,0,1],f[:,0,2]
    y1,z1=f[:,1,1],f[:,1,2]
    y2,z2=f[:,2,1],f[:,2,2]
    ay,az=y1-y0,z1-z0
    by,bz=y2-y0,z2-z0
    det=ay*bz-az*by
    use=np.abs(det)>1e-13
    det=np.where(use,det,1)
    output=np.zeros(len(points),dtype=bool)
    for first in range(0,len(points),chunk):
        p=points[first:first+chunk]
        # small skew avoids ambiguous common triangle-edge ray hits
        dy=p[:,None,1]+1.13e-7-y0[None,:]
        dz=p[:,None,2]+1.71e-7-z0[None,:]
        beta=(dy*bz[None,:]-dz*by[None,:])/det[None,:]
        gamma=(ay[None,:]*dz-az[None,:]*dy)/det[None,:]
        alpha=1-beta-gamma
        xt=alpha*f[None,:,0,0]+beta*f[None,:,1,0]+gamma*f[None,:,2,0]
        hits=use[None,:]&(alpha>1e-9)&(beta>1e-9)&(gamma>1e-9)&(xt>p[:,None,0]+1e-8)
        output[first:first+chunk]=(hits.sum(axis=1)%2)==1
    return output

def tetra_volume_fraction(nodes,tets,faces,samples):
    """Immersed material integration: either 1 barycenter or symmetric 4-point T4 quadrature."""
    verts=nodes[tets]
    if samples==1:
        return inside_stl(verts.mean(axis=1),faces).astype(float)
    if samples!=4:
        raise ValueError("Volume samples must be 1 or 4")
    # Symmetric positive-weight degree-2 integration of constant/linear
    # material fraction. Each cut tetra receives 0, .25, .5, .75 or 1.
    a=.5854101966249685
    b=.1381966011250105
    base=b*verts.sum(axis=1)
    fraction=np.zeros(len(tets),dtype=float)
    for q in range(4):
        fraction+=inside_stl(base+(a-b)*verts[:,q],faces)*.25
    return fraction

def grid(lo,hi,cells):
    nx,ny,nz=map(int,cells)
    axis=[np.linspace(lo[i],hi[i],cells[i]+1) for i in range(3)]
    xx,yy,zz=np.meshgrid(*axis,indexing="ij")
    nodes=np.stack((xx,yy,zz),axis=-1).reshape(-1,3)
    def node(i,j,k):return (i*(ny+1)+j)*(nz+1)+k
    blocks=[]
    for i in range(nx):
        for j in range(ny):
            for k in range(nz):
                cube=np.array([node(i,j,k),node(i+1,j,k),node(i,j+1,k),
                               node(i+1,j+1,k),node(i,j,k+1),node(i+1,j,k+1),
                               node(i,j+1,k+1),node(i+1,j+1,k+1)])
                for sub in ((0,1,3,7),(0,3,2,7),(0,2,6,7),
                            (0,6,4,7),(0,4,5,7),(0,5,1,7)):
                    blocks.append(cube[list(sub)])
    return nodes,np.asarray(blocks,dtype=np.int32)

def elastic(young,poisson):
    lam=young*poisson/((1+poisson)*(1-2*poisson))
    mu=young/(2*(1+poisson))
    d=np.zeros((6,6))
    d[:3,:3]=lam
    d[range(3),range(3)]=lam+2*mu
    d[3:,3:]=np.eye(3)*mu
    return d

def assemble(nodes,tets,inside,young,poisson,emin=1e-4):
    xyz=nodes[tets]
    jac=np.stack((xyz[:,1]-xyz[:,0],
                  xyz[:,2]-xyz[:,0],xyz[:,3]-xyz[:,0]),axis=2)
    det=np.linalg.det(jac)
    if np.any(np.abs(det)<1e-12):raise ArithmeticError("Degenerate T4 element")
    inv=np.linalg.inv(jac)
    gradients=np.zeros((len(tets),4,3))
    gradients[:,1:,:]=inv
    gradients[:,0,:]=-gradients[:,1:,:].sum(axis=1)
    vol=np.abs(det)/6
    b=np.zeros((len(tets),6,12))
    for q in range(4):
        gx,gy,gz=gradients[:,q,:].T
        a=3*q
        b[:,0,a]=gx
        b[:,1,a+1]=gy
        b[:,2,a+2]=gz
        b[:,3,a]=gy;b[:,3,a+1]=gx
        b[:,4,a+1]=gz;b[:,4,a+2]=gy
        b[:,5,a]=gz;b[:,5,a+2]=gx
    d=elastic(young,poisson)
    local=np.einsum("tij,ik,tkl->tjl",b,d,b,optimize=True)
    factors=vol*(emin+(1-emin)*np.asarray(inside,dtype=float))
    local*=factors[:,None,None]
    dofs=(3*tets[:,:,None]+np.arange(3)).reshape(-1,12)
    row=np.broadcast_to(dofs[:,:,None],local.shape).flatten()
    col=np.broadcast_to(dofs[:,None,:],local.shape).flatten()
    stiffness=coo_matrix((local.flatten(),(row,col)),
                         shape=(3*len(nodes),3*len(nodes))).tocsr()
    return stiffness,b,d,vol,inv

def fixtures(nodes,bodies):
    fixed=np.zeros(len(nodes),dtype=bool)
    for body in bodies:
        if body["role"]!="fixed":continue
        shape=body["shape"]
        t=shape["type"]
        if t=="sphere":
            delta=nodes-np.asarray(shape["center"])
            mask=(delta*delta).sum(axis=1)<=shape["radius"]**2
        elif t=="box":
            mask=np.all(np.abs(nodes-np.asarray(shape["center"]))<=
                        np.asarray(shape["halfSize"]),axis=1)
        elif t=="ellipsoid":
            delta=(nodes-np.asarray(shape["center"]))/np.asarray(shape["radii"])
            mask=(delta*delta).sum(axis=1)<=1
        else:
            raise ValueError(f"Independent T4 solver supports sphere/box/ellipsoid supports, not {t}")
        fixed|=mask
    if fixed.sum()<3:raise ValueError("Insufficient FEM support nodes; refine T4 grid")
    return fixed

def tetra_at_point(p,nodes,tets,inv,cells,lo,hi):
    spacing=(hi-lo)/cells
    coords=np.clip(np.floor((p-lo)/spacing).astype(int),0,cells-1)
    cube=(coords[0]*cells[1]+coords[1])*cells[2]+coords[2]
    for i in range(cube*6,cube*6+6):
        bary=inv[i]@(p-nodes[tets[i,0]])
        w=np.r_[1-bary.sum(),bary]
        if np.min(w)>=-1e-8 and np.max(w)<=1+1e-8:
            return i,np.clip(w,0,1)/np.clip(w,0,1).sum()
    raise ArithmeticError(f"Patch pressure point outside all T4 elements: {p}")

def pressure_rhs(nodes,tets,inv,cells,lo,hi,patches,subdivide=10):
    rhs=np.zeros((len(nodes),3))
    expected=np.zeros(3)
    n=subdivide
    for triangle,p in patches:
        a,b,c=triangle
        force=np.cross(b-a,c-a)*(.5*p)
        expected+=force
        def point(i,j):return a+(b-a)*(i/n)+(c-a)*(j/n)
        for i in range(n):
            for j in range(n-i):
                subtris=[(point(i,j),point(i+1,j),point(i,j+1))]
                if i+j<n-1:
                    subtris.append((point(i+1,j),point(i+1,j+1),point(i,j+1)))
                for aa,bb,cc in subtris:
                    normal=np.cross(bb-aa,cc-aa)*(.5*p)
                    # 3-point barycentric on every subdivided triangle
                    for w in ((2/3,1/6,1/6),(1/6,2/3,1/6),(1/6,1/6,2/3)):
                        sample=aa*w[0]+bb*w[1]+cc*w[2]
                        index,weights=tetra_at_point(sample,nodes,tets,inv,cells,lo,hi)
                        for node,shape in zip(tets[index],weights):
                            rhs[node]+=normal*shape/3
    return rhs.reshape(-1),expected

def solve(nodes,tets,occupied,young,poisson,fixed,
          patch_data,cells,lo,hi,subdivide=10):
    K,B,D,vol,inv=assemble(nodes,tets,occupied,young,poisson)
    f,applied=pressure_rhs(nodes,tets,inv,cells,lo,hi,patch_data,subdivide)
    known=np.repeat(fixed,3)
    free=np.flatnonzero(~known)
    u=np.zeros(len(f))
    u[free]=spsolve(K[free,:][:,free],f[free])
    if not np.all(np.isfinite(u)):raise ArithmeticError("Nonfinite independent T4 solution")
    residual=K@u-f
    reactions=residual.reshape(-1,3)[fixed].sum(axis=0)
    errors=np.linalg.norm(reactions+applied)/max(np.linalg.norm(applied),1e-12)
    if errors>1e-5:raise ArithmeticError(f"T4 force equilibrium failure {errors:.3g}")
    ue=u.reshape(-1,3)[tets].reshape(-1,12)
    strains=np.einsum("tij,tj->ti",B,ue)
    stress=strains@D.T
    sx,sy,sz,txy,tyz,txz=stress.T
    vm=np.sqrt(.5*((sx-sy)**2+(sy-sz)**2+(sz-sx)**2)+3*(txy*txy+tyz*tyz+txz*txz))
    material_stress=vm[np.asarray(occupied)>=.25]
    if material_stress.size==0:raise ArithmeticError("STL has no solid T4 elements")
    ux=u.reshape(-1,3)
    # Physical stress/displacement must be measured on ACTUAL solid T4 nodes,
    # not on remote low-stiffness ersatz void nodes (which can displace freely
    # and inflate reported u by orders of magnitude).
    material_tets=tets[np.asarray(occupied)>0]
    material_nodes=np.unique(material_tets)
    is_material=np.zeros(len(nodes),dtype=bool)
    is_material[material_nodes]=True
    matnode=np.full(len(nodes),-1,dtype=np.int32)
    matnode[material_nodes]=np.arange(len(material_nodes),dtype=np.int32)
    compact=matnode[material_tets]
    count=len(material_tets)
    row=np.broadcast_to(compact[:,:,None],(count,4,4)).reshape(-1)
    col=np.broadcast_to(compact[:,None,:],(count,4,4)).reshape(-1)
    graph=coo_matrix((np.ones(len(row),dtype=np.int8),(row,col)),
                     shape=(len(material_nodes),len(material_nodes))).tocsr()
    n_connected,labels=connected_components(graph,directed=False)
    support_ids=matnode[np.flatnonzero(fixed & is_material)]
    force_nodes=np.linalg.norm(f.reshape(-1,3),axis=1)>1e-10
    load_ids=matnode[np.flatnonzero(force_nodes & is_material)]
    supported_groups=set(labels[support_ids])
    loaded_groups=set(labels[load_ids])
    all_forces=np.linalg.norm(f.reshape(-1,3),axis=1).sum()
    force_on_void=np.linalg.norm(f.reshape(-1,3)[~is_material],axis=1).sum()
    max_material_displacement=float(
        np.linalg.norm(ux[material_nodes],axis=1).max())
    return dict(
        nodeCount=len(nodes),tetraCount=len(tets),
        solidTetraCount=int((np.asarray(occupied)>0).sum()),supportedNodes=int(fixed.sum()),
        partiallyFilledTetraCount=int(((np.asarray(occupied)>0)&
            (np.asarray(occupied)<1)).sum()),
        solidConnectedComponents=int(n_connected),
        loadedMaterialConnectedToSupport=bool(loaded_groups and
            loaded_groups.issubset(supported_groups)),
        fractionOfNodalLoadOnErsatzVoid=float(force_on_void/max(all_forces,1e-12)),
        regularizedSolidVolumeMm3=float(vol@np.asarray(occupied,dtype=float)),
        maxVonMisesMpa=float(material_stress.max()),
        p95VonMisesMpa=float(np.percentile(material_stress,95)),
        maxDisplacementMm=max_material_displacement,
        maxErsatzBackgroundDisplacementMm=float(
            np.linalg.norm(ux,axis=1).max()),
        complianceNmm=float(f@u),forceN=applied.tolist(),
        reactionN=reactions.tolist(),forceBalanceRelativeError=float(errors))

def run_scene(scene_path,stl_path,cells,subdivide,volume_samples=1):
    scene=json.loads(Path(scene_path).read_text(encoding="utf8"))
    patches=[]
    for entry in scene.get("surfacePressurePatches",[]):
        faces=stl_faces(Path(scene_path).parent/entry["stlFile"])
        patches.extend((t,entry["pressureMpa"]) for t in faces)
    if not patches:raise ValueError("Independent verification requires physical STL pressure")
    for case in scene.get("loadCases",[]):
        if case.get("forcesN"):raise ValueError("This independent validation does NOT support lumped forces")
    lo=np.array(scene["bounds"]["min"],dtype=float)
    hi=np.array(scene["bounds"]["max"],dtype=float)
    nodes,tets=grid(lo,hi,cells)
    occupied=tetra_volume_fraction(nodes,tets,stl_faces(stl_path),volume_samples)
    fixed=fixtures(nodes,scene["bodies"])
    props=scene["material"]
    output=solve(nodes,tets,occupied,props["youngMpa"],props["poisson"],
                 fixed,patches,np.array(cells),lo,hi,subdivide)
    output["volumeQuadraturePointsPerTetra"]=volume_samples
    return output

def selftest():
    lo=np.array([0.,0.,0.]);hi=np.array([100.,10.,10.])
    x0,y0,z0=lo;x1,y1,z1=hi
    corners=np.array([[x0,y0,z0],[x1,y0,z0],[x0,y1,z0],[x1,y1,z0],
                      [x0,y0,z1],[x1,y0,z1],[x0,y1,z1],[x1,y1,z1]])
    indices=[(0,2,3),(0,3,1),(4,5,7),(4,7,6),
             (0,1,5),(0,5,4),(2,6,7),(2,7,3),
             (0,4,6),(0,6,2),(1,3,7),(1,7,5)]
    solid=corners[np.array(indices)]
    patch=[(corners[[1,3,7]],10.),(corners[[1,7,5]],10.)]
    results=[]
    for cells in ((4,2,2),(8,3,3),(12,4,4)):
        nodes,tets=grid(lo,hi,cells)
        material=inside_stl(nodes[tets].mean(axis=1),solid)
        if not np.all(material):
            raise AssertionError(f"Analytic bar STL incorrectly classified: {np.count_nonzero(~material)} outside")
        fixed=nodes[:,0]<1e-8
        r=solve(nodes,tets,material,200000,.0,fixed,patch,np.array(cells),lo,hi,subdivide=18)
        assert np.allclose(r["reactionN"],[-1000,0,0],atol=1e-6),r
        assert abs(r["maxDisplacementMm"]-.005)<.00012,r
        assert abs(r["maxVonMisesMpa"]-10)<1.2,r
        results.append(r)
        print(f"T4 ANALYTIC PASS cells={cells} u={r['maxDisplacementMm']:.9f}mm "
              f"vm={r['maxVonMisesMpa']:.5f}MPa Rx={r['reactionN'][0]:.6f}N "
              f"V={r['regularizedSolidVolumeMm3']:.2f}mm3")
    return results

if __name__=="__main__":
    ap=argparse.ArgumentParser()
    ap.add_argument("--self-test",action="store_true")
    ap.add_argument("--scene")
    ap.add_argument("--stl")
    ap.add_argument("--cells",type=int,nargs=3,default=[10,7,6])
    ap.add_argument("--pressure-subdivisions",type=int,default=12)
    ap.add_argument("--volume-quadrature",type=int,choices=[1,4],default=1)
    ap.add_argument("--out")
    args=ap.parse_args()
    if args.self_test:selftest()
    if args.scene or args.stl:
        if not args.scene or not args.stl:ap.error("--scene and --stl must both be provided")
        if any(v<2 or v>24 for v in args.cells):ap.error("Cells must be 2..24")
        if not 2<=args.pressure_subdivisions<=32:ap.error("pressure subdivisions must be 2..32")
        result=run_scene(args.scene,args.stl,args.cells,
                         args.pressure_subdivisions,args.volume_quadrature)
        result["model"]="independent Python P1 tetra (6 T4 per Cartesian cell)"
        result["cells"]=args.cells
        result["sourceSTL"]=str(args.stl)
        result["disclaimer"]="Immersed STL T4 with chosen interior quadrature and ersatz background; NOT fitted-surface FEM or certified stress."
        print("T4 INDEPENDENT FEM PASS:",json.dumps(result,sort_keys=True))
        if args.out:Path(args.out).write_text(json.dumps(result,indent=2)+"\n",encoding="utf8")
