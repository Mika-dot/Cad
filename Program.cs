using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

// Один файл, никаких CAD-библиотек и GUI.
// ФВМ в минимальном виде: значение сферы + локальная плоскость + 4/5 M-образов.
internal static class Program
{
    const double R = 3.0;            // Радиус круга/сферы, мм
    const uint RgbMax = 0xFFFFFF;    // 24 бита = 16 777 216 возможных кодов

    // PInvoke нужен только для цветной консоли в старом Windows PowerShell / conhost.
    const int VtOutput = 0x0004;
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(IntPtr handle, out int mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(IntPtr handle, int mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int index);

    sealed class Voxel
    {
        public double[] XYZ = Array.Empty<double>();
        public double F;             // Значение исходной функции
        public double[] Gradient = Array.Empty<double>();
        public double[] M = Array.Empty<double>(); // 4 для 2D, 5 для 3D
        public uint[] RGB = Array.Empty<uint>();   // По одному RGB24-пикселю на M-образ
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Contains("--help"))
            {
                Console.WriteLine("dotnet run -- [--size 7] [--no-color] [--no-table] [--check]");
                Console.WriteLine("Нечётная сетка 3..21, по умолчанию 7; радиус R=3 мм.");
                return 0;
            }

            if (args.Contains("--check"))
            {
                SelfCheck();
                Console.WriteLine("SELF_CHECK_PASS");
                return 0;
            }

            int size = 7;
            int flag = Array.IndexOf(args, "--size");
            if (flag >= 0)
            {
                if (flag + 1 >= args.Length ||
                    !int.TryParse(args[flag + 1], out size) ||
                    size < 3 || size > 21 || size % 2 == 0)
                    throw new ArgumentException("--size: требуется нечётное число от 3 до 21.");
            }

            bool colors = !args.Contains("--no-color") && EnableColors();
            bool table = !args.Contains("--no-table");
            string folder = Path.Combine(Environment.CurrentDirectory, "output");
            Directory.CreateDirectory(folder);

            Console.WriteLine("=== ФУНКЦИОНАЛЬНО-ВОКСЕЛЬНАЯ СФЕРА ===");
            Console.WriteLine("F(x,y,z) = 9 - x² - y² - z²; R=3 мм.");
            Console.WriteLine("Координаты центров: целые точки; шаг сетки 1 мм.");
            Console.WriteLine("F > 0: внутри; F = 0: граница; F < 0: снаружи.");
            Console.WriteLine("Каждый цветной прямоугольник = RGB24 одного коэффициента M.");
            Console.WriteLine("4 изображения: 2D-круг; 5 изображений на каждый Z-срез: 3D-сфера.");
            Console.WriteLine("В таблицах: исходные double-коэффициенты, затем 6-значные RGB-коды.");
            Console.WriteLine();

            // 2D-круг: F(x,y)=9-x²-y². Для u=F(x,y) нужны 4 коэффициента.
            Console.WriteLine("====== ЧАСТЬ А: КРУГ (2D), 4 М-ОБРАЗА ======");
            Voxel[,] circle = new Voxel[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    circle[x, y] = Calculate(x - size / 2, y - size / 2);
            ShowImages(circle, size, 4, colors);
            for (int m = 0; m < 4; m++)
                SaveBmp(Path.Combine(folder, "circle_M" + m + ".bmp"),
                    size, size, (x, y) => circle[x, y].RGB[m]);
            if (table) PrintTable(circle, size, 4);

            // 3D-сфера: те же операции, но добавляется z и пятый коэффициент.
            Console.WriteLine();
            Console.WriteLine("====== ЧАСТЬ Б: СФЕРА (3D), 5 М-ОБРАЗОВ ======");
            Console.WriteLine("Для каждого Z-слоя выводятся 5 отдельных цветовых карт XY.");
            Voxel[,,] sphere = new Voxel[size, size, size];
            for (int z = 0; z < size; z++)
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        sphere[x, y, z] = Calculate(
                            x - size / 2, y - size / 2, z - size / 2);

            for (int z = 0; z < size; z++)
            {
                int coordinateZ = z - size / 2;
                Console.WriteLine();
                Console.WriteLine("----- СРЕЗ Z = " + coordinateZ + " мм, слой " +
                                  (z + 1) + "/" + size + " -----");
                Voxel[,] slice = new Voxel[size, size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        slice[x, y] = sphere[x, y, z];
                ShowImages(slice, size, 5, colors);
                for (int m = 0; m < 5; m++)
                {
                    string name = "sphere_Z" + coordinateZ.ToString("+00;-00;000",
                        CultureInfo.InvariantCulture) + "_M" + m + ".bmp";
                    SaveBmp(Path.Combine(folder, name),
                        size, size, (x, y) => slice[x, y].RGB[m]);
                }
                if (table) PrintTable(slice, size, 5);
            }

            // Демонстрация восстановления значения и градиента только из M.
            Voxel v = Calculate(2, 1, 1);
            Console.WriteLine();
            Console.WriteLine("====== КОНТРОЛЬНЫЙ ВОКСЕЛЬ (2,1,1) ======");
            Console.WriteLine("F = " + N(v.F) + "  |  градиент = (-4,-2,-2)");
            for (int m = 0; m < v.M.Length; m++)
                Console.WriteLine("M" + m + " = " + N(v.M[m]) +
                    "  -> RGB #" + v.RGB[m].ToString("X6"));
            Console.WriteLine("Восстановление F из M: " + N(Reconstruct(v)));
            Console.WriteLine("Восстановление dF/dx из M: " + N(-v.M[0] / v.M[3]));
            Console.WriteLine();
            Console.WriteLine("BMP сохранены: " + folder);
            Console.WriteLine("Всего изображений: " + (4 + size * 5) +
                " (4 круга + 5 × " + size + " срезов сферы).");
            Console.WriteLine("Проверка арифметики: dotnet run -- --check");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка: " + ex.Message);
            return 1;
        }
    }

    // 1. Считаем одну точку для круга (2D) или сферы (3D).
    static Voxel Calculate(params double[] xyz)
    {
        int d = xyz.Length;
        if (d != 2 && d != 3) throw new ArgumentException("Нужно 2 или 3 координаты.");

        double r2 = 0;
        for (int i = 0; i < d; i++) r2 += xyz[i] * xyz[i];
        double f = R * R - r2;

        // Градиент: производные функции в центре вокселя.
        double[] grad = new double[d];
        for (int i = 0; i < d; i++) grad[i] = -2 * xyz[i];

        // Локальная функция:
        // u = f + grad[0]*(x-x0) + ...;
        // Переносим влево: a0*x + ... + a_u*u + a_const = 0.
        // Начальные коэффициенты (до нормировки):
        double[] a = new double[d + 2];
        double offset = -f;
        for (int i = 0; i < d; i++)
        {
            a[i] = -grad[i];
            offset += grad[i] * xyz[i];
        }
        a[d] = 1;             // коэффициент при u=F
        a[d + 1] = offset;    // свободный член

        // 2. Нормируем ВСЕ d+2 коэффициента вместе, включая свободный.
        double squared = 0;
        for (int i = 0; i < a.Length; i++) squared += a[i] * a[i];
        double length = Math.Sqrt(squared);
        for (int i = 0; i < a.Length; i++) a[i] /= length;

        // 3. Каждое число в [-1,+1] превращаем в 24-битный цвет.
        // Используем Max=2^24-1: коды 000000..FFFFFF, включая обе границы.
        uint[] pixels = new uint[a.Length];
        for (int i = 0; i < a.Length; i++) pixels[i] = EncodeRgb24(a[i]);

        return new Voxel { XYZ = xyz, F = f, Gradient = grad, M = a, RGB = pixels };
    }

    static uint EncodeRgb24(double coefficient)
    {
        double number = (Math.Clamp(coefficient, -1, 1) + 1.0) * RgbMax / 2.0;
        return (uint)Math.Round(number, MidpointRounding.AwayFromZero);
    }

    static double DecodeRgb24(uint rgb) => 2.0 * rgb / RgbMax - 1.0;

    // Получаем значение функции, имея только M, без формулы сферы.
    static double Reconstruct(Voxel v)
    {
        int d = v.XYZ.Length;
        double left = v.M[d + 1];
        for (int i = 0; i < d; i++) left += v.M[i] * v.XYZ[i];
        return -left / v.M[d];
    }

    // Печатаем 4 или 5 цветных М-карт ГОРИЗОНТАЛЬНО друг возле друга.
    static void ShowImages(Voxel[,] cells, int n, int channels, bool ansi)
    {
        Console.Write("       ");
        for (int m = 0; m < channels; m++)
            Console.Write(Pad("M" + m + " = " + ChannelName(m, channels), ansi ? n * 2 + 3 : n * 7 + 3));
        Console.WriteLine();

        // Координата Y возрастает снизу вверх на рисунке.
        for (int y = n - 1; y >= 0; y--)
        {
            Console.Write("y=" + (y - n / 2).ToString("+0;-0;0").PadLeft(3) + "  ");
            for (int m = 0; m < channels; m++)
            {
                for (int x = 0; x < n; x++)
                    WriteColor(cells[x, y].RGB[m], ansi);
                Console.Write("   ");
            }
            Console.WriteLine();
        }
        Console.WriteLine("       x слева направо от " + (-n / 2) + " до " + (n / 2));
    }

    static string ChannelName(int m, int channels)
    {
        if (m == channels - 1) return "своб.";
        if (m == channels - 2) return "u";
        return "xyz"[m].ToString();
    }

    static string Pad(string t, int length) => t.Length >= length ? t : t.PadRight(length);

    static void WriteColor(uint rgb, bool ansi)
    {
        int red = (int)((rgb >> 16) & 255);
        int green = (int)((rgb >> 8) & 255);
        int blue = (int)(rgb & 255);
        if (ansi)
            Console.Write("\u001b[48;2;" + red + ";" + green + ";" + blue + "m  \u001b[0m");
        else
            Console.Write(rgb.ToString("X6") + " ");
    }

    static void PrintTable(Voxel[,] cells, int n, int channels)
    {
        Console.WriteLine("ВЫВОД КАЖДОГО ПИКСЕЛЯ / ВОКСЕЛЯ: координаты, F, M-коэффициенты, RGB24");
        for (int y = n - 1; y >= 0; y--)
        for (int x = 0; x < n; x++)
        {
            Voxel v = cells[x, y];
            string coords = "(" + string.Join(",", v.XYZ.Select(N)) + ")";
            string mValues = string.Join(" ", v.M.Select((value, i) =>
                "M" + i + "=" + value.ToString("0.000000", CultureInfo.InvariantCulture)));
            string colors = string.Join(" ", v.RGB.Select((color, i) =>
                "M" + i + "=#" + color.ToString("X6")));
            Console.WriteLine(coords.PadRight(13) +
                " F=" + v.F.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture).PadLeft(6) +
                " " + mValues + "  |  " + colors);
        }
    }

    // Стандартный несжатый BMP, 24 бита на пиксель. Открывается обычным Paint.
    // Не нужны System.Drawing, OpenGL, Windows Forms или сторонние библиотеки.
    static void SaveBmp(string file, int width, int height, Func<int, int, uint> color)
    {
        int stride = (width * 3 + 3) & ~3;
        int imageSize = stride * height;
        using var stream = File.Create(file);
        using var w = new BinaryWriter(stream);
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(54 + imageSize);
        w.Write(0); w.Write(54);
        w.Write(40); w.Write(width); w.Write(height);
        w.Write((ushort)1); w.Write((ushort)24);
        w.Write(0); w.Write(imageSize);
        w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint rgb = color(x, y);
                w.Write((byte)(rgb & 255));         // B
                w.Write((byte)((rgb >> 8) & 255));  // G
                w.Write((byte)((rgb >> 16) & 255)); // R
            }
            for (int padding = width * 3; padding < stride; padding++)
                w.Write((byte)0);
        }
    }

    static string N(double x) => x.ToString("0.000000", CultureInfo.InvariantCulture);

    static bool EnableColors()
    {
        if (Console.IsOutputRedirected) return false;
        if (!OperatingSystem.IsWindows()) return true;
        IntPtr stdout = GetStdHandle(-11);
        if (stdout == IntPtr.Zero || stdout == new IntPtr(-1)) return false;
        if (!GetConsoleMode(stdout, out int mode)) return false;
        return SetConsoleMode(stdout, mode | VtOutput);
    }

    // Простые детерминированные проверки чисел из научного примера.
    static void SelfCheck()
    {
        void Near(double actual, double expected, double tolerance, string what)
        {
            if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
                throw new Exception(what + ": " + N(actual) + " вместо " + N(expected));
        }

        Voxel sphere = Calculate(2, 1, 1);
        if (sphere.M.Length != 5) throw new Exception("У сферы должно быть 5 M-образов.");
        Near(sphere.F, 3, 1e-12, "Значение F(2,1,1)");
        Near(sphere.Gradient[0], -4, 1e-12, "Градиент X");
        Near(sphere.Gradient[1], -2, 1e-12, "Градиент Y");
        Near(sphere.Gradient[2], -2, 1e-12, "Градиент Z");
        double[] raw = { 4, 2, 2, 1, -15 };
        for (int i = 0; i < 5; i++)
        {
            Near(sphere.M[i], raw[i] / Math.Sqrt(250), 1e-12, "Коэффициент M" + i);
            Near(DecodeRgb24(sphere.RGB[i]), sphere.M[i], 1.0 / RgbMax + 1e-13,
                "Цветовое кодирование M" + i);
        }
        Near(Reconstruct(sphere), 3, 1e-10, "Обратное восстановление F");
        Near(-sphere.M[0] / sphere.M[3], -4, 1e-12, "Обратная производная");
        Near(Calculate(0, 0, 0).F, 9, 1e-12, "Центр сферы");
        Near(Calculate(3, 0, 0).F, 0, 1e-12, "Поверхность сферы");
        Near(Calculate(4, 0, 0).F, -7, 1e-12, "Снаружи сферы");

        Voxel circle = Calculate(2, 1);
        if (circle.M.Length != 4) throw new Exception("У круга должно быть 4 M-образа.");
        Near(circle.F, 4, 1e-12, "Значение круга");
        Near(Reconstruct(circle), 4, 1e-10, "Обратное восстановление круга");
        if (EncodeRgb24(-1) != 0 || EncodeRgb24(1) != RgbMax)
            throw new Exception("Границы RGB должны быть 000000 и FFFFFF.");

        string temp = Path.Combine(Path.GetTempPath(), "sphere_assembly_" + Guid.NewGuid().ToString("N") + ".bmp");
        try
        {
            SaveBmp(temp, 2, 2, (x, y) => x == y ? 0xFF0000u : 0x0000FFu);
            byte[] data = File.ReadAllBytes(temp);
            if (data.Length != 54 + 2 * 8 || data[0] != 'B' || data[1] != 'M')
                throw new Exception("Неправильный заголовок/длина BMP.");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
