namespace DCad.Core;

public readonly record struct Ray3d(Vector3d Origin, Vector3d Direction)
{
    public Ray3d Normalized(GeometryTolerance? tolerance = null) => new(Origin, Direction.Normalized(tolerance));
    public Vector3d At(double t) => Origin + Direction * t;
}

public readonly record struct RayHit(double Distance, double U, double V, Vector3d Position, Vector3d Normal);

public static class SpatialQueries
{
    public static bool IntersectRayTriangle(
        Ray3d ray,
        Triangle3d triangle,
        out RayHit hit,
        GeometryTolerance? tolerance = null,
        bool cullBackFaces = false)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        var scale = Math.Max(1.0, Math.Max(triangle.A.Length, Math.Max(triangle.B.Length, triangle.C.Length)));
        var eps = tol.AtScale(scale);
        var e1 = triangle.B - triangle.A;
        var e2 = triangle.C - triangle.A;
        var p = Vector3d.Cross(ray.Direction, e2);
        var det = Vector3d.Dot(e1, p);

        if (cullBackFaces)
        {
            if (det <= eps) { hit = default; return false; }
        }
        else if (Math.Abs(det) <= eps)
        {
            hit = default;
            return false;
        }

        var invDet = 1.0 / det;
        var tvec = ray.Origin - triangle.A;
        var u = Vector3d.Dot(tvec, p) * invDet;
        if (u < -eps || u > 1.0 + eps) { hit = default; return false; }

        var q = Vector3d.Cross(tvec, e1);
        var v = Vector3d.Dot(ray.Direction, q) * invDet;
        if (v < -eps || u + v > 1.0 + eps) { hit = default; return false; }

        var distance = Vector3d.Dot(e2, q) * invDet;
        if (distance < -eps) { hit = default; return false; }
        var normal = triangle.UnitNormal(tol);
        hit = new RayHit(distance, u, v, ray.At(distance), normal);
        return true;
    }

    public static bool IntersectRayAabb(Ray3d ray, Aabb3d box, out double tMin, out double tMax, GeometryTolerance? tolerance = null)
    {
        var eps = (tolerance ?? GeometryTolerance.Default).AtScale(Math.Max(1.0, box.Diagonal));
        tMin = double.NegativeInfinity;
        tMax = double.PositiveInfinity;
        if (!Slab(ray.Origin.X, ray.Direction.X, box.Min.X, box.Max.X, eps, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Y, ray.Direction.Y, box.Min.Y, box.Max.Y, eps, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Z, ray.Direction.Z, box.Min.Z, box.Max.Z, eps, ref tMin, ref tMax))
            return false;
        return tMax >= Math.Max(0.0, tMin);
    }

    public static Vector3d ClosestPointOnTriangle(Vector3d p, Triangle3d t)
    {
        // Ericson-style Voronoi region classification, no random rays or epsilon branches.
        var ab = t.B - t.A; var ac = t.C - t.A; var ap = p - t.A;
        var d1 = Vector3d.Dot(ab, ap); var d2 = Vector3d.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return t.A;

        var bp = p - t.B; var d3 = Vector3d.Dot(ab, bp); var d4 = Vector3d.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return t.B;

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            var v = d1 / (d1 - d3);
            return t.A + ab * v;
        }

        var cp = p - t.C; var d5 = Vector3d.Dot(ab, cp); var d6 = Vector3d.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return t.C;

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            var w = d2 / (d2 - d6);
            return t.A + ac * w;
        }

        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return t.B + (t.C - t.B) * w;
        }

        var denom = 1.0 / (va + vb + vc);
        var baryV = vb * denom;
        var baryW = vc * denom;
        return t.A + ab * baryV + ac * baryW;
    }

    public static ulong Morton3D(uint x, uint y, uint z)
        => SplitBy3(x) | (SplitBy3(y) << 1) | (SplitBy3(z) << 2);

    private static bool Slab(double origin, double direction, double min, double max, double eps, ref double tMin, ref double tMax)
    {
        if (Math.Abs(direction) <= eps) return origin >= min - eps && origin <= max + eps;
        var a = (min - origin) / direction;
        var b = (max - origin) / direction;
        if (a > b) (a, b) = (b, a);
        tMin = Math.Max(tMin, a); tMax = Math.Min(tMax, b);
        return tMin <= tMax;
    }

    private static ulong SplitBy3(uint a)
    {
        ulong x = a & 0x1fffff;
        x = (x | x << 32) & 0x1f00000000ffffUL;
        x = (x | x << 16) & 0x1f0000ff0000ffUL;
        x = (x | x << 8) & 0x100f00f00f00f00fUL;
        x = (x | x << 4) & 0x10c30c30c30c30c3UL;
        x = (x | x << 2) & 0x1249249249249249UL;
        return x;
    }
}
