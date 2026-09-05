namespace DCad.MeshKernel;

public readonly record struct ScaleTolerance(double Absolute = 1e-10, double Relative = 1e-10)
{
    public double AtScale(double scale) => Math.Max(Absolute, Math.Abs(scale) * Relative);

    public static ScaleTolerance ForSolid(CsgSolid solid, double relative = 1e-10)
    {
        var stats = MeshAnalysis.Analyze(solid);
        var diagonal = (stats.Max - stats.Min).Length;
        return new ScaleTolerance(Math.Max(1e-12, diagonal * relative * 1e-3), relative);
    }
}

public enum PointContainment
{
    Outside,
    Inside,
    Boundary
}

public static class RobustSolidQueries
{
    /// <summary>
    /// Deterministic generalized-winding classification for a closed oriented triangle solid.
    /// Unlike the historical random-ray test this has no RNG direction and is stable under
    /// repeated evaluation.  Boundary proximity is checked explicitly first.
    /// </summary>
    public static PointContainment ClassifyPoint(
        CsgSolid solid,
        Vec3d point,
        ScaleTolerance? tolerance = null)
    {
        if (solid is null) throw new ArgumentNullException(nameof(solid));
        var tol = tolerance ?? ScaleTolerance.ForSolid(solid);
        var stats = MeshAnalysis.Analyze(solid);
        double eps = tol.AtScale(Math.Max(1.0, (stats.Max - stats.Min).Length));
        double solidAngle = 0.0;

        foreach (var (a, b, c) in solid.Triangles())
        {
            if (PointTriangleDistanceSquared(point, a, b, c) <= eps * eps)
                return PointContainment.Boundary;
            solidAngle += SignedSolidAngle(a - point, b - point, c - point);
        }

        // A consistently-oriented watertight surface has |Omega| ~= 4*pi inside and ~= 0 outside.
        double winding = solidAngle / (4.0 * Math.PI);
        return Math.Abs(winding) > 0.5 ? PointContainment.Inside : PointContainment.Outside;
    }

    public static double SignedSolidAngle(Vec3d a, Vec3d b, Vec3d c)
    {
        double la = a.Length, lb = b.Length, lc = c.Length;
        if (la <= 1e-30 || lb <= 1e-30 || lc <= 1e-30) return 0.0;
        double numerator = a.Dot(b.Cross(c));
        double denominator = la * lb * lc + a.Dot(b) * lc + b.Dot(c) * la + c.Dot(a) * lb;
        return 2.0 * Math.Atan2(numerator, denominator);
    }

    public static bool BoundsContain(CsgSolid solid, Vec3d point, ScaleTolerance? tolerance = null)
    {
        var s = MeshAnalysis.Analyze(solid);
        var tol = tolerance ?? ScaleTolerance.ForSolid(solid);
        double e = tol.AtScale(Math.Max(1.0, (s.Max - s.Min).Length));
        return point.X >= s.Min.X - e && point.X <= s.Max.X + e &&
               point.Y >= s.Min.Y - e && point.Y <= s.Max.Y + e &&
               point.Z >= s.Min.Z - e && point.Z <= s.Max.Z + e;
    }

    private static double PointTriangleDistanceSquared(Vec3d p, Vec3d a, Vec3d b, Vec3d c)
    {
        // Real-Time Collision Detection, closest-point regions of a triangle.
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = ab.Dot(ap), d2 = ac.Dot(ap);
        if (d1 <= 0.0 && d2 <= 0.0) return (p - a).Dot(p - a);

        var bp = p - b;
        double d3 = ab.Dot(bp), d4 = ac.Dot(bp);
        if (d3 >= 0.0 && d4 <= d3) return (p - b).Dot(p - b);

        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        {
            double v = d1 / (d1 - d3);
            var q = a + ab * v;
            return (p - q).Dot(p - q);
        }

        var cp = p - c;
        double d5 = ab.Dot(cp), d6 = ac.Dot(cp);
        if (d6 >= 0.0 && d5 <= d6) return (p - c).Dot(p - c);

        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        {
            double w = d2 / (d2 - d6);
            var q = a + ac * w;
            return (p - q).Dot(p - q);
        }

        double va = d3 * d6 - d5 * d4;
        if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
        {
            double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            var q = b + (c - b) * w;
            return (p - q).Dot(p - q);
        }

        double denom = 1.0 / (va + vb + vc);
        double faceV = vb * denom, faceW = vc * denom;
        var projection = a + ab * faceV + ac * faceW;
        return (p - projection).Dot(p - projection);
    }
}
