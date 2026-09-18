namespace AFVMDemo;

public readonly record struct DVec3(double X, double Y, double Z)
{
    public static DVec3 Zero => new(0, 0, 0);
    public static DVec3 UnitX => new(1, 0, 0);
    public static DVec3 UnitY => new(0, 1, 0);
    public static DVec3 UnitZ => new(0, 0, 1);
    public static DVec3 operator +(DVec3 a, DVec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static DVec3 operator -(DVec3 a, DVec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static DVec3 operator -(DVec3 a) => new(-a.X, -a.Y, -a.Z);
    public static DVec3 operator *(DVec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static DVec3 operator *(double s, DVec3 a) => a * s;
    public static DVec3 operator /(DVec3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public double LengthSquared => X * X + Y * Y + Z * Z;
    public double Length => Math.Sqrt(LengthSquared);
    public DVec3 Normalized(double eps = 1e-14) => Length > eps ? this / Length : Zero;
    public static double Dot(DVec3 a, DVec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static DVec3 Cross(DVec3 a, DVec3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);
    public static DVec3 Lerp(DVec3 a, DVec3 b, double t) => a + (b - a) * t;
}

public readonly record struct Box3d(DVec3 Min, DVec3 Max)
{
    public DVec3 Center => (Min + Max) * 0.5;
    public DVec3 Size => Max - Min;
    public double Diagonal => Size.Length;
    public bool Contains(DVec3 p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    public Box3d Child(int index)
    {
        var c = Center;
        bool hx = (index & 1) != 0, hy = (index & 2) != 0, hz = (index & 4) != 0;
        return new(
            new(hx ? c.X : Min.X, hy ? c.Y : Min.Y, hz ? c.Z : Min.Z),
            new(hx ? Max.X : c.X, hy ? Max.Y : c.Y, hz ? Max.Z : c.Z));
    }

    public IEnumerable<DVec3> ProbePoints()
    {
        var c = Center;
        yield return c;
        for (int i = 0; i < 8; i++)
            yield return new((i & 1) == 0 ? Min.X : Max.X, (i & 2) == 0 ? Min.Y : Max.Y, (i & 4) == 0 ? Min.Z : Max.Z);
        yield return new(Min.X, c.Y, c.Z); yield return new(Max.X, c.Y, c.Z);
        yield return new(c.X, Min.Y, c.Z); yield return new(c.X, Max.Y, c.Z);
        yield return new(c.X, c.Y, Min.Z); yield return new(c.X, c.Y, Max.Z);
    }
}
