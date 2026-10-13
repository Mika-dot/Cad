using System.Globalization;
using System.Text;

namespace FvmGenerativeDemo;

// Signed distance to a watertight triangulated *input* STL. Geometry is
// accelerated by a BVH; both ASCII and binary STL are accepted.
// Positive value inside a closed mesh, negative outside.
internal sealed class ImplicitMesh : ImplicitSolid
{
    private readonly record struct Triangle(Point3 A,Point3 B,Point3 C)
    {
        internal Point3 Min=>new(Math.Min(A.X,Math.Min(B.X,C.X)),
            Math.Min(A.Y,Math.Min(B.Y,C.Y)),Math.Min(A.Z,Math.Min(B.Z,C.Z)));
        internal Point3 Max=>new(Math.Max(A.X,Math.Max(B.X,C.X)),
            Math.Max(A.Y,Math.Max(B.Y,C.Y)),Math.Max(A.Z,Math.Max(B.Z,C.Z)));
        internal Point3 Centroid=>(A+B+C)*(1.0/3);
    }
    private sealed class Node
    {
        internal Point3 Lo,Hi;
        internal Node? Left,Right;
        internal Triangle[]? Facets;
    }
    private readonly Node _root;
    private readonly int _faces;
    internal Point3 BoundingCentre=>(_root.Lo+_root.Hi)*.5;

    private ImplicitMesh(List<Triangle> triangles)
    {
        if(triangles.Count<4||triangles.Count>200000)
            throw new ArgumentException("STL requires 4..200000 facets.");
        _faces=triangles.Count;
        // Edge incidence alone is NOT proof of a valid printable solid:
        // reject folded winding, duplicate facets and disconnected shells.
        var edgeMap=new Dictionary<(Point3,Point3),List<(int Index,int Sign)>>();
        double signedVolume=0;
        for(int i=0;i<triangles.Count;i++)
        {
            var tri=triangles[i];
            signedVolume+=Point3.Dot(tri.A,Point3.Cross(tri.B,tri.C))/6;
            if(Point3.Cross(tri.B-tri.A,tri.C-tri.A).Norm()<1e-13)
                throw new ArgumentException("Degenerate STL facet.");
            foreach(var (a,b) in new[]{(tri.A,tri.B),(tri.B,tri.C),(tri.C,tri.A)})
            {
                if(a==b)throw new ArgumentException("Degenerate input STL edge.");
                var forward=Compare(a,b)<0;
                var key=forward?(a,b):(b,a);
                if(!edgeMap.TryGetValue(key,out var owners))
                    edgeMap[key]=owners=new List<(int,int)>();
                owners.Add((i,forward?1:-1));
            }
        }
        var neighbours=new List<int>[triangles.Count];
        for(int i=0;i<neighbours.Length;i++)neighbours[i]=new List<int>();
        foreach(var owners in edgeMap.Values)
        {
            if(owners.Count!=2)
                throw new ArgumentException("STL is not a watertight 2-manifold.");
            if(owners[0].Sign==owners[1].Sign)
                throw new ArgumentException("STL triangle winding is inconsistent.");
            neighbours[owners[0].Index].Add(owners[1].Index);
            neighbours[owners[1].Index].Add(owners[0].Index);
        }
        var visited=new bool[triangles.Count];
        var q=new Queue<int>();q.Enqueue(0);visited[0]=true;
        var count=0;
        while(q.Count>0)
        {
            var at=q.Dequeue();count++;
            foreach(var adjacent in neighbours[at])
            {
                if(visited[adjacent])continue;
                visited[adjacent]=true;q.Enqueue(adjacent);
            }
        }
        if(count!=triangles.Count)
            throw new ArgumentException("STL has disconnected shells.");
        if(Math.Abs(signedVolume)<1e-9)
            throw new ArgumentException("STL enclosed signed volume is zero.");
        // Face winding may be consistently inverted. Parity-based sign
        // determination is orientation-independent, so this is permissible.
        _root=Build(triangles,0);
    }
    private static int Compare(Point3 a,Point3 b)
    {
        int c=a.X.CompareTo(b.X);if(c!=0)return c;
        c=a.Y.CompareTo(b.Y);return c!=0?c:a.Z.CompareTo(b.Z);
    }
    internal static ImplicitMesh Load(string path)
    {
        var all=File.ReadAllBytes(path);
        var faces=new List<Triangle>();
        bool binary=false;
        if(all.Length>=84)
        {
            uint n=BitConverter.ToUInt32(all,80);
            binary=(long)84+(long)n*50==all.Length;
        }
        if(binary)
        {
            using var reader=new BinaryReader(new MemoryStream(all,false));
            reader.ReadBytes(80);
            var count=reader.ReadUInt32();
            for(uint t=0;t<count;t++)
            {
                reader.ReadBytes(12); // STL supplied facet normal not trusted.
                Point3 ReadPoint()=>new(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                var a=ReadPoint();var b=ReadPoint();var c=ReadPoint();
                reader.ReadUInt16();
                faces.Add(new Triangle(a,b,c));
            }
        }
        else
        {
            var vertices=new List<Point3>(3);
            foreach(var line in File.ReadLines(path))
            {
                var span=line.Trim().Split((char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries);
                if(span.Length!=4||span[0]!="vertex")continue;
                var v=new Point3(
                    double.Parse(span[1],CultureInfo.InvariantCulture),
                    double.Parse(span[2],CultureInfo.InvariantCulture),
                    double.Parse(span[3],CultureInfo.InvariantCulture));
                vertices.Add(v);
                if(vertices.Count==3)
                {
                    faces.Add(new Triangle(vertices[0],vertices[1],vertices[2]));
                    vertices.Clear();
                }
            }
            if(vertices.Count!=0)throw new ArgumentException("STL vertex triplet incomplete.");
        }
        if(faces.Any(f=>!Finite(f.A)||!Finite(f.B)||!Finite(f.C)))
            throw new ArgumentException("STL vertices not finite.");
        Console.WriteLine($"INPUT STL: {path}, {faces.Count} validated facets");
        return new ImplicitMesh(faces);
    }
    private static bool Finite(Point3 p)=>
        double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);

    private static double Coordinate(Point3 p,int axis)=>axis==0?p.X:axis==1?p.Y:p.Z;
    private static Node Build(List<Triangle> triangles,int depth)
    {
        var node=new Node{
            Lo=new Point3(triangles.Min(t=>t.Min.X),triangles.Min(t=>t.Min.Y),triangles.Min(t=>t.Min.Z)),
            Hi=new Point3(triangles.Max(t=>t.Max.X),triangles.Max(t=>t.Max.Y),triangles.Max(t=>t.Max.Z))
        };
        if(triangles.Count<=12){node.Facets=triangles.ToArray();return node;}
        var ext=node.Hi-node.Lo;
        var axis=ext.X>=ext.Y&&ext.X>=ext.Z?0:(ext.Y>=ext.Z?1:2);
        triangles.Sort((a,b)=>Coordinate(a.Centroid,axis).CompareTo(Coordinate(b.Centroid,axis)));
        int middle=triangles.Count/2;
        node.Left=Build(triangles.GetRange(0,middle),depth+1);
        node.Right=Build(triangles.GetRange(middle,triangles.Count-middle),depth+1);
        return node;
    }
    private static double BoundsSq(Point3 p,Point3 a,Point3 b)
    {
        var dx=Math.Max(a.X-p.X,Math.Max(0,p.X-b.X));
        var dy=Math.Max(a.Y-p.Y,Math.Max(0,p.Y-b.Y));
        var dz=Math.Max(a.Z-p.Z,Math.Max(0,p.Z-b.Z));
        return dx*dx+dy*dy+dz*dz;
    }
    // Exact closest-point squared distance to a triangle, using the
    // face/edge/vertex Voronoi classification (Ericson).
    private static double DistanceSq(Point3 p,Triangle t)
    {
        var a=t.A;var b=t.B;var c=t.C;
        var ab=b-a;var ac=c-a;var ap=p-a;
        double d1=Point3.Dot(ab,ap),d2=Point3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return Point3.Dot(ap,ap);
        var bp=p-b;
        double d3=Point3.Dot(ab,bp),d4=Point3.Dot(ac,bp);
        if(d3>=0&&d4<=d3)return Point3.Dot(bp,bp);
        double vc=d1*d4-d3*d2;
        if(vc<=0&&d1>=0&&d3<=0)
        {
            var v=d1/(d1-d3);var q=a+ab*v;
            return Point3.Dot(p-q,p-q);
        }
        var cp=p-c;
        double d5=Point3.Dot(ab,cp),d6=Point3.Dot(ac,cp);
        if(d6>=0&&d5<=d6)return Point3.Dot(cp,cp);
        double vb=d5*d2-d1*d6;
        if(vb<=0&&d2>=0&&d6<=0)
        {
            var w=d2/(d2-d6);var q=a+ac*w;
            return Point3.Dot(p-q,p-q);
        }
        double va=d3*d6-d5*d4;
        if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)
        {
            var v=(d4-d3)/((d4-d3)+(d5-d6));var q=b+(c-b)*v;
            return Point3.Dot(p-q,p-q);
        }
        var sum=va+vb+vc;
        if(Math.Abs(sum)<1e-30)
            return Math.Min(Point3.Dot(ap,ap),Math.Min(Point3.Dot(bp,bp),Point3.Dot(cp,cp)));
        double vFace=vb/sum,wFace=vc/sum;
        var near=a+ab*vFace+ac*wFace;
        return Point3.Dot(p-near,p-near);
    }
    private static void Closest(Node node,Point3 p,ref double best)
    {
        if(BoundsSq(p,node.Lo,node.Hi)>best)return;
        if(node.Facets is not null)
        {
            foreach(var f in node.Facets)
                best=Math.Min(best,DistanceSq(p,f));
            return;
        }
        if(node.Left is null||node.Right is null)throw new InvalidOperationException("Malformed STL BVH");
        var dl=BoundsSq(p,node.Left.Lo,node.Left.Hi);
        var dr=BoundsSq(p,node.Right.Lo,node.Right.Hi);
        if(dl<dr){Closest(node.Left,p,ref best);Closest(node.Right,p,ref best);}
        else {Closest(node.Right,p,ref best);Closest(node.Left,p,ref best);}
    }
    private static readonly Point3 Ray=new(1,0.12345671,0.23456789);
    private static bool RayBox(Point3 p,Point3 lo,Point3 hi)
    {
        double min=0,max=double.PositiveInfinity;
        for(int i=0;i<3;i++)
        {
            var origin=Coordinate(p,i);
            var direction=Coordinate(Ray,i);
            var a=(Coordinate(lo,i)-origin)/direction;
            var b=(Coordinate(hi,i)-origin)/direction;
            if(a>b)(a,b)=(b,a);
            min=Math.Max(min,a);max=Math.Min(max,b);
            if(max<min)return false;
        }
        return max>0;
    }
    private static bool RayHits(Point3 origin,Triangle tri)
    {
        var a=tri.A;var b=tri.B;var c=tri.C;
        var e1=b-a;var e2=c-a;
        var h=Point3.Cross(Ray,e2);
        var determinant=Point3.Dot(e1,h);
        if(Math.Abs(determinant)<1e-12)return false;
        var inverse=1/determinant;
        var s=origin-a;
        var u=inverse*Point3.Dot(s,h);
        if(u<1e-10||u>1-1e-10)return false;
        var q=Point3.Cross(s,e1);
        var v=inverse*Point3.Dot(Ray,q);
        if(v<1e-10||u+v>1-1e-10)return false;
        var t=inverse*Point3.Dot(e2,q);
        return t>1e-9;
    }
    private static int CountRay(Node node,Point3 p)
    {
        if(!RayBox(p,node.Lo,node.Hi))return 0;
        if(node.Facets is not null)
        {
            int count=0;foreach(var f in node.Facets)if(RayHits(p,f))count++;
            return count;
        }
        return CountRay(node.Left!,p)+CountRay(node.Right!,p);
    }
    internal override double F(Point3 p)
    {
        double best=double.PositiveInfinity;
        Closest(_root,p,ref best);
        var dist=Math.Sqrt(best);
        if(dist<1e-8)return 0;
        return (CountRay(_root,p)&1)==1?dist:-dist;
    }
}
