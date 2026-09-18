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
            int meshRes = GetInt(args, "--mesh-res", 48);
            var domain = new Box3d(new(-1.6, -1.6, -1.6), new(1.6, 1.6, 1.6));
            var options = new AdaptiveBuildOptions(MaxDepth: maxDepth, MinDepth: 1, MinSurfaceDepth: 3, GeometricTolerance: tol);

            IField3d sphere = new SphereField(DVec3.Zero, 1.15);
            IField3d bore = new InfiniteCylinderZField(DVec3.Zero, 0.42);
            IField3d exact = new RBinaryField(sphere, bore, ROperation.Difference, "Exact R-difference: sphere - cylinder");

            Console.WriteLine("Building primitive adaptive FV trees...");
            var sphereTree = AdaptiveFunctionalTree.Build(sphere, domain, options);
            var boreTree = AdaptiveFunctionalTree.Build(bore, domain, options);

            Console.WriteLine("Building direct adaptive FV reference...");
            var directTree = AdaptiveFunctionalTree.Build(exact, domain, options);

            Console.WriteLine("Building R-algebra only from already discretized FV primitive trees...");
            IField3d localAlgebraSource = new RBinaryField(sphereTree, boreTree, ROperation.Difference, "R(AdaptiveFV sphere, AdaptiveFV cylinder)");
            var algebraTree = AdaptiveFunctionalTree.Build(localAlgebraSource, domain, options);

            Console.WriteLine("Building uniform FV baseline...");
            var uniform = new UniformFunctionalGrid(exact, domain, uniformDepth);

            var uniformErr = Metrics.Compare(uniform, exact, domain);
            var directErr = Metrics.Compare(directTree, exact, domain);
            var algebraErr = Metrics.Compare(algebraTree, exact, domain);

            Console.WriteLine();
            Console.WriteLine("=== REPRESENTATION ===");
            Console.WriteLine($"Uniform FV: {uniform.Resolution}^3 = {uniform.CellCount:N0} cells");
            Console.WriteLine($"Sphere adaptive: leaves={sphereTree.LeafCount:N0}, surface={sphereTree.SurfaceLeafCount:N0}");
            Console.WriteLine($"Cylinder adaptive: leaves={boreTree.LeafCount:N0}, surface={boreTree.SurfaceLeafCount:N0}");
            Console.WriteLine($"Direct adaptive composite: leaves={directTree.LeafCount:N0}, surface={directTree.SurfaceLeafCount:N0}");
            Console.WriteLine($"Algebraic adaptive composite: leaves={algebraTree.LeafCount:N0}, surface={algebraTree.SurfaceLeafCount:N0}");
            Console.WriteLine();
            Console.WriteLine("=== ERROR AGAINST ANALYTIC R-GEOMETRY (length units) ===");
            Console.WriteLine($"Uniform FV : {uniformErr}");
            Console.WriteLine($"Adaptive direct: {directErr}");
            Console.WriteLine($"Adaptive R-algebra: {algebraErr}");
            Console.WriteLine();
            Console.WriteLine("Key hypothesis: the last model was formed from local FV approximations of A and B, then R-composed and re-adapted.");
            Console.WriteLine("If its geometric/local-normal error stays controlled while using substantially fewer cells than the uniform field, the demo supports the research direction.");

            Console.WriteLine("Extracting display meshes...");
            var exactMesh = SurfaceExtractor.Extract(exact, domain, meshRes);
            var uniformMesh = SurfaceExtractor.Extract(uniform, domain, meshRes);
            var directMesh = SurfaceExtractor.Extract(directTree, domain, meshRes);
            var algebraMesh = SurfaceExtractor.Extract(algebraTree, domain, meshRes);
            var grid = GridLineBuilder.BuildSurfaceLeafBoxes(algebraTree);

            var models = new[]
            {
                ("1 Exact analytic R-field", exactMesh, new Vector3(.74f,.78f,.84f)),
                ("2 Uniform functional voxels", uniformMesh, new Vector3(.88f,.64f,.26f)),
                ("3 Adaptive FV — direct", directMesh, new Vector3(.25f,.70f,.92f)),
                ("4 Adaptive FV — R-algebra of discretized operands", algebraMesh, new Vector3(.40f,.84f,.48f))
            };
            using var window = new ViewerWindow(models, grid); window.Run(); return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex); return 1;
        }
    }

    private static int GetInt(string[] args, string key, int fallback)
    {
        int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
    }
    private static double GetDouble(string[] args, string key, double fallback)
    {
        int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;
    }
}
