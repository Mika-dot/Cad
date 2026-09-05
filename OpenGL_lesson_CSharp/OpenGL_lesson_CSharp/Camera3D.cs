using System;
using SharpGL;

namespace OpenGL_lesson_CSharp
{
    public enum CameraProjection
    {
        Perspective,
        Orthographic
    }

    public enum StandardView
    {
        Front, Back, Left, Right, Top, Bottom, Isometric
    }

    public sealed class Camera3D
    {
        public Vec3 Target { get; private set; }
        public double YawDegrees { get; private set; }
        public double PitchDegrees { get; private set; }
        public double Distance { get; private set; }
        public double OrthographicHalfHeight { get; private set; }
        public double FieldOfViewDegrees { get; set; }
        public double NearPlane { get; set; }
        public double FarPlane { get; set; }
        public CameraProjection Projection { get; set; }

        public Camera3D()
        {
            Target = Vec3.Zero;
            YawDegrees = 45.0;
            PitchDegrees = 28.0;
            Distance = 18.0;
            OrthographicHalfHeight = 8.0;
            FieldOfViewDegrees = 45.0;
            NearPlane = 0.01;
            FarPlane = 100000.0;
            Projection = CameraProjection.Perspective;
        }

        public Vec3 Forward
        {
            get
            {
                double yaw = YawDegrees * Math.PI / 180.0;
                double pitch = PitchDegrees * Math.PI / 180.0;
                return new Vec3(
                    -Math.Sin(yaw) * Math.Cos(pitch),
                    -Math.Sin(pitch),
                    -Math.Cos(yaw) * Math.Cos(pitch)).Normalized();
            }
        }

        public Vec3 Right
        {
            get
            {
                Vec3 right = Vec3.Cross(Forward, new Vec3(0, 1, 0)).Normalized();
                return right.Length < 1e-9 ? new Vec3(1, 0, 0) : right;
            }
        }

        public Vec3 Up { get { return Vec3.Cross(Right, Forward).Normalized(); } }
        public Vec3 Eye { get { return Target - Forward * Distance; } }

        public void Orbit(double deltaPixelsX, double deltaPixelsY)
        {
            YawDegrees += deltaPixelsX * 0.28;
            PitchDegrees -= deltaPixelsY * 0.28;
            PitchDegrees = Math.Max(-89.5, Math.Min(89.5, PitchDegrees));
        }

        public void Pan(double deltaPixelsX, double deltaPixelsY, int viewportHeight)
        {
            double worldPerPixel;
            if (Projection == CameraProjection.Perspective)
            {
                double halfHeight = Math.Tan(FieldOfViewDegrees * Math.PI / 360.0) * Distance;
                worldPerPixel = 2.0 * halfHeight / Math.Max(1, viewportHeight);
            }
            else
            {
                worldPerPixel = 2.0 * OrthographicHalfHeight / Math.Max(1, viewportHeight);
            }
            Target += Right * (-deltaPixelsX * worldPerPixel) + Up * (deltaPixelsY * worldPerPixel);
        }

        public void MoveLocal(double right, double up, double forward)
        {
            double scale = Math.Max(0.02, Distance * 0.04);
            Target += Right * (right * scale) + Up * (up * scale) + Forward * (forward * scale);
        }

        public void Zoom(int wheelDelta)
        {
            double steps = wheelDelta / 120.0;
            double factor = Math.Pow(0.86, steps);
            Distance = Math.Max(0.02, Distance * factor);
            OrthographicHalfHeight = Math.Max(0.005, OrthographicHalfHeight * factor);
        }

        public void Fit(Bounds3 bounds)
        {
            if (!bounds.IsValid) return;
            Target = bounds.Center;
            double radius = Math.Max(0.05, bounds.Radius);
            double halfFov = Math.Max(5.0, FieldOfViewDegrees) * Math.PI / 360.0;
            Distance = Math.Max(radius * 1.2, radius / Math.Sin(halfFov) * 1.15);
            OrthographicHalfHeight = radius * 1.25;
        }

        public void Focus(Vec3 target, double radius)
        {
            Bounds3 b = new Bounds3();
            b.EncapsulateSphere(target, Math.Max(radius, 0.05));
            Fit(b);
        }

        public void SetView(StandardView view)
        {
            switch (view)
            {
                case StandardView.Front: YawDegrees = 0; PitchDegrees = 0; break;
                case StandardView.Back: YawDegrees = 180; PitchDegrees = 0; break;
                case StandardView.Left: YawDegrees = -90; PitchDegrees = 0; break;
                case StandardView.Right: YawDegrees = 90; PitchDegrees = 0; break;
                case StandardView.Top: YawDegrees = 0; PitchDegrees = -89.4; break;
                case StandardView.Bottom: YawDegrees = 0; PitchDegrees = 89.4; break;
                default: YawDegrees = 45; PitchDegrees = 28; break;
            }
        }

        public void Apply(OpenGL gl, int width, int height)
        {
            double aspect = (double)Math.Max(1, width) / Math.Max(1, height);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            if (Projection == CameraProjection.Perspective)
                gl.Perspective(FieldOfViewDegrees, aspect, NearPlane, FarPlane);
            else
                gl.Ortho(-OrthographicHalfHeight * aspect, OrthographicHalfHeight * aspect,
                    -OrthographicHalfHeight, OrthographicHalfHeight, -FarPlane, FarPlane);

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            Vec3 eye = Eye;
            Vec3 up = Up;
            gl.LookAt(eye.X, eye.Y, eye.Z, Target.X, Target.Y, Target.Z, up.X, up.Y, up.Z);
        }

        public Ray3 BuildRay(int mouseX, int mouseY, int width, int height)
        {
            double w = Math.Max(1, width), h = Math.Max(1, height);
            double nx = 2.0 * mouseX / w - 1.0;
            double ny = 1.0 - 2.0 * mouseY / h;
            double aspect = w / h;

            if (Projection == CameraProjection.Orthographic)
            {
                Vec3 origin = Eye + Right * (nx * OrthographicHalfHeight * aspect) + Up * (ny * OrthographicHalfHeight);
                return new Ray3(origin, Forward);
            }

            double tan = Math.Tan(FieldOfViewDegrees * Math.PI / 360.0);
            Vec3 direction = (Forward + Right * (nx * tan * aspect) + Up * (ny * tan)).Normalized();
            return new Ray3(Eye, direction);
        }
    }
}
