using System.Globalization;

namespace DCad.StlToolkit;

public readonly record struct Vec3d(double X, double Y, double Z)
{
    public static Vec3d operator +(Vec3d a, Vec3d b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Vec3d operator -(Vec3d a, Vec3d b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Vec3d operator *(Vec3d a, double s) => new(a.X*s,a.Y*s,a.Z*s);
    public double Dot(Vec3d b) => X*b.X+Y*b.Y+Z*b.Z;
    public Vec3d Cross(Vec3d b) => new(Y*b.Z-Z*b.Y,Z*b.X-X*b.Z,X*b.Y-Y*b.X);
    public double Length => Math.Sqrt(Dot(this));
    public Vec3d Normalized => Length>1e-15 ? this*(1.0/Length) : default;
}

public readonly record struct StlTriangle(Vec3d A, Vec3d B, Vec3d C)
{
    public Vec3d Normal => (B-A).Cross(C-A).Normalized;
    public double Area => (B-A).Cross(C-A).Length*0.5;
    public double SignedVolume => A.Dot(B.Cross(C))/6.0;
}

public sealed class StlMesh
{
    public List<StlTriangle> Triangles { get; } = new();
    public StlMesh() { }
    public StlMesh(IEnumerable<StlTriangle> triangles) => Triangles.AddRange(triangles);

    public StlMesh Transform(double scale, Vec3d translate)
        => new(Triangles.Select(t => new StlTriangle(t.A*scale+translate,t.B*scale+translate,t.C*scale+translate)));

    public StlMesh RemoveDegenerate(double areaTolerance=1e-12)
        => new(Triangles.Where(t=>t.Area>areaTolerance));
}

public readonly record struct MeshAudit(
    int Triangles,
    int DegenerateTriangles,
    int BoundaryEdges,
    int NonManifoldEdges,
    int DuplicateTriangles,
    double SurfaceArea,
    double AbsoluteVolume,
    Vec3d Min,
    Vec3d Max)
{
    public bool IsClosedManifold => DegenerateTriangles==0 && BoundaryEdges==0 && NonManifoldEdges==0;
}

public static class StlReader
{
    public static StlMesh Read(string path)
    {
        using var stream=File.OpenRead(path);
        if(IsBinary(stream)) return ReadBinary(stream);
        stream.Position=0;
        return ReadAscii(stream);
    }

    private static bool IsBinary(Stream stream)
    {
        if(stream.Length<84) return false;
        using var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true);
        stream.Position=80;
        uint count=reader.ReadUInt32();
        long expected=84L+50L*count;
        stream.Position=0;
        return expected==stream.Length;
    }

    private static StlMesh ReadBinary(Stream stream)
    {
        var mesh=new StlMesh();
        using var reader=new BinaryReader(stream,System.Text.Encoding.ASCII,true);
        reader.ReadBytes(80);
        uint count=reader.ReadUInt32();
        if(84L+50L*count!=stream.Length) throw new InvalidDataException("Invalid binary STL length.");
        for(uint i=0;i<count;i++)
        {
            ReadVec(reader); // stored normal; recomputed from geometry
            var a=ReadVec(reader);var b=ReadVec(reader);var c=ReadVec(reader);
            reader.ReadUInt16();
            mesh.Triangles.Add(new StlTriangle(a,b,c));
        }
        return mesh;
    }

    private static StlMesh ReadAscii(Stream stream)
    {
        var mesh=new StlMesh();
        using var reader=new StreamReader(stream,System.Text.Encoding.ASCII,true,4096,true);
        var vertices=new List<Vec3d>(3);
        string? line;
        while((line=reader.ReadLine())!=null)
        {
            line=line.Trim();
            if(!line.StartsWith("vertex",StringComparison.OrdinalIgnoreCase)) continue;
            var p=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(p.Length<4) throw new InvalidDataException("Malformed ASCII STL vertex: "+line);
            vertices.Add(new Vec3d(Parse(p[1]),Parse(p[2]),Parse(p[3])));
            if(vertices.Count==3)
            {
                mesh.Triangles.Add(new StlTriangle(vertices[0],vertices[1],vertices[2]));
                vertices.Clear();
            }
        }
        if(vertices.Count!=0) throw new InvalidDataException("Incomplete ASCII STL facet.");
        if(mesh.Triangles.Count==0) throw new InvalidDataException("No triangles found in STL.");
        return mesh;
    }

    private static Vec3d ReadVec(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    private static double Parse(string s)=>double.Parse(s,NumberStyles.Float,CultureInfo.InvariantCulture);
}

public static class StlAudit
{
    private readonly record struct Q(long X,long Y,long Z);
    private readonly record struct Edge(Q A,Q B);
    private readonly record struct TriKey(Q A,Q B,Q C);

    public static MeshAudit Analyze(StlMesh mesh,double tolerance=1e-7,double degenerateArea=1e-12)
    {
        tolerance=Math.Max(tolerance,1e-12);
        var edges=new Dictionary<Edge,int>();
        var tris=new HashSet<TriKey>();
        int degenerate=0,duplicates=0;
        double area=0,volume=0;
        var min=new Vec3d(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity);
        var max=new Vec3d(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity);

        foreach(var t in mesh.Triangles)
        {
            double a=t.Area; area+=a; volume+=t.SignedVolume;
            if(a<=degenerateArea) degenerate++;
            min=Min(min,t.A);min=Min(min,t.B);min=Min(min,t.C);
            max=Max(max,t.A);max=Max(max,t.B);max=Max(max,t.C);
            var qa=Quantize(t.A,tolerance);var qb=Quantize(t.B,tolerance);var qc=Quantize(t.C,tolerance);
            AddEdge(edges,qa,qb);AddEdge(edges,qb,qc);AddEdge(edges,qc,qa);
            var key=TriangleKey(qa,qb,qc);
            if(!tris.Add(key)) duplicates++;
        }

        int boundary=0,nonManifold=0;
        foreach(int count in edges.Values)
        {
            if(count==1) boundary++;
            else if(count!=2) nonManifold++;
        }
        if(mesh.Triangles.Count==0) min=max=default;
        return new MeshAudit(mesh.Triangles.Count,degenerate,boundary,nonManifold,duplicates,area,Math.Abs(volume),min,max);
    }

    private static void AddEdge(Dictionary<Edge,int> edges,Q a,Q b)
    {
        var key=Compare(a,b)<=0?new Edge(a,b):new Edge(b,a);
        edges.TryGetValue(key,out int count);edges[key]=count+1;
    }
    private static Q Quantize(Vec3d p,double t)=>new((long)Math.Round(p.X/t),(long)Math.Round(p.Y/t),(long)Math.Round(p.Z/t));
    private static TriKey TriangleKey(Q a,Q b,Q c)
    {
        var v=new[]{a,b,c};Array.Sort(v,Comparer<Q>.Create(Compare));return new TriKey(v[0],v[1],v[2]);
    }
    private static int Compare(Q a,Q b){int c=a.X.CompareTo(b.X);if(c!=0)return c;c=a.Y.CompareTo(b.Y);return c!=0?c:a.Z.CompareTo(b.Z);}
    private static Vec3d Min(Vec3d a,Vec3d b)=>new(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Min(a.Z,b.Z));
    private static Vec3d Max(Vec3d a,Vec3d b)=>new(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),Math.Max(a.Z,b.Z));
}

public static class StlWriter
{
    public static void WriteBinary(string path,StlMesh mesh,string header="DCad STL Toolkit")
    {
        using var stream=File.Create(path);using var writer=new BinaryWriter(stream);
        var headerBytes=new byte[80];var text=System.Text.Encoding.ASCII.GetBytes(header);Array.Copy(text,headerBytes,Math.Min(text.Length,80));
        writer.Write(headerBytes);writer.Write((uint)mesh.Triangles.Count);
        foreach(var t in mesh.Triangles)
        {
            Write(writer,t.Normal);Write(writer,t.A);Write(writer,t.B);Write(writer,t.C);writer.Write((ushort)0);
        }
    }

    public static void WriteAscii(string path,StlMesh mesh,string name="dcad")
    {
        using var w=new StreamWriter(path);var ci=CultureInfo.InvariantCulture;w.WriteLine("solid "+name);
        foreach(var t in mesh.Triangles)
        {
            var n=t.Normal;w.WriteLine($"  facet normal {F(n.X,ci)} {F(n.Y,ci)} {F(n.Z,ci)}");w.WriteLine("    outer loop");
            Vertex(w,t.A,ci);Vertex(w,t.B,ci);Vertex(w,t.C,ci);w.WriteLine("    endloop");w.WriteLine("  endfacet");
        }
        w.WriteLine("endsolid "+name);
    }

    public static void WriteObj(string path,StlMesh mesh,string name="dcad")
    {
        using var w=new StreamWriter(path);var ci=CultureInfo.InvariantCulture;w.WriteLine("o "+name);int i=1;
        foreach(var t in mesh.Triangles)
        {
            ObjVertex(w,t.A,ci);ObjVertex(w,t.B,ci);ObjVertex(w,t.C,ci);w.WriteLine($"f {i} {i+1} {i+2}");i+=3;
        }
    }

    private static void Write(BinaryWriter w,Vec3d v){w.Write((float)v.X);w.Write((float)v.Y);w.Write((float)v.Z);}
    private static string F(double v,CultureInfo ci)=>v.ToString("G17",ci);
    private static void Vertex(StreamWriter w,Vec3d v,CultureInfo ci)=>w.WriteLine($"      vertex {F(v.X,ci)} {F(v.Y,ci)} {F(v.Z,ci)}");
    private static void ObjVertex(StreamWriter w,Vec3d v,CultureInfo ci)=>w.WriteLine($"v {F(v.X,ci)} {F(v.Y,ci)} {F(v.Z,ci)}");
}

public static class StlSelfTest
{
    public static void Run()
    {
        var v=new[]{new Vec3d(-1,-1,-1),new Vec3d(1,-1,-1),new Vec3d(1,1,-1),new Vec3d(-1,1,-1),new Vec3d(-1,-1,1),new Vec3d(1,-1,1),new Vec3d(1,1,1),new Vec3d(-1,1,1)};
        var t=new List<StlTriangle>();
        AddQuad(t,v[0],v[3],v[2],v[1]);AddQuad(t,v[4],v[5],v[6],v[7]);AddQuad(t,v[0],v[1],v[5],v[4]);
        AddQuad(t,v[1],v[2],v[6],v[5]);AddQuad(t,v[2],v[3],v[7],v[6]);AddQuad(t,v[3],v[0],v[4],v[7]);
        var mesh=new StlMesh(t);var audit=StlAudit.Analyze(mesh,1e-8);
        if(!audit.IsClosedManifold||audit.Triangles!=12||Math.Abs(audit.AbsoluteVolume-8)>1e-9)throw new InvalidOperationException("STL audit self-test failed: "+audit);
        string path=Path.Combine(Path.GetTempPath(),"dcad-stl-selftest.stl");StlWriter.WriteBinary(path,mesh);var loaded=StlReader.Read(path);File.Delete(path);
        if(loaded.Triangles.Count!=12)throw new InvalidOperationException("STL round-trip self-test failed.");
        Console.WriteLine("DCad.StlToolkit self-test: PASS");Console.WriteLine(audit);
    }
    private static void AddQuad(List<StlTriangle> t,Vec3d a,Vec3d b,Vec3d c,Vec3d d){t.Add(new StlTriangle(a,b,c));t.Add(new StlTriangle(a,c,d));}
}
