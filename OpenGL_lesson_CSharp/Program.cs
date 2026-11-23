using System;
using System.IO;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "M-images");
            string command = args.Length == 0 ? "" : args[0];
            if (args.Length == 2) folder = Path.GetFullPath(args[1]);

            if (command == "--make-maps")
            {
                FvmRoute.MakeDemoMaps(folder);
                return;
            }
            if (command == "--solve-maps")
            {
                Console.Write(new FvmRoute(folder).RoutesReport());
                return;
            }
            if (command != "--from-maps") FvmRoute.MakeDemoMaps(folder);
            FvmRoute route = new FvmRoute(folder);
            route.LoadVolume();
            if (command == "--check")
            {
                route.Check(Path.Combine(folder, "check.txt"));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SharpGLForm form = new SharpGLForm(route);
            if (args.Length == 3 && command == "--capture") form.StartView(int.Parse(args[1]), args[2]);
            Application.Run(form);
        }
    }
}
