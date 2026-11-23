using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string folder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "M-images");
            if (args.Length == 1 && args[0] == "--check")
            {
                new FvmRoute(folder).Check(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "check.txt"));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SharpGLForm form = new SharpGLForm(new FvmRoute(folder));
            if (args.Length == 3 && args[0] == "--capture") form.StartView(int.Parse(args[1]), args[2]);
            Application.Run(form);
        }
    }
}
