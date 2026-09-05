using BriefFiniteElementNet;
using BriefFiniteElementNet.Elements;
using BriefFiniteElementNet.Materials;
using BriefFiniteElementNet.Sections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OpenGL_lesson_CSharp
{
    public sealed partial class VoxelModel
    {
        // Reference sparse backend. Modern field/morphology methods live in VoxelModel.Modern.cs.
        private readonly HashSet<(int x, int y, int z)> _vox = new HashSet<(int x, int y, int z)>();

        public int Count => _vox.Count;
        public IEnumerable<(int x, int y, int z)> GetVoxels() => _vox;

        public void AddBox(int x0, int y0, int x1, int y1, int z0, int z1)
        {
            if (x1 <= x0 || y1 <= y0 || z1 <= z0) return;
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        _vox.Add((x, y, z));
        }

        public void AddModel(VoxelModel other, int dx = 0, int dy = 0, int dz = 0)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var (x, y, z) in other._vox)
                _vox.Add((x + dx, y + dy, z + dz));
        }

        public void SubtractModel(VoxelModel other, int dx = 0, int dy = 0, int dz = 0)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var (x, y, z) in other._vox)
                _vox.Remove((x + dx, y + dy, z + dz));
        }

        /// <summary>
        /// Lightweight structural bridge used by the historical WinForms demo.
        /// Coordinates and voxelSize are millimetres; force is N, therefore steel E is N/mm^2 (MPa).
        /// This remains a frame-network approximation, not an 8-node solid hexahedral voxel FEM.
        /// </summary>
        public Model ToFiniteElementModel(float voxelSize, out Dictionary<(int, int, int), Node> nodeMap)
        {
            var model = new Model();
            nodeMap = new Dictionary<(int x, int y, int z), Node>();

            const double E = 210000.0; // steel, N/mm^2 (MPa)
            const double nu = 0.3;
            var material = UniformIsotropicMaterial.CreateFromYoungPoisson(E, nu);

            double b = voxelSize * 0.5;
            double h = voxelSize * 0.5;
            double A = b * h;
            double Iy = Math.Pow(h, 3) * b / 12.0;
            double Iz = Math.Pow(b, 3) * h / 12.0;
            double J = Iy + Iz;
            var section = new UniformParametric1DSection(A, Iy, Iz, J);

            foreach (var (x, y, z) in _vox)
            {
                float px = (x + 0.5f) * voxelSize;
                float py = (y + 0.5f) * voxelSize;
                float pz = (z + 0.5f) * voxelSize;
                var node = new Node(px, py, pz) { Label = $"n_{x}_{y}_{z}" };
                model.Nodes.Add(node);
                nodeMap[(x, y, z)] = node;
            }

            // Positive directions only: every neighbour pair is emitted exactly once.
            var directions = new (int dx, int dy, int dz)[] { (1, 0, 0), (0, 1, 0), (0, 0, 1) };
            foreach (var (x, y, z) in _vox)
            {
                var nodeA = nodeMap[(x, y, z)];
                foreach (var (dx, dy, dz) in directions)
                {
                    var neighbor = (x + dx, y + dy, z + dz);
                    if (!_vox.Contains(neighbor) || !nodeMap.TryGetValue(neighbor, out var nodeB)) continue;
                    model.Elements.Add(new BarElement(nodeA, nodeB)
                    {
                        Material = material,
                        Section = section,
                        Behavior = BarElementBehaviours.FullFrame
                    });
                }
            }
            return model;
        }

        public void ExportStl(string path, float voxelSize = 1.0f)
        {
            WriteBinaryStl(path, BuildTriangles(voxelSize));
        }

        public void ExportStlAscii(string path, float voxelSize = 1.0f, string solidName = "voxel_model")
        {
            var tris = BuildTriangles(voxelSize);
            using (var sw = new StreamWriter(path))
            {
                var inv = CultureInfo.InvariantCulture;
                sw.WriteLine($"solid {solidName}");
                foreach (var t in tris)
                {
                    sw.WriteLine($"  facet normal {t.nx.ToString("G9", inv)} {t.ny.ToString("G9", inv)} {t.nz.ToString("G9", inv)}");
                    sw.WriteLine("    outer loop");
                    sw.WriteLine($"      vertex {t.x1.ToString("G9", inv)} {t.y1.ToString("G9", inv)} {t.z1.ToString("G9", inv)}");
                    sw.WriteLine($"      vertex {t.x2.ToString("G9", inv)} {t.y2.ToString("G9", inv)} {t.z2.ToString("G9", inv)}");
                    sw.WriteLine($"      vertex {t.x3.ToString("G9", inv)} {t.y3.ToString("G9", inv)} {t.z3.ToString("G9", inv)}");
                    sw.WriteLine("    endloop");
                    sw.WriteLine("  endfacet");
                }
                sw.WriteLine($"endsolid {solidName}");
            }
        }

        private static void WriteBinaryStl(string path, List<Tri> tris)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new byte[80]);
                bw.Write((uint)tris.Count);
                foreach (var t in tris)
                {
                    bw.Write(t.nx); bw.Write(t.ny); bw.Write(t.nz);
                    bw.Write(t.x1); bw.Write(t.y1); bw.Write(t.z1);
                    bw.Write(t.x2); bw.Write(t.y2); bw.Write(t.z2);
                    bw.Write(t.x3); bw.Write(t.y3); bw.Write(t.z3);
                    bw.Write((ushort)0);
                }
            }
        }

        private List<Tri> BuildTriangles(float voxelSize)
        {
            var tris = new List<Tri>();
            foreach (var (x, y, z) in _vox)
            {
                float X = x * voxelSize, X1 = (x + 1) * voxelSize;
                float Y = y * voxelSize, Y1 = (y + 1) * voxelSize;
                float Z = z * voxelSize, Z1 = (z + 1) * voxelSize;

                if (!Has(x - 1, y, z)) AddQuad(tris, (-1, 0, 0), (X, Y, Z), (X, Y1, Z), (X, Y1, Z1), (X, Y, Z1));
                if (!Has(x + 1, y, z)) AddQuad(tris, (1, 0, 0), (X1, Y, Z), (X1, Y, Z1), (X1, Y1, Z1), (X1, Y1, Z));
                if (!Has(x, y - 1, z)) AddQuad(tris, (0, -1, 0), (X, Y, Z), (X, Y, Z1), (X1, Y, Z1), (X1, Y, Z));
                if (!Has(x, y + 1, z)) AddQuad(tris, (0, 1, 0), (X, Y1, Z), (X1, Y1, Z), (X1, Y1, Z1), (X, Y1, Z1));
                if (!Has(x, y, z - 1)) AddQuad(tris, (0, 0, -1), (X, Y, Z), (X1, Y, Z), (X1, Y1, Z), (X, Y1, Z));
                if (!Has(x, y, z + 1)) AddQuad(tris, (0, 0, 1), (X, Y, Z1), (X, Y1, Z1), (X1, Y1, Z1), (X1, Y, Z1));
            }
            return tris;
        }

        private bool Has(int x, int y, int z) => _vox.Contains((x, y, z));

        private readonly struct Tri
        {
            public readonly float nx, ny, nz;
            public readonly float x1, y1, z1, x2, y2, z2, x3, y3, z3;
            public Tri((float, float, float) n, (float, float, float) a, (float, float, float) b, (float, float, float) c)
            {
                (nx, ny, nz) = n;
                (x1, y1, z1) = a; (x2, y2, z2) = b; (x3, y3, z3) = c;
            }
        }

        private static void AddQuad(List<Tri> tris,
            (float nx, float ny, float nz) n,
            (float x, float y, float z) v0,
            (float x, float y, float z) v1,
            (float x, float y, float z) v2,
            (float x, float y, float z) v3)
        {
            tris.Add(new Tri(n, v0, v1, v2));
            tris.Add(new Tri(n, v0, v2, v3));
        }
    }
}
