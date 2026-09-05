namespace DCad.Core;

public readonly record struct GeometryTolerance(double Absolute = 1e-9, double Relative = 1e-10)
{
    public static GeometryTolerance Default => new();
    public double AtScale(double scale) => Math.Max(Absolute, Math.Abs(scale) * Relative);
    public bool NearlyEqual(double a, double b, double scale = 1.0) => Math.Abs(a - b) <= AtScale(scale);
}

public readonly record struct Vector2d(double X, double Y)
{
    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator *(Vector2d v, double s) => new(v.X * s, v.Y * s);
    public static double Cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;
    public double LengthSquared => X * X + Y * Y;
}

public readonly record struct Vector3d(double X, double Y, double Z)
{
    public static Vector3d Zero => new(0, 0, 0);
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d v) => new(-v.X, -v.Y, -v.Z);
    public static Vector3d operator *(Vector3d v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3d operator /(Vector3d v, double s) => new(v.X / s, v.Y / s, v.Z / s);
    public double LengthSquared => X * X + Y * Y + Z * Z;
    public double Length => Math.Sqrt(LengthSquared);
    public Vector3d Normalized(GeometryTolerance? tolerance = null)
    {
        var eps = (tolerance ?? GeometryTolerance.Default).AtScale(Length);
        var len = Length;
        if (len <= eps) throw new InvalidOperationException("Cannot normalize a near-zero vector.");
        return this / len;
    }
    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3d Cross(Vector3d a, Vector3d b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);
}

public readonly record struct Aabb3d(Vector3d Min, Vector3d Max)
{
    public Vector3d Size => Max - Min;
    public double Diagonal => Size.Length;
    public bool Contains(Vector3d p, double eps = 0) =>
        p.X >= Min.X - eps && p.X <= Max.X + eps &&
        p.Y >= Min.Y - eps && p.Y <= Max.Y + eps &&
        p.Z >= Min.Z - eps && p.Z <= Max.Z + eps;

    public static Aabb3d FromPoints(IEnumerable<Vector3d> points)
    {
        using var e = points.GetEnumerator();
        if (!e.MoveNext()) throw new ArgumentException("At least one point is required.", nameof(points));
        var min = e.Current;
        var max = e.Current;
        while (e.MoveNext())
        {
            var p = e.Current;
            min = new(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }
        return new(min, max);
    }
}

public readonly record struct TriangleIndex(int A, int B, int C)
{
    public IEnumerable<int> Indices { get { yield return A; yield return B; yield return C; } }
}

public readonly record struct Triangle3d(Vector3d A, Vector3d B, Vector3d C)
{
    public Vector3d CrossNormal => Vector3d.Cross(B - A, C - A);
    public double DoubleArea => CrossNormal.Length;
    public double Area => 0.5 * DoubleArea;
    public Vector3d Centroid => (A + B + C) / 3.0;
    public bool IsDegenerate(GeometryTolerance tolerance, double scale = 1.0) => DoubleArea <= tolerance.AtScale(scale * scale);
    public Vector3d UnitNormal(GeometryTolerance? tolerance = null) => CrossNormal.Normalized(tolerance);
}
