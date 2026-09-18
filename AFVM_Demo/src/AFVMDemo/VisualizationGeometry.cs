namespace AFVMDemo;

public sealed class VisualizationGeometry
{
    public RenderMesh PlaneMesh { get; }
    public float[] GridLines { get; }
    public float[] NormalLines { get; }
    public int PlanePolygonCount { get; }
    public int CellCount { get; }

    public VisualizationGeometry(RenderMesh planeMesh, float[] gridLines, float[] normalLines, int planePolygonCount, int cellCount)
    {
        PlaneMesh = planeMesh;
        GridLines = gridLines;
        NormalLines = normalLines;
        PlanePolygonCount = planePolygonCount;
        CellCount = cellCount;
    }
}

public static class FunctionalVisualizationBuilder
{
    private static readonly (int A, int B)[] CubeEdges =
    [
        (0,1),(1,3),(3,2),(2,0),
        (4,5),(5,7),(7,6),(6,4),
        (0,4),(1,5),(2,6),(3,7)
    ];

    public static VisualizationGeometry BuildAdaptive(AdaptiveFunctionalTree tree, IField3d exact, double tolerance)
    {
        var cells = tree.Leaves().Where(x => x.SurfaceCandidate)
            .Select(x => new VisualCell(x.Bounds, x.Local, x.Level, x.EstimatedGeometricError))
            .ToArray();
        return Build(cells, tolerance, Math.Max(1, tree.MaxObservedLevel));
    }

    public static VisualizationGeometry BuildUniform(UniformFunctionalGrid grid, IField3d exact, double tolerance)
    {
        var cells = new List<VisualCell>();
        var step = grid.Domain.Size / grid.Resolution;
        for (int z = 0; z < grid.Resolution; z++)
            for (int y = 0; y < grid.Resolution; y++)
                for (int x = 0; x < grid.Resolution; x++)
                {
                    var min = new DVec3(grid.Domain.Min.X + x * step.X, grid.Domain.Min.Y + y * step.Y, grid.Domain.Min.Z + z * step.Z);
                    var max = min + step;
                    var bounds = new Box3d(min, max);
                    var center = bounds.Center;
                    var local = new LocalLinearFunction(center, grid.Value(center), grid.Gradient(center));
                    if (!TryPlanePolygon(bounds, local, out _)) continue;
                    double error = EstimateLocalError(bounds, local, exact);
                    cells.Add(new(bounds, local, 1, error));
                }
        return Build(cells, tolerance, 1);
    }

    private readonly record struct VisualCell(Box3d Bounds, LocalLinearFunction Local, int Level, double Error);

    private static VisualizationGeometry Build(IReadOnlyCollection<VisualCell> cells, double tolerance, int maxLevel)
    {
        var grid = new List<float>(cells.Count * 12 * 2 * 4);
        var normals = new List<float>(cells.Count * 6 * 4);
        var planeVertices = new List<float>(cells.Count * 6 * 7);
        var planeIndices = new List<uint>(cells.Count * 12);
        int polygonCount = 0;

        foreach (var cell in cells)
        {
            float levelScalar = maxLevel <= 1 ? 0.75f : cell.Level / (float)maxLevel;
            AddBoxLines(cell.Bounds, levelScalar, grid);

            if (!TryPlanePolygon(cell.Bounds, cell.Local, out var polygon)) continue;
            polygonCount++;
            var n = cell.Local.UnitNormal;
            if (n.LengthSquared < 1e-20) continue;
            var centroid = Average(polygon);
            float errorScalar = (float)Math.Clamp(cell.Error / Math.Max(tolerance, 1e-12), 0.0, 1.0);

            uint start = (uint)(planeVertices.Count / 7);
            foreach (var p in polygon) AddPlaneVertex(p, n, errorScalar, planeVertices);
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                planeIndices.Add(start);
                planeIndices.Add(start + (uint)i);
                planeIndices.Add(start + (uint)i + 1);
            }

            double arrowLength = Math.Max(0.02, Math.Min(cell.Bounds.Size.X, Math.Min(cell.Bounds.Size.Y, cell.Bounds.Size.Z)) * 0.55);
            var tip = centroid + n * arrowLength;
            AddLineVertex(centroid, errorScalar, normals);
            AddLineVertex(tip, errorScalar, normals);

            var axis = Math.Abs(n.Z) < 0.85 ? DVec3.UnitZ : DVec3.UnitY;
            var side = DVec3.Cross(n, axis).Normalized() * (arrowLength * 0.18);
            var back = tip - n * (arrowLength * 0.22);
            AddLineVertex(tip, errorScalar, normals); AddLineVertex(back + side, errorScalar, normals);
            AddLineVertex(tip, errorScalar, normals); AddLineVertex(back - side, errorScalar, normals);
        }

        return new(new RenderMesh(planeVertices.ToArray(), planeIndices.ToArray()), grid.ToArray(), normals.ToArray(), polygonCount, cells.Count);
    }

    public static bool TryPlanePolygon(Box3d box, LocalLinearFunction local, out List<DVec3> polygon)
    {
        polygon = [];
        if (local.GradientNorm < 1e-14) return false;
        var c = BoxCorners(box);
        double eps = Math.Max(1e-12, box.Diagonal * 1e-10);

        foreach (var (a, b) in CubeEdges)
        {
            double va = local.Value(c[a]), vb = local.Value(c[b]);
            if (Math.Abs(va) <= eps) AddUnique(polygon, c[a], eps * 20);
            if (Math.Abs(vb) <= eps) AddUnique(polygon, c[b], eps * 20);
            if ((va < -eps && vb > eps) || (va > eps && vb < -eps))
            {
                double t = va / (va - vb);
                AddUnique(polygon, DVec3.Lerp(c[a], c[b], Math.Clamp(t, 0.0, 1.0)), eps * 20);
            }
        }

        if (polygon.Count < 3) { polygon.Clear(); return false; }
        var center = Average(polygon);
        var n = local.UnitNormal;
        var helper = Math.Abs(n.Z) < 0.85 ? DVec3.UnitZ : DVec3.UnitY;
        var u = DVec3.Cross(helper, n).Normalized();
        var v = DVec3.Cross(n, u).Normalized();
        polygon.Sort((a, b) =>
        {
            double aa = Math.Atan2(DVec3.Dot(a - center, v), DVec3.Dot(a - center, u));
            double bb = Math.Atan2(DVec3.Dot(b - center, v), DVec3.Dot(b - center, u));
            return aa.CompareTo(bb);
        });

        if (polygon.Count >= 3)
        {
            var face = DVec3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]);
            if (DVec3.Dot(face, n) < 0) polygon.Reverse();
        }
        return true;
    }

    private static double EstimateLocalError(Box3d box, LocalLinearFunction local, IField3d exact)
    {
        double max = 0;
        foreach (var p in box.ProbePoints())
        {
            var g = exact.Gradient(p);
            double gn = Math.Max(g.Length, 1e-9);
            max = Math.Max(max, Math.Abs(exact.Value(p) - local.Value(p)) / gn);
        }
        return max;
    }

    private static DVec3[] BoxCorners(Box3d b)
    {
        var c = new DVec3[8];
        for (int i = 0; i < 8; i++)
            c[i] = new((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
        return c;
    }

    private static void AddBoxLines(Box3d b, float scalar, List<float> data)
    {
        var c = BoxCorners(b);
        foreach (var (a, d) in CubeEdges)
        {
            AddLineVertex(c[a], scalar, data);
            AddLineVertex(c[d], scalar, data);
        }
    }

    private static void AddUnique(List<DVec3> points, DVec3 p, double eps)
    {
        double e2 = eps * eps;
        if (points.Any(q => (q - p).LengthSquared <= e2)) return;
        points.Add(p);
    }

    private static DVec3 Average(IReadOnlyList<DVec3> p)
    {
        DVec3 s = DVec3.Zero;
        for (int i = 0; i < p.Count; i++) s += p[i];
        return s / p.Count;
    }

    private static void AddPlaneVertex(DVec3 p, DVec3 n, float scalar, List<float> data)
    {
        data.Add((float)p.X); data.Add((float)p.Y); data.Add((float)p.Z);
        data.Add((float)n.X); data.Add((float)n.Y); data.Add((float)n.Z);
        data.Add(scalar);
    }

    private static void AddLineVertex(DVec3 p, float scalar, List<float> data)
    {
        data.Add((float)p.X); data.Add((float)p.Y); data.Add((float)p.Z); data.Add(scalar);
    }
}
