using DCad.Boolean.Manifold;
using DCad.Core;
using DCad.Language;

if (args.Length == 0)
{
    Console.WriteLine("DCad unified kernel\nUsage: dcad <model.dcad> [mesh.obj]");
    return 2;
}

var source = File.ReadAllText(args[0]);
var kernel = new ManifoldKernel();
using var model = CadScript.Execute(source, kernel);
var mesh = model.Result.ToMesh();
var validation = MeshValidator.Validate(mesh);

Console.WriteLine($"volume_mm3={model.Result.Volume:G17}");
Console.WriteLine($"surface_mm2={model.Result.SurfaceArea:G17}");
Console.WriteLine($"vertices={mesh.Vertices.Count}");
Console.WriteLine($"triangles={mesh.Triangles.Count}");
Console.WriteLine($"closed_oriented_manifold={validation.IsClosedOrientedManifold}");
Console.WriteLine($"boundary_edges={validation.BoundaryEdges}");
Console.WriteLine($"nonmanifold_edges={validation.NonManifoldEdges}");

if (args.Length > 1) ObjWriter.Write(args[1], mesh);
return validation.IsClosedOrientedManifold ? 0 : 3;

static class ObjWriter
{
    public static void Write(string path, Mesh3d mesh)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("# DCad generated mesh");
        foreach (var v in mesh.Vertices)
            writer.WriteLine(FormattableString.Invariant($"v {v.X:G17} {v.Y:G17} {v.Z:G17}"));
        foreach (var t in mesh.Triangles)
            writer.WriteLine($"f {t.A + 1} {t.B + 1} {t.C + 1}");
    }
}
