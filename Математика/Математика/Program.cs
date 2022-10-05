using System.Numerics;

Random rnd = new Random();


float[,,] figure = new float[,,]
{

    { {0, 0, 0 }, {1, 0, 0 }, {0, 1, 0 } },
    { {1, 0, 0 }, {0, 1, 0 }, {1, 1, 0 } },

    { {0, 0, 0 }, {0, 0, 1 }, {1, 0, 0 } },
    { {1, 0, 0 }, {1, 0, 1 }, {0, 0, 1 } },

    { {0, 0, 0 }, {0, 1, 0 }, {0, 0, 1 } },
    { {0, 0, 1 }, {0, 1, 0 }, {0, 1, 1 } },

    { {0, 0, 1 }, {1, 0, 1 }, {0, 1, 1 } },
    { {1, 1, 1 }, {1, 0, 1 }, {0, 1, 1 } },

    { {1, 0, 0 }, {1, 0, 1 }, {1, 1, 0 } },
    { {1, 1, 1 }, {1, 0, 1 }, {1, 1, 0 } },

    { {1, 1, 1 }, {0, 1, 1 }, {1, 1, 0 } },
    { {1, 1, 0 }, {0, 1, 1 }, {0, 1, 0 } },
};

int a = rnd.Next();
int b = rnd.Next();
int c = rnd.Next();



for (int j = 0; j < 100; j++)
{
    int y = 0;

    for (int i = 0; i < figure.GetLength(0); i++)
    {

        if (getPoint(
        new Vector3(figure[i, 0, 0], figure[i, 0, 1], figure[i, 0, 2]),//плоскость//{ }, { }, { }
        new Vector3(figure[i, 1, 0], figure[i, 1, 1], figure[i, 1, 2]),
        new Vector3(figure[i, 2, 0], figure[i, 2, 1], figure[i, 2, 2]),

        //new Vector3(a, b, c),//две точки начало
        new Vector3(10f, 10f, 10f),
        new Vector3(a, b, c),
        out var V
        ))
        {
            y++;
        }

    }

    //Console.WriteLine(y / 2);

    y /= 2;
    if (y % 2 != 0)
    {
        Console.WriteLine("Пиздец");
    }
}



bool getPoint(Vector3 A, Vector3 B, Vector3 C, Vector3 X, Vector3 Y, out Vector3 result)
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
    return !(O.X > Math.Max(X.X, Y.X) || O.X < Math.Min(X.X, Y.X)
    || O.Y > Math.Max(X.Y, Y.Y) || O.Y < Math.Min(X.Y, Y.Y)
    || O.Z > Math.Max(X.Z, Y.Z) || O.Z < Math.Min(X.Z, Y.Z));
}
