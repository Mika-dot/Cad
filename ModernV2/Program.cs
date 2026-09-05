using DCad.MeshKernel;

var body = MeshFactory.Box(new Vec3d(-8, -8, -4), new Vec3d(8, 8, 4));
var bore = MeshFactory.Cylinder(new Vec3d(0, 0, 0), 5.2, 14, 64);
var bridge = MeshFactory.Box(new Vec3d(-2, -12, -2), new Vec3d(2, 12, 2));

var result = body.Subtract(bore).Union(bridge);
var stats = MeshAnalysis.Analyze(result);

Console.WriteLine($"triangles={stats.Triangles}");
Console.WriteLine($"area={stats.Area:F6}");
Console.WriteLine($"signedVolume={stats.SignedVolume:F6}");
Console.WriteLine($"bounds={stats.Min} -> {stats.Max}");

var output = args.Length > 0 ? args[0] : "v2-modern-demo.stl";
StlWriter.WriteAscii(output, result, "v2-modern-demo");
Console.WriteLine(Path.GetFullPath(output));
