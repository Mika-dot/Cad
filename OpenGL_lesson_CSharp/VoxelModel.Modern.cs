using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGL_lesson_CSharp
{
    public enum VoxelBooleanMode { Union, Difference, Intersection }
    public enum VoxelNeighborhood { Faces6 = 6, FacesEdges18 = 18, Full26 = 26 }

    public sealed partial class VoxelModel
    {
        public bool Contains(int x, int y, int z) => _vox.Contains((x, y, z));
        public void Clear() => _vox.Clear();

        public VoxelModel Clone()
        {
            var copy = new VoxelModel();
            foreach (var v in _vox) copy._vox.Add(v);
            return copy;
        }

        public bool TryGetBounds(out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
        {
            x0 = y0 = z0 = x1 = y1 = z1 = 0;
            if (_vox.Count == 0) return false;
            x0 = y0 = z0 = int.MaxValue;
            x1 = y1 = z1 = int.MinValue;
            foreach (var v in _vox)
            {
                if (v.x < x0) x0 = v.x; if (v.y < y0) y0 = v.y; if (v.z < z0) z0 = v.z;
                if (v.x + 1 > x1) x1 = v.x + 1; if (v.y + 1 > y1) y1 = v.y + 1; if (v.z + 1 > z1) z1 = v.z + 1;
            }
            return true;
        }

        public double Volume(double voxelSize) => Count * voxelSize * voxelSize * voxelSize;

        public double ApproxSurfaceArea(double voxelSize)
        {
            long faces = 0;
            foreach (var v in _vox)
            {
                if (!Has(v.x - 1, v.y, v.z)) faces++; if (!Has(v.x + 1, v.y, v.z)) faces++;
                if (!Has(v.x, v.y - 1, v.z)) faces++; if (!Has(v.x, v.y + 1, v.z)) faces++;
                if (!Has(v.x, v.y, v.z - 1)) faces++; if (!Has(v.x, v.y, v.z + 1)) faces++;
            }
            return faces * voxelSize * voxelSize;
        }

        public IEnumerable<(int x, int y, int z)> GetSurfaceVoxels()
        {
            foreach (var v in _vox)
                if (!Has(v.x - 1, v.y, v.z) || !Has(v.x + 1, v.y, v.z) || !Has(v.x, v.y - 1, v.z) ||
                    !Has(v.x, v.y + 1, v.z) || !Has(v.x, v.y, v.z - 1) || !Has(v.x, v.y, v.z + 1))
                    yield return v;
        }

        public void SubtractBox(int x0, int y0, int x1, int y1, int z0, int z1)
        {
            Normalize(ref x0, ref x1); Normalize(ref y0, ref y1); Normalize(ref z0, ref z1);
            for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) for (int z = z0; z < z1; z++) _vox.Remove((x, y, z));
        }

        public void IntersectBox(int x0, int y0, int x1, int y1, int z0, int z1)
        {
            Normalize(ref x0, ref x1); Normalize(ref y0, ref y1); Normalize(ref z0, ref z1);
            _vox.RemoveWhere(v => v.x < x0 || v.x >= x1 || v.y < y0 || v.y >= y1 || v.z < z0 || v.z >= z1);
        }

        public void IntersectModel(VoxelModel other, int dx = 0, int dy = 0, int dz = 0)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            _vox.RemoveWhere(v => !other._vox.Contains((v.x - dx, v.y - dy, v.z - dz)));
        }

        public void ApplyImplicit(int x0, int y0, int z0, int x1, int y1, int z1,
            Func<double, double, double, double> sdf, VoxelBooleanMode mode = VoxelBooleanMode.Union)
        {
            if (sdf == null) throw new ArgumentNullException(nameof(sdf));
            Normalize(ref x0, ref x1); Normalize(ref y0, ref y1); Normalize(ref z0, ref z1);
            if (mode == VoxelBooleanMode.Intersection)
            {
                _vox.RemoveWhere(v => sdf(v.x + .5, v.y + .5, v.z + .5) > 0.0);
                return;
            }
            for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) for (int z = z0; z < z1; z++)
            {
                if (sdf(x + .5, y + .5, z + .5) > 0.0) continue;
                if (mode == VoxelBooleanMode.Union) _vox.Add((x, y, z)); else _vox.Remove((x, y, z));
            }
        }

        public void AddSphere(double cx, double cy, double cz, double r) => Sphere(cx, cy, cz, r, VoxelBooleanMode.Union);
        public void SubtractSphere(double cx, double cy, double cz, double r) => Sphere(cx, cy, cz, r, VoxelBooleanMode.Difference);
        public void IntersectSphere(double cx, double cy, double cz, double r) => Sphere(cx, cy, cz, r, VoxelBooleanMode.Intersection);

        private void Sphere(double cx, double cy, double cz, double r, VoxelBooleanMode mode)
        {
            if (r <= 0) return;
            ApplyImplicit((int)Math.Floor(cx-r-.5), (int)Math.Floor(cy-r-.5), (int)Math.Floor(cz-r-.5),
                (int)Math.Ceiling(cx+r+.5), (int)Math.Ceiling(cy+r+.5), (int)Math.Ceiling(cz+r+.5),
                (x,y,z) => Math.Sqrt(Sq(x-cx)+Sq(y-cy)+Sq(z-cz))-r, mode);
        }

        public void AddCylinderZ(double cx, double cy, double z0, double z1, double r) => CylinderZ(cx,cy,z0,z1,r,VoxelBooleanMode.Union);
        public void SubtractCylinderZ(double cx, double cy, double z0, double z1, double r) => CylinderZ(cx,cy,z0,z1,r,VoxelBooleanMode.Difference);
        public void IntersectCylinderZ(double cx, double cy, double z0, double z1, double r) => CylinderZ(cx,cy,z0,z1,r,VoxelBooleanMode.Intersection);

        private void CylinderZ(double cx, double cy, double za, double zb, double r, VoxelBooleanMode mode)
        {
            if (r <= 0) return;
            double z0 = Math.Min(za,zb), z1 = Math.Max(za,zb);
            ApplyImplicit((int)Math.Floor(cx-r-.5), (int)Math.Floor(cy-r-.5), (int)Math.Floor(z0-.5),
                (int)Math.Ceiling(cx+r+.5), (int)Math.Ceiling(cy+r+.5), (int)Math.Ceiling(z1+.5),
                (x,y,z) => Math.Max(Math.Sqrt(Sq(x-cx)+Sq(y-cy))-r, Math.Max(z0-z,z-z1)), mode);
        }

        public void AddTorusZ(double cx,double cy,double cz,double majorRadius,double minorRadius) => TorusZ(cx,cy,cz,majorRadius,minorRadius,VoxelBooleanMode.Union);
        public void SubtractTorusZ(double cx,double cy,double cz,double majorRadius,double minorRadius) => TorusZ(cx,cy,cz,majorRadius,minorRadius,VoxelBooleanMode.Difference);

        private void TorusZ(double cx,double cy,double cz,double R,double r,VoxelBooleanMode mode)
        {
            if (R <= 0 || r <= 0) return;
            double bound = R+r;
            ApplyImplicit((int)Math.Floor(cx-bound-.5),(int)Math.Floor(cy-bound-.5),(int)Math.Floor(cz-r-.5),
                (int)Math.Ceiling(cx+bound+.5),(int)Math.Ceiling(cy+bound+.5),(int)Math.Ceiling(cz+r+.5),
                (x,y,z) => Math.Sqrt(Sq(Math.Sqrt(Sq(x-cx)+Sq(y-cy))-R)+Sq(z-cz))-r, mode);
        }

        public void AddCapsule(double ax,double ay,double az,double bx,double by,double bz,double r) => Capsule(ax,ay,az,bx,by,bz,r,VoxelBooleanMode.Union);
        public void SubtractCapsule(double ax,double ay,double az,double bx,double by,double bz,double r) => Capsule(ax,ay,az,bx,by,bz,r,VoxelBooleanMode.Difference);

        private void Capsule(double ax,double ay,double az,double bx,double by,double bz,double r,VoxelBooleanMode mode)
        {
            if (r <= 0) return;
            double abx=bx-ax, aby=by-ay, abz=bz-az, denom=abx*abx+aby*aby+abz*abz;
            ApplyImplicit((int)Math.Floor(Math.Min(ax,bx)-r-.5),(int)Math.Floor(Math.Min(ay,by)-r-.5),(int)Math.Floor(Math.Min(az,bz)-r-.5),
                (int)Math.Ceiling(Math.Max(ax,bx)+r+.5),(int)Math.Ceiling(Math.Max(ay,by)+r+.5),(int)Math.Ceiling(Math.Max(az,bz)+r+.5),
                (x,y,z) => { double t=denom<1e-18?0:((x-ax)*abx+(y-ay)*aby+(z-az)*abz)/denom; t=Math.Max(0,Math.Min(1,t));
                    return Math.Sqrt(Sq(x-(ax+t*abx))+Sq(y-(ay+t*aby))+Sq(z-(az+t*abz)))-r; }, mode);
        }

        public void AddGyroid(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness) => Gyroid(x0,y0,z0,x1,y1,z1,period,thickness,VoxelBooleanMode.Union);
        public void SubtractGyroid(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness) => Gyroid(x0,y0,z0,x1,y1,z1,period,thickness,VoxelBooleanMode.Difference);

        private void Gyroid(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness,VoxelBooleanMode mode)
        {
            if (period<=0 || thickness<=0) return; double k=2*Math.PI/period;
            ApplyImplicit(x0,y0,z0,x1,y1,z1,(x,y,z)=> { double X=k*x,Y=k*y,Z=k*z;
                double f=Math.Sin(X)*Math.Cos(Y)+Math.Sin(Y)*Math.Cos(Z)+Math.Sin(Z)*Math.Cos(X);
                double gx=k*(Math.Cos(X)*Math.Cos(Y)-Math.Sin(Z)*Math.Sin(X));
                double gy=k*(-Math.Sin(X)*Math.Sin(Y)+Math.Cos(Y)*Math.Cos(Z));
                double gz=k*(-Math.Sin(Y)*Math.Sin(Z)+Math.Cos(Z)*Math.Cos(X));
                double g=Math.Sqrt(gx*gx+gy*gy+gz*gz); return (g<1e-12?Math.Abs(f)/k:Math.Abs(f)/g)-thickness*.5; }, mode);
        }

        public void AddSchwarzP(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness) => SchwarzP(x0,y0,z0,x1,y1,z1,period,thickness,VoxelBooleanMode.Union);
        public void SubtractSchwarzP(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness) => SchwarzP(x0,y0,z0,x1,y1,z1,period,thickness,VoxelBooleanMode.Difference);

        private void SchwarzP(int x0,int y0,int z0,int x1,int y1,int z1,double period,double thickness,VoxelBooleanMode mode)
        {
            if (period<=0 || thickness<=0) return; double k=2*Math.PI/period;
            ApplyImplicit(x0,y0,z0,x1,y1,z1,(x,y,z)=> { double X=k*x,Y=k*y,Z=k*z, f=Math.Cos(X)+Math.Cos(Y)+Math.Cos(Z);
                double gx=-k*Math.Sin(X),gy=-k*Math.Sin(Y),gz=-k*Math.Sin(Z),g=Math.Sqrt(gx*gx+gy*gy+gz*gz);
                return (g<1e-12?Math.Abs(f)/k:Math.Abs(f)/g)-thickness*.5; }, mode);
        }

        public void AddBccLattice(int x0,int y0,int z0,int x1,int y1,int z1,double cellSize,double strutRadius)
        {
            if (cellSize<=0 || strutRadius<=0) return;
            Normalize(ref x0,ref x1); Normalize(ref y0,ref y1); Normalize(ref z0,ref z1);
            var lattice=new VoxelModel();
            for(double x=x0;x<x1;x+=cellSize) for(double y=y0;y<y1;y+=cellSize) for(double z=z0;z<z1;z+=cellSize)
            {
                double xe=Math.Min(x+cellSize,x1),ye=Math.Min(y+cellSize,y1),ze=Math.Min(z+cellSize,z1);
                double cx=(x+xe)*.5,cy=(y+ye)*.5,cz=(z+ze)*.5;
                for(int ix=0;ix<2;ix++) for(int iy=0;iy<2;iy++) for(int iz=0;iz<2;iz++)
                    lattice.AddCapsule(cx,cy,cz,ix==0?x:xe,iy==0?y:ye,iz==0?z:ze,strutRadius);
            }
            lattice.IntersectBox(x0,y0,x1,y1,z0,z1); AddModel(lattice);
        }

        public void Dilate(int iterations=1,VoxelNeighborhood neighborhood=VoxelNeighborhood.Faces6)
        {
            var dirs=NeighborOffsets(neighborhood).ToArray();
            for(int it=0;it<Math.Max(0,iterations);it++) { var next=new HashSet<(int x,int y,int z)>(_vox);
                foreach(var v in _vox) foreach(var d in dirs) next.Add((v.x+d.dx,v.y+d.dy,v.z+d.dz)); _vox.Clear(); _vox.UnionWith(next); }
        }

        public void Erode(int iterations=1,VoxelNeighborhood neighborhood=VoxelNeighborhood.Faces6)
        {
            var dirs=NeighborOffsets(neighborhood).ToArray();
            for(int it=0;it<Math.Max(0,iterations);it++) { var keep=new HashSet<(int x,int y,int z)>();
                foreach(var v in _vox) if(dirs.All(d=>_vox.Contains((v.x+d.dx,v.y+d.dy,v.z+d.dz)))) keep.Add(v); _vox.Clear(); _vox.UnionWith(keep); }
        }

        public void Open(int iterations=1,VoxelNeighborhood neighborhood=VoxelNeighborhood.Faces6) { Erode(iterations,neighborhood); Dilate(iterations,neighborhood); }
        public void Close(int iterations=1,VoxelNeighborhood neighborhood=VoxelNeighborhood.Faces6) { Dilate(iterations,neighborhood); Erode(iterations,neighborhood); }

        public void SmoothMajority(int iterations=1,int threshold=14)
        {
            threshold=Math.Max(1,Math.Min(27,threshold)); var dirs=NeighborOffsets(VoxelNeighborhood.Full26).ToArray();
            for(int it=0;it<Math.Max(0,iterations);it++) { var candidates=new HashSet<(int x,int y,int z)>();
                foreach(var v in _vox) { candidates.Add(v); foreach(var d in dirs) candidates.Add((v.x+d.dx,v.y+d.dy,v.z+d.dz)); }
                var next=new HashSet<(int x,int y,int z)>(); foreach(var c in candidates) { int n=_vox.Contains(c)?1:0;
                    foreach(var d in dirs) if(_vox.Contains((c.x+d.dx,c.y+d.dy,c.z+d.dz))) n++; if(n>=threshold) next.Add(c); }
                _vox.Clear(); _vox.UnionWith(next); }
        }

        public int KeepLargestConnectedComponent()
        {
            if(_vox.Count==0) return 0; var unvisited=new HashSet<(int x,int y,int z)>(_vox); HashSet<(int x,int y,int z)> largest=null;
            var dirs=NeighborOffsets(VoxelNeighborhood.Faces6).ToArray();
            while(unvisited.Count>0) { var seed=unvisited.First(); unvisited.Remove(seed); var comp=new HashSet<(int x,int y,int z)>{seed}; var q=new Queue<(int x,int y,int z)>(); q.Enqueue(seed);
                while(q.Count>0) { var v=q.Dequeue(); foreach(var d in dirs) { var n=(v.x+d.dx,v.y+d.dy,v.z+d.dz); if(unvisited.Remove(n)){comp.Add(n);q.Enqueue(n);} } }
                if(largest==null || comp.Count>largest.Count) largest=comp; }
            int removed=_vox.Count-largest.Count; _vox.Clear(); _vox.UnionWith(largest); return removed;
        }

        public void ExportStlGreedy(string path,float voxelSize=1.0f)
        {
            var tris=BuildGreedyTriangles(voxelSize);
            using(var fs=new FileStream(path,FileMode.Create,FileAccess.Write)) using(var bw=new BinaryWriter(fs))
            { bw.Write(new byte[80]); bw.Write((uint)tris.Count); foreach(var t in tris) { bw.Write(t.nx);bw.Write(t.ny);bw.Write(t.nz); bw.Write(t.x1);bw.Write(t.y1);bw.Write(t.z1); bw.Write(t.x2);bw.Write(t.y2);bw.Write(t.z2); bw.Write(t.x3);bw.Write(t.y3);bw.Write(t.z3); bw.Write((ushort)0); } }
        }

        private List<Tri> BuildGreedyTriangles(float voxelSize)
        {
            var tris=new List<Tri>();
            for(int axis=0;axis<3;axis++) { int ua=(axis+1)%3,va=(axis+2)%3;
                foreach(int side in new[]{-1,1}) { var planes=new Dictionary<int,HashSet<(int u,int v)>>();
                    foreach(var voxel in _vox) { int[] q={voxel.x,voxel.y,voxel.z}; int nx=voxel.x,ny=voxel.y,nz=voxel.z;
                        if(axis==0) nx+=side; else if(axis==1) ny+=side; else nz+=side; if(Has(nx,ny,nz)) continue;
                        int plane=q[axis]+(side>0?1:0); if(!planes.TryGetValue(plane,out var cells)){cells=new HashSet<(int u,int v)>();planes[plane]=cells;} cells.Add((q[ua],q[va])); }
                    foreach(var kv in planes) { var cells=kv.Value; while(cells.Count>0) { var s=cells.First(); int w=1; while(cells.Contains((s.u+w,s.v))) w++;
                            int h=1; bool grow=true; while(grow) { int vv=s.v+h; for(int du=0;du<w;du++) if(!cells.Contains((s.u+du,vv))){grow=false;break;} if(grow) h++; }
                            for(int du=0;du<w;du++) for(int dv=0;dv<h;dv++) cells.Remove((s.u+du,s.v+dv)); AddGreedyFace(tris,axis,side,kv.Key,s.u,s.v,w,h,voxelSize); } }
                }
            }
            return tris;
        }

        private static void AddGreedyFace(List<Tri> tris,int axis,int side,int plane,int u0,int v0,int w,int h,float s)
        {
            int ua=(axis+1)%3,va=(axis+2)%3; float[] p=new float[3],du=new float[3],dv=new float[3]; p[axis]=plane*s;p[ua]=u0*s;p[va]=v0*s;du[ua]=w*s;dv[va]=h*s;
            var a=(p[0],p[1],p[2]); var b=(p[0]+du[0],p[1]+du[1],p[2]+du[2]); var c=(p[0]+du[0]+dv[0],p[1]+du[1]+dv[1],p[2]+du[2]+dv[2]); var d=(p[0]+dv[0],p[1]+dv[1],p[2]+dv[2]);
            (float,float,float) n=axis==0?((float)side,0f,0f):axis==1?(0f,(float)side,0f):(0f,0f,(float)side); if(side>0) AddQuad(tris,n,a,b,c,d); else AddQuad(tris,n,a,d,c,b);
        }

        private static double Sq(double x)=>x*x;
        private static void Normalize(ref int a,ref int b){if(b<a){int t=a;a=b;b=t;}}
        private static IEnumerable<(int dx,int dy,int dz)> NeighborOffsets(VoxelNeighborhood n)
        {
            for(int dx=-1;dx<=1;dx++) for(int dy=-1;dy<=1;dy++) for(int dz=-1;dz<=1;dz++) { if(dx==0&&dy==0&&dz==0) continue; int m=Math.Abs(dx)+Math.Abs(dy)+Math.Abs(dz);
                if(n==VoxelNeighborhood.Faces6&&m!=1) continue; if(n==VoxelNeighborhood.FacesEdges18&&m>2) continue; yield return (dx,dy,dz); }
        }
    }
}
