using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenGL_lesson_CSharp.Modern
{
    public struct LayerRun
    {
        public readonly int Z;
        public readonly int Y;
        public readonly int X0;
        public readonly int X1Exclusive;
        public readonly byte Material;

        public LayerRun(int z, int y, int x0, int x1Exclusive, byte material)
        {
            Z = z; Y = y; X0 = x0; X1Exclusive = x1Exclusive; Material = material;
        }

        public int CellCount { get { return Math.Max(0, X1Exclusive - X0); } }
    }

    public struct ManufacturingEstimate
    {
        public readonly int Layers;
        public readonly int Runs;
        public readonly int ActiveCells;
        public readonly double DepositionLengthMm;
        public readonly double ApproxVolumeMm3;

        public ManufacturingEstimate(int layers, int runs, int activeCells, double lengthMm, double volumeMm3)
        {
            Layers = layers; Runs = runs; ActiveCells = activeCells;
            DepositionLengthMm = lengthMm; ApproxVolumeMm3 = volumeMm3;
        }
    }

    /// <summary>
    /// Manufacturing-oriented utilities for the historical V1 voxel model.
    /// The old exporter emitted one G1 point per voxel.  This planner compresses contiguous
    /// X-neighbours into layer runs, which is a more useful intermediate representation for a
    /// future slicer/toolpath module in Unified-CAD.
    /// </summary>
    public static class VoxelManufacturing
    {
        public static List<LayerRun> ExtractRuns(SparseVoxelGrid grid)
        {
            if (grid == null) throw new ArgumentNullException("grid");
            var result = new List<LayerRun>();
            foreach (var layer in grid.Cells.GroupBy(p => p.Key.Z).OrderBy(g => g.Key))
            {
                foreach (var row in layer.GroupBy(p => p.Key.Y).OrderBy(g => g.Key))
                {
                    var ordered = row.OrderBy(p => p.Key.X).ToArray();
                    if (ordered.Length == 0) continue;
                    int start = ordered[0].Key.X;
                    int previous = start;
                    byte material = ordered[0].Value.Material;
                    for (int i = 1; i < ordered.Length; i++)
                    {
                        int x = ordered[i].Key.X;
                        byte m = ordered[i].Value.Material;
                        if (x != previous + 1 || m != material)
                        {
                            result.Add(new LayerRun(layer.Key, row.Key, start, previous + 1, material));
                            start = x;
                            material = m;
                        }
                        previous = x;
                    }
                    result.Add(new LayerRun(layer.Key, row.Key, start, previous + 1, material));
                }
            }
            return result;
        }

        public static ManufacturingEstimate Estimate(SparseVoxelGrid grid, double voxelSizeMm)
        {
            if (!(voxelSizeMm > 0.0)) throw new ArgumentOutOfRangeException("voxelSizeMm");
            var runs = ExtractRuns(grid);
            int layers = runs.Select(r => r.Z).Distinct().Count();
            double length = runs.Sum(r => r.CellCount * voxelSizeMm);
            double volume = grid.Count * voxelSizeMm * voxelSizeMm * voxelSizeMm;
            return new ManufacturingEstimate(layers, runs.Count, grid.Count, length, volume);
        }

        public static void ExportRunGCode(
            string path,
            SparseVoxelGrid grid,
            double voxelSizeMm = 1.0,
            double feedMmPerMin = 1200.0,
            double travelMmPerMin = 3000.0,
            double extrusionPerMm = 0.04)
        {
            if (grid == null) throw new ArgumentNullException("grid");
            if (!(voxelSizeMm > 0.0)) throw new ArgumentOutOfRangeException("voxelSizeMm");
            var runs = ExtractRuns(grid);
            var ci = CultureInfo.InvariantCulture;
            double e = 0.0;

            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("; DCad V1 run-compressed voxel toolpath");
                writer.WriteLine("; research output: validate machine/material settings before physical use");
                writer.WriteLine("G21 ; millimetres");
                writer.WriteLine("G90 ; absolute XYZ");
                writer.WriteLine("M82 ; absolute extrusion");

                foreach (var layer in runs.GroupBy(r => r.Z).OrderBy(g => g.Key))
                {
                    writer.WriteLine("; layer " + layer.Key.ToString(ci));
                    bool reverse = false;
                    foreach (var run in layer.OrderBy(r => r.Y).ThenBy(r => r.X0))
                    {
                        double z = (run.Z + 0.5) * voxelSizeMm;
                        double y = (run.Y + 0.5) * voxelSizeMm;
                        double xa = (run.X0 + 0.5) * voxelSizeMm;
                        double xb = (run.X1Exclusive - 0.5) * voxelSizeMm;
                        double start = reverse ? xb : xa;
                        double end = reverse ? xa : xb;
                        reverse = !reverse;

                        writer.WriteLine(string.Format(ci, "G0 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3:0.#}", start, y, z, travelMmPerMin));
                        double runLength = Math.Max(voxelSizeMm, Math.Abs(end - start) + voxelSizeMm);
                        e += runLength * Math.Max(0.0, extrusionPerMm);
                        writer.WriteLine(string.Format(ci, "G1 X{0:0.###} Y{1:0.###} Z{2:0.###} E{3:0.#####} F{4:0.#}", end, y, z, e, feedMmPerMin));
                    }
                }
            }
        }

        public static void SelfTest()
        {
            var grid = new SparseVoxelGrid();
            grid.AddBox(0, 0, 0, 10, 2, 2, 1);
            var runs = ExtractRuns(grid);
            if (runs.Count != 4) throw new InvalidOperationException("Run extraction self-test failed.");
            var estimate = Estimate(grid, 0.5);
            if (estimate.Layers != 2 || estimate.ActiveCells != 40) throw new InvalidOperationException("Manufacturing estimate self-test failed.");
            if (Math.Abs(estimate.ApproxVolumeMm3 - 5.0) > 1e-9) throw new InvalidOperationException("Manufacturing volume self-test failed.");
        }
    }
}
