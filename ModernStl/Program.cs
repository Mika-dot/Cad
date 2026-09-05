using DCad.StlToolkit;

if(args.Any(a=>string.Equals(a,"--self-test",StringComparison.OrdinalIgnoreCase)))
{
    StlSelfTest.Run();
    return;
}

if(args.Length==0 || args.Any(a=>a=="--help"||a=="-h"))
{
    Console.WriteLine("DCad.StlToolkit");
    Console.WriteLine("  dcad-stl <input.stl> [--audit] [--repair output.stl] [--obj output.obj] [--ascii output.stl] [--scale N]");
    Console.WriteLine("  dcad-stl --self-test");
    return;
}

string input=args[0];
var mesh=StlReader.Read(input);
double scale=ReadDouble(args,"--scale",1.0);
if(Math.Abs(scale-1.0)>1e-15) mesh=mesh.Transform(scale,default);

var audit=StlAudit.Analyze(mesh);
PrintAudit(audit);

string? repair=Value(args,"--repair");
if(repair!=null)
{
    var cleaned=mesh.RemoveDegenerate();
    StlWriter.WriteBinary(repair,cleaned,"DCad repaired STL");
    Console.WriteLine("repaired="+Path.GetFullPath(repair));
    PrintAudit(StlAudit.Analyze(cleaned));
}

string? obj=Value(args,"--obj");
if(obj!=null){StlWriter.WriteObj(obj,mesh,Path.GetFileNameWithoutExtension(input));Console.WriteLine("obj="+Path.GetFullPath(obj));}
string? ascii=Value(args,"--ascii");
if(ascii!=null){StlWriter.WriteAscii(ascii,mesh,Path.GetFileNameWithoutExtension(input));Console.WriteLine("ascii="+Path.GetFullPath(ascii));}

static string? Value(string[] args,string key)
{
    int i=Array.FindIndex(args,a=>string.Equals(a,key,StringComparison.OrdinalIgnoreCase));
    return i>=0&&i+1<args.Length?args[i+1]:null;
}
static double ReadDouble(string[] args,string key,double fallback)
{
    var value=Value(args,key);return value==null?fallback:double.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
}
static void PrintAudit(MeshAudit a)
{
    Console.WriteLine($"triangles={a.Triangles}");
    Console.WriteLine($"degenerate={a.DegenerateTriangles}");
    Console.WriteLine($"boundaryEdges={a.BoundaryEdges}");
    Console.WriteLine($"nonManifoldEdges={a.NonManifoldEdges}");
    Console.WriteLine($"duplicateTriangles={a.DuplicateTriangles}");
    Console.WriteLine($"surfaceArea={a.SurfaceArea:G12}");
    Console.WriteLine($"absoluteVolume={a.AbsoluteVolume:G12}");
    Console.WriteLine($"bounds={a.Min} -> {a.Max}");
    Console.WriteLine($"closedManifold={a.IsClosedManifold}");
}
