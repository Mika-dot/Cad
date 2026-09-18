namespace AFVMDemo;

/// <summary>
/// Local first-order function L(x)=f(c)+grad(f,c)·(x-c).
/// Plane characteristics are the scale-invariant coefficients of L(x)=0:
/// n = grad/|grad| and d=(f(c)-grad·c)/|grad|.
/// </summary>
public readonly record struct LocalLinearFunction(DVec3 Center, double CenterValue, DVec3 Gradient)
{
    public double Value(DVec3 p) => CenterValue + DVec3.Dot(Gradient, p - Center);
    public double GradientNorm => Gradient.Length;
    public DVec3 UnitNormal => Gradient.Normalized();
    public double PlaneOffset => GradientNorm > 1e-14 ? (CenterValue - DVec3.Dot(Gradient, Center)) / GradientNorm : 0.0;

    public LocalLinearFunction Prolong(DVec3 childCenter) => new(childCenter, Value(childCenter), Gradient);

    public static LocalLinearFunction Restrict(IReadOnlyList<LocalLinearFunction> children, DVec3 parentCenter)
    {
        if (children.Count == 0) throw new ArgumentException("At least one child is required.", nameof(children));
        DVec3 g = DVec3.Zero;
        double v = 0;
        foreach (var child in children)
        {
            g += child.Gradient;
            v += child.Value(parentCenter);
        }
        return new(parentCenter, v / children.Count, g / children.Count);
    }
}

public sealed class FunctionalVoxelNode
{
    public Box3d Bounds { get; }
    public int Level { get; }
    public LocalLinearFunction Local { get; }
    public double EstimatedGeometricError { get; }
    public bool SurfaceCandidate { get; }
    public FunctionalVoxelNode[]? Children { get; set; }
    public bool IsLeaf => Children is null;

    public FunctionalVoxelNode(Box3d bounds, int level, LocalLinearFunction local, double error, bool surfaceCandidate)
    {
        Bounds = bounds; Level = level; Local = local; EstimatedGeometricError = error; SurfaceCandidate = surfaceCandidate;
    }
}

public sealed record AdaptiveBuildOptions(
    int MaxDepth = 6,
    int MinDepth = 1,
    int MinSurfaceDepth = 3,
    double GeometricTolerance = 0.025,
    double SurfaceBandFactor = 1.10);

public sealed class AdaptiveFunctionalTree : IField3d
{
    public string Name { get; }
    public Box3d Domain { get; }
    public FunctionalVoxelNode Root { get; }
    public int LeafCount { get; private set; }
    public int SurfaceLeafCount { get; private set; }
    public int MaxObservedLevel { get; private set; }

    private readonly IField3d _source;
    private readonly AdaptiveBuildOptions _options;

    private AdaptiveFunctionalTree(IField3d source, Box3d domain, AdaptiveBuildOptions options)
    {
        _source = source; Domain = domain; _options = options; Name = $"AdaptiveFV[{source.Name}]";
        Root = BuildNode(domain, 0);
        CountLeaves(Root);
    }

    public static AdaptiveFunctionalTree Build(IField3d source, Box3d domain, AdaptiveBuildOptions? options = null) =>
        new(source, domain, options ?? new AdaptiveBuildOptions());

    private FunctionalVoxelNode BuildNode(Box3d box, int level)
    {
        var c = box.Center;
        var local = new LocalLinearFunction(c, _source.Value(c), _source.Gradient(c));
        var (error, candidate) = Estimate(box, local);
        var node = new FunctionalVoxelNode(box, level, local, error, candidate);

        bool force = level < _options.MinDepth;
        bool refineSurface = candidate && level < _options.MaxDepth &&
            (level < _options.MinSurfaceDepth || error > _options.GeometricTolerance);
        if (force || refineSurface)
        {
            node.Children = new FunctionalVoxelNode[8];
            for (int i = 0; i < 8; i++) node.Children[i] = BuildNode(box.Child(i), level + 1);
        }
        return node;
    }

    private (double error, bool candidate) Estimate(Box3d box, LocalLinearFunction local)
    {
        bool neg = false, pos = false;
        double maxGeom = 0, minDistance = double.PositiveInfinity;
        foreach (var p in box.ProbePoints())
        {
            double exact = _source.Value(p);
            if (exact <= 0) neg = true; else pos = true;
            var g = _source.Gradient(p);
            double gn = Math.Max(g.Length, 1e-9);
            maxGeom = Math.Max(maxGeom, Math.Abs(exact - local.Value(p)) / gn);
            minDistance = Math.Min(minDistance, Math.Abs(exact) / gn);
        }
        bool signChange = neg && pos;
        bool nearSurface = minDistance <= box.Diagonal * 0.5 * _options.SurfaceBandFactor;
        return (maxGeom, signChange || nearSurface);
    }

    private void CountLeaves(FunctionalVoxelNode node)
    {
        MaxObservedLevel = Math.Max(MaxObservedLevel, node.Level);
        if (node.IsLeaf)
        {
            LeafCount++;
            if (node.SurfaceCandidate) SurfaceLeafCount++;
            return;
        }
        foreach (var child in node.Children!) CountLeaves(child);
    }

    public IEnumerable<FunctionalVoxelNode> Leaves()
    {
        var stack = new Stack<FunctionalVoxelNode>(); stack.Push(Root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.IsLeaf) yield return n;
            else for (int i = 7; i >= 0; i--) stack.Push(n.Children![i]);
        }
    }

    public FunctionalVoxelNode FindLeaf(DVec3 p)
    {
        if (!Domain.Contains(p)) throw new ArgumentOutOfRangeException(nameof(p), "Point is outside the FV domain.");
        var n = Root;
        while (!n.IsLeaf)
        {
            var c = n.Bounds.Center;
            int index = (p.X >= c.X ? 1 : 0) | (p.Y >= c.Y ? 2 : 0) | (p.Z >= c.Z ? 4 : 0);
            n = n.Children![index];
        }
        return n;
    }

    public double Value(DVec3 p) => FindLeaf(p).Local.Value(p);
    public DVec3 Gradient(DVec3 p) => FindLeaf(p).Local.Gradient;
}

public sealed class UniformFunctionalGrid : IField3d
{
    public string Name { get; }
    public Box3d Domain { get; }
    public int Resolution { get; }
    public int CellCount => _cells.Length;
    private readonly LocalLinearFunction[] _cells;
    private readonly DVec3 _step;

    public UniformFunctionalGrid(IField3d source, Box3d domain, int depth)
    {
        if (depth < 1 || depth > 8) throw new ArgumentOutOfRangeException(nameof(depth));
        Name = $"UniformFV[{source.Name}]"; Domain = domain; Resolution = 1 << depth;
        _step = domain.Size / Resolution;
        _cells = new LocalLinearFunction[Resolution * Resolution * Resolution];
        for (int z = 0; z < Resolution; z++)
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    var c = new DVec3(domain.Min.X + (x + 0.5) * _step.X, domain.Min.Y + (y + 0.5) * _step.Y, domain.Min.Z + (z + 0.5) * _step.Z);
                    _cells[Index(x, y, z)] = new(c, source.Value(c), source.Gradient(c));
                }
    }

    private int Index(int x, int y, int z) => x + Resolution * (y + Resolution * z);
    private LocalLinearFunction Cell(DVec3 p)
    {
        int x = Math.Clamp((int)((p.X - Domain.Min.X) / _step.X), 0, Resolution - 1);
        int y = Math.Clamp((int)((p.Y - Domain.Min.Y) / _step.Y), 0, Resolution - 1);
        int z = Math.Clamp((int)((p.Z - Domain.Min.Z) / _step.Z), 0, Resolution - 1);
        return _cells[Index(x, y, z)];
    }
    public double Value(DVec3 p) => Cell(p).Value(p);
    public DVec3 Gradient(DVec3 p) => Cell(p).Gradient;
}
