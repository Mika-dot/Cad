using DCad.Core;

namespace DCad.FunctionalVoxel;

/// <summary>
/// Extracts the zero iso-surface of a three-argument functional-voxel field.
/// This is a geometric OUTPUT adapter for subsequent CAD use, not a renderer.
/// Tetrahedra use a face-consistent Freudenthal decomposition and global edge
/// vertices are shared, rather than making isolated triangles per voxel.
/// </summary>
public static class FunctionalIsoSurface
{
    // Hexahedron indices: 0=000, 1=100, 2=110, 3=010,
    //                     4=001, 5=101, 6=111, 7=011.
    private static readonly int[,] Tetrahedra = new int[,]
    {
        { 0, 1, 2, 6 },
        { 0, 2, 3, 6 },
        { 0, 3, 7, 6 },
        { 0, 7, 4, 6 },
        { 0, 4, 5, 6 },
        { 0, 5, 1, 6 }
    };

    /// <returns>A mesh with shared vertices, or null if there are no crossings.</returns>
    public static Mesh3d? Extract(FunctionalVoxelModel model, double isoValue = 0)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Dimension != 3) throw new ArgumentException("Iso-surface extraction requires three spatial arguments.");
        if (!double.IsFinite(isoValue)) throw new ArgumentOutOfRangeException(nameof(isoValue));
        var grid = model.Grid;
        var nx = grid.Count(0);
        var ny = grid.Count(1);
        var nz = grid.Count(2);
        if (nx < 2 || ny < 2 || nz < 2) return null;

        var samples = new double[grid.CellCount];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = model.ValueAtCentre(i);
            if (samples[i] == isoValue)
                throw new InvalidOperationException(
                    "An iso-surface passes exactly through a sample centre; shift or refine the lattice to avoid degenerate triangles.");
        }

        var vertices = new List<Vector3d>();
        var triangles = new List<TriangleIndex>();
        var edgeCache = new Dictionary<(int, int), int>();

        Vector3d Point(int sample)
        {
            var p = grid.Centre(sample);
            return new Vector3d(p[0], p[1], p[2]);
        }

        int Interpolate(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (edgeCache.TryGetValue(key, out var cached)) return cached;
            var va = samples[a];
            var vb = samples[b];
            var t = (isoValue - va) / (vb - va);
            if (!double.IsFinite(t) || t < 0 || t > 1)
                throw new ArithmeticException("Invalid edge crossing.");
            var point = Point(a) * (1 - t) + Point(b) * t;
            var index = vertices.Count;
            vertices.Add(point);
            edgeCache.Add(key, index);
            return index;
        }

        void AddOriented(int i0, int i1, int i2, Vector3d outwards)
        {
            var p0 = vertices[i0];
            var p1 = vertices[i1];
            var p2 = vertices[i2];
            var cross = Vector3d.Cross(p1 - p0, p2 - p0);
            // Inside is positive, hence the outward orientation points towards negative samples.
            if (Vector3d.Dot(cross, outwards) < 0)
                (i1, i2) = (i2, i1);
            if (cross.LengthSquared == 0)
                throw new InvalidOperationException("Degenerate iso triangle.");
            triangles.Add(new TriangleIndex(i0, i1, i2));
        }

        for (var i = 0; i < nx - 1; i++)
        for (var j = 0; j < ny - 1; j++)
        for (var k = 0; k < nz - 1; k++)
        {
            var node = new[]
            {
                grid.LinearIndex(i,     j,     k),
                grid.LinearIndex(i + 1, j,     k),
                grid.LinearIndex(i + 1, j + 1, k),
                grid.LinearIndex(i,     j + 1, k),
                grid.LinearIndex(i,     j,     k + 1),
                grid.LinearIndex(i + 1, j,     k + 1),
                grid.LinearIndex(i + 1, j + 1, k + 1),
                grid.LinearIndex(i,     j + 1, k + 1)
            };

            for (var t = 0; t < Tetrahedra.GetLength(0); t++)
            {
                var inside = new List<int>(4);
                var outside = new List<int>(4);
                for (var q = 0; q < 4; q++)
                {
                    var vertex = node[Tetrahedra[t, q]];
                    if (samples[vertex] > isoValue) inside.Add(vertex);
                    else outside.Add(vertex);
                }
                if (inside.Count == 0 || outside.Count == 0) continue;

                var inCentre = Vector3d.Zero;
                var outCentre = Vector3d.Zero;
                foreach (var a in inside) inCentre += Point(a);
                foreach (var b in outside) outCentre += Point(b);
                var outward = outCentre / outside.Count - inCentre / inside.Count;

                if (inside.Count == 1)
                {
                    var a = inside[0];
                    AddOriented(Interpolate(a, outside[0]), Interpolate(a, outside[1]),
                        Interpolate(a, outside[2]), outward);
                }
                else if (outside.Count == 1)
                {
                    var b = outside[0];
                    AddOriented(Interpolate(b, inside[0]), Interpolate(b, inside[1]),
                        Interpolate(b, inside[2]), outward);
                }
                else
                {
                    var a = Interpolate(inside[0], outside[0]);
                    var b = Interpolate(inside[0], outside[1]);
                    var c = Interpolate(inside[1], outside[0]);
                    var d = Interpolate(inside[1], outside[1]);
                    AddOriented(a, b, c, outward);
                    AddOriented(b, d, c, outward);
                }
            }
        }
        return vertices.Count == 0 ? null : new Mesh3d(vertices, triangles);
    }
}
