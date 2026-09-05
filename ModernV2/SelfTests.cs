namespace DCad.MeshKernel;

public static class SelfTests
{
    public static void Run()
    {
        var cube = MeshFactory.Box(new Vec3d(-1, -1, -1), new Vec3d(1, 1, 1));
        var stats = MeshAnalysis.Analyze(cube);
        Assert(stats.Triangles == 12, "cube triangle count");
        Assert(Math.Abs(Math.Abs(stats.SignedVolume) - 8.0) < 1e-9, "cube volume");

        var validation = MeshValidation.Validate(cube, 1e-8);
        Assert(validation.DegenerateTriangles == 0, "cube has no degenerate triangles");
        Assert(validation.BoundaryEdges == 0, "cube has no boundary edges");
        Assert(validation.NonManifoldEdges == 0, "cube is two-manifold");

        var moved = MeshTransforms.Translate(cube, new Vec3d(5, -3, 2));
        var movedStats = MeshAnalysis.Analyze(moved);
        Assert(Math.Abs(movedStats.Min.X - 4) < 1e-9 && Math.Abs(movedStats.Max.X - 6) < 1e-9, "translation bounds");
        Assert(Math.Abs(Math.Abs(movedStats.SignedVolume) - 8.0) < 1e-8, "translation preserves volume");

        var scaled = MeshTransforms.Scale(cube, new Vec3d(2, 3, 4));
        var scaledStats = MeshAnalysis.Analyze(scaled);
        Assert(Math.Abs(Math.Abs(scaledStats.SignedVolume) - 192.0) < 1e-8, "scale volume determinant");

        Assert(MeshPicking.TryIntersectRay(cube, new Vec3d(0, 0, 5), new Vec3d(0, 0, -1), out var distance, out var hit), "ray hits cube");
        Assert(Math.Abs(distance - 4.0) < 1e-8 && Math.Abs(hit.Z - 1.0) < 1e-8, "ray hit distance");

        var sphere = PrimitiveFactory.Sphere(default, 2.0, 32, 16);
        var sphereValidation = MeshValidation.Validate(sphere, 1e-7);
        Assert(sphereValidation.DegenerateTriangles == 0, "sphere has no degenerate triangles");
        Assert(sphereValidation.BoundaryEdges == 0, "sphere is closed");

        var cut = cube.Subtract(MeshFactory.Cylinder(default, 0.4, 4, 32));
        var cutStats = MeshAnalysis.Analyze(cut);
        Assert(cutStats.Triangles > 12, "CSG creates split topology");
        Assert(Math.Abs(cutStats.SignedVolume) < Math.Abs(stats.SignedVolume), "subtraction reduces absolute volume");

        string stl = Path.Combine(Path.GetTempPath(), "dcad-v2-selftest.stl");
        MeshExport.WriteBinaryStl(stl, cube);
        Assert(new FileInfo(stl).Length == 84 + 50L * 12, "binary STL layout");
        File.Delete(stl);

        Console.WriteLine("DCad.MeshKernel self-test: PASS");
        Console.WriteLine($"cube: {validation}");
        Console.WriteLine($"sphere: {sphereValidation}");
        Console.WriteLine($"CSG triangles: {cutStats.Triangles}");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Self-test failed: " + name);
    }
}
