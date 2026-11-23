using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;

namespace OpenGL_lesson_CSharp
{
    // Один пиксель каждого из пяти изображений образует один ФВ-воксель.
    class MImages
    {
        public const int X = 25, Y = 17, Z = 8, Count = X * Y * Z;
        const int MaxColor = 16777215;
        public double[,] Voxel = new double[Count, 5];
        public int[,] Pixel = new int[Count, 5];

        public static int Index(int x, int y, int z)
        {
            return (z * Y + y) * X + x;
        }

        public static Vector3 Point(int i)
        {
            return new Vector3(i % X, i / X % Y, i / (X * Y));
        }

        public void Write(int i, double f, double gx, double gy, double gz)
        {
            Vector3 p = Point(i);
            double[] a = { -gx, -gy, -gz, 1, gx * p.X + gy * p.Y + gz * p.Z - f };
            double norm = 0;
            for (int k = 0; k < 5; k++) norm += a[k] * a[k];
            norm = Math.Sqrt(norm);
            for (int k = 0; k < 5; k++)
                Pixel[i, k] = (int)Math.Round((a[k] / norm + 1) * MaxColor / 2);
        }

        public void Save(string folder)
        {
            Directory.CreateDirectory(folder);
            for (int z = 0; z < Z; z++)
            for (int k = 0; k < 5; k++)
                using (Bitmap image = new Bitmap(X, Y, PixelFormat.Format24bppRgb))
                {
                    for (int x = 0; x < X; x++)
                    for (int y = 0; y < Y; y++)
                        image.SetPixel(x, y, Color.FromArgb(Pixel[Index(x, y, z), k]));
                    image.Save(FileName(folder, z, k), ImageFormat.Bmp);
                }
        }

        public static MImages Load(string folder)
        {
            MImages images = new MImages();
            for (int z = 0; z < Z; z++)
            for (int k = 0; k < 5; k++)
                using (Bitmap image = new Bitmap(FileName(folder, z, k)))
                {
                    if (image.Width != X || image.Height != Y)
                        throw new InvalidDataException("Размер М-изображения не совпадает с сеткой.");
                    for (int x = 0; x < X; x++)
                    for (int y = 0; y < Y; y++)
                    {
                        int i = Index(x, y, z);
                        int rgb = image.GetPixel(x, y).ToArgb() & MaxColor;
                        images.Pixel[i, k] = rgb;
                        images.Voxel[i, k] = 2.0 * rgb / MaxColor - 1;
                    }
                }
            return images;
        }

        static string FileName(string folder, int z, int k)
        {
            return Path.Combine(folder, "Z" + z + "_M" + k + ".bmp");
        }

        public double Value(int i, Vector3 p)
        {
            return -(Voxel[i, 0] * p.X + Voxel[i, 1] * p.Y +
                     Voxel[i, 2] * p.Z + Voxel[i, 4]) / Voxel[i, 3];
        }

        public Vector3 Gradient(int i)
        {
            return new Vector3((float)(-Voxel[i, 0] / Voxel[i, 3]),
                               (float)(-Voxel[i, 1] / Voxel[i, 3]),
                               (float)(-Voxel[i, 2] / Voxel[i, 3]));
        }

        public double Between(int i, int j, double t)
        {
            Vector3 p = Vector3.Lerp(Point(i), Point(j), (float)t);
            return (1 - t) * Value(i, p) + t * Value(j, p);
        }

        // В разрезе смешиваем функции восьми соседних вокселей.
        public double At(Vector3 p)
        {
            int x = Math.Min(X-2, Math.Max(0, (int)p.X));
            int y = Math.Min(Y-2, Math.Max(0, (int)p.Y));
            int z = Math.Min(Z-2, Math.Max(0, (int)p.Z));
            double tx = p.X-x, ty = p.Y-y, tz = p.Z-z, sum = 0;
            for (int c=0; c<2; c++)
            for (int b=0; b<2; b++)
            for (int a=0; a<2; a++)
                sum += Value(Index(x+a,y+b,z+c),p) *
                    (a==0 ? 1-tx : tx) * (b==0 ? 1-ty : ty) * (c==0 ? 1-tz : tz);
            return sum;
        }
        public static MImages Build(string folder, Func<Vector3, double> function)
        {
            MImages images = new MImages();
            const float h = 0.01f;
            for (int i = 0; i < Count; i++)
            {
                Vector3 p = Point(i);
                double gx = (function(p + h * Vector3.UnitX) - function(p - h * Vector3.UnitX)) / (2 * h);
                double gy = (function(p + h * Vector3.UnitY) - function(p - h * Vector3.UnitY)) / (2 * h);
                double gz = (function(p + h * Vector3.UnitZ) - function(p - h * Vector3.UnitZ)) / (2 * h);
                images.Write(i, function(p), gx, gy, gz);
            }
            images.Save(folder);
            return Load(folder);
        }
    }

    class FvmRoute
    {
        public static readonly int A = MImages.Index(1, 8, 3), B = MImages.Index(23, 8, 3);
        public MImages Free, Ground, Zone;
        public MImages Surface, ClayLayer, RockLayer, PipeBody, FoundationBody;
        public int[] Material = new int[MImages.Count];
        public List<string[]>[] StepTable = new List<string[]>[2];
        public MImages[] Potential = new MImages[2];
        public List<int>[] PathNodes = new List<int>[2];
        public double[] Price = new double[2], Length = new double[2], Exposure = new double[2];
        public double[] Tariff = { 0.25, 22 };
        string Folder;

        public FvmRoute(string folder) : this(folder, true) { }

        FvmRoute(string folder, bool trace)
        {
            Folder = folder;
            Free = MImages.Load(Path.Combine(folder, "Free"));
            Ground = MImages.Load(Path.Combine(folder, "Ground"));
            Zone = MImages.Load(Path.Combine(folder, "Zone"));
            if (trace)
                for (int mode = 0; mode < 2; mode++)
                {
                    Potential[mode] = MImages.Load(PotentialFolder(mode));
                    Trace(mode);
                }
        }

        public void LoadVolume()
        {
            Surface = MImages.Load(Path.Combine(Folder, "Surface"));
            ClayLayer = MImages.Load(Path.Combine(Folder, "Clay"));
            RockLayer = MImages.Load(Path.Combine(Folder, "Rock"));
            PipeBody = MImages.Load(Path.Combine(Folder, "Pipe"));
            FoundationBody = MImages.Load(Path.Combine(Folder, "Foundation"));
            for (int i = 0; i < MImages.Count; i++)
            {
                Vector3 p = MImages.Point(i);
                if (Surface.Value(i, p) < 0) continue;
                Material[i] = 1;
                if (ClayLayer.Value(i, p) >= 0) Material[i] = 2;
                if (RockLayer.Value(i, p) >= 0) Material[i] = 3;
                if (PipeBody.Value(i, p) >= 0) Material[i] = 4;
                if (FoundationBody.Value(i, p) >= 0) Material[i] = 5;
            }
        }

        public static void MakeDemoMaps(string folder)
        {
            DemoScene.WriteMaps(folder);
            FvmRoute builder = new FvmRoute(folder, false);
            for (int mode = 0; mode < 2; mode++) builder.WritePotential(mode);
        }

        string PotentialFolder(int mode)
        {
            return Path.Combine(Folder, "Potential" + (mode + 1));
        }

        // В простой версии разрешаем 26 соседей; промежуточные точки проверяем по М-полям.
        List<int> Neighbours(int i)
        {
            Vector3 p = MImages.Point(i);
            List<int> result = new List<int>();
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int px = (int)p.X + x, py = (int)p.Y + y, pz = (int)p.Z + z;
                if ((x == 0 && y == 0 && z == 0) || px < 0 || py < 0 || pz < 0 ||
                    px >= MImages.X || py >= MImages.Y || pz >= MImages.Z) continue;
                int j = MImages.Index(px, py, pz);
                bool free = true;
                for (int s = 0; s <= 8; s++)
                    if (Free.Between(i, j, s / 8.0) <= 0.06) free = false;
                if (free) result.Add(j);
            }
            return result;
        }

        double ZoneOnEdge(int i, int j)
        {
            double sum = 0;
            for (int s = 0; s < 8; s++)
                sum += Math.Max(0, Zone.Between(i, j, (s + 0.5) / 8));
            return sum * Vector3.Distance(MImages.Point(i), MImages.Point(j)) / 8;
        }

        double Cost(int i, int j, int mode)
        {
            double sum = 0;
            for (int s = 0; s < 8; s++)
                sum += Math.Max(0, Ground.Between(i, j, (s + 0.5) / 8));
            return sum * Vector3.Distance(MImages.Point(i), MImages.Point(j)) / 8 +
                   Tariff[mode] * ZoneOnEdge(i, j);
        }

        static double Difference(double[] value, int i, int step, int coordinate, int count)
        {
            bool left = coordinate > 0 && !double.IsInfinity(value[i - step]);
            bool right = coordinate < count - 1 && !double.IsInfinity(value[i + step]);
            if (left && right) return (value[i + step] - value[i - step]) / 2;
            if (left) return value[i] - value[i - step];
            if (right) return value[i + step] - value[i];
            return 0;
        }

        double[] CalculatePotential(int mode)
        {
            int count = MImages.Count;
            List<int>[] neighbours = new List<int>[count];
            double[] value = new double[count];
            bool[] finished = new bool[count];
            for (int i = 0; i < count; i++)
            {
                neighbours[i] = Neighbours(i);
                value[i] = double.PositiveInfinity;
            }

            // U(B)=0. От B передаём соседям минимальную стоимость остатка пути.
            value[B] = 0;
            for (int step = 0; step < count; step++)
            {
                int best = -1;
                double minimum = double.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (!finished[i] && value[i] < minimum) { best = i; minimum = value[i]; }
                if (best == -1) break;
                finished[best] = true;
                foreach (int i in neighbours[best])
                    value[i] = Math.Min(value[i], minimum + Cost(i, best, mode));
            }
            if (double.IsInfinity(value[A])) throw new Exception("Точки A и B не связаны.");

            return value;
        }

        void WritePotential(int mode)
        {
            double[] value = CalculatePotential(mode);
            MImages images = new MImages();
            for (int i = 0; i < MImages.Count; i++)
            {
                Vector3 p = MImages.Point(i);
                // Отрицательное значение отмечает недостижимый воксель.
                if (double.IsInfinity(value[i])) images.Write(i, -1, 0, 0, 0);
                else images.Write(i, value[i],
                    Difference(value, i, 1, (int)p.X, MImages.X),
                    Difference(value, i, MImages.X, (int)p.Y, MImages.Y),
                    Difference(value, i, MImages.X * MImages.Y, (int)p.Z, MImages.Z));
            }
            images.Save(PotentialFolder(mode));
        }

        void Trace(int mode)
        {
            MImages u = Potential[mode];
            var path = new List<int> { A };
            int current = A;
            StepTable[mode] = new List<string[]>();
            while (current != B)
            {
                Vector3 p = MImages.Point(current);
                Vector3 gradient = u.Gradient(current);
                double here = u.Value(current, p);

                // Переходы и цены читаются из Free/Ground/Zone, поле U — из Potential.
                // Выбираем минимальное c+U среди убывающих соседей. При близких
                // значениях берём направление по градиенту из тех же М-карт.
                var candidates = new List<(int Node, double U, double Edge, double Slope)>();
                double minimum = double.PositiveInfinity;
                foreach (int j in Neighbours(current))
                {
                    Vector3 q = MImages.Point(j);
                    double there = u.Value(j, q);
                    double edge = Cost(current, j, mode);
                    double slope = Vector3.Dot(gradient, Vector3.Normalize(q - p));
                    candidates.Add((j, there, edge, slope));
                    if (there >= -0.001 && there < here)
                        minimum = Math.Min(minimum, edge + there);
                }

                int next = -1;
                double bestSlope = double.PositiveInfinity;
                var options = new List<string>();
                foreach (var candidate in candidates)
                {
                    var (j, there, edge, slope) = candidate;
                    Vector3 q = MImages.Point(j);
                    bool nearMinimum = there >= -0.001 && there < here && edge + there <= minimum + 0.00025;
                    options.Add(string.Format("({0,2},{1,2},{2})    {3,6:F2}    {4,6:F2}    {5,6:F2}    {6,6:F2}    {7}",
                        q.X, q.Y, q.Z, there, edge, there + edge, slope, nearMinimum ? "минимум RGB" : ""));
                    if (nearMinimum && slope < bestSlope) { bestSlope = slope; next = j; }
                }
                StepTable[mode].Add(options.ToArray());
                if (next < 0 || path.Count >= MImages.Count)
                    throw new Exception("Спуск по М-картам остановился. Проверьте поля допустимости и стоимости.");
                Price[mode] += Cost(current, next, mode);
                Exposure[mode] += ZoneOnEdge(current, next);
                Length[mode] += Vector3.Distance(p, MImages.Point(next));
                path.Add(next);
                current = next;
            }
            PathNodes[mode] = path;
        }

        public string RoutesReport()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Routes reconstructed from BMP only.\r\n" +
                "Low tariff:  {0:F3} m, {1:F6} cost units, zone integral {2:F3}.\r\n" +
                "High tariff: {3:F3} m, {4:F6} cost units, zone integral {5:F3}.\r\n",
                Length[0], Price[0], Exposure[0], Length[1], Price[1], Exposure[1]);
        }

        public void Check(string file)
        {
            MImages affine = MImages.Build(Path.Combine(Folder, "Test"), p => 2 + 0.3 * p.X - 0.2 * p.Y + 0.4 * p.Z);
            double error = 0;
            int blocked = 0;
            for (int i = 0; i < MImages.Count; i++)
            {
                Vector3 center = MImages.Point(i);
                Vector3 p = center + new Vector3(0.2f, 0.1f, -0.15f);
                error = Math.Max(error, Math.Abs(affine.Value(i, p) - (2 + 0.3 * p.X - 0.2 * p.Y + 0.4 * p.Z)));
                if (Vector3.Distance(affine.Gradient(i), new Vector3(0.3f, -0.2f, 0.4f)) > 0.00015)
                    throw new Exception("После BMP не восстановился градиент.");
                if (Free.Value(i, center) < 0) blocked++;
                if (Math.Abs(DemoScene.Space(center)) > 0.001 &&
                    Math.Sign(Free.Value(i, center)) != Math.Sign(DemoScene.Space(center)))
                    throw new Exception("После BMP изменился знак вокселя.");
            }
            if (error > 0.001 || blocked == 0 || Exposure[0] < 0.5 || Exposure[1] >= Exposure[0] * 0.2 ||
                !PathNodes[0].Exists(i => MImages.Point(i).Z > MImages.Point(A).Z))
                throw new Exception("Не пройдена проверка восстановления или смены трассы.");
            // Отдельно сверяем геометрию с исходной формулой на более частой сетке.
            foreach (List<int> path in PathNodes)
            for (int i = 1; i < path.Count; i++)
            for (int s = 0; s <= 64; s++)
                if (DemoScene.Space(Vector3.Lerp(MImages.Point(path[i - 1]), MImages.Point(path[i]), s / 64f)) <= 0)
                    throw new Exception("Аналитическая проверка зазора не пройдена.");

            if (!PathNodes[0].Exists(i => MImages.Point(i).X == 7 && MImages.Point(i).Z >= 5) ||
                !PathNodes[0].Exists(i => MImages.Point(i).X == 16 && MImages.Point(i).Z <= 3) ||
                !PathNodes[1].Exists(i => MImages.Point(i).Y <= 4) ||
                !PathNodes[1].Exists(i => MImages.Point(i).Z != MImages.Point(A).Z))
                throw new Exception("Не пройдена проверка пространственного обхода.");
            for (int type = 0; type <= 5; type++)
                if (Array.IndexOf(Material, type) < 0) throw new Exception("Пропущен слой или тело.");
            for (int m = 0; m < 2; m++)
                File.WriteAllLines(Path.Combine(Folder, "route-" + (m + 1) + ".txt"),
                    PathNodes[m].ConvertAll(i => MImages.Point(i).ToString()));
            double lowGap = Price[0] - CalculatePotential(0)[A], highGap = Price[1] - CalculatePotential(1)[A];
            if (Math.Abs(lowGap) > 0.01 || Math.Abs(highGap) > 0.01)
                throw new Exception("Цена спуска по RGB отличается от дискретного минимума более чем на 0,01.");
            string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "PASS: RGB24 BMP -> {0} voxels, max affine reconstruction error {1:G6}, blocked voxels {2}.\r\n" +
                "Cost gap vs. full-precision Dijkstra: low {3:G9}, high {4:G9}.\r\n" +
                "Verified sampled analytic clearance: underpass, overpass and lateral bypass.\r\n",
                MImages.Count, error, blocked, lowGap, highGap) + RoutesReport();
            File.WriteAllText(file, report);
            Console.Write(report);
        }
    }
    static class DemoScene
    {
        // R-пересечение двух условий. Положительный знак означает, что оба выполняются.
        static double And(double a, double b)
        {
            return a + b - Math.Sqrt(a * a + b * b);
        }

        static double Sq(double x) { return x * x; }

        // Z направлена вниз от общего высотного горизонта. Глубина считается от рельефа.
        public static double Terrain(double x, double y)
        {
            return 0.15 + 1.45 * Math.Exp(-Sq((x - 10.5) / 4.4) - Sq((y - 8) / 5.0))
                - 0.70 * Math.Exp(-Sq((x - 19) / 3.6) - Sq((y - 8) / 4.5))
                + 0.22 * Math.Sin(y * 0.44);
        }

        public static double Clay(double x, double y)
        {
            return Terrain(x, y) + 2.55 + 0.32 * Math.Sin(0.4 * x + 0.2 * y);
        }

        public static double Rock(double x, double y)
        {
            return Terrain(x, y) + 4.8 + 0.32 * Math.Cos(0.4 * x - 0.28 * y);
        }

        public static double Pipe(Vector3 p)
        {
            return Sq((p.X - 7.5) / 1.85) + Sq((p.Y - 8) / 3.8) + Sq((p.Z - 2.9) / 1.3) - 1;
        }

        public static double Foundation(Vector3 p)
        {
            return Sq((p.X - 16.2) / 2.1) + Sq((p.Y - 8.2) / 2.55) + Sq((p.Z - 5.2) / 1.85) - 1;
        }

        public static double Space(Vector3 p)
        {
            double depth = p.Z - Terrain(p.X, p.Y);
            double cover = And(depth - 0.65, 6.5 - depth);
            return And(cover, And(Pipe(p), Foundation(p)));
        }

        static double Earth(Vector3 p)
        {
            double depth = Math.Max(0, p.Z - Terrain(p.X, p.Y));
            double inClay = Math.Max(0, p.Z - Clay(p.X, p.Y));
            double inRock = Math.Max(0, p.Z - Rock(p.X, p.Y));
            return 3.5 + 0.35 * depth + 0.65 * inClay + 1.8 * inRock
                + 9 * Sq(Math.Max(0, 2.15 - depth));
        }

        static double Toll(Vector3 p)
        {
            double inside = 1 - Sq((p.X - 17.5) / 4.2) - Sq((p.Y - 8) / 3.5);
            return Sq(Math.Max(0, inside));
        }

        public static void WriteMaps(string folder)
        {
            MImages.Build(Path.Combine(folder, "Surface"), p => p.Z - Terrain(p.X, p.Y));
            MImages.Build(Path.Combine(folder, "Clay"), p => p.Z - Clay(p.X, p.Y));
            MImages.Build(Path.Combine(folder, "Rock"), p => p.Z - Rock(p.X, p.Y));
            MImages.Build(Path.Combine(folder, "Pipe"), p => -Pipe(p));
            MImages.Build(Path.Combine(folder, "Foundation"), p => -Foundation(p));
            MImages.Build(Path.Combine(folder, "Free"), Space);
            MImages.Build(Path.Combine(folder, "Ground"), Earth);
            MImages.Build(Path.Combine(folder, "Zone"), Toll);
        }
    }
}
