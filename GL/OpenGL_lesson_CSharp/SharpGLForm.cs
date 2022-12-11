using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SharpGL;
using System.Numerics;
using VARCad;
using IxMilia.Stl;
using System.Linq;
using System.IO;
//using System.ComponentModel;
//using System.Data;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading;
//using SharpGL.SceneGraph.Primitives;
//using SharpGL.SceneGraph.Raytracing;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
//using System.Globalization;
//using System.Windows.Media.Media3D;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
//using SharpGL.SceneGraph.Quadrics;

namespace OpenGL_lesson_CSharp
{

    public partial class SharpGLForm : Form
    {
        float AngleX = 0, AngleY = 0;
        double POSX = 2, POSY = 0, POSZ = 0;
        const float Rad = 3.14f / 180f;
        Vector3[] Colors = new Vector3[]
        {
            new Vector3(1,0,0),
            new Vector3(0,1,0),
            new Vector3(0,0,1),
            new Vector3(1,1,0),
            new Vector3(1,0,1),
            new Vector3(0,1,1),
            new Vector3(0.5f,0,0),
            new Vector3(0,0.5f,0),
            new Vector3(0,0,0.5f),
            new Vector3(0.5f,0.5f,0),
            new Vector3(0.5f,0,0.5f),
            new Vector3(0,0.5f,0.5f),
            new Vector3(0.3f,0,0),
            new Vector3(0,0.3f,0),
            new Vector3(0,0,0.3f),
            new Vector3(0.3f,0.3f,0),
            new Vector3(0.3f,0,0.3f),
            new Vector3(0,0.3f,0.3f),
            new Vector3(0.4f,0,0),
            new Vector3(0,0.4f,0),
            new Vector3(0,0,0.4f),
            new Vector3(0.4f,0.4f,0),
            new Vector3(0.4f,0,0.4f),
            new Vector3(0,0.4f,0.4f)
        };
        
        public SharpGLForm()
        {
            InitializeComponent();
        }
        public static OpenGL gl;
        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            //  Возьмём OpenGL объект
            gl = openGLControl.OpenGL;

            //  Очищаем буфер цвета и глубины
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            //  Загружаем единичную матрицу
            gl.LoadIdentity();

            if (Figures != null)
            {
                for (int i = 0; i < Figures.Length; i++)
                {
                    if (Figures[i] != null) DrawFigure(Figures[i]);
                }
            }

            gl.Flush();
        }
        public void Cub(OpenGL gl, float[] coordinates, float size, Color color)
        {
            // рисуем куб
            //gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Begin(OpenGL.GL_QUADS);


            // Top
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            // Bottom
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            // Front
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Back
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            // Left
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Right
            gl.Color(color.R, color.G, color.B);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);

            gl.End();
        }
        public void DrawFigure(List<Triangle> what)
        {
            for (int i = 0; i < what.Count; i++)
            {
                var n = i;
                while (n > Colors.Length - 1) n -= Colors.Length;
                gl.Color(Colors[n].X, Colors[n].Y, Colors[n].Z);
                gl.Begin(OpenGL.GL_TRIANGLES);
                gl.Vertex(what[i].p1.X, what[i].p1.Y, what[i].p1.Z);
                gl.Vertex(what[i].p2.X, what[i].p2.Y, what[i].p2.Z);
                gl.Vertex(what[i].p3.X, what[i].p3.Y, what[i].p3.Z);
                gl.Vertex(what[i].p3.X, what[i].p3.Y, what[i].p3.Z);
                gl.End();
            }
        }



        List<Triangle>[] Figures;
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //string[] LINES = textBox1.Text.Replace("\r\n", " ").Split('')
                for (int i = 0; i < textBox1.Lines.Length; i++)
                {
                    var S = textBox1.Lines[i];
                    if (S.Length == 0) continue;
                    S = S.Trim();
                    if (S.Length == 0) continue;
                    if (S.IndexOf("//") == 0) continue;
                    S = S.Replace(" ", "");
                    if (S.Length == 0) continue;
                    var ss = S.ToLower().Split(',');
                    switch (ss[0])
                    {
                        case "фигур":
                            {
                                int c = int.Parse(ss[1]);
                                Figures = new List<Triangle>[c];
                                Console.WriteLine($"Теперь фигур будет {c}.");
                                break;
                            }
                        case "создать":
                            {
                                int ID = int.Parse(ss[1]);
                                Console.WriteLine($"Создаётся фигура {ID}:");
                                int in_c = int.Parse(ss[2]);
                                Console.WriteLine($"{in_c} внутренних:");
                                Vector2[] IN = new Vector2[in_c];
                                int k = 0;
                                for (int h = 3; h < 3 + in_c * 2; h += 2)
                                {
                                    float X = float.Parse(ss[h].Replace('.', ','));
                                    float Y = float.Parse(ss[h + 1].Replace('.', ','));
                                    IN[k] = new Vector2(X, Y);
                                    k++;
                                    Console.WriteLine($"{X} {Y}");
                                }
                                float Zin = float.Parse(ss[3 + in_c * 2].Replace('.', ','));
                                Console.WriteLine($"Zвнут = {Zin}.");
                                int out_c = int.Parse(ss[4 + in_c * 2]);
                                Console.WriteLine($"{out_c} внешних:");
                                Vector2[] OUT = new Vector2[out_c];
                                k = 0;
                                for (int h = 5 + in_c * 2; h < 5 + in_c * 2 + out_c * 2; h += 2)
                                {
                                    float X = float.Parse(ss[h].Replace('.', ','));
                                    float Y = float.Parse(ss[h + 1].Replace('.', ','));
                                    OUT[k] = new Vector2(X, Y);
                                    k++;
                                    Console.WriteLine($"{X} {Y}");
                                }
                                float Zout = float.Parse(ss[5 + in_c * 2 + out_c * 2].Replace('.', ','));
                                Console.WriteLine($"Zвнеш = {Zout}.");
                                Figures[ID] = CADMath.CreateComplexFigure(IN, Zin, OUT, Zout).ToList();
                                break;
                            }
                        case "переместить":
                            {
                                var link = Figures[int.Parse(ss[1])];
                                float X = float.Parse(ss[2].Replace('.', ','));
                                float Y = float.Parse(ss[3].Replace('.', ','));
                                float Z = float.Parse(ss[4].Replace('.', ','));
                                for (int j = 0; j < link.Count; j++)
                                {
                                    var c = link[j];
                                    c.p1.X += X;
                                    c.p2.X += X;
                                    c.p3.X += X;
                                    c.p1.Y += Y;
                                    c.p2.Y += Y;
                                    c.p3.Y += Y;
                                    c.p1.Z += Z;
                                    c.p2.Z += Z;
                                    c.p3.Z += Z;
                                    link[j] = c;
                                }
                                break;
                            }
                        case "сохранить":
                            {
                                int ID = int.Parse(ss[1]);
                                var link = Figures[ID];
                                StlFile stlFile = new StlFile();
                                stlFile.SolidName = "VARCAD_Figure" + ID;
                                for (int k = 0; k < Figures[ID].Count; k++)
                                {
                                    var tr = Figures[ID][k];
                                    stlFile.Triangles.Add(new StlTriangle(new StlNormal(0, 0, 0),
                                        new StlVertex(tr.p1.X, tr.p1.Y, tr.p1.Z),
                                        new StlVertex(tr.p2.X, tr.p2.Y, tr.p2.Z),
                                        new StlVertex(tr.p3.X, tr.p3.Y, tr.p3.Z)));
                                }
                                string file = ID + ".stl";
                                if (!File.Exists(file))
                                {
                                    var fss = File.Create(file);
                                    fss.Close();
                                }
                                FileStream fs = new FileStream(file, FileMode.Open);
                                stlFile.Save(fs);
                                fs.Close();
                                break;
                            }
                        case "загрузить":
                            {
                                int ID = int.Parse(ss[1]);
                                StlFile stlFile = new StlFile();
                                string file = ID + ".stl";
                                if (!File.Exists(file))
                                {
                                    var fss = File.Create(file);
                                    fss.Close();
                                }
                                FileStream fs = new FileStream(file, FileMode.Open);
                                stlFile = StlFile.Load(fs);
                                fs.Close();
                                Figures[ID] = new List<Triangle>();
                                var link = Figures[ID];
                                for (int k = 0; k < stlFile.Triangles.Count; k++)
                                {
                                    var tr = stlFile.Triangles[k];
                                    link.Add(new Triangle(
                                            new Vector3(tr.Vertex1.X, tr.Vertex1.Y, tr.Vertex1.Z),
                                            new Vector3(tr.Vertex2.X, tr.Vertex2.Y, tr.Vertex2.Z),
                                            new Vector3(tr.Vertex3.X, tr.Vertex3.Y, tr.Vertex3.Z)
                                        ));
                                }
                                break;
                            }
                        case "сложить":
                            {
                                int ID1 = int.Parse(ss[1]);
                                int ID2 = int.Parse(ss[2]);
                                Figures[ID1] = CADMath.OR(Figures[ID1], Figures[ID2]);
                                Figures[ID2] = null;
                                break;
                            }
                        case "вычесть":
                            {
                                int ID1 = int.Parse(ss[1]);
                                int ID2 = int.Parse(ss[2]);
                                Figures[ID1] = CADMath.XOR(Figures[ID1], Figures[ID2]);
                                Figures[ID2] = null;
                                break;
                            }
                    }

                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.Message);
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Figures = null;
        }



        private void openGLControl_OpenGLInitialized(object sender, EventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl.OpenGL;

            //  Фоновый цвет по умолчанию (в данном случае цвет голубой)
            gl.ClearColor(0.1f, 0.5f, 1.0f, 0);
        }
        // Данная функция используется для преобразования изображения 
        // в объёмный вид с перспективой
        private void openGLControl_Resized(object sender, EventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl.OpenGL;

            //  Зададим матрицу проекции
            gl.MatrixMode(OpenGL.GL_PROJECTION);

            //  Единичная матрица для последующих преобразований
            gl.LoadIdentity();

            //  Преобразование
            gl.Perspective(60.0f, (double)Width / (double)Height, 0.01, 100.0);


            //  Данная функция позволяет установить камеру и её положение
            double dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
            double dY = Math.Sin(AngleY * Rad);
            double dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);

            gl.LookAt(POSX, POSY, POSZ,    // Позиция самой камеры
                      POSX + dX,
                      POSY + dY,
                      POSZ + dZ,     // Направление, куда мы смотрим
                       0, 1, 0);    // Верх камеры

            //  Зададим модель отображения
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            //gl.PushMatrix();
            //gl.Translate(100.0f, 0.0f, 0.0f);
            //gl.LoadIdentity();
            //float[] pos1 = { 100f, 0f, 0f, 100f };
            //gl.Enable(OpenGL.GL_COLOR_MATERIAL);
            //gl.Enable(OpenGL.GL_DEPTH_TEST);
            //gl.Enable(OpenGL.GL_LIGHTING);
            //gl.Enable(OpenGL.GL_LIGHT0);
            //gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_POSITION, pos1);
            //gl.PopMatrix();
        }



        // Мышка
        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;
        int lX = -1, lY = -1;
        bool b = false;
        // Камера
        private void SharpGLForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (b)
            {
                if (lX != -1)
                {
                    AngleX += (lX - e.X) / 5f;
                }
                if (lY != -1) AngleY += (lY - e.Y) / 5f;
                //Console.WriteLine($"mouse {lX} {lY}");
                openGLControl_Resized(sender, e);
            }
            lX = e.X;
            lY = e.Y;
        }
        private void openGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            double K = 1;
            double dX, dY, dZ;
            switch (e.KeyCode)
            {
                case Keys.W:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * K;
                    POSY += dY * K;
                    POSZ += dZ * K;
                    break;
                case Keys.S:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX -= dX * K;
                    POSY -= dY * K;
                    POSZ -= dZ * K;
                    break;

                case Keys.A:
                    dX = Math.Sin((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * K;
                    POSZ += dZ * K;
                    break;
                case Keys.D:
                    dX = Math.Sin((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX * K;
                    POSZ += dZ * K;
                    break;
                case Keys.Space:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    dY = Math.Sin((AngleY - 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    POSX += dX * K;
                    POSY += dY * K;
                    POSZ += dZ * K;
                    break;
                case Keys.Shift:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    dY = Math.Sin((AngleY + 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    POSX += dX * K;
                    POSY += dY * K;
                    POSZ += dZ * K;
                    break;
            }
            //openGLControl.Invalidate();
            openGLControl_Resized(sender, e);
        }


    }

}