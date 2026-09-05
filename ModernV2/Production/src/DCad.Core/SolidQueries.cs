namespace DCad.Core;

public enum PointContainment { Outside, Boundary, Inside }

public static class SolidQueries
{
    public static PointContainment ClassifyPoint(Mesh3d mesh, Vector3d point, GeometryTolerance? tolerance = null)
    {
        var tol = tolerance ?? GeometryTolerance.Default;
        var scale = Math.Max(1.0, mesh.Bounds.Diagonal);
        var eps = tol.AtScale(scale);
        if (!mesh.Bounds.Contains(point, eps)) return PointContainment.Outside;

        double omega = 0;
        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            var tri = mesh.GetTriangle(i);
            if (PointOnTriangle(point, tri, eps)) return PointContainment.Boundary;
            omega += SolidAngle(point, tri);
        }

        return Math.Abs(omega) > 2.0 * Math.PI ? PointContainment.Inside : PointContainment.Outside;
    }

    public static bool PointOnTriangle(Vector3d p, Triangle3d t, double eps)
    {
        var n = t.CrossNormal;
        var nLen = n.Length;
        if (nLen <= eps) return false;
        var planeDistance = Math.Abs(Vector3d.Dot(p - t.A, n)) / nLen;
        if (planeDistance > eps) return false;

        var v0 = t.B - t.A;
        var v1 = t.C - t.A;
        var v2 = p - t.A;
        var d00 = Vector3d.Dot(v0, v0);
        var d01 = Vector3d.Dot(v0, v1);
        var d11 = Vector3d.Dot(v1, v1);
        var d20 = Vector3d.Dot(v2, v0);
        var d21 = Vector3d.Dot(v2, v1);
        var denom = d00 * d11 - d01 * d01;
        if (Math.Abs(denom) <= eps * eps) return false;
        var v = (d11 * d20 - d01 * d21) / denom;
        var w = (d00 * d21 - d01 * d20) / denom;
        var u = 1.0 - v - w;
        return u >= -eps && v >= -eps && w >= -eps;
    }

    private static double SolidAngle(Vector3d p, Triangle3d t)
    {
        var a = t.A - p;
        var b = t.B - p;
        var c = t.C - p;
        var la = a.Length;
        var lb = b.Length;
        var lc = c.Length;
        if (la == 0 || lb == 0 || lc == 0) return 4.0 * Math.PI;
        var numerator = Vector3d.Dot(a, Vector3d.Cross(b, c));
        var denominator = la * lb * lc
            + Vector3d.Dot(a, b) * lc
            + Vector3d.Dot(b, c) * la
            + Vector3d.Dot(c, a) * lb;
        return 2.0 * Math.Atan2(numerator, denominator);
    }
}
