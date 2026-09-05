using System;
using System.Linq;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool legacy = args != null && args.Any(a => string.Equals(a, "--legacy", StringComparison.OrdinalIgnoreCase));
            Application.Run(legacy ? (Form)new SharpGLForm() : new ModernVoxelDemoForm());
        }
    }
}
