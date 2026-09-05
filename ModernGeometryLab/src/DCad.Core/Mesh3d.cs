namespace DCad.Core;

public sealed class Mesh3d
{
    public IReadOnlyList<Vector3d> Vertices { get; }
    public IReadOnlyList<TriangleIndex> Triangles { get; }
    public Aabb3d Bounds { get; }

    public Mesh3d(IEnumerable<Vector3d> vertices, IEnumerable<TriangleIndex> triangles)
    {
        var v = vertices.ToArray();
        var t = triangles.ToArray();
        if (v.Length == 0) throw new ArgumentException("Mesh must contain vertices.", nameof(vertices));
        foreach (var tri in t)
        {
            if ((uint)tri.A >= v.Length || (uint)tri.B >= v.Length || (uint)tri.C >= v.Length)
                throw new ArgumentOutOfRangeException(nameof(triangles), "Triangle index is outside the vertex array.");
        }
        Vertices = v;
        Triangles = t;
        Bounds = Aabb3d.FromPoints(v);
    }

    public Triangle3d GetTriangle(int index)
    {
        var t = Triangles[index];
        return new(Vertices[t.A], Vertices[t.B], Vertices[t.C]);
    }

    public double SignedVolume()
    {
        double sixVolume = 0;
        foreach (var tri in Triangles)
        {
            var a = Vertices[tri.A];
            var b = Vertices[tri.B];
            var c = Vertices[tri.C];
            sixVolume += Vector3d.Dot(a, Vector3d.Cross(b, c));
        }
        return sixVolume / 6.0;
    }

    public double SurfaceArea()
    {
        double area = 0;
        for (var i = 0; i < Triangles.Count; i++) area += GetTriangle(i).Area;
        return area;
    }
}

public sealed record MeshValidationReport(
    int DegenerateTriangles,
    int BoundaryEdges,
    int NonManifoldEdges,
    int InconsistentWindingEdges,
    double SignedVolume)
{
    public bool IsClosedOrientedManifold => DegenerateTriangles == 0 && BoundaryEdges == 0 && NonManifoldEdges == 0 && InconsistentWindingEdges == 0;
}

public static class MeshValidator
{
    private readonly record struct EdgeKey(int A, int B)
    {
        public static EdgeKey Create(int a, int b) => a < b ? new(a, b) : new(b, a);
    }

    private sealed class EdgeUse
    {
        public int Count;
        public int OrientationSum;
    }

    public static MeshValidationReport Validate(Mesh3d mesh, GeometryTolerance? tolerance = null)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        var scale = Math.Max(1.0, mesh.Bounds.Diagonal);
        var degenerate = 0;
        var edges = new Dictionary<EdgeKey, EdgeUse>();

        void AddEdge(int a, int b)
        {
            var key = EdgeKey.Create(a, b);
            if (!edges.TryGetValue(key, out var use)) edges[key] = use = new EdgeUse();
            use.Count++;
            use.OrientationSum += a < b ? 1 : -1;
        }

        foreach (var ti in mesh.Triangles)
        {
            var tri = new Triangle3d(mesh.Vertices[ti.A], mesh.Vertices[ti.B], mesh.Vertices[ti.C]);
            if (tri.IsDegenerate(tol, scale)) degenerate++;
            AddEdge(ti.A, ti.B);
            AddEdge(ti.B, ti.C);
            AddEdge(ti.C, ti.A);
        }

        var boundary = 0;
        var nonManifold = 0;
        var winding = 0;
        foreach (var use in edges.Values)
        {
            if (use.Count == 1) boundary++;
            else if (use.Count != 2) nonManifold++;
            else if (use.OrientationSum != 0) winding++;
        }

        return new(degenerate, boundary, nonManifold, winding, mesh.SignedVolume());
    }
}
