using System.Drawing;
using System.Numerics;
using static TrianglesIntersection;

var triangle1 = new TrianglesIntersection.Triangle(
    new Vector3(0, 0, 10),
    new Vector3(1, 0, 10),
    new Vector3(0, 1, 10)
);
var triangle2 = new TrianglesIntersection.Triangle(
    new Vector3(1, 0, 1),
    new Vector3(0, 1, 1),
    new Vector3(0.25f, 0.25f, -0.25f)
);
var a = TrianglesIntersection.Intersection(triangle1, triangle2, out List<Vector3> ListTrianglesWorld1, out List<Vector3> ListTrianglesCrushing1);

var q = Triangulation.Triangulate(ListTrianglesWorld1);
var w = Triangulation.Triangulate(ListTrianglesCrushing1);

Console.WriteLine();

public class Triangulation
{
    private static bool PointInTriangle(Triangle triangle, Vector3 P)
    {
        if (triangle.p1 == P || triangle.p2 == P || triangle.p3 == P) return true;
        float x, y, z, x1, x2, x3, y1, y2, y3, z1, z2, z3, x31, x21, y31, y21, z31, z21, alpha = 0.0f, beta = 0.0f;//Луче, конечно double, но float быстрее
        x1 = triangle.p1.X; x2 = triangle.p2.X; x3 = triangle.p3.X;//Всю эту ерунду можно было бы и не писать, только можно запутаться с индексами.
        y1 = triangle.p1.Y; y2 = triangle.p2.Y; y3 = triangle.p3.Y;
        z1 = triangle.p1.Z; z2 = triangle.p2.Z; z3 = triangle.p3.Z;
        x31 = x3 - x1; x21 = x2 - x1;//А вот это уже нужно.
        y31 = y3 - y1; y21 = y2 - y1;
        z31 = z3 - z1; z21 = z2 - z1;
        x = P.X; y = P.Y; z = P.Z;
        if ((x21 * y31 - x31 * y21) != 0)
        {//Если известна nz (z-компонента нормали треуг), то можно if(nz!=0)
            beta = ((y - y2) * x31 - (x - x2) * y31) / (x21 * y31 - x31 * y21);//или разделить на nz
            alpha = (x - x2) / (beta * x31) + x21 / x31;
        }
        else if ((z21 * y31 - z31 * y21) != 0)
        {//Если известна nx (x-компонента нормали треуг), то можно if(nx!=0)
            beta = ((y - y2) * z31 - (z - z2) * y31) / (z21 * y31 - z31 * y21);//или разделить на nx
            alpha = (z - z2) / (beta * z31) + z21 / z31;
        }
        else if ((z21 * x31 - z31 * x21) != 0)
        {//Если известна ny (y-компонента нормали треуг), то можно if(ny!=0)
            beta = ((x - x2) * z31 - (z - z2) * x31) / (z21 * x31 - z31 * x21);//или разделить на ny
            alpha = (x - x2) / (beta * x31) + x21 / x31;
        }

        if (alpha > -0.001f && alpha < 1.001f && beta > -0.001f && beta < 1.001f) //Тут можно поиграть со значениями для float и double
            return true;
        else return false;


        /*
        bool SameSide(Vector3 p1, Vector3 p2, Vector3 a, Vector3 b)
        {
        Vector3 cp1 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p1, a));
        Vector3 cp2 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p2, a));
        if (Vector3.Dot(cp1, cp2) >= 0) return true;
        return false;

        }
        Vector3 A = checkForZeros(triangle.p1, 0.001f), B = checkForZeros(triangle.p2, 0.001f), C = checkForZeros(triangle.p3, 0.001f);
        P = checkForZeros(P, 0.001f);
        if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
        {
        Vector3 vc1 = Vector3.Cross(Vector3.Subtract(A, B), Vector3.Subtract(A, C));
        if (Math.Abs(Vector3.Dot(Vector3.Subtract(A, P), vc1)) <= .01f)
        return true;
        }

        return false;
        */
    }

    protected static float distances(in Triangle triangle, in Vector3 point)
    {
        return
            Vector3.Distance(triangle.p1, point)
            +
            Vector3.Distance(triangle.p2, point)
            +
            Vector3.Distance(triangle.p3, point);
    }

    public static bool BelongingToAStraightLine(Triangle finalList)
    {
        float ConstX = (finalList.p3.X - finalList.p1.X) / (finalList.p2.X - finalList.p1.X);
        float ConstY = (finalList.p3.Y - finalList.p1.Y) / (finalList.p2.Y - finalList.p1.Y);
        float ConstZ = (finalList.p3.Z - finalList.p1.Z) / (finalList.p2.Z - finalList.p1.Z);

        if (float.IsNaN(ConstX)) ConstX = (float.IsNaN(ConstY)) ? ConstZ : ConstY;
        if (float.IsNaN(ConstY)) ConstY = (float.IsNaN(ConstZ)) ? ConstX : ConstZ;
        if (float.IsNaN(ConstZ)) ConstZ = (float.IsNaN(ConstX)) ? ConstY : ConstX;


        if ((Math.Abs(ConstX - ConstY) < 0.001f) && (Math.Abs(ConstX - ConstZ) < 0.001f))
        {
            return true;
        }
        return false;
    }
    public static List<Triangle> Triangulate(in List<Vector3> points)
    {
        List<Triangle> finalList = new List<Triangle>();
        Triangle triangle1 = new Triangle(points[1], points[3], points[0]);
        Triangle triangle2 = new Triangle(points[2], points[3], points[0]);
        Triangle triangle3 = new Triangle(points[1], points[3], points[2]);
        finalList.Add(triangle1);
        finalList.Add(triangle2);
        finalList.Add(triangle3);


        // 1.
        int n = 0;
        //PointInTriangle(triangle1, points[4])
        if (distances(triangle1, points[4]) - distances(triangle1, points[3]) <= 0.1f)
        {
            finalList.RemoveAt(n);
            Triangle triangle4 = new Triangle(points[1], points[4], points[0]);
            Triangle triangle5 = new Triangle(points[0], points[4], points[3]);
            Triangle triangle6 = new Triangle(points[1], points[4], points[3]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
            n--;
        }

        // 2.
        //PointInTriangle(triangle2, points[4])
        if (distances(triangle2, points[4]) - distances(triangle2, points[3]) <= 0.1f)
        {
            finalList.RemoveAt(n + 1);
            Triangle triangle4 = new Triangle(points[0], points[4], points[2]);
            Triangle triangle5 = new Triangle(points[0], points[4], points[3]);
            Triangle triangle6 = new Triangle(points[3], points[4], points[2]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
            n--;
        }

        // 3.
        //PointInTriangle(triangle3, points[4])
        if (distances(triangle3, points[4]) - distances(triangle3, points[3]) <= 0.1f)
        {
            finalList.RemoveAt(n + 2);
            Triangle triangle4 = new Triangle(points[1], points[4], points[3]);
            Triangle triangle5 = new Triangle(points[3], points[4], points[2]);
            Triangle triangle6 = new Triangle(points[1], points[4], points[2]);
            finalList.Add(triangle4);
            finalList.Add(triangle5);
            finalList.Add(triangle6);
            n--;
        }

        for (int i = 0; i < finalList.Count; i++)
        {
            if (Triangulation.BelongingToAStraightLine(finalList[i]))
            {
                finalList.RemoveAt(i);
            }
        }

        return finalList;
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

        Vector3 N = Vector3.Cross(B - A, C - A);
        Vector3 V = A - X;
        float d = Vector3.Dot(N, V);
        Vector3 W = Y - X;
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

        //return Vector3.Distance(X, Y) + 0.1f >= Vector3.Distance(X, O) + Vector3.Distance(Y, O);

        return !(
            (
            O.X > Math.Max(X.X, Y.X) || O.X < Math.Min(X.X, Y.X)
         || O.Y > Math.Max(X.Y, Y.Y) || O.Y < Math.Min(X.Y, Y.Y)
         || O.Z > Math.Max(X.Z, Y.Z) || O.Z < Math.Min(X.Z, Y.Z))
        &&
            (
            O.X < Math.Max(X.X, Y.X) || O.X > Math.Min(X.X, Y.X)
         || O.Y < Math.Max(X.Y, Y.Y) || O.Y > Math.Min(X.Y, Y.Y)
         || O.Z < Math.Max(X.Z, Y.Z) || O.Z > Math.Min(X.Z, Y.Z))
        );
    }


    // тестили не подошло "евклидова растояние".
    // тест ошибки {3 -1 2}{0 0 2}{0.5 0.5 0.5} и точка {1 0,555! 0}
    private static float distance(in Triangle triangle, in Vector3 point)
    {
        return (float)(
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
            );
    }
    private static float distanceSegment(Vector3 P1, Vector3 P2, Vector3 defined)
    {
        return (float)(
            Math.Sqrt(
            Math.Pow(P1.X - defined.X, 2) +
            Math.Pow(P1.Y - defined.Y, 2) +
            Math.Pow(P1.Z - defined.Z, 2)
            )
            +
            Math.Sqrt(
            Math.Pow(P2.X - defined.X, 2) +
            Math.Pow(P2.Y - defined.Y, 2) +
            Math.Pow(P2.Z - defined.Z, 2)
            )
            );
    }
    private static bool fuzzy(float a, float b, float c)
    {
        return a <= b + c;// && a >= b-c;
    }


    // тестили не подошло "Матиматический".
    // не справился с числами в периуде.
    private static bool inside_triangle(double P_x, double P_y, double P_z, double A_x, double A_y, double A_z, double B_x, double B_y, double B_z, double C_x, double C_y, double C_z)
    {
        bool inside = false;
        double AB = Math.Sqrt((A_x - B_x) * (A_x - B_x) + (A_y - B_y) * (A_y - B_y) + (A_z - B_z) * (A_z - B_z));
        double BC = Math.Sqrt((B_x - C_x) * (B_x - C_x) + (B_y - C_y) * (B_y - C_y) + (B_z - C_z) * (B_z - C_z));
        double CA = Math.Sqrt((A_x - C_x) * (A_x - C_x) + (A_y - C_y) * (A_y - C_y) + (A_z - C_z) * (A_z - C_z));

        double AP = Math.Sqrt((P_x - A_x) * (P_x - A_x) + (P_y - A_y) * (P_y - A_y) + (P_z - A_z) * (P_z - A_z));
        double BP = Math.Sqrt((P_x - B_x) * (P_x - B_x) + (P_y - B_y) * (P_y - B_y) + (P_z - B_z) * (P_z - B_z));
        double CP = Math.Sqrt((P_x - C_x) * (P_x - C_x) + (P_y - C_y) * (P_y - C_y) + (P_z - C_z) * (P_z - C_z));
        double diff = (triangle_square(AP, BP, AB) + triangle_square(AP, CP, CA) + triangle_square(BP, CP, BC)) - triangle_square(AB, BC, CA);
        if (Math.Abs(diff) < Double.Epsilon) inside = true;
        return inside;
    }
    private static double triangle_square(double a, double b, double c)
    {
        double p = (a + b + c) / 2;
        return Math.Sqrt(p * (p - a) * (p - b) * (p - c));
    }

    private static Vector3 checkForZeros(Vector3 vector, float kof)
    {
        if (vector.X < kof && -kof < vector.X)
        {
            vector.X = 0;
        }
        if (vector.Y < kof && -kof < vector.Y)
        {
            vector.Y = 0;
        }
        if (vector.Z < kof && -kof < vector.Z)
        {
            vector.Z = 0;
        }
        return vector;
    }

    // тест подошел.
    static bool isInsideTriangle(Triangle triangle, Vector3 p)
    {
        Vector3 Mult(Vector3 a, Vector3 b) => new Vector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        // Векторное произведение
        Vector3 ABxAP = checkForZeros(Mult(triangle.p2 - triangle.p1, p - triangle.p1), 0.0001f);
        Vector3 BCxBP = checkForZeros(Mult(triangle.p3 - triangle.p2, p - triangle.p2), 0.0001f);
        Vector3 CAxCP = checkForZeros(Mult(triangle.p1 - triangle.p3, p - triangle.p3), 0.0001f);

        return ((ABxAP.Z >= 0 && BCxBP.Z >= 0 && CAxCP.Z >= 0)
        || (ABxAP.Z < 0 && BCxBP.Z < 0 && CAxCP.Z < 0));
    }

    // Не пошел ошибка перессечений стык в стык
    static bool PointTriangle(Vector3[] TriangleVectors, Vector3 P)
    {
        bool SameSide(Vector3 p1, Vector3 p2, Vector3 a, Vector3 b)
        {
            Vector3 cp1 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p1, a));
            Vector3 cp2 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p2, a));
            if (Vector3.Dot(cp1, cp2) >= 0) return true;
            return false;

        }
        Vector3
            A = checkForZeros(TriangleVectors[0], 0.001f),
            B = checkForZeros(TriangleVectors[1], 0.001f),
            C = checkForZeros(TriangleVectors[2], 0.001f);
        P = checkForZeros(P, 0.001f);
        if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
        {
            Vector3 vc1 = Vector3.Cross(Vector3.Subtract(A, B), Vector3.Subtract(A, C));
            if (Math.Abs(Vector3.Dot(Vector3.Subtract(A, P), vc1)) <= .01f)
                return true;
        }

        return false;
    }


    private static bool PointInTriangle(Triangle triangle, Vector3 P)
    {
        if (triangle.p1 == P || triangle.p2 == P || triangle.p3 == P) return true;
        float x, y, z, x1, x2, x3, y1, y2, y3, z1, z2, z3, x31, x21, y31, y21, z31, z21, alpha = 0.0f, beta = 0.0f;//Луче, конечно double, но float быстрее
        x1 = triangle.p1.X; x2 = triangle.p2.X; x3 = triangle.p3.X;//Всю эту ерунду можно было бы и не писать, только можно запутаться с индексами.
        y1 = triangle.p1.Y; y2 = triangle.p2.Y; y3 = triangle.p3.Y;
        z1 = triangle.p1.Z; z2 = triangle.p2.Z; z3 = triangle.p3.Z;
        x31 = x3 - x1; x21 = x2 - x1;//А вот это уже нужно.
        y31 = y3 - y1; y21 = y2 - y1;
        z31 = z3 - z1; z21 = z2 - z1;
        x = P.X; y = P.Y; z = P.Z;
        if ((x21 * y31 - x31 * y21) != 0)
        {//Если известна nz (z-компонента нормали треуг), то можно if(nz!=0)
            beta = ((y - y2) * x31 - (x - x2) * y31) / (x21 * y31 - x31 * y21);//или разделить на nz
            alpha = (x - x2) / (beta * x31) + x21 / x31;
        }
        else if ((z21 * y31 - z31 * y21) != 0)
        {//Если известна nx (x-компонента нормали треуг), то можно if(nx!=0)
            beta = ((y - y2) * z31 - (z - z2) * y31) / (z21 * y31 - z31 * y21);//или разделить на nx
            alpha = (z - z2) / (beta * z31) + z21 / z31;
        }
        else if ((z21 * x31 - z31 * x21) != 0)
        {//Если известна ny (y-компонента нормали треуг), то можно if(ny!=0)
            beta = ((x - x2) * z31 - (z - z2) * x31) / (z21 * x31 - z31 * x21);//или разделить на ny
            alpha = (x - x2) / (beta * x31) + x21 / x31;
        }

        if (alpha > -0.001f && alpha < 1.001f && beta > -0.001f && beta < 1.001f) //Тут можно поиграть со значениями для float и double
            return true;
        else return false;


        /*
        bool SameSide(Vector3 p1, Vector3 p2, Vector3 a, Vector3 b)
        {
        Vector3 cp1 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p1, a));
        Vector3 cp2 = Vector3.Cross(Vector3.Subtract(b, a), Vector3.Subtract(p2, a));
        if (Vector3.Dot(cp1, cp2) >= 0) return true;
        return false;

        }
        Vector3 A = checkForZeros(triangle.p1, 0.001f), B = checkForZeros(triangle.p2, 0.001f), C = checkForZeros(triangle.p3, 0.001f);
        P = checkForZeros(P, 0.001f);
        if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
        {
        Vector3 vc1 = Vector3.Cross(Vector3.Subtract(A, B), Vector3.Subtract(A, C));
        if (Math.Abs(Vector3.Dot(Vector3.Subtract(A, P), vc1)) <= .01f)
        return true;
        }

        return false;
        */
    }

    private static Vector3[] PointCheckInside(in Triangle triangle1, in Triangle triangle2)
    {
        List<Vector3> tr = new List<Vector3>();


        if (getPoint(triangle1.p1, triangle1.p2, triangle1.p3, triangle2.p1, triangle2.p2, out Vector3 V1)
        && PointInTriangle(triangle1, V1))
        {
            tr.Add(V1);
        }

        if (getPoint(triangle1.p1, triangle1.p2, triangle1.p3, triangle2.p2, triangle2.p3, out Vector3 V2)
        && PointInTriangle(triangle1, V2))
        {
            tr.Add(V2);
        }

        if (getPoint(triangle1.p1, triangle1.p2, triangle1.p3, triangle2.p1, triangle2.p3, out Vector3 V3)
        && PointInTriangle(triangle1, V3))
        {
            tr.Add(V3);
        }
        return tr.ToArray();
    }

    // Находит все перечения.
    // не нужно
    private static Vector3[] PointCheckAll(in Triangle triangle1, in Triangle triangle2)
    {
        List<Vector3> tr = new List<Vector3>();
        if (getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                out Vector3 V1
                )) tr.Add(V1);
        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p2.X, triangle2.p2.Y, triangle2.p2.Z),
                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
            out Vector3 V2
            )) tr.Add(V2);
        if (
            getPoint(
                new Vector3(triangle1.p1.X, triangle1.p1.Y, triangle1.p1.Z), //плоскость 
                new Vector3(triangle1.p2.X, triangle1.p2.Y, triangle1.p2.Z),
                new Vector3(triangle1.p3.X, triangle1.p3.Y, triangle1.p3.Z),

                new Vector3(triangle2.p3.X, triangle2.p3.Y, triangle2.p3.Z),
                new Vector3(triangle2.p1.X, triangle2.p1.Y, triangle2.p1.Z),
            out Vector3 V3
            )) tr.Add(V3);
        return tr.ToArray();
    }

    // это проверка на совместимость кубов 
    private static bool IntersectionCheck(in Triangle triangle1, in Triangle triangle2)
    {

        if (
            (Math.Min(Math.Min(triangle1.p1.X, triangle1.p2.X), triangle1.p3.X) <= Math.Min(Math.Min(triangle2.p1.X, triangle2.p2.X), triangle2.p3.X)
            && Math.Min(Math.Min(triangle2.p1.X, triangle2.p2.X), triangle2.p3.X) <= Math.Max(Math.Max(triangle1.p1.X, triangle1.p2.X), triangle1.p3.X) &&
            (Math.Min(Math.Min(triangle1.p1.Y, triangle1.p2.Y), triangle1.p3.Y) <= Math.Min(Math.Min(triangle2.p1.Y, triangle2.p2.Y), triangle2.p3.Y)
            && Math.Min(Math.Min(triangle2.p1.Y, triangle2.p2.Y), triangle2.p3.Y) <= Math.Max(Math.Max(triangle1.p1.Y, triangle1.p2.Y), triangle1.p3.Y)) &&
            (Math.Min(Math.Min(triangle1.p1.Z, triangle1.p2.Z), triangle1.p3.Z) <= Math.Min(Math.Min(triangle2.p1.Z, triangle2.p2.Z), triangle2.p3.Z)
            && Math.Min(Math.Min(triangle2.p1.Z, triangle2.p2.Z), triangle2.p3.Z) <= Math.Max(Math.Max(triangle1.p1.Z, triangle1.p2.Z), triangle1.p3.Z))
            ))
        {
            return false;
        }

        if (
                (Math.Min(Math.Min(triangle1.p1.X, triangle1.p2.X), triangle1.p3.X) <= Math.Max(Math.Max(triangle2.p1.X, triangle2.p2.X), triangle2.p3.X)
                && Math.Max(Math.Max(triangle2.p1.X, triangle2.p2.X), triangle2.p3.X) <= Math.Max(Math.Max(triangle1.p1.X, triangle1.p2.X), triangle1.p3.X) &&
                (Math.Min(Math.Min(triangle1.p1.Y, triangle1.p2.Y), triangle1.p3.Y) <= Math.Max(Math.Max(triangle2.p1.Y, triangle2.p2.Y), triangle2.p3.Y)
                && Math.Max(Math.Max(triangle2.p1.Y, triangle2.p2.Y), triangle2.p3.Y) <= Math.Max(Math.Max(triangle1.p1.Y, triangle1.p2.Y), triangle1.p3.Y)) &&
                (Math.Min(Math.Min(triangle1.p1.Z, triangle1.p2.Z), triangle1.p3.Z) <= Math.Max(Math.Max(triangle2.p1.Z, triangle2.p2.Z), triangle2.p3.Z)
                && Math.Max(Math.Max(triangle2.p1.Z, triangle2.p2.Z), triangle2.p3.Z) <= Math.Max(Math.Max(triangle1.p1.Z, triangle1.p2.Z), triangle1.p3.Z))
                ))
        {
            return false;
        }


        return true;
    }

    private static Vector3? CHECK(in Vector3 p1, in Vector3 p2, in Vector3 p3, in Vector3 p4)
    {
        // Проекция на плоскость ОZ.
        bool b1 = CutIntersecion(
        new Vector2(p1.X, p1.Y),
        new Vector2(p2.X, p2.Y),
        new Vector2(p3.X, p3.Y),
        new Vector2(p4.X, p4.Y),
        out float x1, out float y1);
        bool b2 = CutIntersecion(
        new Vector2(p1.Y, p1.Z),
        new Vector2(p2.Y, p2.Z),
        new Vector2(p3.Y, p3.Z),
        new Vector2(p4.Y, p4.Z),
        out float y2, out float z1);
        bool b3 = CutIntersecion(
        new Vector2(p1.X, p1.Z),
        new Vector2(p2.X, p2.Z),
        new Vector2(p3.X, p3.Z),
        new Vector2(p4.X, p4.Z),
        out float x2, out float z2);

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

    public static Int16 Intersection(in Triangle triangle1, in Triangle triangle2, out List<Vector3> TransformedTriangle1, out List<Vector3> TransformedTriangle2)
    {
        TransformedTriangle1 = new List<Vector3>();
        TransformedTriangle2 = new List<Vector3>();

        // проверка на пересечений ариалов триугольников
        //if (IntersectionCheck(triangle1, triangle2))
        //{
        //    return false;
        //}

        // нахождения взаимных точек для двух колекций треугольников
        Vector3[] PointTriangle1 = PointCheckInside(triangle1, triangle2);

        Vector3[] PointTriangle2 = PointCheckInside(triangle2, triangle1);

        //Console.WriteLine(PointTriangle1.Length + " " + PointTriangle2.Length);

        TransformedTriangle1.Add(triangle1.p1);
        TransformedTriangle1.Add(triangle1.p2);
        TransformedTriangle1.Add(triangle1.p3);

        TransformedTriangle2.Add(triangle2.p1);
        TransformedTriangle2.Add(triangle2.p2);
        TransformedTriangle2.Add(triangle2.p3);

        //// Проверка на треугольник
        //if (PointTriangle1.Length == 3 && PointTriangle2.Length == 3)
        //{
        //    return true;
        //}

        //// Проверка 3 и 4 исключение
        //if ((PointTriangle1.Length == 3 && PointTriangle2.Length == 4) || (PointTriangle2.Length == 3 && PointTriangle1.Length == 4))
        //{
        //    return false;
        //}
        //if (PointTriangle1.Length == 3 || PointTriangle2.Length == 3)
        //{
        //    return true;
        //}

        //// Запись 
        if (PointTriangle1.Length == 2)
        {
            TransformedTriangle1.Add(PointTriangle1[0]);
            TransformedTriangle1.Add(PointTriangle1[1]);

            TransformedTriangle2.Add(PointTriangle1[0]);
            TransformedTriangle2.Add(PointTriangle1[1]);
            if (PointTriangle1.Length == PointTriangle2.Length)
            {
                return 4;
            }
            return 1;
        }

        if (PointTriangle2.Length == 2)
        {
            TransformedTriangle1.Add(PointTriangle2[0]);
            TransformedTriangle1.Add(PointTriangle2[1]);

            TransformedTriangle2.Add(PointTriangle2[0]);
            TransformedTriangle2.Add(PointTriangle2[1]);
            if (PointTriangle1.Length == PointTriangle2.Length)
            {
                return 4;
            }
            return 1;
        }

        // если пересечений не оказалось
        if (PointTriangle1.Length == 0 || PointTriangle2.Length == 0)
        {
            return 2;
        }

        if (PointTriangle1.Length == 1 && PointTriangle2.Length == 1)
        {
            TransformedTriangle1.Add(PointTriangle1[0]);
            TransformedTriangle2.Add(PointTriangle1[0]);

            TransformedTriangle1.Add(PointTriangle2[0]);
            TransformedTriangle2.Add(PointTriangle2[0]);

            return 3;
        }
        //if (PointTriangle1.Length == 1 && PointTriangle2.Length == 1)
        //{
        //    TransformedTriangle1.Add(PointTriangle1[0]);

        //    TransformedTriangle2.Add(PointTriangle1[0]);

        //    Vector3? oldpoint = CHECK(PointTriangle1[0], PointTriangle2[0], triangle1.p1, triangle1.p2);
        //    if (oldpoint != null)
        //    {
        //        TransformedTriangle1.Add(oldpoint.Value);
        //        TransformedTriangle2.Add(oldpoint.Value);
        //        //return true;
        //    }

        //    oldpoint = CHECK(PointTriangle1[0], PointTriangle2[0], triangle1.p3, triangle1.p2);
        //    if (oldpoint != null)
        //    {
        //        TransformedTriangle1.Add(oldpoint.Value);
        //        TransformedTriangle2.Add(oldpoint.Value);
        //        //return true;
        //    }

        //    oldpoint = CHECK(PointTriangle1[0], PointTriangle2[0], triangle1.p1, triangle1.p3);
        //    if (oldpoint != null)
        //    {
        //        TransformedTriangle1.Add(oldpoint.Value);
        //        TransformedTriangle2.Add(oldpoint.Value);
        //        //return true;
        //    }

        //}

        return 0;

        // 0) ошибка
        // 1) 2 пересечения
        // 2) Нету пересечений - ошибки нету
        // 3) Пересечения на угле
        // 4) Пересечения в двух местах
    }

}