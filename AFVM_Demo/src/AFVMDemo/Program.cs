using OpenTK.Mathematics;

namespace AFVMDemo;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Metrics.RunSelfTests();
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) return 0;

            double tol = GetDouble(args, "--tol", 0.025);
            int maxDepth = GetInt(args, "--depth", 6);
            int uniformDepth = GetInt(args, "--uniform-depth", 5);
            int meshRes = GetInt(args, "--mesh-res", 52);
            var domain = new Box3d(new(-1.6, -1.6, -1.6), new(1.6, 1.6, 1.6));
            var options = new AdaptiveBuildOptions(MaxDepth: maxDepth, MinDepth: 1, MinSurfaceDepth: 3, GeometricTolerance: tol);

            IField3d sphere = new SphereField(DVec3.Zero, 1.15);
            IField3d bore = new InfiniteCylinderZField(DVec3.Zero, 0.42);
            IField3d exact = new RBinaryField(sphere, bore, ROperation.Difference, "Exact R-difference: sphere - cylinder");

            Console.WriteLine("AFVM research demonstrator");
            Console.WriteLine("==========================");
            Console.WriteLine($"Tolerance={tol:G5}; maxDepth={maxDepth}; uniformDepth={uniformDepth}; meshResolution={meshRes}");
            Console.WriteLine();

            Console.WriteLine("[1/7] Building primitive adaptive FV trees...");
            var sphereTree = AdaptiveFunctionalTree.Build(sphere, domain, options);
            var boreTree = AdaptiveFunctionalTree.Build(bore, domain, options);

            Console.WriteLine("[2/7] Building direct adaptive FV reference...");
            var directTree = AdaptiveFunctionalTree.Build(exact, domain, options);

            Console.WriteLine("[3/7] R-composing already discretized primitive FV trees...");
            IField3d localAlgebraSource = new RBinaryField(sphereTree, boreTree, ROperation.Difference, "R(AdaptiveFV sphere, AdaptiveFV cylinder)");
            var algebraTree = AdaptiveFunctionalTree.Build(localAlgebraSource, domain, options);

            Console.WriteLine("[4/7] Building uniform FV baseline...");
            var uniform = new UniformFunctionalGrid(exact, domain, uniformDepth);

            var uniformErr = Metrics.Compare(uniform, exact, domain);
            var directErr = Metrics.Compare(directTree, exact, domain);
            var algebraErr = Metrics.Compare(algebraTree, exact, domain);
            var zeroErr = new ErrorMetrics(0, 0, 0, 0, 0);

            Console.WriteLine("[5/7] Extracting comparison surfaces...");
            Func<IField3d, Func<DVec3, double>> errorScalar = approx => p =>
            {
                var eg = exact.Gradient(p);
                double gn = Math.Max(eg.Length, 1e-9);
                double d = Math.Abs(approx.Value(p) - exact.Value(p)) / gn;
                return Math.Clamp(d / Math.Max(tol, 1e-12), 0.0, 1.0);
            };

            var exactMesh = SurfaceExtractor.Extract(exact, domain, meshRes, _ => 0.0);
            var uniformMesh = SurfaceExtractor.Extract(uniform, domain, meshRes, errorScalar(uniform));
            var directMesh = SurfaceExtractor.Extract(directTree, domain, meshRes, errorScalar(directTree));
            var algebraMesh = SurfaceExtractor.Extract(algebraTree, domain, meshRes, errorScalar(algebraTree));

            Console.WriteLine("[6/7] Building functional-voxel visualization: cell boxes, clipped local planes, normals...");
            var emptyVisual = new VisualizationGeometry(new RenderMesh([], []), [], [], 0, 0);
            var uniformVisual = FunctionalVisualizationBuilder.BuildUniform(uniform, exact, tol);
            var directVisual = FunctionalVisualizationBuilder.BuildAdaptive(directTree, exact, tol);
            var algebraVisual = FunctionalVisualizationBuilder.BuildAdaptive(algebraTree, exact, tol);

            var models = new[]
            {
                new ResearchModel(
                    "Analytic R-field reference", "EXACT", exactMesh, emptyVisual,
                    new Vector3(.68f,.72f,.78f), zeroErr,
                    "Continuous analytic R-function; no discretized local planes."),
                new ResearchModel(
                    "Uniform functional voxels", "UNIFORM", uniformMesh, uniformVisual,
                    new Vector3(.90f,.58f,.22f), uniformErr,
                    $"{uniform.Resolution}^3 = {uniform.CellCount:N0} local linear cells; {uniformVisual.PlanePolygonCount:N0} surface plane polygons."),
                new ResearchModel(
                    "Adaptive FV built directly from analytic composite", "ADAPTIVE-DIRECT", directMesh, directVisual,
                    new Vector3(.20f,.63f,.92f), directErr,
                    $"{directTree.LeafCount:N0} leaves; {directTree.SurfaceLeafCount:N0} surface candidates; {directVisual.PlanePolygonCount:N0} clipped local planes."),
                new ResearchModel(
                    "Adaptive FV from R-algebra of discretized operands", "ADAPTIVE-R", algebraMesh, algebraVisual,
                    new Vector3(.30f,.82f,.44f), algebraErr,
                    $"{algebraTree.LeafCount:N0} leaves; source is R(A_h,B_h), not the original composite function; {algebraVisual.PlanePolygonCount:N0} clipped local planes.")
            };

            Console.WriteLine("[7/7] Ready.");
            Console.WriteLine();
            PrintMetrics(uniform, sphereTree, boreTree, directTree, algebraTree, uniformErr, directErr, algebraErr);
            PrintControls();

            using var window = new ViewerWindow(models, domain, tol);
            window.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintMetrics(
        UniformFunctionalGrid uniform,
        AdaptiveFunctionalTree sphere,
        AdaptiveFunctionalTree bore,
        AdaptiveFunctionalTree direct,
        AdaptiveFunctionalTree algebra,
        ErrorMetrics uniformErr,
        ErrorMetrics directErr,
        ErrorMetrics algebraErr)
    {
        Console.WriteLine("=== REPRESENTATION ===");
        Console.WriteLine($"Uniform FV                : {uniform.Resolution}^3 = {uniform.CellCount:N0} cells");
        Console.WriteLine($"Sphere adaptive operand   : leaves={sphere.LeafCount:N0}, surface={sphere.SurfaceLeafCount:N0}");
        Console.WriteLine($"Cylinder adaptive operand : leaves={bore.LeafCount:N0}, surface={bore.SurfaceLeafCount:N0}");
        Console.WriteLine($"Adaptive direct composite : leaves={direct.LeafCount:N0}, surface={direct.SurfaceLeafCount:N0}");
        Console.WriteLine($"Adaptive R composite      : leaves={algebra.LeafCount:N0}, surface={algebra.SurfaceLeafCount:N0}");
        Console.WriteLine();
        Console.WriteLine("=== ERROR AGAINST ANALYTIC R-GEOMETRY ===");
        Console.WriteLine($"Uniform FV       : {uniformErr}");
        Console.WriteLine($"Adaptive direct  : {directErr}");
        Console.WriteLine($"Adaptive R       : {algebraErr}");
        double saving = uniform.CellCount == 0 ? 0 : 100.0 * (1.0 - algebra.LeafCount / (double)uniform.CellCount);
        Console.WriteLine($"Adaptive-R leaf reduction against uniform: {saving:F1}%");
        Console.WriteLine();
    }

    private static void PrintControls()
    {
        Console.WriteLine("=== VIEWER CONTROLS ===");
        Console.WriteLine("F1      overview 2x2 (TL exact | TR uniform | BL adaptive direct | BR adaptive R)");
        Console.WriteLine("1..4    inspect one representation full-screen");
        Console.WriteLine("Tab     next representation");
        Console.WriteLine("G       adaptive/uniform cell boxes");
        Console.WriteLine("P       clipped local tangent-plane polygons L_i(x)=0");
        Console.WriteLine("N       local normals");
        Console.WriteLine("H       geometric-error heatmap");
        Console.WriteLine("W       surface wireframe");
        Console.WriteLine("Space   surface on/off (useful for inspecting FV planes alone)");
        Console.WriteLine("F       reset camera; left mouse rotate; right mouse pan; wheel zoom; Esc exit");
        Console.WriteLine();
    }

    private static int GetInt(string[] args, string key, int fallback)
    {
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
    }

    private static double GetDouble(string[] args, string key, double fallback)
    {
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length &&
               double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? v : fallback;
    }
}
