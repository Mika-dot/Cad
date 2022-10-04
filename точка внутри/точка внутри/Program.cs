using System.Text;

bool Dot(int x, int y, int[] CircuitX, int[] CircuitY)
{
    int npol = CircuitX.GetLength(0);
    int j = npol - 1;
    bool c = false;
    for (var i = 0; i < npol; i++)
    {
        if ((((CircuitY[i] <= y) && (y < CircuitY[j])) || ((CircuitY[j] <= y) && (y < CircuitY[i]))) &&
        (x > (CircuitX[j] - CircuitX[i]) * (y - CircuitY[i]) / (CircuitY[j] - CircuitY[i]) + CircuitX[i]))
        {
            c = !c;
         }
        j = i;
    }
    return c;
} // Точка принадлежности

int[] Dimensions(int[] CircuitX, int[] CircuitY)
{
    int[] MinMax = new int[] { int.MaxValue, int.MaxValue, int.MinValue, int.MinValue };

    for (int i = 0; i < CircuitX.GetLength(0); i++)
    {
        if (MinMax[0] > CircuitX[i])
        {
            MinMax[0] = CircuitX[i];
        }
        if (MinMax[2] < CircuitX[i])
        {
            MinMax[2] = CircuitX[i];
        }

        if (MinMax[1] > CircuitY[i])
        {
            MinMax[1] = CircuitY[i];
        }
        if (MinMax[3] < CircuitY[i])
        {
            MinMax[3] = CircuitY[i];
        }
    }
    return MinMax;
} // Точки контура мин. и макс.

void Extrusion(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int Height, int[] CircuitX, int[] CircuitY)
{
    int[] dimensions = Dimensions(CircuitX, CircuitY);

    for (int x = dimensions[0]; x < dimensions[2]; x++)
    {
        for (int y = dimensions[1]; y < dimensions[3]; y++)
        {
            if (Dot(x, y, CircuitX, CircuitY))
            {
                for (int z = 0; z < Height; z++)
                {
                    Array.Resize(ref ExtruderX, ExtruderX.Length + 1);
                    ExtruderX[ExtruderX.Length - 1] = x;

                    Array.Resize(ref ExtruderY, ExtruderY.Length + 1);
                    ExtruderY[ExtruderY.Length - 1] = y;

                    Array.Resize(ref ExtruderZ, ExtruderZ.Length + 1);
                    ExtruderZ[ExtruderZ.Length - 1] = z;
                }
            }
        }
    }
}

void AxesRotation(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int[] Corners)
{
    for (int move = 0; move < ExtruderX.GetLength(0); move++)
    {
        int[] dot = new int[] { ExtruderX[move], ExtruderY[move], ExtruderZ[move] };
        dot = MX(dot, Corners[0]);
        dot = MY(dot, Corners[1]);
        dot = MZ(dot, Corners[2]);

        ExtruderX[move] = dot[0];
        ExtruderY[move] = dot[1];
        ExtruderZ[move] = dot[2];
    }
}

void MovingPoints(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int[] Moving)
{
    for (int moving = 0; moving < ExtruderX.GetLength(0); moving++)
    {
        ExtruderX[moving] += Moving[0];
        ExtruderY[moving] += Moving[1];
        ExtruderZ[moving] += Moving[2];
    }
}

void AdditionWorld(
    int[] CircuitX, int[] CircuitY, int Height, int[] Corners, int[] Moving, int Type,
    ref int[] worldX, ref int[] worldY, ref int[] worldZ
    )
{
    int[] ExtruderX = new int[0]; // Значение вокселей
    int[] ExtruderY = new int[0];
    int[] ExtruderZ = new int[0];

    Extrusion(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Height, CircuitX, CircuitY);
    AxesRotation(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Corners);
    MovingPoints(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Moving);

    for (int i = 0; i < ExtruderX.GetLength(0); i++)
    {
        bool Existence = true;
        for (int MATW = 0; MATW < worldX.GetLength(0); MATW++)
        {
            if (
                ExtruderX[i] == worldX[MATW] &&
                ExtruderY[i] == worldY[MATW] &&
                ExtruderZ[i] == worldZ[MATW]
                )
            {
                Existence = false;
                if (Type == 0)
                {
                    Delete(ref worldX, MATW);
                    Delete(ref worldY, MATW);
                    Delete(ref worldZ, MATW);
                }
                break;
            }
        }
        if (Existence && (Type != 0))
        {
            Array.Resize(ref worldX, worldX.Length + 1);
            worldX[worldX.Length - 1] = ExtruderX[i];

            Array.Resize(ref worldY, worldY.Length + 1);
            worldY[worldY.Length - 1] = ExtruderY[i];

            Array.Resize(ref worldZ, worldZ.Length + 1);
            worldZ[worldZ.Length - 1] = ExtruderZ[i];
        }
    }
}

float TemperatureVoxel(ref float[] worldID, int[] worldX, int[] worldY, int[] worldZ, int[,] Temperature )
{
    Array.Resize(ref worldID, worldX.Length);
    float ModuleMore = 0;
    for (int i = 0; i < worldID.GetLength(0); i++)
    {
        float temper = 0;
        for (int t = 0; t < Temperature.GetLength(0); t++)
        {
            temper += Temperature[t, 3] / GeometricDistance(
                new int[]
                {
                Temperature[t, 0] , Temperature[t, 1], Temperature[t, 2]
                },
                new int[]
                {
                worldX[i], worldY[i], worldZ[i]
                }
            );
        }
        worldID[i] = temper;
        if (ModuleMore < Math.Abs(temper))
        {
            ModuleMore = temper;
        }
    }
    return ModuleMore;
}

void Gcode(string file, ref int[] worldX, ref int[] worldY, ref int[] worldZ)
{
    StreamWriter f = new StreamWriter(file + ".gcode");
    f.WriteLine("G21 G40 G49 G53 G80 G90 G17");
    f.WriteLine("G90");
    f.WriteLine("M82");
    f.WriteLine("M140 S80");
    f.WriteLine("M104 S200");
    f.WriteLine("G28");
    f.WriteLine("G29");

    f.WriteLine("G0 X" + worldX[0] + " Y" + worldY[0] + " Z" + worldZ[0] + ";");


    f.WriteLine("G0 X" + worldX[0] + " Y" + worldY[0] + " Z" + worldZ[0] + ";");

    for (int i = 1; i < worldX.GetLength(0); i++)
    {
        int[] Start = new int[] { worldX[i - 1], worldY[i - 1], worldZ[i - 1] };
        int[] end = new int[] { worldX[i], worldY[i], worldZ[i] };

        if (GeometricDistance(Start, end) == 1)
        {
            f.WriteLine("G1 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + " E2;");
        }
        else
        {
            f.WriteLine("G0 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + ";");
            f.WriteLine("G1 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + " E1;");
        }


    }

    f.WriteLine("M107;");
    f.WriteLine("M104 S0;");
    //f.WriteLine("G0 Z" + (worldZ[worldZ.GetLength(0) - 1] + 100) + ";");
    f.WriteLine("M140 S0;");
    f.WriteLine("M84;");
    f.Close();
}

//----------------------------------------------------------

void Delete(ref int[] array, int index)
{
    if (index < array.Length && index >= 0)
    {
        int[] array2 = new int[array.Length - 1];
        for (int i = 0, j = 0; i < array.Length; i++)
        {
            if (i == index) continue;
            array2[j++] = array[i];
        }
        array = array2;
    }
} // Удаление по индексу

//----------------------------------------------------------

int[] MX(int[] A, int angle){
    int[] B = new int[3];
    double q = angle * (Math.PI / 180);

    B[0] = A[0];
    B[1] = (int)(A[1] * Math.Cos(q) + A[2] * Math.Sin(q));
    B[2] = (int)((-1) * A[1] * Math.Sin(q) + A[2] * Math.Cos(q));

    return B;
}
int[] MY(int[] A, int angle)
{
    int[] B = new int[3];
    double q = angle * (Math.PI / 180);

    B[0] = (int)(A[0] * Math.Cos(q) + A[2] * Math.Sin(q));
    B[1] = A[1];
    B[2] = (int)((-1) * A[0] * Math.Sin(q) + A[2] * Math.Cos(q));

    return B;
}
int[] MZ(int[] A, int angle)
{
    int[] B = new int[3];
    double q = angle * (Math.PI / 180);

    B[0] = (int)(A[0] * Math.Cos(q) - A[1] * Math.Sin(q));
    B[1] = (int)(A[0] * Math.Sin(q) + A[1] * Math.Cos(q));
    B[2] = A[2];

    return B;
}

float GeometricDistance(int[] dot, int[] temp)
{
    return (float)Math.Sqrt(Math.Pow(dot[0] - temp[0], 2) + Math.Pow(dot[1] - temp[1], 2) + Math.Pow(dot[2] - temp[2], 2));
}

//----------------------------------------------------------


int[] CircuitX = new int[] { 0, 10, 10 }; // Массив X-координат полигона
int[] CircuitY = new int[] { 10, 0, 10 }; // Массив Y-координат полигона
int Height = 5; // Высота выдавливание
int Tupe = 1;

int[] Corners = new int[] { 0, 0, 0 }; // Кручение вокселей
int[] Moving = new int[] { 0, 0, -1 }; // Перемещение

int[] worldX = new int[0]; // Значение вокселей
int[] worldY = new int[0];
int[] worldZ = new int[0];
float[] worldID = new float[0];

int[,] Temperature = new int[,] {
    { 0, 0, 0, -10 },
    { 10, 10, 10, 10 }
};


AdditionWorld(
    new int[] { 0, 10, 10, 0 }, // контур по Х
    new int[] { 0, 0, 10, 10 }, // контур по У
    10, // экструзия
    new int[] { 0, 0, 0 }, // Кручение вокселей
    new int[] { 0, 0, 0 }, // Перемещение
    1, // тип экструзии

    ref worldX, ref worldY, ref worldZ
);

AdditionWorld(
    new int[] { 0, 5, 5, 0 }, // контур по Х
    new int[] { 0, 0, 5, 5 }, // контур по У
    10, // экструзия
    new int[] { 0, 0, 0 }, // Кручение вокселей
    new int[] { 2, 2, 2 }, // Перемещение
    0, // тип экструзии

    ref worldX, ref worldY, ref worldZ
);

AdditionWorld(
    new int[] { 0, 10, 10 }, // контур по Х
    new int[] { 10, 0, 10 }, // контур по У
    3, // экструзия
    new int[] { 0, 0, 0 }, // Кручение вокселей
    new int[] { 0, 0, 0 }, // Перемещение
    0, // тип экструзии

    ref worldX, ref worldY, ref worldZ
);

//float temp = TemperatureVoxel(ref worldID, worldX, worldY, worldZ, Temperature);

Gcode("test", ref worldX, ref worldY, ref worldZ);





Console.ReadKey();