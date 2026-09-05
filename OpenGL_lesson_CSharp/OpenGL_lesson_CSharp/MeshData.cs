using System;
using System.Collections.Generic;

namespace OpenGL_lesson_CSharp
{
    public sealed class MeshData
    {
        public Vec3[] Positions { get; private set; }
        public Vec3[] Normals { get; private set; }
        public int[] Indices { get; private set; }
        public double BoundingRadius { get; private set; }

        public MeshData(Vec3[] positions, Vec3[] normals, int[] indices)
        {
            Positions = positions ?? new Vec3[0];
            Normals = normals ?? new Vec3[0];
            Indices = indices ?? new int[0];
            double r = 0;
            for (int i = 0; i < Positions.Length; i++) r = Math.Max(r, Positions[i].Length);
            BoundingRadius = Math.Max(r, 0.001);
        }
    }

    public static class MeshFactory
    {
        public static MeshData Cube(double size)
        {
            double h = size * 0.5;
            List<Vec3> p = new List<Vec3>();
            List<Vec3> n = new List<Vec3>();
            List<int> idx = new List<int>();
            AddFace(p, n, idx, new Vec3(0, 0, 1), new Vec3(-h,-h,h), new Vec3(h,-h,h), new Vec3(h,h,h), new Vec3(-h,h,h));
            AddFace(p, n, idx, new Vec3(0, 0,-1), new Vec3(h,-h,-h), new Vec3(-h,-h,-h), new Vec3(-h,h,-h), new Vec3(h,h,-h));
            AddFace(p, n, idx, new Vec3(1, 0, 0), new Vec3(h,-h,h), new Vec3(h,-h,-h), new Vec3(h,h,-h), new Vec3(h,h,h));
            AddFace(p, n, idx, new Vec3(-1,0,0), new Vec3(-h,-h,-h), new Vec3(-h,-h,h), new Vec3(-h,h,h), new Vec3(-h,h,-h));
            AddFace(p, n, idx, new Vec3(0, 1, 0), new Vec3(-h,h,h), new Vec3(h,h,h), new Vec3(h,h,-h), new Vec3(-h,h,-h));
            AddFace(p, n, idx, new Vec3(0,-1,0), new Vec3(-h,-h,-h), new Vec3(h,-h,-h), new Vec3(h,-h,h), new Vec3(-h,-h,h));
            return new MeshData(p.ToArray(), n.ToArray(), idx.ToArray());
        }

        public static MeshData Sphere(double radius, int slices, int stacks)
        {
            slices = Math.Max(8, slices); stacks = Math.Max(4, stacks);
            List<Vec3> p = new List<Vec3>();
            List<Vec3> n = new List<Vec3>();
            List<int> idx = new List<int>();
            for (int y = 0; y <= stacks; y++)
            {
                double v = (double)y / stacks;
                double phi = Math.PI * (v - 0.5);
                double cp = Math.Cos(phi), sp = Math.Sin(phi);
                for (int x = 0; x <= slices; x++)
                {
                    double u = (double)x / slices;
                    double theta = 2.0 * Math.PI * u;
                    Vec3 normal = new Vec3(Math.Cos(theta) * cp, sp, Math.Sin(theta) * cp).Normalized();
                    n.Add(normal); p.Add(normal * radius);
                }
            }
            int stride = slices + 1;
            for (int y = 0; y < stacks; y++)
                for (int x = 0; x < slices; x++)
                {
                    int a = y * stride + x, b = a + 1, c = a + stride + 1, d = a + stride;
                    idx.Add(a); idx.Add(b); idx.Add(c); idx.Add(a); idx.Add(c); idx.Add(d);
                }
            return new MeshData(p.ToArray(), n.ToArray(), idx.ToArray());
        }

        private static void AddFace(List<Vec3> p, List<Vec3> n, List<int> idx, Vec3 normal, Vec3 a, Vec3 b, Vec3 c, Vec3 d)
        {
            int start = p.Count;
            p.Add(a); p.Add(b); p.Add(c); p.Add(d);
            for (int i = 0; i < 4; i++) n.Add(normal);
            idx.Add(start); idx.Add(start + 1); idx.Add(start + 2);
            idx.Add(start); idx.Add(start + 2); idx.Add(start + 3);
        }
    }
}
