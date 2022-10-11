using System.Drawing;
using System.Numerics;

var triangle1 = new TrianglesIntersection.Triangle(
    new Vector3(0, 0, 0),
    new Vector3(1, 0, 0),
    new Vector3(0, 1, 0)
);
var triangle2 = new TrianglesIntersection.Triangle(
    new Vector3(1, 0, 1),
    new Vector3(0, 1, 1),
    new Vector3(0.25f, 0.25f, -0.25f)
);
var Triangles = new List<TrianglesIntersection.Triangle>();
TrianglesIntersection.Intersection(triangle1, triangle2, out var TransformedTriangle1, out var TransformedTriangle2);
var result = Triangulation.Triangulate(TransformedTriangle1);
Console.WriteLine();

public class Triangulation: TrianglesIntersection
{
    public static Triangle[] Triangulate(in List<Vector3> points)
    {
        var finalList = new List<Triangle>();
        var triangle1 = new Triangle(points[1], points[3], points[0]);
        var triangle2 = new Triangle(points[2], points[3], points[0]);
        var triangle3 = new Triangle(points[1], points[3], points[2]);
        finalList.Add(triangle1);
        finalList.Add(triangle2);
        finalList.Add(triangle3);

        // 1.
        if (distance(triangle1, points[4])
            <=
            distance(triangle1, points[3]))
        {
            finalList.RemoveAt(0);
            var triangle4 = new Triangle(points[1], points[4], points[0]);
            var triangle5 = new Triangle(points[0], points[4], points[3]);
            var triangle6 = new Triangle(points[1], points[4], points[3]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
        }

        // 2.
        if (distance(triangle2, points[4])
            <=
            distance(triangle2, points[3]))
        {
            finalList.RemoveAt(1);
            var triangle4 = new Triangle(points[0], points[4], points[2]);
            var triangle5 = new Triangle(points[0], points[4], points[3]);
            var triangle6 = new Triangle(points[3], points[4], points[2]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
        }

        // 3.
        if (distance(triangle3, points[4])
            <=
            distance(triangle3, points[3]))
        {
            finalList.RemoveAt(2);
            var triangle4 = new Triangle(points[1], points[4], points[3]);
            var triangle5 = new Triangle(points[3], points[4], points[2]);
            var triangle6 = new Triangle(points[1], points[4], points[2]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
        }

        return finalList.ToArray();
    }
}

public class TrianglesIntersection
{

    public struct Triangle
    {
        public Vector3 p1, p2, p3;

        public Triangle(Vector3 P1, Vector3 P2, Vector3 P3)
        {
            p1 = P1;
            p2 = P2;
            p3 = P3;
        }
    }

    private static bool getPoint(in Vector3 A, in Vector3 B, in Vector3 C, in Vector3 X, in Vector3 Y, out Vector3 result)
    {

        var N = Vector3.Cross(B - A, C - A);
        var V = A - X;
        float d = Vector3.Dot(N, V);
        var W = Y - X;
        float e = Vector3.Dot(N, W);

        Vector3 O = Vector3.Zero;
        if (e != 0)
        {
            O = X + W * d / e;
        }
        else if (d == 0)
        {
            O = X + W;
        }
        result = O;
        return !(
            (
            O.X > Math.Max(X.X, Y.X) || O.X < Math.Min(X.X, Y.X)
         || O.Y > Math.Max(X.Y, Y.Y) || O.Y < Math.Min(X.Y, Y.Y)
         || O.Z > Math.Max(X.Z, Y.Z) || O.Z < Math.Min(X.Z, Y.Z))

        &

            (
            O.X < Math.Max(X.X, Y.X) || O.X > Math.Min(X.X, Y.X)
         || O.Y < Math.Max(X.Y, Y.Y) || O.Y > Math.Min(X.Y, Y.Y)
         || O.Z < Math.Max(X.Z, Y.Z) || O.Z > Math.Min(X.Z, Y.Z))
        );
    }

    protected static float distance(in Triangle triangle, in Vector3 point)
    {
        return (int)((
            Math.Sqrt(
            Math.Pow(triangle.p1.X - point.X, 2) +
            Math.Pow(triangle.p1.Y - point.Y, 2) +
            Math.Pow(triangle.p1.Z - point.Z, 2)
            )
            +
            Math.Sqrt(
            Math.Pow(triangle.p2.X - point.X, 2) +
            Math.Pow(triangle.p2.Y - point.Y, 2) +
            Math.Pow(triangle.p2.Z - point.Z, 2)
            )
            +
            Math.Sqrt(
            Math.Pow(triangle.p3.X - point.X, 2) +
            Math.Pow(triangle.p3.Y - point.Y, 2) +
            Math.Pow(triangle.p3.Z - point.Z, 2)
            )
            ) * 10) / 10f;
    }

    private static Vector3[] PointCheckInside(in Triangle triangle1, in Triangle triangle2)
    {
        float max = distance(triangle1, new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z));

        var tr = new List<Vector3>();

        if (getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                out var V1
                )
                && distance(triangle1, V1) <= max
            )
        {
            tr.Add(V1);
        }

        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
            out var V2
            )
            && distance(triangle1, V2) <= max
            )
        {
            tr.Add(V2);
        }

        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
            out var V3
            )
            && distance(triangle1, V3) <= max
            )
        {
            tr.Add(V3);
        }
        return tr.ToArray();
    }

    private static Vector3[] PointCheckAll(in Triangle triangle1, in Triangle triangle2)
    {
        var tr = new List<Vector3>();
        if (getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                out var V1
                )) tr.Add(V1);
        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
            out var V2
            )) tr.Add(V2);
        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
            out var V3
            )) tr.Add(V3);
        return tr.ToArray();
    }

    public static void Intersection(in Triangle triangle1, in Triangle triangle2, out List<Vector3> TransformedTriangle1, out List<Vector3> TransformedTriangle2)
    {
        Vector3[] Point = PointCheckInside(triangle1, triangle2);
        Vector3[] Points = PointCheckAll(triangle1, triangle2);
        TransformedTriangle1 = new List<Vector3>();
        TransformedTriangle2 = new List<Vector3>();
        if (Point.Length == 2)
        {
            TransformedTriangle1.Add(triangle1.p1);
            TransformedTriangle1.Add(triangle1.p2);
            TransformedTriangle1.Add(triangle1.p3);
            TransformedTriangle1.Add(Point[0]);
            TransformedTriangle1.Add(Point[1]);
            TransformedTriangle2.Add(triangle2.p1);
            TransformedTriangle2.Add(triangle2.p2);
            TransformedTriangle2.Add(triangle2.p3);
            TransformedTriangle2.Add(Point[0]);
            TransformedTriangle2.Add(Point[1]);
        }

        if (Point.Length == 1)
        {
            TransformedTriangle1.Add(triangle1.p1);
            TransformedTriangle1.Add(triangle1.p2);
            TransformedTriangle1.Add(triangle1.p3);
            TransformedTriangle1.Add(Point[0]);


            Vector3? oldpoint = CHECK(Points[0], Points[1], triangle1.p1, triangle1.p2);
            if (oldpoint != null)
            {
                TransformedTriangle1.Add(oldpoint.Value);
                TransformedTriangle2.Add(oldpoint.Value);
            }

            oldpoint = CHECK(Points[0], Points[1], triangle1.p3, triangle1.p2);
            if (oldpoint != null)
            {
                TransformedTriangle1.Add(oldpoint.Value);
                TransformedTriangle2.Add(oldpoint.Value);
            }

            oldpoint = CHECK(Points[0], Points[1], triangle1.p1, triangle1.p3);
            if (oldpoint != null)
            {
                TransformedTriangle1.Add(oldpoint.Value);
                TransformedTriangle2.Add(oldpoint.Value);
            }

            TransformedTriangle2.Add(triangle2.p1);
            TransformedTriangle2.Add(triangle2.p2);
            TransformedTriangle2.Add(triangle2.p3);

            TransformedTriangle2.Add(Point[0]);
        }


    }

    private static Vector3? CHECK(in Vector3 p1, in Vector3 p2, in Vector3 p3, in Vector3 p4)
    {
        // Проекция на плоскость ОZ.
        bool b1 = CutIntersecion(
        new Vector2(p1.X, p1.Y),
        new Vector2(p2.X, p2.Y),
        new Vector2(p3.X, p3.Y),
        new Vector2(p4.X, p4.Y),
        out var x1, out var y1);
        bool b2 = CutIntersecion(
        new Vector2(p1.Y, p1.Z),
        new Vector2(p2.Y, p2.Z),
        new Vector2(p3.Y, p3.Z),
        new Vector2(p4.Y, p4.Z),
        out var y2, out var z1);
        bool b3 = CutIntersecion(
        new Vector2(p1.X, p1.Z),
        new Vector2(p2.X, p2.Z),
        new Vector2(p3.X, p3.Z),
        new Vector2(p4.X, p4.Z),
        out var x2, out var z2);

        if (b1 & b2 & b3)
        {
            return new Vector3(x2, y2, z2);
        }
        return null;
    }

    private static bool CutIntersecion(in Vector2 L1p1, in Vector2 L1p2, in Vector2 L2p1, in Vector2 L2p2, out float X, out float Y)
    {
        bool K1IsInf = false;
        bool K2IsInf = false;
        float K1 = (L1p1.Y - L1p2.Y) / (L1p1.X - L1p2.X);
        float B1 = L1p1.Y - K1 * L1p1.X;
        if (L1p1.X == L1p2.X) K1IsInf = true;
        float K2 = (L2p1.Y - L2p2.Y) / (L2p1.X - L2p2.X);
        float B2 = L2p1.Y - K2 * L2p1.X;
        if (L2p1.X == L2p2.X) K2IsInf = true;

        X = 0;
        Y = 0;
        if (K1 == K2)
        {
            //Console.WriteLine("Они параллельны.");
            return false;
        }
        else
        {
            if (K1IsInf)
            {
                if (K2IsInf)
                {
                    //Оба вертикальные
                    if (L1p1.X == L2p1.X)
                    {
                        //Console.WriteLine("Они друг в друге.");
                        X = L1p1.X;
                        Y = L1p1.Y;
                    }
                    else
                    {
                        //Console.WriteLine("Они вертикальные и не пересекаются.");
                        return false;
                    }
                }
                else
                {
                    X = L1p1.X;
                    Y = K2 * X + B2;
                }
            }
            else
            {
                if (K2IsInf)
                {
                    X = L2p1.X;
                    Y = K1 * X + B1;
                }
                else
                {
                    X = (B2 - B1) / (K1 - K2);
                    Y = K1 * X + B1;
                }
            }
            if (X > Math.Max(Math.Max(L1p1.X, L1p2.X), Math.Max(L2p1.X, L2p2.X))) return false;
            if (Y > Math.Max(Math.Max(L1p1.Y, L1p2.Y), Math.Max(L2p1.Y, L2p2.Y))) return false;
            return true;
        }
    }

}