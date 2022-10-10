using System.Drawing;

float[,] first1 = new float[,]
{
    { -5,  5, 0 },
    { -5, -5, 0 },
    {  5, -5, 0 },
};

float[,] first2 = new float[,]
{
    { -4, 4, -1 },
    {  0, 0, -1 },
    {  0, 0,  4 },
};


float[] PlaneEquation1 = SurfaceCalculation(
    first1[0, 0], first1[0, 1], first1[0, 2],
    first1[1, 0], first1[1, 1], first1[1, 2],
    first1[2, 0], first1[2, 1], first1[2, 2]
    );
// 0 0 100 0

float[] PlaneEquation2 = SurfaceCalculation(
    first2[0, 0], first2[0, 1], first2[0, 2],
    first2[1, 0], first2[1, 1], first2[1, 2],
    first2[2, 0], first2[2, 1], first2[2, 2]
    );
// -20 -20 0 0

float[,] StraightPlanes = PlaneIntersections(PlaneEquation1, PlaneEquation2);
// 0 -20
// 0 -20
// 100 -100


float[,] point = points(first1, first1, StraightPlanes);
// 100 100 600
// -100 -100 -400




Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first1[0, 0], first1[0, 1], first1[0, 2] },
    new float[] { first1[1, 0], first1[1, 1], first1[1, 2] }
    );
Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first1[2, 0], first1[2, 1], first1[2, 2] },
    new float[] { first1[1, 0], first1[1, 1], first1[1, 2] }
    );
Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first1[2, 0], first1[2, 1], first1[2, 2] },
    new float[] { first1[0, 0], first1[0, 1], first1[0, 2] }
    );

Console.WriteLine();

Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first2[0, 0], first2[0, 1], first2[0, 2] },
    new float[] { first2[1, 0], first2[1, 1], first2[1, 2] }
    );
Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first2[2, 0], first2[2, 1], first2[2, 2] },
    new float[] { first2[1, 0], first2[1, 1], first2[1, 2] }
    );
Intersection(
    new float[] { point[0, 0], point[0, 1], point[0, 2] },
    new float[] { point[1, 0], point[1, 1], point[1, 2] },
    new float[] { first2[2, 0], first2[2, 1], first2[2, 2] },
    new float[] { first2[0, 0], first2[0, 1], first2[0, 2] }
    );



Console.ReadLine();

float[] SurfaceCalculation(
                       float x1, float y1, float z1,
                       float x2, float y2, float z2,
                       float x3, float y3, float z3)
{
    float[] PlaneEquation = new float[4];

    double k2 = x1 - x2;

    //-------------------
    PlaneEquation[0] = y1 * (z2 - z3) + y2 * (z3 - z1) + y3 * (z1 - z2);
    PlaneEquation[1] = z1 * (x2 - x3) + z2 * (x3 - x1) + z3 * (x1 - x2);
    PlaneEquation[2] = x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2);
    PlaneEquation[3] = -(x1 * (y2 * z3 - y3 * z2) + x2 * (y3 * z1 - y1 * z3) + x3 * (y1 * z2 - y2 * z1));
    //-----------------

    return PlaneEquation;
}

float[,] PlaneIntersections(
    float[] PlaneEquationOne,
    float[] PlaneEquationTwe)
{
    return new float[,]
    {
        {PlaneEquationOne[0],  PlaneEquationTwe[0] - PlaneEquationOne[0]},
        {PlaneEquationOne[1],  PlaneEquationTwe[1] - PlaneEquationOne[1]},
        {PlaneEquationOne[2],  PlaneEquationTwe[2] - PlaneEquationOne[2]},
    };
}

float DirectConstant(float x, float one, float twe)
{
    return x - one / twe;
}
float DirectConstantXYZ(float cons, float one, float twe)
{
    return cons * twe + one;
}


float[] MinMax(float[,] first1, float[,] first2)
{
    float[] x = new float[] { 
        first1[0, 0], first1[1, 0], first1[2, 0],
        first2[0, 0], first2[1, 0], first2[2, 0]
    };

    float[] MinMax = new float[2] { int.MaxValue , int.MinValue };

    for (int i = 0; i < x.Length; i++)
    {
        if (MinMax[0] > x[i])
        {
            MinMax[0] = x[i];
        }
        if (MinMax[1] < x[i])
        {
            MinMax[1] = x[i];
        }
    }
    return MinMax;
}

float[,] points(float[,] first1, float[,] first2, float[,] StraightPlanes)
{
    float[] MiMa = MinMax(first1, first2);

    float const1 = DirectConstant(MiMa[0], StraightPlanes[0, 0], StraightPlanes[0, 1]);
    float const2 = DirectConstant(MiMa[1], StraightPlanes[0, 0], StraightPlanes[0, 1]);

    float[,] XYZ = new float[,] {
        { 
            DirectConstantXYZ(const1, StraightPlanes[0, 0], StraightPlanes[0, 1]),
            DirectConstantXYZ(const1, StraightPlanes[1, 0], StraightPlanes[1, 1]),
            DirectConstantXYZ(const1, StraightPlanes[2, 0], StraightPlanes[2, 1])
        },
                {
            DirectConstantXYZ(const2, StraightPlanes[0, 0], StraightPlanes[0, 1]),
            DirectConstantXYZ(const2, StraightPlanes[1, 0], StraightPlanes[1, 1]),
            DirectConstantXYZ(const2, StraightPlanes[2, 0], StraightPlanes[2, 1])
        },
    };

    return XYZ;
}

float[] Intersection(float[] A, float[] B, float[] C, float[] D)
{
    float xo = A[0], yo = A[1], zo = A[2];
    float p = B[0] - A[0], q = B[1] - A[1], r = B[2] - A[2];

    float x1 = C[0], y1 = C[1], z1 = C[2];
    float p1 = D[0] - C[0], q1 = D[1] - C[1], r1 = D[2] - C[2];

    float x = (xo * q * p1 - x1 * q1 * p - yo * p * p1 + y1 * p * p1) /
        (q * p1 - q1 * p);
    float y = (yo * p * q1 - y1 * p1 * q - xo * q * q1 + x1 * q * q1) /
        (p * q1 - p1 * q);
    float z = (zo * q * r1 - z1 * q1 * r - yo * r * r1 + y1 * r * r1) /
        (q * r1 - q1 * r);

    if (double.IsNaN(x))
    {
        Console.WriteLine("нету");
        return new float[] { float.NaN, float.NaN, float.NaN };
    }
    if (double.IsNaN(y))
    {
        Console.WriteLine("нету");
        return new float[] { float.NaN, float.NaN, float.NaN };
    }
    if (double.IsNaN(z))
    {
        Console.WriteLine("нету");
        return new float[] { float.NaN, float.NaN, float.NaN };
    }

    Console.WriteLine("{0} {1} {2}", x, y, z);
    return new float[] { x, y, z };
}