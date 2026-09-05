using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenGL_lesson_CSharp.Modern
{
    public struct VoxelKey : IEquatable<VoxelKey>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public VoxelKey(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(VoxelKey other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is VoxelKey && Equals((VoxelKey)obj);
        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + X;
                h = h * 31 + Y;
                h = h * 31 + Z;
                return h;
            }
        }
        public override string ToString() => X + "," + Y + "," + Z;
    }

    public struct VoxelCell
    {
        public byte Material;
        public float Scalar;

        public VoxelCell(byte material, float scalar = 0f)
        {
            Material = material;
            Scalar = scalar;
        }
    }

    public enum VoxelFaceDirection
    {
        NegativeX, PositiveX, NegativeY, PositiveY, NegativeZ, PositiveZ
    }

    public struct VoxelFace
    {
        public readonly VoxelKey Cell;
        public readonly VoxelFaceDirection Direction;
        public readonly VoxelCell Data;

        public VoxelFace(VoxelKey cell, VoxelFaceDirection direction, VoxelCell data)
        {
            Cell = cell;
            Direction = direction;
            Data = data;
        }
    }

    /// <summary>
    /// Sparse voxel solid for the V1 research branch. Empty space consumes no cells.
    /// This is deliberately dependency-free so it can later move into the unified CAD core.
    /// </summary>
    public sealed class SparseVoxelGrid
    {
        private readonly Dictionary<VoxelKey, VoxelCell> _cells = new Dictionary<VoxelKey, VoxelCell>();

        public int Count => _cells.Count;
        public IEnumerable<KeyValuePair<VoxelKey, VoxelCell>> Cells => _cells;

        public bool Contains(int x, int y, int z) => _cells.ContainsKey(new VoxelKey(x, y, z));
        public bool TryGet(int x, int y, int z, out VoxelCell cell) => _cells.TryGetValue(new VoxelKey(x, y, z), out cell);
        public void Set(int x, int y, int z, byte material = 1, float scalar = 0f) => _cells[new VoxelKey(x, y, z)] = new VoxelCell(material, scalar);
        public bool Remove(int x, int y, int z) => _cells.Remove(new VoxelKey(x, y, z));
        public void Clear() => _cells.Clear();

        public SparseVoxelGrid Clone()
        {
            var copy = new SparseVoxelGrid();
            foreach (var pair in _cells) copy._cells[pair.Key] = pair.Value;
            return copy;
        }

        public void UnionWith(SparseVoxelGrid other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var pair in other._cells) _cells[pair.Key] = pair.Value;
        }

        public void Subtract(SparseVoxelGrid other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            foreach (var key in other._cells.Keys) _cells.Remove(key);
        }

        public void IntersectWith(SparseVoxelGrid other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var remove = _cells.Keys.Where(k => !other._cells.ContainsKey(k)).ToArray();
            for (int i = 0; i < remove.Length; i++) _cells.Remove(remove[i]);
        }

        public void AddBox(int x0, int y0, int z0, int x1, int y1, int z1, byte material = 1)
        {
            Normalize(ref x0, ref x1); Normalize(ref y0, ref y1); Normalize(ref z0, ref z1);
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        Set(x, y, z, material);
        }

        public void SubtractBox(int x0, int y0, int z0, int x1, int y1, int z1)
        {
            Normalize(ref x0, ref x1); Normalize(ref y0, ref y1); Normalize(ref z0, ref z1);
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    for (int z = z0; z < z1; z++)
                        Remove(x, y, z);
        }

        public void AddSphere(double cx, double cy, double cz, double radius, byte material = 1)
        {
            if (radius <= 0) return;
            double r2 = radius * radius;
            int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
            int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);
            int z0 = (int)Math.Floor(cz - radius), z1 = (int)Math.Ceiling(cz + radius);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        double dx = x + .5 - cx, dy = y + .5 - cy, dz = z + .5 - cz;
                        if (dx * dx + dy * dy + dz * dz <= r2) Set(x, y, z, material);
                    }
        }

        public void SubtractSphere(double cx, double cy, double cz, double radius)
        {
            if (radius <= 0) return;
            double r2 = radius * radius;
            var remove = new List<VoxelKey>();
            foreach (var pair in _cells)
            {
                double dx = pair.Key.X + .5 - cx, dy = pair.Key.Y + .5 - cy, dz = pair.Key.Z + .5 - cz;
                if (dx * dx + dy * dy + dz * dz <= r2) remove.Add(pair.Key);
            }
            for (int i = 0; i < remove.Count; i++) _cells.Remove(remove[i]);
        }

        public void AddCylinderZ(double cx, double cy, int z0, int z1, double radius, byte material = 1)
        {
            if (radius <= 0) return;
            Normalize(ref z0, ref z1);
            double r2 = radius * radius;
            int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
            int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    double dx = x + .5 - cx, dy = y + .5 - cy;
                    if (dx * dx + dy * dy > r2) continue;
                    for (int z = z0; z < z1; z++) Set(x, y, z, material);
                }
        }

        public void ExtrudePolygon(IList<double> xs, IList<double> ys, int z0, int z1, byte material = 1, bool subtract = false)
        {
            if (xs == null || ys == null || xs.Count != ys.Count || xs.Count < 3)
                throw new ArgumentException("Polygon must contain at least three paired X/Y coordinates.");

            Normalize(ref z0, ref z1);
            int minX = (int)Math.Floor(xs.Min());
            int maxX = (int)Math.Ceiling(xs.Max());
            int minY = (int)Math.Floor(ys.Min());
            int maxY = (int)Math.Ceiling(ys.Max());

            for (int x = minX; x < maxX; x++)
                for (int y = minY; y < maxY; y++)
                {
                    if (!PointInPolygon(x + .5, y + .5, xs, ys)) continue;
                    for (int z = z0; z < z1; z++)
                    {
                        if (subtract) Remove(x, y, z); else Set(x, y, z, material);
                    }
                }
        }

        public void Translate(int dx, int dy, int dz)
        {
            if (dx == 0 && dy == 0 && dz == 0) return;
            var moved = new Dictionary<VoxelKey, VoxelCell>(_cells.Count);
            foreach (var pair in _cells)
                moved[new VoxelKey(pair.Key.X + dx, pair.Key.Y + dy, pair.Key.Z + dz)] = pair.Value;
            _cells.Clear();
            foreach (var pair in moved) _cells[pair.Key] = pair.Value;
        }

        public void SetScalar(Func<int, int, int, float> field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            var keys = _cells.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                VoxelCell c = _cells[keys[i]];
                c.Scalar = field(keys[i].X, keys[i].Y, keys[i].Z);
                _cells[keys[i]] = c;
            }
        }

        public IEnumerable<VoxelFace> SurfaceFaces()
        {
            foreach (var pair in _cells)
            {
                var p = pair.Key;
                if (!Contains(p.X - 1, p.Y, p.Z)) yield return new VoxelFace(p, VoxelFaceDirection.NegativeX, pair.Value);
                if (!Contains(p.X + 1, p.Y, p.Z)) yield return new VoxelFace(p, VoxelFaceDirection.PositiveX, pair.Value);
                if (!Contains(p.X, p.Y - 1, p.Z)) yield return new VoxelFace(p, VoxelFaceDirection.NegativeY, pair.Value);
                if (!Contains(p.X, p.Y + 1, p.Z)) yield return new VoxelFace(p, VoxelFaceDirection.PositiveY, pair.Value);
                if (!Contains(p.X, p.Y, p.Z - 1)) yield return new VoxelFace(p, VoxelFaceDirection.NegativeZ, pair.Value);
                if (!Contains(p.X, p.Y, p.Z + 1)) yield return new VoxelFace(p, VoxelFaceDirection.PositiveZ, pair.Value);
            }
        }

        public int SurfaceFaceCount() => SurfaceFaces().Count();

        public bool TryGetBounds(out VoxelKey min, out VoxelKey maxExclusive)
        {
            min = new VoxelKey(); maxExclusive = new VoxelKey();
            if (_cells.Count == 0) return false;
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            foreach (var p in _cells.Keys)
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
                maxX = Math.Max(maxX, p.X + 1); maxY = Math.Max(maxY, p.Y + 1); maxZ = Math.Max(maxZ, p.Z + 1);
            }
            min = new VoxelKey(minX, minY, minZ);
            maxExclusive = new VoxelKey(maxX, maxY, maxZ);
            return true;
        }

        private static bool PointInPolygon(double x, double y, IList<double> xs, IList<double> ys)
        {
            bool inside = false;
            int j = xs.Count - 1;
            for (int i = 0; i < xs.Count; j = i++)
            {
                bool crosses = ((ys[i] > y) != (ys[j] > y));
                if (crosses)
                {
                    double hitX = (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i];
                    if (x < hitX) inside = !inside;
                }
            }
            return inside;
        }

        private static void Normalize(ref int a, ref int b)
        {
            if (b < a) { int t = a; a = b; b = t; }
        }
    }

    /// <summary>Simple layer-wise toolpath planner for voxel solids.</summary>
    public static class VoxelGCodePlanner
    {
        public static void Export(string path, SparseVoxelGrid grid, double voxelSizeMm = 1.0, double feed = 1200.0)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("; DCad V1 modern sparse voxel toolpath");
                writer.WriteLine("G21");
                writer.WriteLine("G90");

                foreach (var layer in grid.Cells.GroupBy(p => p.Key.Z).OrderBy(g => g.Key))
                {
                    writer.WriteLine("; layer z=" + layer.Key);
                    bool reverse = false;
                    foreach (var row in layer.GroupBy(p => p.Key.Y).OrderBy(g => g.Key))
                    {
                        var cells = reverse
                            ? row.OrderByDescending(p => p.Key.X).ToArray()
                            : row.OrderBy(p => p.Key.X).ToArray();
                        reverse = !reverse;
                        for (int i = 0; i < cells.Length; i++)
                        {
                            double x = (cells[i].Key.X + .5) * voxelSizeMm;
                            double y = (cells[i].Key.Y + .5) * voxelSizeMm;
                            double z = (cells[i].Key.Z + .5) * voxelSizeMm;
                            writer.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                "G1 X{0:0.###} Y{1:0.###} Z{2:0.###} F{3:0.#}", x, y, z, feed));
                        }
                    }
                }
            }
        }
    }
}
