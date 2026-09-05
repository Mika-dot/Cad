using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || a == "-h"))
                {
                    PrintHelp();
                    return;
                }

                int sceneIndex = IndexOf(args, "--scene");
                if (sceneIndex >= 0 && sceneIndex + 1 < args.Length)
                {
                    RunHeadless(args[sceneIndex + 1], args);
                    return;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SharpGLForm());
        }

        private static void RunHeadless(string scenePath, string[] args)
        {
            if (!File.Exists(scenePath))
                throw new FileNotFoundException("Voxel scene JSON not found.", scenePath);

            var built = SceneBuilder.FromJsonFile(scenePath);
            var model = built.model;
            float voxelSizeMm = (float)(1.0 / built.voxelsPerMm);

            int outIndex = IndexOf(args, "--out");
            string outputPath = outIndex >= 0 && outIndex + 1 < args.Length
                ? args[outIndex + 1]
                : Path.ChangeExtension(scenePath, ".stl");

            bool ascii = args.Any(a => string.Equals(a, "--ascii", StringComparison.OrdinalIgnoreCase));
            bool classic = args.Any(a => string.Equals(a, "--classic-stl", StringComparison.OrdinalIgnoreCase));

            if (ascii)
                model.ExportStlAscii(outputPath, voxelSizeMm, Path.GetFileNameWithoutExtension(scenePath));
            else if (classic)
                model.ExportStl(outputPath, voxelSizeMm);
            else
                model.ExportStlGreedy(outputPath, voxelSizeMm);

            Console.WriteLine($"VoxelCAD: {model.Count:N0} active voxels");
            Console.WriteLine($"Voxel size: {voxelSizeMm:G6} mm");
            Console.WriteLine($"Approx. volume: {model.Volume(voxelSizeMm):G8} mm^3");
            Console.WriteLine($"Approx. surface area: {model.ApproxSurfaceArea(voxelSizeMm):G8} mm^2");
            Console.WriteLine($"STL: {Path.GetFullPath(outputPath)}");
        }

        private static int IndexOf(string[] args, string value)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("DCad / VoxelCAD");
            Console.WriteLine("UI:     OpenGL_lesson_CSharp.exe");
            Console.WriteLine("CLI:    OpenGL_lesson_CSharp.exe --scene scene.json [--out model.stl]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --ascii         ASCII STL (classic per-voxel surface)");
            Console.WriteLine("  --classic-stl   Binary STL without greedy face merging");
            Console.WriteLine("  --help, -h      Show this help");
        }
    }
}
