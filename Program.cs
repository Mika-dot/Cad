// Все вычисления и вывод — в одном Program.cs, без других библиотек.
using System.Text;

// Этот класс получает формулу фигуры и шаг пикселя (в миллиметрах).
// На одном Z-срезе он вычисляет четыре цвета для каждой точки (X, Y).
class FunctionPixels
{
    private readonly Func<double, double, double, double> formula;
    private readonly double step;

    public FunctionPixels(Func<double, double, double, double> figure, double pixelStepMm)
    {
        formula = figure;
        step = pixelStepMm;
    }

    public uint[] FourColors(double x, double y, double z)
    {
        // 1. Значение функции в центре пикселя.
        double value = formula(x, y, z);

        // 2. Смотрим, насколько функция меняется при движении по X.
        double valueRight = formula(x + step / 100, y, z);
        double valueLeft = formula(x - step / 100, y, z);
        double changeX = (valueRight - valueLeft) / (2 * step / 100);

        // 3. То же самое по Y. Z пока фиксирован: это ОДИН 2D-срез.
        double valueUp = formula(x, y + step / 100, z);
        double valueDown = formula(x, y - step / 100, z);
        double changeY = (valueUp - valueDown) / (2 * step / 100);

        // 4. Четыре числа локального уравнения:
        // coefficientX * X + coefficientY * Y
        // + coefficientValue * F + coefficientFree = 0.
        double coefficientX = -changeX;
        double coefficientY = -changeY;
        double coefficientValue = 1;
        double coefficientFree = changeX * x + changeY * y - value;

        // 5. Нормируем все четыре числа одним и тем же делителем.
        double squareX = coefficientX * coefficientX;
        double squareY = coefficientY * coefficientY;
        double squareValue = coefficientValue * coefficientValue;
        double squareFree = coefficientFree * coefficientFree;
        double total = squareX + squareY + squareValue + squareFree;
        double length = Math.Sqrt(total);

        double m0 = coefficientX / length;
        double m1 = coefficientY / length;
        double m2 = coefficientValue / length;
        double m3 = coefficientFree / length;

        // 6. Каждое нормированное число [-1, +1] -> цвет #000000..#FFFFFF.
        uint color0 = (uint)Math.Round((m0 + 1) * 16777215 / 2);
        uint color1 = (uint)Math.Round((m1 + 1) * 16777215 / 2);
        uint color2 = (uint)Math.Round((m2 + 1) * 16777215 / 2);
        uint color3 = (uint)Math.Round((m3 + 1) * 16777215 / 2);

        return new[] { color0, color1, color2, color3 };
    }
}

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // МЕНЯТЬ ЗДЕСЬ: формулу, шаг пикселя и границы расчёта.
        Func<double, double, double, double> sphere =
            (x, y, z) => 9 - x * x - y * y - z * z;

        // Цилиндр вдоль оси Z, радиус 1 мм.
        Func<double, double, double, double> cylinder =
            (x, y, z) => 1 - x * x - y * y;

        // Разность: оставляем всё внутри сферы, кроме внутренности цилиндра.
        // Это R-пересечение сферы и ОБРАТНОЙ функции цилиндра.
        Func<double, double, double, double> sphereMinusCylinder = (x, y, z) =>
        {
            double sphereValue = sphere(x, y, z);
            double cylinderValue = cylinder(x, y, z);
            double outsideCylinder = -cylinderValue;
            double squareSphere = sphereValue * sphereValue;
            double squareOutside = outsideCylinder * outsideCylinder;
            return sphereValue + outsideCylinder -
                   Math.Sqrt(squareSphere + squareOutside);
        };

        // Два примера считаются одним и тем же классом FunctionPixels.
        string[] figureNames = { "sphere", "sphere_minus_cylinder" };
        Func<double, double, double, double>[] figures = { sphere, sphereMinusCylinder };

        double pixelStepMm = 1;
        double fromMm = -3;
        double toMm = 3;
        int pixelsPerSide = (int)Math.Round((toMm - fromMm) / pixelStepMm) + 1;

        Directory.CreateDirectory("output");

        // Идём по Z-слоям, затем по всем Y и X внутри слоя.
        for (int layer = 0; layer < pixelsPerSide; layer++)
        {
            double z = fromMm + layer * pixelStepMm;

            // Самый простой контроль выреза: два сечения РЯДОМ.
            // '#' — внутри, 'o' — поверхность, '.' — снаружи.
            Console.WriteLine();
            Console.WriteLine($"Z = {z:0.##} мм: СФЕРА                     СФЕРА - ЦИЛИНДР");
            for (int yPixel = pixelsPerSide - 1; yPixel >= 0; yPixel--)
            {
                double y = fromMm + yPixel * pixelStepMm;
                for (int figureIndex = 0; figureIndex < figures.Length; figureIndex++)
                {
                    for (int xPixel = 0; xPixel < pixelsPerSide; xPixel++)
                    {
                        double x = fromMm + xPixel * pixelStepMm;
                        double value = figures[figureIndex](x, y, z);
                        Console.Write(value > 0.0000001 ? "##" : value < -0.0000001 ? ".." : "oo");
                    }
                    Console.Write("       ");
                }
                Console.WriteLine();
            }

            // Сначала выводим 4 М-образа сферы, затем 4 образа сферы с вырезом.
            for (int figureIndex = 0; figureIndex < figures.Length; figureIndex++)
            {
                var calculator = new FunctionPixels(figures[figureIndex], pixelStepMm);
                uint[,,] image = new uint[4, pixelsPerSide, pixelsPerSide];

            for (int yPixel = 0; yPixel < pixelsPerSide; yPixel++)
            {
                double y = fromMm + yPixel * pixelStepMm;
                for (int xPixel = 0; xPixel < pixelsPerSide; xPixel++)
                {
                    double x = fromMm + xPixel * pixelStepMm;

                    uint[] colors = calculator.FourColors(x, y, z);

                    image[0, xPixel, yPixel] = colors[0];
                    image[1, xPixel, yPixel] = colors[1];
                    image[2, xPixel, yPixel] = colors[2];
                    image[3, xPixel, yPixel] = colors[3];
                }
            }

            // Четыре цветные картинки рядом прямо в консоли.
            Console.WriteLine();
            Console.WriteLine($"{figureNames[figureIndex]}, Z = {z:0.##} мм");
            Console.WriteLine("M0 / X           M1 / Y           M2 / F           M3 / свободный");
            for (int yPixel = pixelsPerSide - 1; yPixel >= 0; yPixel--)
            {
                for (int channel = 0; channel < 4; channel++)
                {
                    for (int xPixel = 0; xPixel < pixelsPerSide; xPixel++)
                    {
                        uint color = image[channel, xPixel, yPixel];
                        int red = (int)((color >> 16) & 255);
                        int green = (int)((color >> 8) & 255);
                        int blue = (int)(color & 255);
                        Console.Write($"\u001b[48;2;{red};{green};{blue}m  \u001b[0m");
                    }
                    Console.Write("   ");
                }
                Console.WriteLine();
            }

            // Те же четыре картинки как обычные BMP-файлы.
            for (int channel = 0; channel < 4; channel++)
            {
                string file = $"output/{figureNames[figureIndex]}_Z{layer:00}_M{channel}.bmp";
                SaveBitmap(file, image, channel, pixelsPerSide);
            }
            } // конец цикла по двум фигурам
        }

        Console.WriteLine();
        Console.WriteLine($"Готово: {pixelsPerSide} слоёв × 2 фигуры × 4 картинки = {pixelsPerSide * 8} BMP; файлы в output/");
        var sampleCalculator = new FunctionPixels(sphereMinusCylinder, pixelStepMm);
        uint[] sample = sampleCalculator.FourColors(2, 1, 1);
        Console.WriteLine("СФЕРА МИНУС ЦИЛИНДР, пиксель (2,1,1):");
        for (int channel = 0; channel < 4; channel++)
            Console.WriteLine($"M{channel} = #{sample[channel]:X6}");
    }

    // Запись простого BMP: 24 бита на пиксель. Paint открывает без библиотек.
    static void SaveBitmap(string path, uint[,,] image, int channel, int size)
    {
        int rowBytes = (size * 3 + 3) / 4 * 4;
        int imageBytes = rowBytes * size;
        using var writer = new BinaryWriter(File.Create(path));

        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(54 + imageBytes); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(size); writer.Write(size);
        writer.Write((short)1); writer.Write((short)24);
        writer.Write(0); writer.Write(imageBytes);
        writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                uint color = image[channel, x, y];
                writer.Write((byte)(color & 255));
                writer.Write((byte)((color >> 8) & 255));
                writer.Write((byte)((color >> 16) & 255));
            }
            for (int padding = size * 3; padding < rowBytes; padding++)
                writer.Write((byte)0);
        }
    }
}
