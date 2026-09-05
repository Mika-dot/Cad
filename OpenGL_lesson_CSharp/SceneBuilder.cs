using System;
using System.IO;
using System.Text.Json;

namespace OpenGL_lesson_CSharp
{
    public static class SceneBuilder
    {
        private static int VoxFloor(double mm, double vpm) => (int)Math.Floor(mm * vpm);
        private static int VoxCeil(double mm, double vpm) => (int)Math.Ceiling(mm * vpm);
        private static double Vox(double mm, double vpm) => mm * vpm;

        public static (VoxelModel model, double voxelsPerMm) FromJsonFile(string path)
        {
            var json = File.ReadAllText(path);
            var scene = JsonSerializer.Deserialize<Scene>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            }) ?? new Scene();

            double vpm = Math.Max(1e-6, scene.VoxelsPerMm);
            var vm = new VoxelModel();

            foreach (var op in scene.Operations)
                ApplyOperation(vm, op, vpm);

            return (vm, vpm);
        }

        public static void ApplyOperation(VoxelModel vm, Operation op, double voxelsPerMm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (op == null) throw new ArgumentNullException(nameof(op));
            double vpm = Math.Max(1e-6, voxelsPerMm);
            string name = (op.Op ?? string.Empty).Trim().ToLowerInvariant();

            // Bounds include the optional translation. They are reused by boxes, TPMS and lattices.
            double x0mm = op.x0 + op.dx, x1mm = op.x1 + op.dx;
            double y0mm = op.y0 + op.dy, y1mm = op.y1 + op.dy;
            double z0mm = op.z0 + op.dz, z1mm = op.z1 + op.dz;
            int x0 = VoxFloor(Math.Min(x0mm, x1mm), vpm);
            int x1 = VoxCeil(Math.Max(x0mm, x1mm), vpm);
            int y0 = VoxFloor(Math.Min(y0mm, y1mm), vpm);
            int y1 = VoxCeil(Math.Max(y0mm, y1mm), vpm);
            int z0 = VoxFloor(Math.Min(z0mm, z1mm), vpm);
            int z1 = VoxCeil(Math.Max(z0mm, z1mm), vpm);

            double cx = Vox(op.cx + op.dx, vpm);
            double cy = Vox(op.cy + op.dy, vpm);
            double cz = Vox(op.cz + op.dz, vpm);
            double radius = Math.Abs(Vox(op.radius, vpm));

            switch (name)
            {
                case "addbox":
                    vm.AddBox(x0, y0, x1, y1, z0, z1);
                    break;
                case "subtractbox":
                    vm.SubtractBox(x0, y0, x1, y1, z0, z1);
                    break;
                case "intersectbox":
                    vm.IntersectBox(x0, y0, x1, y1, z0, z1);
                    break;

                case "addsphere":
                    vm.AddSphere(cx, cy, cz, radius);
                    break;
                case "subtractsphere":
                    vm.SubtractSphere(cx, cy, cz, radius);
                    break;
                case "intersectsphere":
                    vm.IntersectSphere(cx, cy, cz, radius);
                    break;

                case "addcylinderz":
                    vm.AddCylinderZ(cx, cy, Vox(op.z0 + op.dz, vpm), Vox(op.z1 + op.dz, vpm), radius);
                    break;
                case "subtractcylinderz":
                    vm.SubtractCylinderZ(cx, cy, Vox(op.z0 + op.dz, vpm), Vox(op.z1 + op.dz, vpm), radius);
                    break;
                case "intersectcylinderz":
                    vm.IntersectCylinderZ(cx, cy, Vox(op.z0 + op.dz, vpm), Vox(op.z1 + op.dz, vpm), radius);
                    break;

                case "addtorusz":
                    vm.AddTorusZ(cx, cy, cz,
                        Math.Abs(Vox(op.majorRadius, vpm)), Math.Abs(Vox(op.minorRadius, vpm)));
                    break;
                case "subtracttorusz":
                    vm.SubtractTorusZ(cx, cy, cz,
                        Math.Abs(Vox(op.majorRadius, vpm)), Math.Abs(Vox(op.minorRadius, vpm)));
                    break;

                case "addcapsule":
                    vm.AddCapsule(
                        Vox(op.ax + op.dx, vpm), Vox(op.ay + op.dy, vpm), Vox(op.az + op.dz, vpm),
                        Vox(op.bx + op.dx, vpm), Vox(op.by + op.dy, vpm), Vox(op.bz + op.dz, vpm),
                        radius);
                    break;
                case "subtractcapsule":
                    vm.SubtractCapsule(
                        Vox(op.ax + op.dx, vpm), Vox(op.ay + op.dy, vpm), Vox(op.az + op.dz, vpm),
                        Vox(op.bx + op.dx, vpm), Vox(op.by + op.dy, vpm), Vox(op.bz + op.dz, vpm),
                        radius);
                    break;

                case "addgyroid":
                    vm.AddGyroid(x0, y0, z0, x1, y1, z1,
                        Math.Abs(Vox(op.period, vpm)), Math.Abs(Vox(op.thickness, vpm)));
                    break;
                case "subtractgyroid":
                    vm.SubtractGyroid(x0, y0, z0, x1, y1, z1,
                        Math.Abs(Vox(op.period, vpm)), Math.Abs(Vox(op.thickness, vpm)));
                    break;
                case "addschwarzp":
                    vm.AddSchwarzP(x0, y0, z0, x1, y1, z1,
                        Math.Abs(Vox(op.period, vpm)), Math.Abs(Vox(op.thickness, vpm)));
                    break;
                case "subtractschwarzp":
                    vm.SubtractSchwarzP(x0, y0, z0, x1, y1, z1,
                        Math.Abs(Vox(op.period, vpm)), Math.Abs(Vox(op.thickness, vpm)));
                    break;
                case "addbcclattice":
                    vm.AddBccLattice(x0, y0, z0, x1, y1, z1,
                        Math.Abs(Vox(op.cell, vpm)), Math.Abs(Vox(op.strut, vpm)));
                    break;

                case "dilate":
                    vm.Dilate(Math.Max(0, op.iterations), ParseNeighborhood(op.neighborhood));
                    break;
                case "erode":
                    vm.Erode(Math.Max(0, op.iterations), ParseNeighborhood(op.neighborhood));
                    break;
                case "open":
                    vm.Open(Math.Max(0, op.iterations), ParseNeighborhood(op.neighborhood));
                    break;
                case "close":
                    vm.Close(Math.Max(0, op.iterations), ParseNeighborhood(op.neighborhood));
                    break;
                case "smooth":
                    vm.SmoothMajority(Math.Max(0, op.iterations), op.threshold);
                    break;
                case "keeplargest":
                    vm.KeepLargestConnectedComponent();
                    break;

                default:
                    Console.WriteLine($"[WARN] Unknown voxel CAD operation '{op.Op}', skipped.");
                    break;
            }
        }

        private static VoxelNeighborhood ParseNeighborhood(string value)
        {
            switch ((value ?? "6").Trim().ToLowerInvariant())
            {
                case "18":
                case "facesedges18":
                    return VoxelNeighborhood.FacesEdges18;
                case "26":
                case "full26":
                    return VoxelNeighborhood.Full26;
                default:
                    return VoxelNeighborhood.Faces6;
            }
        }
    }
}
