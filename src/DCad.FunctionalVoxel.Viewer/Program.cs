using DCad.FunctionalVoxel.Viewer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            return SelfTest();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int SelfTest()
    {
        var temporaryFolder = Path.Combine(Path.GetTempPath(),
            "dcad_fvm_viewer_smoke_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryFolder);
        try
        {
            // Run real kernel operations, not synthetic placeholder geometry.
            foreach (ShapeKind kind in Enum.GetValues<ShapeKind>())
            {
                var scene = SceneBuilder.Build(new SceneSettings(kind,
                    FieldView.Original, 10, 3, 16));
                if (scene.Model.ChannelCount != 5 || scene.Mesh is null ||
                    scene.Mesh.Triangles.Count == 0)
                    throw new InvalidOperationException($"No surface/M-images for {kind}.");
                Console.WriteLine($"{kind}: {scene.Mesh.Triangles.Count} triangles, " +
                    $"volume={scene.PositiveVoxelVolume:F3} mm3");
            }

            var sample = SceneBuilder.Build(new SceneSettings(
                ShapeKind.Sphere, FieldView.Original, 10, 3, 24));
            if (sample.MeshAudit?.IsClosedOrientedManifold != true)
                throw new InvalidOperationException("Sphere surface must be a closed manifold.");

            var obj = Path.Combine(temporaryFolder, "sphere.obj");
            var csv = Path.Combine(temporaryFolder, "slice.csv");
            SceneExports.WriteObj(sample, obj);
            SceneExports.WriteSliceCsv(sample, 12, csv);
            if (!File.ReadLines(obj).Any(line => line.StartsWith("f ", StringComparison.Ordinal)) ||
                File.ReadLines(csv).Count() != 1 + 24 * 24)
                throw new InvalidOperationException("OBJ or CSV export is incomplete.");

            var derivative = SceneBuilder.Build(new SceneSettings(
                ShapeKind.Sphere, FieldView.DerivativeX, 10, 3, 16));
            if (derivative.Model.VoxelCount != 16 * 16 * 16)
                throw new InvalidOperationException("Derivative was not computed.");
            Console.WriteLine("VIEWER_SMOKE_PASS");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("VIEWER_SMOKE_FAIL: " + e);
            return 1;
        }
        finally
        {
            try { Directory.Delete(temporaryFolder, true); }
            catch (IOException) { /* diagnostics do not depend on temporary cleanup */ }
        }
    }
}
