using System.Globalization;
using System.Text;

namespace DCad.StlTools;

public readonly record struct Vec3(double X,double Y,double Z)
{
    public static Vec3 operator +(Vec3 a,Vec3 b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Vec3 operator -(Vec3 a,Vec3 b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Vec3 operator *(Vec3 a,double s)=>new(a.X*s,a.Y*s,a.Z*s);
    public double Dot(Vec3 b)=>X*b.X+Y*b.Y+Z*b.Z;
    public Vec3 Cross(Vec3 b)=>new(Y*b.Z-Z*b.Y,Z*b.X-X*b.Z,X*b.Y-Y*b.X);
    public double Length=>Math.Sqrt(Dot(this));
    public Vec3 Normalized=>Length>1e-18?this*(1.0/Length):default;
}

public readonly record struct Face(int A,int B,int C);
public readonly record struct Edge(int A,int B)
{
    public static Edge Of(int a,int b)=>a<b?new Edge(a,b):new Edge(b,a);
}

public sealed class IndexedMesh
{
    public List<Vec3> Vertices { get; }=new();
    public List<Face> Faces { get; }=new();

    public (Vec3 Min,Vec3 Max) Bounds()
    {
        if(Vertices.Count==0)return(default,default);
        var min=new Vec3(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity);
        var max=new Vec3(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity);
        foreach(var p in Vertices){min=new Vec3(Math.Min(min.X,p.X),Math.Min(min.Y,p.Y),Math.Min(min.Z,p.Z));max=new Vec3(Math.Max(max.X,p.X),Math.Max(max.Y,p.Y),Math.Max(max.Z,p.Z));}
        return(min,max);
    }
    public double SurfaceArea()=>Faces.Sum(f=>(Vertices[f.B]-Vertices[f.A]).Cross(Vertices[f.C]-Vertices[f.A]).Length*.5);
    public double SignedVolume()=>Faces.Sum(f=>Vertices[f.A].Dot(Vertices[f.B].Cross(Vertices[f.C]))/6.0);
}

public static class StlReader
{
    public static IndexedMesh Read(string path,double weldTolerance=1e-6)
    {
        using var stream=File.OpenRead(path);
        bool binary=LooksBinary(stream);
        stream.Position=0;
        var triangles=binary?ReadBinary(stream):ReadAscii(stream);
        return MeshRepair.Weld(triangles,weldTolerance);
    }

    static bool LooksBinary(Stream s)
    {
        if(s.Length<84)return false;
        Span<byte> header=stackalloc byte[84];s.ReadExactly(header);uint count=BitConverter.ToUInt32(header[80..84]);
        long expected=84L+50L*count;
        if(expected==s.Length)return true;
        string first=Encoding.ASCII.GetString(header[..Math.Min(20,header.Length)]).TrimStart();
        return !first.StartsWith("solid",StringComparison.OrdinalIgnoreCase);
    }

    static List<(Vec3 A,Vec3 B,Vec3 C)> ReadBinary(Stream stream)
    {
        using var br=new BinaryReader(stream,Encoding.ASCII,leaveOpen:true);br.ReadBytes(80);uint n=br.ReadUInt32();
        var list=new List<(Vec3,Vec3,Vec3)>((int)Math.Min(n,int.MaxValue));
        for(uint i=0;i<n;i++)
        {
            br.ReadSingle();br.ReadSingle();br.ReadSingle();
            Vec3 ReadV()=>new(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
            var a=ReadV();var b=ReadV();var c=ReadV();br.ReadUInt16();list.Add((a,b,c));
        }
        return list;
    }

    static List<(Vec3 A,Vec3 B,Vec3 C)> ReadAscii(Stream stream)
    {
        using var sr=new StreamReader(stream,Encoding.UTF8,true,4096,leaveOpen:true);var verts=new List<Vec3>();var tris=new List<(Vec3,Vec3,Vec3)>();string? line;
        while((line=sr.ReadLine())!=null)
        {
            string t=line.Trim();if(!t.StartsWith("vertex ",StringComparison.OrdinalIgnoreCase))continue;
            var parts=t.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(parts.Length<4)continue;
            verts.Add(new Vec3(double.Parse(parts[1],CultureInfo.InvariantCulture),double.Parse(parts[2],CultureInfo.InvariantCulture),double.Parse(parts[3],CultureInfo.InvariantCulture)));
            if(verts.Count==3){tris.Add((verts[0],verts[1],verts[2]));verts.Clear();}
        }
        return tris;
    }
}

public readonly record struct MeshAudit(
    int Vertices,int Faces,int DegenerateFaces,int BoundaryEdges,int NonManifoldEdges,int Components,double Area,double SignedVolume,Vec3 Min,Vec3 Max);

public static class MeshRepair
{
    readonly record struct Q(long X,long Y,long Z);

    public static IndexedMesh Weld(IEnumerable<(Vec3 A,Vec3 B,Vec3 C)> triangles,double tolerance=1e-6)
    {
        tolerance=Math.Max(tolerance,1e-12);var mesh=new IndexedMesh();var lut=new Dictionary<Q,int>();
        int Add(Vec3 p){var q=new Q((long)Math.Round(p.X/tolerance),(long)Math.Round(p.Y/tolerance),(long)Math.Round(p.Z/tolerance));if(lut.TryGetValue(q,out int id))return id;id=mesh.Vertices.Count;mesh.Vertices.Add(p);lut[q]=id;return id;}
        foreach(var (a,b,c) in triangles){int ia=Add(a),ib=Add(b),ic=Add(c);mesh.Faces.Add(new Face(ia,ib,ic));}
        return mesh;
    }

    public static int RemoveDegenerateFaces(IndexedMesh mesh,double areaEpsilon=1e-12)
    {
        int before=mesh.Faces.Count;mesh.Faces.RemoveAll(f=>f.A==f.B||f.B==f.C||f.C==f.A||(mesh.Vertices[f.B]-mesh.Vertices[f.A]).Cross(mesh.Vertices[f.C]-mesh.Vertices[f.A]).Length*.5<=areaEpsilon);return before-mesh.Faces.Count;
    }

    public static int RemoveDuplicateFaces(IndexedMesh mesh)
    {
        var seen=new HashSet<(int,int,int)>();int before=mesh.Faces.Count;
        mesh.Faces.RemoveAll(f=>{var a=new[]{f.A,f.B,f.C};Array.Sort(a);return !seen.Add((a[0],a[1],a[2]));});return before-mesh.Faces.Count;
    }

    public static MeshAudit Audit(IndexedMesh mesh,double areaEpsilon=1e-12)
    {
        int deg=0;var edges=new Dictionary<Edge,List<int>>();
        for(int fi=0;fi<mesh.Faces.Count;fi++)
        {
            var f=mesh.Faces[fi];double area=(mesh.Vertices[f.B]-mesh.Vertices[f.A]).Cross(mesh.Vertices[f.C]-mesh.Vertices[f.A]).Length*.5;if(area<=areaEpsilon)deg++;
            foreach(var e in new[]{Edge.Of(f.A,f.B),Edge.Of(f.B,f.C),Edge.Of(f.C,f.A)}){if(!edges.TryGetValue(e,out var faces)){faces=new List<int>();edges[e]=faces;}faces.Add(fi);}
        }
        int boundary=edges.Count(e=>e.Value.Count==1);int nonManifold=edges.Count(e=>e.Value.Count>2);int components=CountComponents(mesh.Faces.Count,edges.Values);
        var (min,max)=mesh.Bounds();return new MeshAudit(mesh.Vertices.Count,mesh.Faces.Count,deg,boundary,nonManifold,components,mesh.SurfaceArea(),mesh.SignedVolume(),min,max);
    }

    static int CountComponents(int faceCount,IEnumerable<List<int>> edgeFaces)
    {
        if(faceCount==0)return 0;var adj=new List<int>[faceCount];for(int i=0;i<faceCount;i++)adj[i]=new List<int>();
        foreach(var fs in edgeFaces)for(int i=0;i<fs.Count;i++)for(int j=i+1;j<fs.Count;j++){adj[fs[i]].Add(fs[j]);adj[fs[j]].Add(fs[i]);}
        var seen=new bool[faceCount];int count=0;var q=new Queue<int>();for(int root=0;root<faceCount;root++){if(seen[root])continue;count++;seen[root]=true;q.Enqueue(root);while(q.Count>0){int f=q.Dequeue();foreach(int n in adj[f])if(!seen[n]){seen[n]=true;q.Enqueue(n);}}}return count;
    }
}

public static class StlWriter
{
    public static void WriteBinary(string path,IndexedMesh mesh)
    {
        using var fs=File.Create(path);using var bw=new BinaryWriter(fs);bw.Write(new byte[80]);bw.Write((uint)mesh.Faces.Count);
        foreach(var f in mesh.Faces)
        {
            var a=mesh.Vertices[f.A];var b=mesh.Vertices[f.B];var c=mesh.Vertices[f.C];var n=(b-a).Cross(c-a).Normalized;
            void F(Vec3 p){bw.Write((float)p.X);bw.Write((float)p.Y);bw.Write((float)p.Z);}F(n);F(a);F(b);F(c);bw.Write((ushort)0);
        }
    }
}
