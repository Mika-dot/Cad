// Isolated benchmark for the educational FunctionPixels class.
// dotnet run --project bench/PerformanceBench.csproj -c Release
// In a benchmark we do NOT print pixels, write BMP files, or time dotnet startup.
// This is not a universal performance ranking: CPU, JIT, compiler and cache matter.
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

internal static class PerformanceBench
{
    const int Side = 32;
    const double Radius2 = 9.0;
    const double CylinderRadius2 = 1.0;
    const double RgbMax = 16777215.0;
    const int Trials = 5;
    const int Repeats = 25;

    static double Sphere(double x, double y, double z) => Radius2 - x*x - y*y - z*z;
    static double Cylinder(double x, double y, double z) => CylinderRadius2 - x*x - y*y;

    static double DirectR(double x, double y, double z)
    {
        double sphereValue = Sphere(x, y, z);
        double outsideCylinder = -Cylinder(x, y, z);
        return sphereValue + outsideCylinder -
               Math.Sqrt(sphereValue * sphereValue + outsideCylinder * outsideCylinder);
    }

    static bool Occupied(double x, double y, double z) =>
        Sphere(x, y, z) >= 0 && Cylinder(x, y, z) <= 0;

    static double Restore(uint[] pixels, int position, double x, double y)
    {
        int k = 4 * position;
        double m0 = 2.0 * pixels[k] / RgbMax - 1.0;
        double m1 = 2.0 * pixels[k + 1] / RgbMax - 1.0;
        double m2 = 2.0 * pixels[k + 2] / RgbMax - 1.0;
        double m3 = 2.0 * pixels[k + 3] / RgbMax - 1.0;
        return -(m0*x + m1*y + m3) / m2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static (double NsPerItem, double Checksum) Time(Func<double> work, int operations)
    {
        // Warming up just the current code path. Collect nothing during timing.
        work();
        var samples = new List<double>(Trials);
        double checksum = 0;
        for (int t = 0; t < Trials; t++)
        {
            long begin = Stopwatch.GetTimestamp();
            checksum += work();
            long finish = Stopwatch.GetTimestamp();
            samples.Add((finish - begin) * 1_000_000_000.0 /
                        Stopwatch.Frequency / operations);
        }
        samples.Sort();
        return (samples[Trials / 2], checksum);
    }

    static int Main()
    {
        int voxels = Side * Side * Side;
        double[] xs = new double[voxels];
        double[] ys = new double[voxels];
        double[] zs = new double[voxels];
        double step = 6.0 / Side;

        for (int z = 0; z < Side; z++)
        for (int y = 0; y < Side; y++)
        for (int x = 0; x < Side; x++)
        {
            int i = x + Side * (y + Side * z);
            xs[i] = -3 + (x + .5) * step;
            ys[i] = -3 + (y + .5) * step;
            zs[i] = -3 + (z + .5) * step;
        }

        // Regular occupancy grid, packed manually: one bit per voxel.
        byte[] bits = new byte[(voxels + 7) / 8];
        for (int i = 0; i < voxels; i++)
            if (Occupied(xs[i], ys[i], zs[i]))
                bits[i / 8] |= (byte)(1 << (i % 8));

        // Packed 4 x uint32 per centre, as in the real educational Program.cs.
        uint[] packed = new uint[4 * voxels];
        var calculator = new FunctionPixels(DirectR, step);
        double BuildOne()
        {
            double sum = 0;
            for (int i = 0; i < voxels; i++)
            {
                uint[] m = calculator.FourColors(xs[i], ys[i], zs[i]);
                int pos = 4 * i;
                packed[pos] = m[0];
                packed[pos + 1] = m[1];
                packed[pos + 2] = m[2];
                packed[pos + 3] = m[3];
                sum += m[0] * 0.00000001;
            }
            return sum;
        }
        var construction = Time(BuildOne, voxels);
        BuildOne(); // Guarantee all packed coefficients correspond to current points.

        double QuerySphere()
        {
            double sum = 0;
            for (int r = 0; r < Repeats; r++)
                for (int i = 0; i < voxels; i++)
                    sum += Sphere(xs[i], ys[i], zs[i]);
            return sum;
        }

        double QueryBoolean()
        {
            double sum = 0;
            for (int r = 0; r < Repeats; r++)
                for (int i = 0; i < voxels; i++)
                    if (Occupied(xs[i], ys[i], zs[i])) sum++;
            return sum;
        }

        double QueryR()
        {
            double sum = 0;
            for (int r = 0; r < Repeats; r++)
                for (int i = 0; i < voxels; i++)
                    sum += DirectR(xs[i], ys[i], zs[i]);
            return sum;
        }

        double QueryMask()
        {
            double sum = 0;
            for (int r = 0; r < Repeats; r++)
                for (int i = 0; i < voxels; i++)
                    sum += (bits[i >> 3] >> (i & 7)) & 1;
            return sum;
        }

        double QueryM()
        {
            double sum = 0;
            for (int r = 0; r < Repeats; r++)
                for (int i = 0; i < voxels; i++)
                    sum += Restore(packed, i, xs[i], ys[i]);
            return sum;
        }

        int mismatches = 0;
        double maxValueError = 0;
        for (int i = 0; i < voxels; i++)
        {
            double exact = DirectR(xs[i], ys[i], zs[i]);
            double reconstructed = Restore(packed, i, xs[i], ys[i]);
            maxValueError = Math.Max(maxValueError, Math.Abs(exact - reconstructed));
            if ((exact >= 0) != (reconstructed >= 0)) mismatches++;
        }

        int operations = Repeats * voxels;
        var sphere = Time(QuerySphere, operations);
        var boolean = Time(QueryBoolean, operations);
        var rvalue = Time(QueryR, operations);
        var bitLookup = Time(QueryMask, operations);
        var mLookup = Time(QueryM, operations);

        // The exact same array traversal is used by all five query variants.
        void Print(string name, (double NsPerItem, double Checksum) s)
        {
            Console.WriteLine(name.PadRight(23) + " " +
                s.NsPerItem.ToString("F2", CultureInfo.InvariantCulture).PadLeft(10) +
                " ns/voxel" + "  checksum=" +
                s.Checksum.ToString("G8", CultureInfo.InvariantCulture));
        }

        Console.WriteLine("FVM_PERFORMANCE_BENCHMARK_V1");
        Console.WriteLine("OS=" + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        Console.WriteLine("CPU_count=" + Environment.ProcessorCount +
                          "; dotnet=" + Environment.Version);
        Console.WriteLine("32^3 centres=" + voxels + "; queries_per_trial=" + operations +
                          "; trials=" + Trials + "; median_ns_per_voxel");
        Console.WriteLine("byte[] mask bytes=" + bits.Length +
                          "; 4xuint[] bytes=" + 4 * voxels * sizeof(uint));
        Print("analytic sphere F", sphere);
        Print("analytic CSG bool", boolean);
        Print("analytic CSG R sqrt", rvalue);
        Print("cached bitmask read", bitLookup);
        Print("cached 4xRGB decode F", mLookup);
        Print("build 4xRGB FVM", construction);
        Console.WriteLine("CSG_R_vs_M_lookup=" +
            (rvalue.NsPerItem / mLookup.NsPerItem).ToString("F2", CultureInfo.InvariantCulture));
        Console.WriteLine("CSG_R_vs_M_build=" +
            (rvalue.NsPerItem / construction.NsPerItem).ToString("F3", CultureInfo.InvariantCulture));
        Console.WriteLine("max_abs_F_RGB24_error=" +
            maxValueError.ToString("G10", CultureInfo.InvariantCulture));
        Console.WriteLine("sign_mismatches_at_centres=" + mismatches);
        Console.WriteLine("BENCHMARK_COMPLETE");
        return 0;
    }
}
