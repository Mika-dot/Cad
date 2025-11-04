using BriefFiniteElementNet;
using BriefFiniteElementNet.Elements;
using BriefFiniteElementNet.Materials;
using BriefFiniteElementNet.Sections;
using SharpGL;
using SharpGL.SceneGraph;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        // === Камера ===
        float AngleX = 0, AngleY = 0;
        double POSX = 15, POSY = 15, POSZ = 80; // дальше камеру, чтобы влезли 2 вида
        const float Rad = 3.14f / 180f;

        // === Свет ===
        private bool lightingEnabled = true;
        private float lightPositionX = 10f;
        private float lightPositionY = 20f;
        private float lightPositionZ = 10f;

        // === Воксельные данные ===
        private List<Voxel> voxelsOriginal = new List<Voxel>();   // исходная геометрия (CAD)
        private List<Voxel> voxelsDeformed = new List<Voxel>();   // деформированная геометрия (FEM)
        private float voxelCubeHalf = 0.45f;                      // половина ребра кубика для рисования

        // === FEM-модель ===
        private VoxelModel vm;
        private Model femModel;
        private Dictionary<(int x, int y, int z), Node> nodeMap;

        // Слои для условий
        private int minZLayer, maxZLayer;

        // Карта перемещений (в метрах, из FEM) по центрам вокселей
        private Dictionary<(int x, int y, int z), (double dx, double dy, double dz)> voxelDisp = new Dictionary<(int x, int y, int z), (double dx, double dy, double dz)>();

        // Для раскраски по величине перемещения
        private double maxDispMagnitude = 1e-12; // чтобы избежать деления на ноль

        // Масштаб визуализации перемещений (увеличение деформаций)
        private double deformationScale = 200.0; // подберите под себя

        // Сдвиг правой сцены относительно левой (по X)
        private float deformedViewOffsetX = 60f;

        // Визуализация опор/нагрузок
        private bool showBCs = true;

        public SharpGLForm()
        {
            InitializeComponent();
            InitializeVoxels();     // строим CAD + FEM + деформации
            this.KeyPreview = true;
            this.DoubleBuffered = true;
        }

        /// <summary>
        /// Вместо старой псевдосцены теперь:
        /// 1) Строим VoxelModel (большой куб минус внутренний).
        /// 2) Конвертим в FEM-модель, ставим закрепления и нагрузку, решаем.
        /// 3) Готовим две коллекции кубов: исходные и деформированные.
        /// </summary>
        private void InitializeVoxels()
        {
            voxelsOriginal.Clear();
            voxelsDeformed.Clear();
            voxelDisp.Clear();
            nodeMap = null;
            femModel = null;

            // === 1) CAD (воксельная геометрия) ===
            double vpm = 1.0;                // вокселей на мм
            float voxelSize = 1.0f;          // 1 / vpm (мм) — визуальные единицы пусть соответствуют мм
            vm = new VoxelModel();

            // Пример: внешний 20x20x20 и вычитаем внутренний 10³ (с 5..15)
            AddBoxMm(vm, 0, 0, 0, 20, 20, 20, vpm);
            var inner = new VoxelModel();
            AddBoxMm(inner, 5, 5, 5, 15, 15, 15, vpm);
            vm.SubtractModel(inner);

            // === 2) FEM ===
            femModel = vm.ToFiniteElementModel(voxelSize, out nodeMap);

            // Находим слои Z
            minZLayer = int.MaxValue; maxZLayer = int.MinValue;
            foreach (var (x, y, z) in vm.GetVoxels())
            {
                if (z < minZLayer) minZLayer = z;
                if (z > maxZLayer) maxZLayer = z;
            }

            // Закрепления: нижний слой
            foreach (var kv in nodeMap)
            {
                var (x, y, z) = kv.Key;
                var node = kv.Value;
                if (z == minZLayer)
                    node.Constraints = Constraints.Fixed;
            }

            // Нагрузки: верхний слой — вертикальная вниз
            foreach (var kv in nodeMap)
            {
                var (x, y, z) = kv.Key;
                var node = kv.Value;
                if (z == maxZLayer)
                    node.Loads.Add(new NodalLoad(new Force(0, 0, -10, 0, 0, 0)));
            }

            // Решение
            femModel.Solve_MPC();

            // === 3) Подготовка визуализации ===

            // Исходная сцена — кубики по центрам вокселей
            foreach (var (x, y, z) in vm.GetVoxels())
            {
                float cx = (x + 0.5f) * voxelSize;
                float cy = (y + 0.5f) * voxelSize;
                float cz = (z + 0.5f) * voxelSize;

                // Цвет по высоте (как раньше) для исходной геометрии
                var color = GetColorByHeight(y, 20);
                voxelsOriginal.Add(new Voxel(cx, cy, cz, voxelCubeHalf, color));
            }

            // Сохраняем перемещения для каждого вокселя (через центр — это узел с меткой n_x_y_z)
            maxDispMagnitude = 1e-12;
            foreach (var kvp in nodeMap)
            {
                var (x, y, z) = kvp.Key;
                var node = kvp.Value;
                var disp = node.GetNodalDisplacement(); // метры
                var mag = Math.Sqrt(disp.DX * disp.DX + disp.DY * disp.DY + disp.DZ * disp.DZ);
                if (mag > maxDispMagnitude) maxDispMagnitude = mag;

                voxelDisp[kvp.Key] = (disp.DX, disp.DY, disp.DZ);
            }

            // Деформированная сцена (правее): та же геометрия, но сдвинутая
            foreach (var (x, y, z) in vm.GetVoxels())
            {
                float cx = (x + 0.5f) * voxelSize;
                float cy = (y + 0.5f) * voxelSize;
                float cz = (z + 0.5f) * voxelSize;

                // Перемещение узла (в метрах) -> конвертим в "мм" (условные единицы сцены)
                (double dx, double dy, double dz) = (0, 0, 0);
                if (voxelDisp.TryGetValue((x, y, z), out var d))
                    (dx, dy, dz) = d;

                // увеличим деформации для видимости
                float sx = cx + (float)(dx * deformationScale * 1000.0); // м → мм
                float sy = cy + (float)(dy * deformationScale * 1000.0);
                float sz = cz + (float)(dz * deformationScale * 1000.0);

                // Цвет по величине перемещения (min->max : синий->зелёный->красный)
                double mag = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                var col = ColorByNorm(mag, maxDispMagnitude);

                // Сдвигаем всю деформированную сцену вправо
                voxelsDeformed.Add(new Voxel(sx + deformedViewOffsetX, sy, sz, voxelCubeHalf, col));
            }
        }

        // ==== Рендер ====

        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            var gl = openGLControl.OpenGL;

            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            gl.LoadIdentity();

            SetupLighting(gl);
            SetupCamera(gl);

            // Слева — исходная
            foreach (var v in voxelsOriginal)
                RenderVoxel(gl, v);

            // Справа — деформированная
            foreach (var v in voxelsDeformed)
                RenderVoxel(gl, v);

            // Отобразим опоры/нагрузки (маркерно) на обеих сценах
            if (showBCs)
            {
                DrawSupportsAndLoads(gl, offsetX: 0f);                 // слева
                DrawSupportsAndLoads(gl, offsetX: deformedViewOffsetX); // справа
            }

            gl.Flush();
        }

        private void SetupLighting(OpenGL gl)
        {
            if (lightingEnabled)
            {
                gl.Enable(OpenGL.GL_LIGHTING);
                gl.Enable(OpenGL.GL_LIGHT0);
                gl.Enable(OpenGL.GL_COLOR_MATERIAL);
                gl.ColorMaterial(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_AMBIENT_AND_DIFFUSE);

                float[] lightPosition = { lightPositionX, lightPositionY, lightPositionZ, 1.0f };
                float[] lightAmbient = { 0.3f, 0.3f, 0.3f, 1.0f };
                float[] lightDiffuse = { 0.8f, 0.8f, 0.8f, 1.0f };
                float[] lightSpecular = { 1.0f, 1.0f, 1.0f, 1.0f };

                gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_POSITION, lightPosition);
                gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_AMBIENT, lightAmbient);
                gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_DIFFUSE, lightDiffuse);
                gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_SPECULAR, lightSpecular);

                float[] materialShininess = { 50.0f };
                gl.Material(OpenGL.GL_FRONT_AND_BACK, OpenGL.GL_SHININESS, materialShininess);
            }
            else
            {
                gl.Disable(OpenGL.GL_LIGHTING);
            }
        }

        private void SetupCamera(OpenGL gl)
        {
            var dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
            var dY = Math.Sin(AngleY * Rad);
            var dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);

            gl.LookAt(POSX, POSY, POSZ,
                      POSX + dX,
                      POSY + dY,
                      POSZ + dZ,
                      0, 1, 0);
        }

        private void RenderVoxel(OpenGL gl, Voxel voxel)
        {
            gl.PushMatrix();
            gl.Translate(voxel.X, voxel.Y, voxel.Z);

            if (voxel.DisplayList == 0)
            {
                voxel.DisplayList = gl.GenLists(1);
                gl.NewList(voxel.DisplayList, OpenGL.GL_COMPILE);
                DrawCube(gl, voxel.Size, voxel.Color);
                gl.EndList();
            }

            gl.CallList(voxel.DisplayList);
            gl.PopMatrix();
        }

        private void DrawCube(OpenGL gl, float half, Color color)
        {
            float r = color.R / 255.0f;
            float g = color.G / 255.0f;
            float b = color.B / 255.0f;

            float s = half;

            gl.Begin(OpenGL.GL_QUADS);

            // Front
            gl.Color(r * 0.8f, g * 0.8f, b * 0.8f);
            gl.Vertex(-s, -s, +s); gl.Vertex(+s, -s, +s); gl.Vertex(+s, +s, +s); gl.Vertex(-s, +s, +s);
            // Back
            gl.Color(r * 0.7f, g * 0.7f, b * 0.7f);
            gl.Vertex(-s, -s, -s); gl.Vertex(-s, +s, -s); gl.Vertex(+s, +s, -s); gl.Vertex(+s, -s, -s);
            // Top
            gl.Color(r, g, b);
            gl.Vertex(-s, +s, -s); gl.Vertex(-s, +s, +s); gl.Vertex(+s, +s, +s); gl.Vertex(+s, +s, -s);
            // Bottom
            gl.Color(r * 0.5f, g * 0.5f, b * 0.5f);
            gl.Vertex(-s, -s, -s); gl.Vertex(+s, -s, -s); gl.Vertex(+s, -s, +s); gl.Vertex(-s, -s, +s);
            // Right
            gl.Color(r * 0.9f, g * 0.9f, b * 0.9f);
            gl.Vertex(+s, -s, -s); gl.Vertex(+s, +s, -s); gl.Vertex(+s, +s, +s); gl.Vertex(+s, -s, +s);
            // Left
            gl.Color(r * 0.6f, g * 0.6f, b * 0.6f);
            gl.Vertex(-s, -s, -s); gl.Vertex(-s, -s, +s); gl.Vertex(-s, +s, +s); gl.Vertex(-s, +s, -s);

            gl.End();
        }

        private void DrawSupportsAndLoads(OpenGL gl, float offsetX)
        {
            if (nodeMap == null) return;

            gl.Disable(OpenGL.GL_LIGHTING);

            // Опоры: циановые "штриховки" (малые треугольники)
            gl.Color(0f, 1f, 1f);
            gl.Begin(OpenGL.GL_TRIANGLES);
            foreach (var kvp in nodeMap)
            {
                var (x, y, z) = kvp.Key;
                var node = kvp.Value;
                if (z != minZLayer) continue;

                var p = node.Location; // в метрах
                float cx = (float)(p.X * 1000.0) + offsetX; // м -> мм (виз. единицы)
                float cy = (float)(p.Y * 1000.0);
                float cz = (float)(p.Z * 1000.0);

                float s = 0.4f;
                gl.Vertex(cx - s, cy - s, cz - s);
                gl.Vertex(cx + s, cy - s, cz - s);
                gl.Vertex(cx, cy - s, cz + s);
            }
            gl.End();

            // Нагрузки: красные стрелки вниз
            gl.Color(1f, 0f, 0f);
            foreach (var kvp in nodeMap)
            {
                var (x, y, z) = kvp.Key;
                var node = kvp.Value;
                if (z != maxZLayer) continue;

                var p = node.Location;
                float cx = (float)(p.X * 1000.0) + offsetX;
                float cy = (float)(p.Y * 1000.0);
                float cz = (float)(p.Z * 1000.0);

                DrawArrow(gl, cx, cy + 2.0f, cz, cx, cy + 0.5f, cz);
            }

            if (lightingEnabled) gl.Enable(OpenGL.GL_LIGHTING);
        }

        private void DrawArrow(OpenGL gl, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            // Ствол
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(x0, y0, z0);
            gl.Vertex(x1, y1, z1);
            gl.End();

            // Наконечник
            var vx = x1 - x0; var vy = y1 - y0; var vz = z1 - z0;
            var len = Math.Max(1e-6f, (float)Math.Sqrt(vx * vx + vy * vy + vz * vz));
            vx /= len; vy /= len; vz /= len;

            float h = 0.6f;
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(x1, y1, z1);
            gl.Vertex(x1 + (-vy - vz) * h * 0.3f, y1 + (vx) * h * 0.3f, z1 + (vx) * h * 0.3f);
            gl.Vertex(x1 + (-vy + vz) * h * 0.3f, y1 + (vx) * h * 0.3f, z1 + (-vx) * h * 0.3f);
            gl.End();
        }

        // === Цвета ===

        private Color GetColorByHeight(int y, int maxHeight)
        {
            float ratio = Math.Max(0f, Math.Min(1f, (float)y / Math.Max(1, maxHeight)));
            if (ratio < 0.3f) return Color.FromArgb(70, 130, 180);   // SteelBlue
            else if (ratio < 0.6f) return Color.FromArgb(34, 139, 34); // ForestGreen
            else return Color.FromArgb(139, 69, 19);                // SaddleBrown
        }

        // Сине-зелёно-красный градиент по норме перемещения
        private Color ColorByNorm(double value, double max)
        {
            double t = max <= 0 ? 0 : Math.Max(0.0, Math.Min(1.0, value / max));
            // 0 -> синий, 0.5 -> зелёный, 1 -> красный
            if (t < 0.5)
            {
                double k = t / 0.5; // 0..1
                // синий (0,0,255) -> зелёный (0,255,0)
                int r = 0;
                int g = (int)(255 * k);
                int b = (int)(255 * (1 - k));
                return Color.FromArgb(r, g, b);
            }
            else
            {
                double k = (t - 0.5) / 0.5; // 0..1
                // зелёный (0,255,0) -> красный (255,0,0)
                int r = (int)(255 * k);
                int g = (int)(255 * (1 - k));
                int b = 0;
                return Color.FromArgb(r, g, b);
            }
        }

        // === Инициализация GL ===

        private void openGLControl_OpenGLInitialized(object sender, EventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;

            gl.ClearColor(0.2f, 0.3f, 0.4f, 1.0f);

            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.DepthFunc(OpenGL.GL_LEQUAL);

            gl.Enable(OpenGL.GL_CULL_FACE);
            gl.CullFace(OpenGL.GL_BACK);

            gl.ShadeModel(OpenGL.GL_SMOOTH);

            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
        }

        private void openGLControl_Resized(object sender, EventArgs e)
        {
            OpenGL gl = openGLControl.OpenGL;

            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();

            gl.Perspective(60.0f, (double)Width / (double)Height, 0.1, 2000.0);

            gl.MatrixMode(OpenGL.GL_MODELVIEW);
        }

        // === Управление мышью/клавишами ===

        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;

        private bool b = false;
        private int lX = -1, lY = -1;

        private void SharpGLForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (b)
            {
                if (lX != -1) AngleX += (lX - e.X) / 3f;
                if (lY != -1) AngleY += (lY - e.Y) / 3f;
                AngleY = Math.Max(-89, Math.Min(89, AngleY));
                openGLControl.Invalidate();
            }
            lX = e.X;
            lY = e.Y;
        }

        private void openGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            double dX, dY, dZ;
            float moveSpeed = 1.2f;

            switch (e.KeyCode)
            {
                case Keys.W:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * moveSpeed; POSY += dY * moveSpeed; POSZ += dZ * moveSpeed; break;
                case Keys.S:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX -= dX * moveSpeed; POSY -= dY * moveSpeed; POSZ -= dZ * moveSpeed; break;
                case Keys.D:
                    dX = Math.Sin((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * moveSpeed; POSZ += dZ * moveSpeed; break;
                case Keys.A:
                    dX = Math.Sin((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * moveSpeed; POSZ += dZ * moveSpeed; break;
                case Keys.Space:
                    POSY += moveSpeed; break;
                case Keys.ShiftKey:
                case Keys.ControlKey:
                    POSY -= moveSpeed; break;
                case Keys.L:
                    lightingEnabled = !lightingEnabled; break;
                case Keys.R:
                    // Сброс камеры
                    POSX = 15; POSY = 15; POSZ = 80;
                    AngleX = 0; AngleY = 0; break;
                case Keys.OemMinus: // уменьшить масштаб деформаций
                    deformationScale = Math.Max(1.0, deformationScale * 0.8);
                    RebuildDeformedVoxels();
                    break;
                case Keys.Oemplus:  // увеличить масштаб деформаций
                case Keys.Add:
                    deformationScale = Math.Min(1e6, deformationScale * 1.25);
                    RebuildDeformedVoxels();
                    break;
                case Keys.B:
                    showBCs = !showBCs; break;
            }

            openGLControl.Invalidate();
        }

        private void RebuildDeformedVoxels()
        {
            if (vm == null || nodeMap == null) return;

            voxelsDeformed.Clear();

            float voxelSize = 1.0f;
            foreach (var (x, y, z) in vm.GetVoxels())
            {
                float cx = (x + 0.5f) * voxelSize;
                float cy = (y + 0.5f) * voxelSize;
                float cz = (z + 0.5f) * voxelSize;

                (double dx, double dy, double dz) = (0, 0, 0);
                if (voxelDisp.TryGetValue((x, y, z), out var d))
                    (dx, dy, dz) = d;

                float sx = cx + (float)(dx * deformationScale * 1000.0);
                float sy = cy + (float)(dy * deformationScale * 1000.0);
                float sz = cz + (float)(dz * deformationScale * 1000.0);

                double mag = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                var col = ColorByNorm(mag, maxDispMagnitude);

                voxelsDeformed.Add(new Voxel(sx + deformedViewOffsetX, sy, sz, voxelCubeHalf, col));
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ==== Утилиты ====

        private static void AddBoxMm(VoxelModel vm, double x0mm, double y0mm, double z0mm,
                                     double x1mm, double y1mm, double z1mm, double vpm)
        {
            int VoxFloor(double mm) => (int)Math.Floor(mm * vpm);
            int VoxCeil(double mm) => (int)Math.Ceiling(mm * vpm);

            int x0 = VoxFloor(Math.Min(x0mm, x1mm));
            int x1 = VoxCeil(Math.Max(x0mm, x1mm));
            int y0 = VoxFloor(Math.Min(y0mm, y1mm));
            int y1 = VoxCeil(Math.Max(y0mm, y1mm));
            int z0 = VoxFloor(Math.Min(z0mm, z1mm));
            int z1 = VoxCeil(Math.Max(z0mm, z1mm));

            vm.AddBox(x0, y0, x1, y1, z0, z1);
        }
    }

    public class Voxel
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Size { get; set; } // половина ребра куба
        public Color Color { get; set; }
        public uint DisplayList { get; set; }

        public Voxel(float x, float y, float z, float halfSize, Color color)
        {
            X = x; Y = y; Z = z;
            Size = halfSize;
            Color = color;
            DisplayList = 0;
        }
    }
}
