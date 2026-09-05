using System.Globalization;

namespace DCad.MeshKernel;

public readonly record struct MeshValidationReport(
    int Triangles,
    int DegenerateTriangles,
    int BoundaryEdges,
    int NonManifoldEdges,
    double AbsoluteVolume)
{
    public bool IsClosedManifold => DegenerateTriangles == 0 && BoundaryEdges == 0 && NonManifoldEdges == 0;
}

public static class MeshValidation
{
    private readonly record struct QuantizedVertex(long X, long Y, long Z);
    private readonly record struct EdgeKey(QuantizedVertex A, QuantizedVertex B);

    public static MeshValidationReport Validate(CsgSolid solid, double tolerance = 1e-7)
    {
        if (solid is null) throw new ArgumentNullException(nameof(solid));
        tolerance = Math.Max(tolerance, 1e-12);
        var edges = new Dictionary<EdgeKey, int>();
        int triangles = 0, degenerate = 0;
        double signedVolume = 0;

        foreach (var (a, b, c) in solid.Triangles())
        {
            triangles++;
            var cross = (b - a).Cross(c - a);
            if (cross.Length <= tolerance * tolerance) degenerate++;
            signedVolume += a.Dot(b.Cross(c)) / 6.0;
            AddEdge(edges, a, b, tolerance);
            AddEdge(edges, b, c, tolerance);
            AddEdge(edges, c, a, tolerance);
        }

        int boundary = 0, nonManifold = 0;
        foreach (var count in edges.Values)
        {
            if (count == 1) boundary++;
            else if (count != 2) nonManifold++;
        }
        return new MeshValidationReport(triangles, degenerate, boundary, nonManifold, Math.Abs(signedVolume));
    }

    private static void AddEdge(Dictionary<EdgeKey, int> edges, Vec3d a, Vec3d b, double tolerance)
    {
        var qa = Quantize(a, tolerance);
        var qb = Quantize(b, tolerance);
        var key = Compare(qa, qb) <= 0 ? new EdgeKey(qa, qb) : new EdgeKey(qb, qa);
        edges.TryGetValue(key, out int count);
        edges[key] = count + 1;
    }

    private static QuantizedVertex Quantize(Vec3d p, double tolerance) => new(
        (long)Math.Round(p.X / tolerance),
        (long)Math.Round(p.Y / tolerance),
        (long)Math.Round(p.Z / tolerance));

    private static int Compare(QuantizedVertex a, QuantizedVertex b)
    {
        int c = a.X.CompareTo(b.X); if (c != 0) return c;
        c = a.Y.CompareTo(b.Y); if (c != 0) return c;
        return a.Z.CompareTo(b.Z);
    }
}

public static class MeshTransforms
{
    public static CsgSolid Translate(CsgSolid solid, Vec3d delta) => Map(solid, p => p + delta);

    public static CsgSolid Scale(CsgSolid solid, Vec3d scale, Vec3d origin = default) =>
        Map(solid, p => new Vec3d(
            origin.X + (p.X - origin.X) * scale.X,
            origin.Y + (p.Y - origin.Y) * scale.Y,
            origin.Z + (p.Z - origin.Z) * scale.Z));

    public static CsgSolid RotateX(CsgSolid solid, double radians, Vec3d origin = default)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return Map(solid, p =>
        {
            var q = p - origin;
            return origin + new Vec3d(q.X, q.Y * c - q.Z * s, q.Y * s + q.Z * c);
        });
    }

    public static CsgSolid RotateY(CsgSolid solid, double radians, Vec3d origin = default)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return Map(solid, p =>
        {
            var q = p - origin;
            return origin + new Vec3d(q.X * c + q.Z * s, q.Y, -q.X * s + q.Z * c);
        });
    }

    public static CsgSolid RotateZ(CsgSolid solid, double radians, Vec3d origin = default)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return Map(solid, p =>
        {
            var q = p - origin;
            return origin + new Vec3d(q.X * c - q.Y * s, q.X * s + q.Y * c, q.Z);
        });
    }

    public static CsgSolid Map(CsgSolid solid, Func<Vec3d, Vec3d> transform)
    {
        if (solid is null) throw new ArgumentNullException(nameof(solid));
        if (transform is null) throw new ArgumentNullException(nameof(transform));
        return new CsgSolid(solid.Polygons.Select(poly =>
            new CsgPolygon(poly.Vertices.Select(v => new CsgVertex(transform(v.Position))))));
    }
}

public static class PrimitiveFactory
{
    public static CsgSolid Sphere(Vec3d center, double radius, int slices = 48, int stacks = 24)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        slices = Math.Max(8, slices); stacks = Math.Max(4, stacks);
        var polygons = new List<CsgPolygon>();

        Vec3d Point(int stack, int slice)
        {
            double v = (double)stack / stacks;
            double phi = Math.PI * (v - 0.5);
            double cp = Math.Cos(phi), sp = Math.Sin(phi);
            double theta = 2.0 * Math.PI * slice / slices;
            return center + new Vec3d(Math.Cos(theta) * cp, Math.Sin(theta) * cp, sp) * radius;
        }

        for (int y = 0; y < stacks; y++)
        {
            for (int x = 0; x < slices; x++)
            {
                int xn = (x + 1) % slices;
                var a = Point(y, x); var b = Point(y, xn);
                var c = Point(y + 1, xn); var d = Point(y + 1, x);
                if (y == 0)
                    polygons.Add(new CsgPolygon(new[] { new CsgVertex(a), new CsgVertex(c), new CsgVertex(d) }));
                else if (y == stacks - 1)
                    polygons.Add(new CsgPolygon(new[] { new CsgVertex(a), new CsgVertex(b), new CsgVertex(c) }));
                else
                    polygons.Add(new CsgPolygon(new[] { new CsgVertex(a), new CsgVertex(b), new CsgVertex(c), new CsgVertex(d) }));
            }
        }
        return new CsgSolid(polygons);
    }
}

public static class MeshPicking
{
    public static bool TryIntersectRay(CsgSolid solid, Vec3d origin, Vec3d direction, out double distance, out Vec3d hit)
    {
        direction = direction.Normalized;
        distance = double.PositiveInfinity;
        hit = default;
        bool found = false;
        foreach (var (a, b, c) in solid.Triangles())
        {
            if (!RayTriangle(origin, direction, a, b, c, out double t)) continue;
            if (t < distance)
            {
                distance = t;
                hit = origin + direction * t;
                found = true;
            }
        }
        return found;
    }

    private static bool RayTriangle(Vec3d origin, Vec3d direction, Vec3d a, Vec3d b, Vec3d c, out double t)
    {
        const double eps = 1e-10;
        var e1 = b - a; var e2 = c - a;
        var p = direction.Cross(e2);
        double det = e1.Dot(p);
        if (Math.Abs(det) < eps) { t = 0; return false; }
        double invDet = 1.0 / det;
        var s = origin - a;
        double u = s.Dot(p) * invDet;
        if (u < -eps || u > 1.0 + eps) { t = 0; return false; }
        var q = s.Cross(e1);
        double v = direction.Dot(q) * invDet;
        if (v < -eps || u + v > 1.0 + eps) { t = 0; return false; }
        t = e2.Dot(q) * invDet;
        return t >= 0;
    }
}

public static class MeshExport
{
    public static void WriteBinaryStl(string path, CsgSolid solid, string header = "DCad MeshKernel")
    {
        var triangles = solid.Triangles().ToList();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var bytes = new byte[80];
        var label = System.Text.Encoding.ASCII.GetBytes(header);
        Array.Copy(label, bytes, Math.Min(label.Length, bytes.Length));
        writer.Write(bytes);
        writer.Write((uint)triangles.Count);
        foreach (var (a, b, c) in triangles)
        {
            var n = (b - a).Cross(c - a).Normalized;
            Write(writer, n); Write(writer, a); Write(writer, b); Write(writer, c);
            writer.Write((ushort)0);
        }
    }

    public static void WriteObj(string path, CsgSolid solid, string objectName = "dcad")
    {
        using var writer = new StreamWriter(path);
        var ci = CultureInfo.InvariantCulture;
        writer.WriteLine("o " + objectName);
        int index = 1;
        foreach (var (a, b, c) in solid.Triangles())
        {
            WriteVertex(writer, a, ci); WriteVertex(writer, b, ci); WriteVertex(writer, c, ci);
            writer.WriteLine($"f {index} {index + 1} {index + 2}");
            index += 3;
        }
    }

    private static void Write(BinaryWriter writer, Vec3d v)
    {
        writer.Write((float)v.X); writer.Write((float)v.Y); writer.Write((float)v.Z);
    }

    private static void WriteVertex(StreamWriter writer, Vec3d v, CultureInfo ci) =>
        writer.WriteLine($"v {v.X.ToString("G17", ci)} {v.Y.ToString("G17", ci)} {v.Z.ToString("G17", ci)}");
}
