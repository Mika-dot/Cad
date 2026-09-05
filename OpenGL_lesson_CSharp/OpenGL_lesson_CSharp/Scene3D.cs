using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace OpenGL_lesson_CSharp
{
    public sealed class SceneObject
    {
        [Browsable(false)] public int Id { get; internal set; }
        [Category("Object")] public string Name { get; set; }
        [Category("Object")] public bool Visible { get; set; }
        [Category("Object")] public bool Selectable { get; set; }
        [Category("Transform")] public double X { get; set; }
        [Category("Transform")] public double Y { get; set; }
        [Category("Transform")] public double Z { get; set; }
        [Category("Transform")] public double RotateX { get; set; }
        [Category("Transform")] public double RotateY { get; set; }
        [Category("Transform")] public double RotateZ { get; set; }
        [Category("Transform")] public double ScaleX { get; set; }
        [Category("Transform")] public double ScaleY { get; set; }
        [Category("Transform")] public double ScaleZ { get; set; }
        [Category("Appearance")] public Color Color { get; set; }
        [Browsable(false)] public MeshData Mesh { get; set; }

        public SceneObject(string name, MeshData mesh)
        {
            Name = name; Mesh = mesh; Visible = true; Selectable = true;
            ScaleX = ScaleY = ScaleZ = 1.0;
            Color = Color.FromArgb(194, 205, 218);
        }

        [Browsable(false)] public Vec3 Position { get { return new Vec3(X, Y, Z); } }
        [Browsable(false)] public double WorldBoundingRadius
        {
            get
            {
                double scale = Math.Max(Math.Abs(ScaleX), Math.Max(Math.Abs(ScaleY), Math.Abs(ScaleZ)));
                return (Mesh == null ? 0.1 : Mesh.BoundingRadius) * Math.Max(scale, 0.0001);
            }
        }

        public override string ToString() { return Name; }
    }

    public sealed class Scene3D
    {
        private readonly List<SceneObject> objects = new List<SceneObject>();
        private int nextId = 1;
        public IList<SceneObject> Objects { get { return objects.AsReadOnly(); } }
        public SceneObject Selected { get; private set; }

        public event EventHandler SelectionChanged;
        public event EventHandler SceneChanged;

        public SceneObject Add(SceneObject obj)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            obj.Id = nextId++;
            objects.Add(obj);
            OnSceneChanged();
            return obj;
        }

        public void Remove(SceneObject obj)
        {
            if (obj == null) return;
            if (Selected == obj) Select(null);
            objects.Remove(obj);
            OnSceneChanged();
        }

        public void Select(SceneObject obj)
        {
            if (Selected == obj) return;
            Selected = obj;
            EventHandler handler = SelectionChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public Bounds3 GetBounds()
        {
            Bounds3 b = new Bounds3();
            foreach (SceneObject obj in objects)
                if (obj.Visible) b.EncapsulateSphere(obj.Position, obj.WorldBoundingRadius);
            return b;
        }

        public SceneObject Pick(Ray3 ray)
        {
            SceneObject best = null;
            double bestT = double.MaxValue;
            foreach (SceneObject obj in objects)
            {
                if (!obj.Visible || !obj.Selectable) continue;
                Vec3 oc = ray.Origin - obj.Position;
                double r = obj.WorldBoundingRadius;
                double a = Vec3.Dot(ray.Direction, ray.Direction);
                double b = 2.0 * Vec3.Dot(oc, ray.Direction);
                double c = Vec3.Dot(oc, oc) - r * r;
                double d = b * b - 4 * a * c;
                if (d < 0) continue;
                double sd = Math.Sqrt(d);
                double t0 = (-b - sd) / (2 * a), t1 = (-b + sd) / (2 * a);
                double t = t0 >= 0 ? t0 : t1;
                if (t >= 0 && t < bestT) { bestT = t; best = obj; }
            }
            Select(best);
            return best;
        }

        public void NotifyChanged() { OnSceneChanged(); }
        private void OnSceneChanged()
        {
            EventHandler handler = SceneChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
