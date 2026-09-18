namespace AFVMDemo;

public sealed class RenderMesh
{
    // xyz, normal xyz, scalar
    public float[] Vertices { get; }
    public uint[] Indices { get; }
    public RenderMesh(float[] vertices, uint[] indices) { Vertices = vertices; Indices = indices; }
}

public static class SurfaceExtractor
{
    private static readonly int[][] Tetrahedra =
    [
        [0, 5, 1, 6], [0, 1, 2, 6], [0, 2, 3, 6],
        [0, 3, 7, 6], [0, 7, 4, 6], [0, 4, 5, 6]
    ];

    private static readonly (int x, int y, int z)[] CornerOffset =
    [
        (0,0,0),(1,0,0),(1,1,0),(0,1,0),(0,0,1),(1,0,1),(1,1,1),(0,1,1)
    ];

    public static RenderMesh Extract(IField3d field, Box3d domain, int resolution)
    {
        if (resolution < 8) throw new ArgumentOutOfRangeException(nameof(resolution));
        var step = domain.Size / (resolution - 1);
        int total = resolution * resolution * resolution;
        var values = new double[total];
        int Idx(int x, int y, int z) => x + resolution * (y + resolution * z);
        DVec3 Pos(int x, int y, int z) => new(domain.Min.X + x * step.X, domain.Min.Y + y * step.Y, domain.Min.Z + z * step.Z);

        for (int z = 0; z < resolution; z++) for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            values[Idx(x, y, z)] = field.Value(Pos(x, y, z));

        var verts = new List<float>(); var indices = new List<uint>();
        for (int z = 0; z < resolution - 1; z++) for (int y = 0; y < resolution - 1; y++) for (int x = 0; x < resolution - 1; x++)
        {
            var p = new DVec3[8]; var v = new double[8];
            for (int c = 0; c < 8; c++)
            {
                var o = CornerOffset[c]; int xi = x + o.x, yi = y + o.y, zi = z + o.z;
                p[c] = Pos(xi, yi, zi); v[c] = values[Idx(xi, yi, zi)];
            }
            foreach (var t in Tetrahedra) PolygoniseTetra(field, p, v, t, verts, indices);
        }
        return new(verts.ToArray(), indices.ToArray());
    }

    private static void PolygoniseTetra(IField3d field, DVec3[] p, double[] v, int[] t, List<float> verts, List<uint> indices)
    {
        var inside = t.Where(i => v[i] <= 0).ToArray();
        var outside = t.Where(i => v[i] > 0).ToArray();
        if (inside.Length == 0 || inside.Length == 4) return;

        DVec3 Cross(int a, int b)
        {
            double va = v[a], vb = v[b];
            double denom = va - vb;
            double u = Math.Abs(denom) < 1e-15 ? 0.5 : va / denom;
            return DVec3.Lerp(p[a], p[b], Math.Clamp(u, 0.0, 1.0));
        }

        if (inside.Length == 1)
        {
            int i = inside[0]; AddTriangle(field, Cross(i, outside[0]), Cross(i, outside[1]), Cross(i, outside[2]), verts, indices);
        }
        else if (inside.Length == 3)
        {
            int o = outside[0]; AddTriangle(field, Cross(o, inside[0]), Cross(o, inside[2]), Cross(o, inside[1]), verts, indices);
        }
        else
        {
            int i0 = inside[0], i1 = inside[1], o0 = outside[0], o1 = outside[1];
            var p00 = Cross(i0, o0); var p01 = Cross(i0, o1); var p10 = Cross(i1, o0); var p11 = Cross(i1, o1);
            AddTriangle(field, p00, p10, p11, verts, indices);
            AddTriangle(field, p00, p11, p01, verts, indices);
        }
    }

    private static void AddTriangle(IField3d field, DVec3 a, DVec3 b, DVec3 c, List<float> verts, List<uint> indices)
    {
        var face = DVec3.Cross(b - a, c - a).Normalized();
        var mid = (a + b + c) / 3.0;
        var expected = field.Gradient(mid).Normalized();
        if (DVec3.Dot(face, expected) < 0) (b, c) = (c, b);
        uint start = (uint)(verts.Count / 7);
        AddVertex(a); AddVertex(b); AddVertex(c);
        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);

        void AddVertex(DVec3 q)
        {
            var n = field.Gradient(q).Normalized();
            if (n.LengthSquared < 1e-20) n = face;
            verts.Add((float)q.X); verts.Add((float)q.Y); verts.Add((float)q.Z);
            verts.Add((float)n.X); verts.Add((float)n.Y); verts.Add((float)n.Z);
            verts.Add((float)Math.Clamp(0.5 + 0.5 * n.Z, 0, 1));
        }
    }
}

public static class GridLineBuilder
{
    public static float[] BuildSurfaceLeafBoxes(AdaptiveFunctionalTree tree)
    {
        var data = new List<float>();
        foreach (var leaf in tree.Leaves().Where(l => l.SurfaceCandidate))
        {
            var b = leaf.Bounds; float s = tree.MaxObservedLevel == 0 ? 0 : leaf.Level / (float)tree.MaxObservedLevel;
            var c = new DVec3[8];
            for (int i = 0; i < 8; i++) c[i] = new((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
            int[,] edges = { {0,1},{1,3},{3,2},{2,0},{4,5},{5,7},{7,6},{6,4},{0,4},{1,5},{2,6},{3,7} };
            for (int e = 0; e < 12; e++) { Add(c[edges[e,0]], s); Add(c[edges[e,1]], s); }
        }
        return data.ToArray();

        void Add(DVec3 p, float scalar) { data.Add((float)p.X); data.Add((float)p.Y); data.Add((float)p.Z); data.Add(scalar); }
    }
}
