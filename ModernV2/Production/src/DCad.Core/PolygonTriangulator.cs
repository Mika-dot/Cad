namespace DCad.Core;

public static class PolygonTriangulator
{
    public static IReadOnlyList<TriangleIndex> Triangulate(IReadOnlyList<Vector2d> polygon, GeometryTolerance? tolerance = null)
    {
        if (polygon.Count < 3) throw new ArgumentException("A polygon requires at least three vertices.", nameof(polygon));
        var tol = tolerance ?? GeometryTolerance.Default;
        var scale = Math.Max(1.0, polygon.Max(p => Math.Sqrt(p.LengthSquared)));
        var eps = tol.AtScale(scale * scale);

        ValidateSimplePolygon(polygon, eps);
        var signedArea = SignedArea(polygon);
        if (Math.Abs(signedArea) <= eps) throw new ArgumentException("Polygon area is zero.", nameof(polygon));
        var ccw = signedArea > 0;

        var remaining = Enumerable.Range(0, polygon.Count).ToList();
        var result = new List<TriangleIndex>(polygon.Count - 2);
        var guard = 0;
        while (remaining.Count > 3)
        {
            var clipped = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var ia = remaining[(i - 1 + remaining.Count) % remaining.Count];
                var ib = remaining[i];
                var ic = remaining[(i + 1) % remaining.Count];
                var a = polygon[ia];
                var b = polygon[ib];
                var c = polygon[ic];
                var cross = Vector2d.Cross(b - a, c - b);
                if (ccw ? cross <= eps : cross >= -eps) continue;

                var containsOther = false;
                foreach (var ip in remaining)
                {
                    if (ip == ia || ip == ib || ip == ic) continue;
                    if (PointInTriangle(polygon[ip], a, b, c, eps))
                    {
                        containsOther = true;
                        break;
                    }
                }
                if (containsOther) continue;

                result.Add(ccw ? new(ia, ib, ic) : new(ic, ib, ia));
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped || ++guard > polygon.Count * polygon.Count)
                throw new InvalidOperationException("Ear clipping failed; polygon may be self-intersecting or numerically degenerate.");
        }

        result.Add(ccw
            ? new(remaining[0], remaining[1], remaining[2])
            : new(remaining[2], remaining[1], remaining[0]));
        return result;
    }

    public static double SignedArea(IReadOnlyList<Vector2d> polygon)
    {
        double twice = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            twice += a.X * b.Y - a.Y * b.X;
        }
        return 0.5 * twice;
    }

    private static bool PointInTriangle(Vector2d p, Vector2d a, Vector2d b, Vector2d c, double eps)
    {
        var c1 = Vector2d.Cross(b - a, p - a);
        var c2 = Vector2d.Cross(c - b, p - b);
        var c3 = Vector2d.Cross(a - c, p - c);
        var hasNeg = c1 < -eps || c2 < -eps || c3 < -eps;
        var hasPos = c1 > eps || c2 > eps || c3 > eps;
        return !(hasNeg && hasPos);
    }

    private static void ValidateSimplePolygon(IReadOnlyList<Vector2d> p, double eps)
    {
        for (var i = 0; i < p.Count; i++)
        {
            var a0 = p[i];
            var a1 = p[(i + 1) % p.Count];
            if ((a1 - a0).LengthSquared <= eps * eps)
                throw new ArgumentException("Polygon contains a zero-length edge.", nameof(p));

            for (var j = i + 1; j < p.Count; j++)
            {
                if (j == i || j == (i + 1) % p.Count || (j + 1) % p.Count == i) continue;
                var b0 = p[j];
                var b1 = p[(j + 1) % p.Count];
                if (SegmentsProperlyIntersect(a0, a1, b0, b1, eps))
                    throw new ArgumentException("Polygon is self-intersecting.", nameof(p));
            }
        }
    }

    private static bool SegmentsProperlyIntersect(Vector2d a, Vector2d b, Vector2d c, Vector2d d, double eps)
    {
        var abC = Vector2d.Cross(b - a, c - a);
        var abD = Vector2d.Cross(b - a, d - a);
        var cdA = Vector2d.Cross(d - c, a - c);
        var cdB = Vector2d.Cross(d - c, b - c);
        return ((abC > eps && abD < -eps) || (abC < -eps && abD > eps)) &&
               ((cdA > eps && cdB < -eps) || (cdA < -eps && cdB > eps));
    }
}
