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

            // Exercise the actual Windows drawing controls without showing a window.
            using (var viewport = new Viewport3D { Width = 560, Height = 420 })
            using (var preview = new System.Drawing.Bitmap(560, 420))
            {
                viewport.SetMesh(sample.Mesh);
                viewport.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, 560, 420));
                var coloured = 0;
                for (var y = 40; y < 370; y += 6)
                for (var x = 40; x < 520; x += 6)
                {
                    var pixel = preview.GetPixel(x, y);
                    if (pixel.B > pixel.R + 10 && pixel.B > pixel.G + 8)
                        coloured++;
                }
                if (coloured < 150)
                    throw new InvalidOperationException(
                        $"3D viewport rendered too few surface pixels ({coloured}).");
            }
            using (var slice = new SliceView { Width = 330, Height = 330 })
            using (var image = new System.Drawing.Bitmap(330, 330))
            {
                slice.SetModel(sample.Model);
                slice.SetChannel(4);
                slice.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, 330, 330));
                if (image.GetPixel(165, 165).ToArgb() == System.Drawing.Color.White.ToArgb())
                    throw new InvalidOperationException("M-image slice was not rendered.");
                var png = Path.Combine(temporaryFolder, "m-image.png");
                slice.SavePng(png);
                if (!File.Exists(png) || new FileInfo(png).Length < 200)
                    throw new InvalidOperationException("M-image PNG export failed.");
            }
            using (var engineeringForm = new MainForm())
            {
                if (engineeringForm.Controls.Count < 2)
                    throw new InvalidOperationException("Missing GUI layout or status bar.");
            }

            var obj = Path.Combine(temporaryFolder, "sphere.obj");
            var csv = Path.Combine(temporaryFolder, "slice.csv");
            SceneExports.WriteObj(sample, obj);
            SceneExports.WriteSliceCsv(sample, 12, csv);
            if (!File.ReadLines(obj).Any(line => line.StartsWith("f ", StringComparison.Ordinal)) ||
                File.ReadLines(csv).Count() != 1 + 24 * 24)
                throw new InvalidOperationException("OBJ or CSV export is incomplete.");

            var alphaModel = SceneBuilder.Build(new SceneSettings(
                ShapeKind.TwoSpheresUnion, FieldView.Original, 10, 3, 16, .35));
            if (alphaModel.Model.ChannelCount != 5)
                throw new InvalidOperationException("R-alpha reconstruction failed.");

            var integral = SceneBuilder.Build(new SceneSettings(
                ShapeKind.Sphere, FieldView.IntegralX, 10, 3, 16, 0, -.02));
            var grid = integral.Model.Grid;
            var atInitial = grid.LinearIndex(0, 4, 4);
            if (Math.Abs(integral.Model.ValueAtCentre(atInitial) - (-.02)) > 1e-8)
                throw new InvalidOperationException("Cauchy initial value was ignored.");

            // Zero Cauchy value is mathematically valid, even though its
            // initial lattice plane is singular for the mesh extractor.
            var zeroCauchy = SceneBuilder.Build(new SceneSettings(
                ShapeKind.Sphere, FieldView.IntegralX, 10, 3, 16, 0, 0));
            if (zeroCauchy.Model.VoxelCount != 16 * 16 * 16 ||
                string.IsNullOrWhiteSpace(zeroCauchy.SurfaceWarning))
                throw new InvalidOperationException("Singular iso-surface must not lose computed M-images.");

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
