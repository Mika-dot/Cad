using System;

namespace OpenGL_lesson_CSharp
{
    public struct Vec3
    {
        public double X;
        public double Y;
        public double Z;

        public Vec3(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vec3 Zero { get { return new Vec3(0, 0, 0); } }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }

        public Vec3 Normalized()
        {
            double l = Length;
            return l < 1e-12 ? Zero : this / l;
        }

        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, double s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
        public static Vec3 operator *(double s, Vec3 a) { return a * s; }
        public static Vec3 operator /(Vec3 a, double s) { return new Vec3(a.X / s, a.Y / s, a.Z / s); }

        public override string ToString() { return string.Format("{0:0.###}, {1:0.###}, {2:0.###}", X, Y, Z); }
    }

    public struct Ray3
    {
        public Vec3 Origin;
        public Vec3 Direction;
        public Ray3(Vec3 origin, Vec3 direction) { Origin = origin; Direction = direction.Normalized(); }
    }

    public struct Bounds3
    {
        public Vec3 Min;
        public Vec3 Max;
        public bool IsValid;

        public Vec3 Center { get { return (Min + Max) * 0.5; } }
        public Vec3 Size { get { return Max - Min; } }
        public double Radius { get { return Size.Length * 0.5; } }

        public void Encapsulate(Vec3 p)
        {
            if (!IsValid)
            {
                Min = Max = p;
                IsValid = true;
                return;
            }
            Min = new Vec3(Math.Min(Min.X, p.X), Math.Min(Min.Y, p.Y), Math.Min(Min.Z, p.Z));
            Max = new Vec3(Math.Max(Max.X, p.X), Math.Max(Max.Y, p.Y), Math.Max(Max.Z, p.Z));
        }

        public void EncapsulateSphere(Vec3 center, double radius)
        {
            Vec3 r = new Vec3(radius, radius, radius);
            Encapsulate(center - r);
            Encapsulate(center + r);
        }
    }
}
