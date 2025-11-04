using BriefFiniteElementNet;
using BriefFiniteElementNet.Elements;
using BriefFiniteElementNet.Materials;
using BriefFiniteElementNet.Sections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGL_lesson_CSharp
{
    public static class SceneBuilder
    {
        // Округление в индексы вокселей: левую границу берём floor, правую — ceil (для полуинтервалов)
        private static int VoxFloor(double mm, double vpm) => (int)Math.Floor(mm * vpm);
        private static int VoxCeil(double mm, double vpm) => (int)Math.Ceiling(mm * vpm);

        public static (VoxelModel model, double voxelsPerMm) FromJsonFile(string path)
        {
            var json = File.ReadAllText(path);
            var scene = JsonSerializer.Deserialize<Scene>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new Scene();

            double vpm = Math.Max(1e-6, scene.VoxelsPerMm);
            var vm = new VoxelModel();

            foreach (var op in scene.Operations)
            {
                // Учтём перенос dx,dy,dz (мм) прямо в границах:
                double x0mm = op.x0 + op.dx, x1mm = op.x1 + op.dx;
                double y0mm = op.y0 + op.dy, y1mm = op.y1 + op.dy;
                double z0mm = op.z0 + op.dz, z1mm = op.z1 + op.dz;

                int x0 = VoxFloor(Math.Min(x0mm, x1mm), vpm);
                int x1 = VoxCeil(Math.Max(x0mm, x1mm), vpm);
                int y0 = VoxFloor(Math.Min(y0mm, y1mm), vpm);
                int y1 = VoxCeil(Math.Max(y0mm, y1mm), vpm);
                int z0 = VoxFloor(Math.Min(z0mm, z1mm), vpm);
                int z1 = VoxCeil(Math.Max(z0mm, z1mm), vpm);

                if (string.Equals(op.Op, "addBox", StringComparison.OrdinalIgnoreCase))
                {
                    vm.AddBox(x0, y0, x1, y1, z0, z1);
                }
                else if (string.Equals(op.Op, "subtractBox", StringComparison.OrdinalIgnoreCase))
                {
                    // Быстро: построим временную модель и вычтем
                    var tmp = new VoxelModel();
                    tmp.AddBox(x0, y0, x1, y1, z0, z1);
                    vm.SubtractModel(tmp);
                }
                else
                {
                    Console.WriteLine($"[WARN] Unknown op '{op.Op}', skipped.");
                }
            }

            return (vm, vpm);
        }
    }
}
