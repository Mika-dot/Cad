using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpGL;

namespace OpenGL_lesson_CSharp
{
    partial class SharpGLForm
    {
        FvmRoute route;
        int view = 5, layer = 3, field, mode, step = 5, cut = 8;
        string[] fields = { "Free", "Ground", "Zone", "U low", "U high", "Surface", "Clay", "Rock", "Pipe", "Foundation" };
        Button[] viewButtons = new Button[7];
        Label status = new Label();
        Timer timer = new Timer();
        string capture;

        // Воздух, верхний грунт, глина, порода, сеть, фундамент.
        static Vector3[] colors = { new Vector3(), new Vector3(.65f,.53f,.32f), new Vector3(.54f,.36f,.25f),
            new Vector3(.49f,.50f,.48f), new Vector3(.27f,.37f,.41f), new Vector3(.41f,.37f,.32f) };
        static Vector3[] paths = { new Vector3(.05f,.14f,.31f), new Vector3(.42f,.12f,.055f) };

        void AddViewButtons()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            FlowLayoutPanel controls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false };
            string[] names = { "1. М-карты", "2. Воксели", "3. Дешёвый проход", "4. Обход",
                "5. Алгоритм по шагам", "6. Профили", "7. План" };
            for (int i = 0; i < names.Length; i++)
            {
                int number = i + 1;
                viewButtons[i] = AddButton(bar, names[i], () => { timer.Stop(); view = number; if (view == 3) cut = 8; if (view == 4) cut = 3; UpdateStatus(); });
            }
            AddButton(controls, "Сначала", () => { step = 0; view = 5; UpdateStatus(); });
            AddButton(controls, "Назад", () => MoveStep(-1));
            AddButton(controls, "Шаг", () => MoveStep(1));
            AddButton(controls, "Пуск / стоп", () =>
            {
                view = 5;
                if (!timer.Enabled && step == route.PathNodes[mode].Count - 1) step = 0;
                timer.Enabled = !timer.Enabled;
                UpdateStatus();
            });
            AddButton(controls, "Сменить тариф", () => { mode = 1 - mode; step = 0; view = 5; UpdateStatus(); });
            AddButton(controls, "Соседи", ShowNeighbours);
            AddButton(controls, "Срез Y−", () => { cut = Math.Max(0, cut - 1); UpdateStatus(); });
            AddButton(controls, "Срез Y+", () => { cut = Math.Min(MImages.Y - 1, cut + 1); UpdateStatus(); });
            AddButton(controls, "Слой Z−", () => { layer = Math.Max(0, layer - 1); view = 1; UpdateStatus(); });
            AddButton(controls, "Слой Z+", () => { layer = Math.Min(MImages.Z - 1, layer + 1); view = 1; UpdateStatus(); });
            AddButton(controls, "Сменить М-поле", () => { field = (field + 1) % fields.Length; view = 1; UpdateStatus(); });
            timer.Interval = 650;
            timer.Tick += (sender, args) =>
            {
                MoveStep(1);
                if (step == route.PathNodes[mode].Count - 1) timer.Stop();
            };
            FormClosed += (sender, args) => timer.Dispose();
            status.Dock = DockStyle.Bottom;
            status.Height = 43;
            status.Padding = new Padding(8, 3, 0, 0);
            Controls.Add(status);
            Controls.Add(controls);
            Controls.Add(bar);
        }

        static Button AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 28 };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button);
            return button;
        }

        void MoveStep(int delta)
        {
            view = 5;
            step = Math.Max(0, Math.Min(route.PathNodes[mode].Count - 1, step + delta));
            UpdateStatus();
        }

        void ShowNeighbours()
        {
            timer.Stop();
            view = 5;
            UpdateStatus();
            if (step >= route.StepTable[mode].Count) return;
            using (Form form = new Form { Text = "Соседи текущего вокселя", Width = 660, Height = 450 })
            {
                TextBox table = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                    ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10) };
                int next = route.PathNodes[mode][step + 1];
                table.Text = "Выбран " + PointText(MImages.Point(next)) +
                    "\r\n  x, y,z         U        c      U+c     grad·v\r\n" +
                    string.Join("\r\n", route.StepTable[mode][step]);
                form.Controls.Add(table);
                form.ShowDialog(this);
            }
        }

        static string PointText(Vector3 p)
        {
            return string.Format("({0:0}, {1:0}, {2:0})", p.X, p.Y, p.Z);
        }

        internal void StartView(int number, string file)
        {
            Shown += (sender, args) =>
            {
                if (number == 5) step = 5;
                viewButtons[number - 1].PerformClick();
                if (view != number) throw new InvalidOperationException("Не переключился режим.");
                capture = file;
            };
        }

        void UpdateStatus()
        {
            Text = "SharpGL CAD — рельеф, слои и трассировка по М-картам";
            for (int i = 0; i < viewButtons.Length; i++)
                viewButtons[i].BackColor = i + 1 == view ? Color.LightSteelBlue : SystemColors.Control;
            if (view == 5)
            {
                int i = route.PathNodes[mode][step], j = route.PathNodes[mode][Math.Min(step + 1, route.PathNodes[mode].Count - 1)];
                Vector3 p = MImages.Point(i), q = MImages.Point(j), g = route.Potential[mode].Gradient(i);
                status.Text = string.Format("Шаг {0}/{1}   {2} → {3}   U: {4:F2} → {5:F2}   ∇U=({6:F2}; {7:F2}; {8:F2})   ΔZ={9:+0;-0;0}\r\n",
                    step, route.PathNodes[mode].Count - 1, PointText(p), PointText(q), route.Potential[mode].Value(i,p),
                    route.Potential[mode].Value(j,q), g.X,g.Y,g.Z,q.Z-p.Z) +
                    "Стрелка в объёме — −∇U, выделенный отрезок — выбранный сосед. Z растёт вниз.";
            }
            else if (view == 1) status.Text = "Поле " + fields[field] + ", слой Z=" + layer + ". Цвет каждого пикселя хранит один коэффициент.";
            else status.Text = "Охра — верхний грунт, коричневый — глина, серый — порода. Сине-серое тело — сеть, серо-коричневое — фундамент.\r\n" +
                (view == 6 ? "Профили построены вдоль каждой трассы с её фактическими X, Y и Z." :
                "Срез Y=" + cut + ". Двигай срез кнопками или вращай сцену мышью. Пунктир показывает трассу за грунтом.");
        }

        void ChangeView(object sender, KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D7)
                viewButtons[(int)e.KeyCode - (int)Keys.D1].PerformClick();
        }

        static void SetColor(OpenGL gl, Vector3 color, float shade = 1)
        {
            gl.Color(color.X * shade, color.Y * shade, color.Z * shade);
        }

        static void Screen(OpenGL gl, int w, int h)
        {
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Ortho(0, w, 0, h, -1, 1);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
            gl.Disable(OpenGL.GL_DEPTH_TEST);
        }

        static void Caption(OpenGL gl, int x, int y, string text)
        {
            gl.DrawText(x, y, .08f, .12f, .18f, "Arial", 12, text);
        }

        void DrawFvm(OpenGL gl)
        {
            int w = openGLControl.ClientSize.Width, h = openGLControl.ClientSize.Height;
            int left = view == 5 ? w * 54 / 100 : w;
            gl.Viewport(0, 0, w, h);
            if (view == 1)
            {
                MImages[] images = { route.Free, route.Ground, route.Zone, route.Potential[0], route.Potential[1],
                    route.Surface, route.ClayLayer, route.RockLayer, route.PipeBody, route.FoundationBody };
                Screen(gl, w, h);
                int size = Math.Min((w - 65) / (3 * MImages.X), (h - 85) / (2 * MImages.Y));
                DrawSlice(gl, images[field], layer, 14, h - 40, size, false, new Vector3(-1));
                DrawSlice(gl, images[field], layer, 14, h - 70 - size * MImages.Y, size, true, new Vector3(-1));
                Caption(gl, 14, h - 21, fields[field] + " / Z=" + layer + " / M0 M1 M2 / M3 M4");
            }
            else if (view == 7)
            {
                Screen(gl, w, h);
                DrawPlan(gl, w, h);
            }
            else if (view == 6)
            {
                Screen(gl, w, h);
                DrawProfile(gl, 0, 42, h / 2 + 25, w - 84, h / 2 - 72);
                DrawProfile(gl, 1, 42, 24, w - 84, h / 2 - 72);
            }
            else
            {
                gl.Viewport(0, 0, left, h);
                SetCamera(gl, left, h);
                gl.Enable(OpenGL.GL_DEPTH_TEST);
                DrawVolume(gl);
                gl.Viewport(0, 0, w, h);
                if (view == 5) DrawStepMaps(gl, left, w, h);
                else Caption(gl, 14, h - 21, view == 2 ? "Voxel cutaway | Y=" + cut :
                    view == 3 ? "A to B | below utility, above foundation" : "A to B | lateral bypass");
            }

            if (capture != null)
            {
                string file = capture;
                capture = null;
                gl.Finish();
                SaveFrame(gl, file);
                BeginInvoke(new Action(Close));
            }
        }

        void DrawStepMaps(OpenGL gl, int left, int w, int h)
        {
            var path = route.PathNodes[mode];
            Vector3 p = MImages.Point(path[step]), q = MImages.Point(path[Math.Min(step + 1, path.Count - 1)]);
            Vector3 g = route.Potential[mode].Gradient(path[step]);
            Screen(gl, w, h);
            int x = left + 8;
            int cell = Math.Min((w - left - 36) / (3 * MImages.X), (h - 112) / (4 * MImages.Y));
            DrawSlice(gl, route.Potential[mode], (int)p.Z, x, h - 74, cell, false, p);
            DrawSlice(gl, route.Potential[mode], (int)p.Z, x, h - 87 - cell * MImages.Y, cell, true, p);
            int middle = h - 122 - 2 * cell * MImages.Y;
            DrawSlice(gl, route.Potential[mode], (int)q.Z, x, middle - 25, cell, false, q);
            DrawSlice(gl, route.Potential[mode], (int)q.Z, x, middle - 38 - cell * MImages.Y, cell, true, q);
            DrawMapTrack(gl, x, h - 74, cell, (int)p.Z, path);
            DrawMapTrack(gl, x, middle - 25, cell, (int)q.Z, path);
            Caption(gl, 12, h - 22, "Step " + step + " | " + PointText(p) + " -> " + PointText(q));
            Caption(gl, 12, h - 44, string.Format("-grad U = ({0:F2}, {1:F2}, {2:F2}) | dZ = {3:+0;-0;0}", -g.X,-g.Y,-g.Z,q.Z-p.Z));
            Caption(gl, x, h - 57, "Current U: Z=" + p.Z + "   M0 M1 M2 / M3 M4");
            Caption(gl, x, middle - 9, "Next U: Z=" + q.Z + "   M0 M1 M2 / M3 M4");
        }

        void DrawMapTrack(OpenGL gl, int x, int top, int cell, int z, System.Collections.Generic.List<int> path)
        {
            // След на M0: соединяем только участки, действительно лежащие в этом слое.
            if (cell < 1) return;
            gl.LineWidth(2);
            gl.Color(.96f,.96f,.92f);
            gl.Begin(OpenGL.GL_LINES);
            for (int i=1; i<=Math.Min(step+1,path.Count-1); i++)
            {
                Vector3 a=MImages.Point(path[i-1]), b=MImages.Point(path[i]);
                if (a.Z != z || b.Z != z) continue;
                gl.Vertex(x+(a.X+.5f)*cell,top-(a.Y+.5f)*cell);
                gl.Vertex(x+(b.X+.5f)*cell,top-(b.Y+.5f)*cell);
            }
            gl.End();
        }

        void DrawPlan(OpenGL gl, int w, int h)
        {
            float cell = Math.Min((w-80f)/MImages.X, (h-78f)/MImages.Y);
            float x0 = (w-cell*MImages.X)/2, top = h-36;
            gl.Begin(OpenGL.GL_QUADS);
            for (int y=0; y<MImages.Y; y++)
            for (int x=0; x<MImages.X; x++)
            {
                int i=MImages.Index(x,y,3);
                Vector3 p=MImages.Point(i);
                float shade=.78f+.18f*(float)route.Surface.Value(i,p)/4;
                Vector3 color= colors[1];
                if (route.Zone.Value(i,p)>.07) color=new Vector3(.66f,.60f,.49f);
                for (int z=0; z<MImages.Z; z++)
                {
                    int type=route.Material[MImages.Index(x,y,z)];
                    if (type>=4) { color=colors[type]; break; }
                }
                SetColor(gl,color,shade);
                Rect(gl,x0+x*cell,top-(y+1)*cell,cell-.35f,cell-.35f);
            }
            gl.End();
            for (int m=0; m<2; m++)
            {
                SetColor(gl,paths[m]);
                gl.LineWidth(4);
                gl.Begin(OpenGL.GL_LINE_STRIP);
                foreach (int i in route.PathNodes[m])
                {
                    Vector3 p=MImages.Point(i);
                    gl.Vertex(x0+(p.X+.5f)*cell,top-(p.Y+.5f)*cell);
                }
                gl.End();
                var path=route.PathNodes[m];
                for (int i=1; i<path.Count; i++)
                {
                    Vector3 p=MImages.Point(path[i]);
                    if (p.Z==MImages.Point(path[i-1]).Z) continue;
                    Caption(gl,(int)(x0+(p.X+.5f)*cell), (int)(top-(p.Y+.5f)*cell+(m==0 ? -17 : 6)), "Z"+p.Z);
                }
            }
            Caption(gl,14,h-22,"XY plan | labels show depth levels at every vertical transition");
        }
        static void DrawSlice(OpenGL gl, MImages image, int z, int left, int top, int cell, bool lower, Vector3 selected)
        {
            if (cell < 1) return;
            int first = lower ? 3 : 0, last = lower ? 5 : 3;
            for (int k = first; k < last; k++)
            {
                int origin = left + (k - first) * (MImages.X * cell + 9);
                gl.Begin(OpenGL.GL_QUADS);
                for (int y = 0; y < MImages.Y; y++)
                for (int x = 0; x < MImages.X; x++)
                {
                    int rgb = image.Pixel[MImages.Index(x,y,z),k];
                    gl.Color((rgb >> 16) / 255.0, ((rgb >> 8) & 255) / 255.0, (rgb & 255) / 255.0);
                    Rect(gl, origin + x * cell, top - (y + 1) * cell, cell, cell);
                }
                gl.End();
                if (selected.X < 0) continue;
                int px = origin + (int)selected.X * cell, py = top - ((int)selected.Y + 1) * cell;
                gl.LineWidth(4);
                gl.Color(0f,0f,0f);
                gl.Begin(OpenGL.GL_LINE_LOOP); Rect(gl,px,py,cell,cell); gl.End();
                gl.LineWidth(2);
                gl.Color(1f,1f,1f);
                gl.Begin(OpenGL.GL_LINE_LOOP); Rect(gl,px,py,cell,cell); gl.End();
            }
        }

        static void Rect(OpenGL gl, float x, float y, float w, float h)
        {
            gl.Vertex(x,y); gl.Vertex(x+w,y); gl.Vertex(x+w,y+h); gl.Vertex(x,y+h);
        }

        static void Vertex(OpenGL gl, Vector3 p)
        {
            gl.Vertex(12 - p.X, 3 - p.Z, p.Y - 8);
        }

        void DrawVolume(OpenGL gl)
        {
            int start = cut;
            if (view == 5) start = (int)MImages.Point(route.PathNodes[mode][step]).Y;
            for (int i = 0; i < MImages.Count; i++)
            {
                int material = route.Material[i];
                if (material == 0) continue;
                Vector3 p = MImages.Point(i);
                int x = (int)p.X, y = (int)p.Y, z = (int)p.Z;
                if (y < start) continue;
                // Рисуем только внешние воксели, включая границу среза.
                if (x > 0 && x < MImages.X - 1 && y > start && y < MImages.Y - 1 && z > 0 &&
                    z < MImages.Z - 1 && route.Material[i - MImages.X*MImages.Y] != 0) continue;
                MaterialCube(gl, p, colors[material], y == start);
            }

            if (view == 2) return;
            int m = view == 5 ? mode : view - 3;
            int last = view == 5 ? step : route.PathNodes[m].Count - 1;
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_LINE_STIPPLE);
            gl.LineStipple(1, 0x3333);
            gl.LineWidth(3);
            DrawPath(gl, m, last);
            gl.Disable(OpenGL.GL_LINE_STIPPLE);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.LineWidth(4);
            DrawPath(gl, m, last);

            gl.Disable(OpenGL.GL_DEPTH_TEST);
            Vector3 a = MImages.Point(FvmRoute.A), b = MImages.Point(FvmRoute.B);
            SetColor(gl, paths[m]);
            gl.PointSize(8);
            gl.Begin(OpenGL.GL_POINTS); Vertex(gl,a); Vertex(gl,b); gl.End();
            if (view == 5 && step < route.PathNodes[m].Count - 1)
            {
                int i = route.PathNodes[m][step];
                Vector3 p = MImages.Point(i), q = MImages.Point(route.PathNodes[m][step+1]);
                Vector3 g = route.Potential[m].Gradient(i);
                SetColor(gl, new Vector3(.42f,.12f,.055f));
                gl.LineWidth(4);
                gl.Begin(OpenGL.GL_LINES); Vertex(gl,p); Vertex(gl,q); gl.End();
                if (g.LengthSquared() > .00001f)
                {
                    Vector3 d = -Vector3.Normalize(g), tip = p + 1.65f * d;
                    Vector3 side = Vector3.Cross(d,Vector3.UnitZ);
                    if (side.LengthSquared() < .00001f) side = Vector3.UnitY;
                    side = .25f * Vector3.Normalize(side);
                    SetColor(gl, new Vector3(.10f,.23f,.57f));
                    gl.LineWidth(2);
                    gl.Begin(OpenGL.GL_LINES);
                    Vertex(gl,p); Vertex(gl,tip);
                    Vertex(gl,tip); Vertex(gl,tip-.35f*d+side);
                    Vertex(gl,tip); Vertex(gl,tip-.35f*d-side);
                    gl.End();
                }
                gl.PointSize(9);
                gl.Color(1f,1f,1f);
                gl.Begin(OpenGL.GL_POINTS); Vertex(gl,p); Vertex(gl,q); gl.End();
            }
            gl.Enable(OpenGL.GL_DEPTH_TEST);
        }

        static void MaterialCube(OpenGL gl, Vector3 p, Vector3 color, bool cutFace)
        {
            float x=p.X-.49f, a=p.X+.49f, y=p.Y+(cutFace ? .04f : -.49f), b=p.Y+.49f, z=p.Z-.49f, c=p.Z+.49f;
            gl.Begin(OpenGL.GL_QUADS);
            Face(gl,color,1.08f,new Vector3(x,y,z),new Vector3(a,y,z),new Vector3(a,b,z),new Vector3(x,b,z));
            Face(gl,color,.88f,new Vector3(x,y,z),new Vector3(a,y,z),new Vector3(a,y,c),new Vector3(x,y,c));
            Face(gl,color,.74f,new Vector3(a,y,z),new Vector3(a,b,z),new Vector3(a,b,c),new Vector3(a,y,c));
            Face(gl,color,.78f,new Vector3(x,y,z),new Vector3(x,b,z),new Vector3(x,b,c),new Vector3(x,y,c));
            Face(gl,color,.86f,new Vector3(x,b,z),new Vector3(a,b,z),new Vector3(a,b,c),new Vector3(x,b,c));
            Face(gl,color,.65f,new Vector3(x,y,c),new Vector3(a,y,c),new Vector3(a,b,c),new Vector3(x,b,c));
            gl.End();
        }

        static void Face(OpenGL gl, Vector3 color, float shade, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            SetColor(gl,color,shade); Vertex(gl,a); Vertex(gl,b); Vertex(gl,c); Vertex(gl,d);
        }

        void DrawPath(OpenGL gl, int mode, int last)
        {
            SetColor(gl, paths[mode]);
            gl.Begin(OpenGL.GL_LINE_STRIP);
            for (int j = 0; j <= last; j++) Vertex(gl, MImages.Point(route.PathNodes[mode][j]));
            gl.End();
        }

        void DrawProfile(OpenGL gl, int mode, int x, int y, int w, int h)
        {
            var path = route.PathNodes[mode];
            float[] dist = new float[path.Count];
            for (int i = 1; i < path.Count; i++)
                dist[i] = dist[i-1] + Vector3.Distance(MImages.Point(path[i-1]),MImages.Point(path[i]));
            float sx = (float)(w / route.Length[1]), sy = h / 8f;
            float top = y + 7 * sy;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = MImages.Point(path[i-1]), b = MImages.Point(path[i]);
                float xx = x + dist[i-1]*sx, nx = x + dist[i]*sx;
                double[] from = { a.Z-route.Surface.At(a), a.Z-route.ClayLayer.At(a), a.Z-route.RockLayer.At(a), 7 };
                double[] to = { b.Z-route.Surface.At(b), b.Z-route.ClayLayer.At(b), b.Z-route.RockLayer.At(b), 7 };
                gl.Begin(OpenGL.GL_QUADS);
                for (int k = 0; k < 3; k++)
                {
                    SetColor(gl,colors[k+1]);
                    gl.Vertex(xx,top-from[k]*sy); gl.Vertex(nx,top-to[k]*sy);
                    gl.Vertex(nx,top-to[k+1]*sy); gl.Vertex(xx,top-from[k+1]*sy);
                }
                gl.End();
                // Сечение подземных тел именно вдоль XY-проекции выбранного пути.
                gl.Begin(OpenGL.GL_QUADS);
                for (int j = 0; j < 8; j++)
                for (int z = 0; z < 28; z++)
                {
                    Vector3 p = Vector3.Lerp(a,b,(j+.5f)/8);
                    p.Z=(z+.5f)/4;
                    bool pipe = route.PipeBody.At(p) >= 0;
                    if (!pipe && route.FoundationBody.At(p) < 0) continue;
                    SetColor(gl,pipe ? colors[4] : colors[5]);
                    Rect(gl,xx+(nx-xx)*j/8, top-(z+1)*sy/4,(nx-xx)/8,sy/4);
                }
                gl.End();
            }
            SetColor(gl,paths[mode]);
            gl.LineWidth(4);
            gl.Begin(OpenGL.GL_LINE_STRIP);
            for (int i = 0; i < path.Count; i++)
                gl.Vertex(x+dist[i]*sx,top-MImages.Point(path[i]).Z*sy);
            gl.End();
            Caption(gl,x,y+h+8,mode == 0 ? "Through zone | utility above route, foundation below" : "Bypass | profile follows the actual XY route");
            Caption(gl,x,y-16,"A / 0 m");
            Caption(gl,x+(int)(dist[path.Count-1]*sx)-100,y-16,string.Format("B / {0:F2} m",dist[path.Count-1]));
        }

        void SaveFrame(OpenGL gl, string file)
        {
            int width = openGLControl.ClientSize.Width, height = openGLControl.ClientSize.Height;
            int stride = (width * 3 + 3) & ~3;
            byte[] pixels = new byte[stride * height];
            GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                gl.ReadPixels(0, 0, width, height, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, pinned.AddrOfPinnedObject());
            }
            finally { pinned.Free(); }
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, (height - 1 - y) * stride, data.Scan0 + y * data.Stride, width * 3);
                bitmap.UnlockBits(data);
                bitmap.Save(file, ImageFormat.Png);
            }
        }
    }
}
