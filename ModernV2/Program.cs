using DCad.MeshKernel;

if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
{
    SelfTests.Run();
    return;
}

var body = MeshFactory.Box(new Vec3d(-8, -8, -4), new Vec3d(8, 8, 4));
var bore = MeshFactory.Cylinder(new Vec3d(0, 0, 0), 5.2, 14, 64);
var bridge = MeshTransforms.RotateZ(
    MeshFactory.Box(new Vec3d(-2, -12, -2), new Vec3d(2, 12, 2)),
    8.0 * Math.PI / 180.0);
var boss = PrimitiveFactory.Sphere(new Vec3d(6.2, 0, 1.0), 2.2, 32, 16);

var result = body.Subtract(bore).Union(bridge).Union(boss);
var stats = MeshAnalysis.Analyze(result);
var validation = MeshValidation.Validate(result, 1e-6);

Console.WriteLine($"triangles={stats.Triangles}");
Console.WriteLine($"area={stats.Area:F6}");
Console.WriteLine($"signedVolume={stats.SignedVolume:F6}");
Console.WriteLine($"bounds={stats.Min} -> {stats.Max}");
Console.WriteLine($"validation={validation}");

var output = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "v2-modern-demo.stl";
if (args.Any(a => string.Equals(a, "--ascii", StringComparison.OrdinalIgnoreCase)))
    StlWriter.WriteAscii(output, result, "v2-modern-demo");
else
    MeshExport.WriteBinaryStl(output, result, "DCad V2 modern BSP-CSG");

if (args.Any(a => string.Equals(a, "--obj", StringComparison.OrdinalIgnoreCase)))
    MeshExport.WriteObj(Path.ChangeExtension(output, ".obj"), result, "v2-modern-demo");

Console.WriteLine(Path.GetFullPath(output));
