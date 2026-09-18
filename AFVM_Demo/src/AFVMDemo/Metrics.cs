namespace AFVMDemo;

public readonly record struct ErrorMetrics(int Samples, double RmsDistance, double MaxDistance, double MeanNormalAngleDeg, double MaxNormalAngleDeg)
{
    public override string ToString() => $"samples={Samples}, rms={RmsDistance:F5}, max={MaxDistance:F5}, meanNormal={MeanNormalAngleDeg:F3}°, maxNormal={MaxNormalAngleDeg:F3}°";
}

public static class Metrics
{
    public static ErrorMetrics Compare(IField3d approximation, IField3d exact, Box3d domain, int n = 34, double surfaceBand = 0.12)
    {
        double sum2 = 0, max = 0, sumAngle = 0, maxAngle = 0; int count = 0;
        var step = domain.Size / (n - 1);
        for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            var p = new DVec3(domain.Min.X + x * step.X, domain.Min.Y + y * step.Y, domain.Min.Z + z * step.Z);
            double ev = exact.Value(p);
            var eg = exact.Gradient(p); double gn = eg.Length;
            if (gn < 1e-9 || Math.Abs(ev) / gn > surfaceBand) continue;
            double d = Math.Abs(approximation.Value(p) - ev) / gn;
            sum2 += d * d; max = Math.Max(max, d);
            var ag = approximation.Gradient(p).Normalized(); var en = eg.Normalized();
            double dot = Math.Clamp(DVec3.Dot(ag, en), -1.0, 1.0);
            double angle = Math.Acos(dot) * 180.0 / Math.PI;
            sumAngle += angle; maxAngle = Math.Max(maxAngle, angle); count++;
        }
        return count == 0 ? default : new(count, Math.Sqrt(sum2 / count), max, sumAngle / count, maxAngle);
    }

    public static void RunSelfTests()
    {
        var affine = new AffineField(1.25, new DVec3(2, -3, 0.5));
        var parentCenter = DVec3.Zero;
        var parent = new LocalLinearFunction(parentCenter, affine.Value(parentCenter), affine.Gradient(parentCenter));
        var parentBox = new Box3d(new(-1, -1, -1), new(1, 1, 1));
        var children = Enumerable.Range(0, 8).Select(i => parent.Prolong(parentBox.Child(i).Center)).ToArray();
        var restricted = LocalLinearFunction.Restrict(children, parentCenter);
        var probes = parentBox.ProbePoints().ToArray();
        double worst = probes.Max(p => Math.Abs(restricted.Value(p) - affine.Value(p)));
        if (worst > 1e-12 || (restricted.Gradient - affine.Gradient(DVec3.Zero)).Length > 1e-12)
            throw new InvalidOperationException($"Prolong/restrict affine invariant failed: {worst:E3}");

        foreach (var op in Enum.GetValues<ROperation>())
        {
            var inside = RFunctions.Evaluate(-1, -2, op).value;
            if (op != ROperation.Difference && inside >= 0) throw new InvalidOperationException($"R-{op} sign invariant failed.");
        }
        if (RFunctions.Evaluate(-1, 1, ROperation.Difference).value >= 0)
            throw new InvalidOperationException("R-difference inside/outside invariant failed.");
        if (RFunctions.Evaluate(-1, -1, ROperation.Difference).value <= 0)
            throw new InvalidOperationException("R-difference subtraction invariant failed.");

        Console.WriteLine("SELF-TEST PASS: affine prolong/restrict and R-sign invariants.");
    }
}
