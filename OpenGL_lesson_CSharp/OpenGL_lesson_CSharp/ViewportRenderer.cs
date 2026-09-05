using System;
using System.Drawing;
using SharpGL;

namespace OpenGL_lesson_CSharp
{
    public enum ViewportRenderMode
    {
        Shaded,
        ShadedEdges,
        Wireframe,
        XRay
    }

    public sealed class RenderSettings
    {
        public bool ShowGrid { get; set; }
        public bool ShowAxes { get; set; }
        public bool Lighting { get; set; }
        public bool ShowOrigin { get; set; }
        public ViewportRenderMode Mode { get; set; }
        public double GridSpacing { get; set; }
        public int GridHalfCount { get; set; }
        public int MajorGridEvery { get; set; }

        public RenderSettings()
        {
            ShowGrid = true; ShowAxes = true; Lighting = true; ShowOrigin = true;
            Mode = ViewportRenderMode.ShadedEdges;
            GridSpacing = 1.0; GridHalfCount = 30; MajorGridEvery = 5;
        }
    }

    public sealed class ViewportRenderer
    {
        public RenderSettings Settings { get; private set; }
        public ViewportRenderer() { Settings = new RenderSettings(); }

        public void Initialize(OpenGL gl)
        {
            gl.ClearColor(0.055f, 0.065f, 0.08f, 1.0f);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LEQUAL);
            gl.Enable(OpenGL.GL_NORMALIZE);
            gl.ShadeModel(OpenGL.GL_SMOOTH);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Hint(OpenGL.GL_PERSPECTIVE_CORRECTION_HINT, OpenGL.GL_NICEST);
        }

        public void Render(OpenGL gl, Camera3D camera, Scene3D scene, int width, int height)
        {
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            camera.Apply(gl, width, height);

            if (Settings.ShowGrid) DrawGrid(gl);
            if (Settings.ShowAxes) DrawAxes(gl);
            DrawObjects(gl, scene);
            gl.Flush();
        }

        private void ConfigureLighting(OpenGL gl, bool enabled)
        {
            if (!enabled)
            {
                gl.Disable(OpenGL.GL_LIGHTING);
                return;
            }
            gl.Enable(OpenGL.GL_LIGHTING);
            gl.Enable(OpenGL.GL_LIGHT0);
            gl.Enable(OpenGL.GL_COLOR_MATERIAL);
            gl.ColorMaterial(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_AMBIENT_AND_DIFFUSE);
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_POSITION, new float[] { 0.35f, 0.8f, 0.55f, 0.0f });
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_AMBIENT, new float[] { 0.26f, 0.28f, 0.32f, 1.0f });
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_DIFFUSE, new float[] { 0.82f, 0.84f, 0.88f, 1.0f });
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_SPECULAR, new float[] { 0.55f, 0.58f, 0.62f, 1.0f });
        }

        private void DrawGrid(OpenGL gl)
        {
            gl.Disable(OpenGL.GL_LIGHTING);
            gl.LineWidth(1.0f);
            int count = Math.Max(1, Settings.GridHalfCount);
            double s = Math.Max(0.0001, Settings.GridSpacing);
            gl.Begin(OpenGL.GL_LINES);
            for (int i = -count; i <= count; i++)
            {
                bool major = i == 0 || (Settings.MajorGridEvery > 0 && i % Settings.MajorGridEvery == 0);
                if (i == 0) gl.Color(0.34f, 0.38f, 0.44f, 0.9f);
                else if (major) gl.Color(0.24f, 0.27f, 0.32f, 0.8f);
                else gl.Color(0.15f, 0.17f, 0.21f, 0.65f);
                double p = i * s;
                gl.Vertex(-count * s, 0.0, p); gl.Vertex(count * s, 0.0, p);
                gl.Vertex(p, 0.0, -count * s); gl.Vertex(p, 0.0, count * s);
            }
            gl.End();
        }

        private void DrawAxes(OpenGL gl)
        {
            gl.Disable(OpenGL.GL_LIGHTING);
            double l = Math.Max(2.0, Settings.GridSpacing * 5.0);
            gl.LineWidth(2.2f);
            gl.Begin(OpenGL.GL_LINES);
            gl.Color(0.95f, 0.25f, 0.22f); gl.Vertex(0,0,0); gl.Vertex(l,0,0);
            gl.Color(0.32f, 0.9f, 0.4f); gl.Vertex(0,0,0); gl.Vertex(0,l,0);
            gl.Color(0.28f, 0.52f, 1.0f); gl.Vertex(0,0,0); gl.Vertex(0,0,l);
            gl.End();
            gl.LineWidth(1.0f);
        }

        private void DrawObjects(OpenGL gl, Scene3D scene)
        {
            bool filled = Settings.Mode != ViewportRenderMode.Wireframe;
            bool xray = Settings.Mode == ViewportRenderMode.XRay;
            if (xray) gl.Disable(OpenGL.GL_DEPTH_TEST); else gl.Enable(OpenGL.GL_DEPTH_TEST);

            foreach (SceneObject obj in scene.Objects)
            {
                if (!obj.Visible || obj.Mesh == null) continue;
                if (filled)
                {
                    gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
                    ConfigureLighting(gl, Settings.Lighting);
                    DrawMesh(gl, obj, xray ? 0.28f : 1.0f, false, false);
                }
                if (Settings.Mode == ViewportRenderMode.Wireframe || Settings.Mode == ViewportRenderMode.ShadedEdges)
                {
                    gl.Disable(OpenGL.GL_LIGHTING);
                    gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
                    gl.LineWidth(obj == scene.Selected ? 2.4f : 1.0f);
                    DrawMesh(gl, obj, 1.0f, true, false);
                }
                if (obj == scene.Selected && Settings.Mode != ViewportRenderMode.Wireframe)
                {
                    gl.Disable(OpenGL.GL_LIGHTING);
                    gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_LINE);
                    gl.LineWidth(2.5f);
                    DrawMesh(gl, obj, 1.0f, true, true);
                }
            }
            gl.LineWidth(1.0f);
            gl.PolygonMode(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_FILL);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }

        private void DrawMesh(OpenGL gl, SceneObject obj, float alpha, bool edges, bool selectedOutline)
        {
            gl.PushMatrix();
            gl.Translate(obj.X, obj.Y, obj.Z);
            gl.Rotate(obj.RotateX, 1, 0, 0); gl.Rotate(obj.RotateY, 0, 1, 0); gl.Rotate(obj.RotateZ, 0, 0, 1);
            gl.Scale(obj.ScaleX, obj.ScaleY, obj.ScaleZ);

            if (selectedOutline) gl.Color(1.0f, 0.72f, 0.12f, alpha);
            else if (edges) gl.Color(0.035f, 0.045f, 0.06f, alpha);
            else
            {
                Color c = obj.Color;
                gl.Color(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, alpha);
            }

            MeshData mesh = obj.Mesh;
            gl.Begin(OpenGL.GL_TRIANGLES);
            for (int i = 0; i < mesh.Indices.Length; i++)
            {
                int vi = mesh.Indices[i];
                if (vi < 0 || vi >= mesh.Positions.Length) continue;
                if (!edges && vi < mesh.Normals.Length)
                {
                    Vec3 n = mesh.Normals[vi];
                    gl.Normal(n.X, n.Y, n.Z);
                }
                Vec3 p = mesh.Positions[vi];
                gl.Vertex(p.X, p.Y, p.Z);
            }
            gl.End();
            gl.PopMatrix();
        }
    }
}
