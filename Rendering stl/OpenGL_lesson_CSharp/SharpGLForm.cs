using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Primitives;
using static System.Windows.Forms.AxHost;
using static OpenGL_lesson_CSharp.SharpGLForm;
using ZedGraph;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;


namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        float AngleX = 0, AngleY = 0;
        double POSX = 2, POSY = 0, POSZ = 0;

        const float Rad = 3.14f / 180f;

        Random random = new Random();

        ValueSave vs = new ValueSave();

        int currentMixture = 0;
        int detail = 0;

        int[] time;

        NumericUpDown[,] mixture;


        NumericUpDown[] life;

        int[][] timersvaluedetails = new int[31][];


        System.Windows.Forms.Label[] lifeLabel;


        //Шнеки
        string[] fileStem = new string[]
           {
                "AISI304",
                "backcoverfront",
                "backcoverrear",
                "backcover",

                "body1",
                "body2",
                "body3",
                "body4",

                "fatcover1",
                "fatcover2",
                "frontcover",
                "pad",

                "pin1",
                "pin2",
                "pin3",
                "pin4",

                "rearconnections1",
                "rearconnections2",
                "rearconnections3",
                "rearconnections4",
           };
        Model[,] stem = new Model[3,19];

        //Защита бункера
        string[] fileHopperProtection = new string[] 
        {
            "auger right",
            "behind the auger",
            "cover protection",
            "front of the auger",
            "left auger",
        };
        Model[] HopperProtection = new Model[5];

        //Бункер
        string[] fileBunker = new string[]
        {
            "back",
            "bottom",
            "front",
            "left",
            "oblique",
            "parabola",
            "right",
        };
        Model[] Bunker = new Model[7];

        //Бункер
        string[] fileTable = new string[]
        {
            "before",
            "behind",
            "bottom corer",
            "door",
            "left",

            "perpendicular one (1)",
            "perpendicular one (2)",
            "perpendicular one (3)",
            "perpendicular one",

            "perpendicular two (1)",
            "perpendicular two (2)",
            "perpendicular two (3)",
            "perpendicular two",

            "pillars (1)",
            "pillars (2)",
            "pillars (3)",
            "pillars",

            "pillars",
            "right",
            "top",

        };
        Model[] Table = new Model[20];

        Model[] allModels = new Model[31];

        bool gogo = false;



        public SharpGLForm()
        {
            InitializeComponent();

            

            mixture = new NumericUpDown[,]{
                { numericUpDownCoefficient1, numericUpDowntemperature21, numericUpSpeed21 },
                { numericUpDownCoefficient2, numericUpDowntemperature22, numericUpSpeed22 },
                { numericUpDownCoefficient3, numericUpDowntemperature23, numericUpSpeed23 },
                { numericUpDownCoefficient4, numericUpDowntemperature24, numericUpSpeed24 },
                { numericUpDownCoefficient5, numericUpDowntemperature25, numericUpSpeed25 },
                { numericUpDownCoefficient6, numericUpDowntemperature26, numericUpSpeed26 },
            };


            for (int i = 0; i < mixture.GetLength(0); i++)
            {
                for (int j = 0; j < mixture.GetLength(1); j++)
                {
                    mixture[i, j].Value = int.Parse(vs.GetSaveValue("mixture" + i.ToString() + j.ToString()));
                }
            }


            life = new NumericUpDown[31];
            lifeLabel = new System.Windows.Forms.Label[31];
            time = new int[31]; // надо с названием именно времени
            int p = 1;

            for (int i = 0; i < time.Length; i++)
            {
                time[i] = int.Parse(vs.GetSaveValue("time" + i.ToString()));
                Array.Resize(ref timersvaluedetails[i], time[i]);
            }

            for (int i = 0; i < timersvaluedetails.GetLength(0); i++)
            {
                for (int j = 0; j < time[i]; j++)
                {
                    //timersvaluedetails[i][j] = int.Parse(vs.GetSaveValue("timersvaluedetails" + i + "H" + j));
                }
            }

            for (int i = 0; i < life.Length; i++, p++)
            {
                life[i] = (NumericUpDown)Controls.Find("numericUpDownmeaning" + p, true)[0];
                lifeLabel[i] = (System.Windows.Forms.Label)Controls.Find("labelLife" + p, true)[0];

            }


            for (int i = 0; i < life.Length; i++)
            {
                life[i].Value = int.Parse(vs.GetSaveValue("life" + i.ToString()));
            }


            for (int i = 0; i < lifeLabel.Length; i++)
            {
                lifeLabel[i].Text = vs.GetSaveValue("lifeLabel" + i.ToString());
            }



            int y = 0;

            for (int i = 0; i < stem.GetLength(0); i++)
            {
                for (int j = 0; j < stem.GetLength(1); j++, y++)
                {
                    stem[i, j] = new Model();
                    stem[i, j].LoadFromObj(new StreamReader("body\\Шнеки\\" + fileStem[j] + ".stl"));
                    stem[i, j].Transformation(0.01f, new Vector3(0, 0f, 0f), new Vector3(i * 1.33f, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });

                }
                stem[i, 4].Transformation(0.1f, new Vector3(0, 0f, 0f), new Vector3(i * 1.195f, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
                stem[i, 5].Transformation(0.1f, new Vector3(0, 0f, 0f), new Vector3(i * 1.195f, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
                stem[i, 6].Transformation(0.1f, new Vector3(0, 0f, 0f), new Vector3(i * 1.195f, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
                stem[i, 7].Transformation(0.1f, new Vector3(0, 0f, 0f), new Vector3(i * 1.195f, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });

            }

            for (int j = 0; j < HopperProtection.GetLength(0); j++, y++)
            {
                HopperProtection[j] = new Model();
                HopperProtection[j].LoadFromObj(new StreamReader("body\\Защита бункера\\" + fileHopperProtection[j] + ".stl"));
                HopperProtection[j].Transformation(0.01f, new Vector3(0, 0f, 0f), new Vector3(0, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
            }

            for (int j = 0; j < Bunker.GetLength(0); j++, y++)
            {
                Bunker[j] = new Model();
                Bunker[j].LoadFromObj(new StreamReader("body\\Бункер\\" + fileBunker[j] + ".stl"));
                Bunker[j].Transformation(0.01f, new Vector3(0, 0f, 0f), new Vector3(0, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
            }

            for (int j = 0; j < Table.GetLength(0); j++, y++)
            {
                Table[j] = new Model();
                Table[j].LoadFromObj(new StreamReader("body\\Стол\\" + fileTable[j] + ".stl"));
                Table[j].Transformation(0.01f, new Vector3(0, 0f, 0f), new Vector3(0, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
            }

            string[] filevse = Directory.GetFiles("body\\Все");
            for (int i = 0; i < 31; i++)
            {
                allModels[i] = new Model();
                allModels[i].LoadFromObj(new StreamReader(filevse[i]));
                allModels[i].Transformation(0.01f, new Vector3(0, 0f, 0f), new Vector3(0, 0, 0), new float[] { (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0), (float)(random.NextDouble() * (1 - 0) + 0) });
            }


            Console.WriteLine(y);

            gogo = true;
        }

        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl.OpenGL;

            //  Очищаем буфер цвета и глубины
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            //  Загружаем единичную матрицу
            gl.LoadIdentity();


            //Cub(gl, new float[] {1f, 1f, 1f }, 1, new float[] { 1, 0, 0});

            gl.Begin(OpenGL.GL_QUADS);

            for (int i = 0; i < stem.GetLength(0); i++)
            {
                for (int j = 0; j < stem.GetLength(1); j++)
                {
                    stlOutputOBJ(gl, stem[i, j]);
                }
            }

            for (int j = 0; j < HopperProtection.GetLength(0); j++)
            {
                stlOutputOBJ(gl, HopperProtection[j]);
            }

            for (int j = 0; j < Bunker.GetLength(0); j++)
            {
                stlOutputOBJ(gl, Bunker[j]);
            }

            for (int j = 0; j < Table.GetLength(0); j++)
            {
                stlOutputOBJ(gl, Table[j]);
            }

            gl.End();


            // Контроль полной отрисовки следующего изображения
            gl.Flush();
        }

        void stlOutputSTL(OpenGL gl, Model model)
        {
            gl.Color(model.color[0], model.color[1], model.color[2]);
            for (int i = 0; i < model.triangle.Count; i++)
            {
                gl.Vertex(model.triangle[i].p1.X, model.triangle[i].p1.Y, model.triangle[i].p1.Z);
                gl.Vertex(model.triangle[i].p2.X, model.triangle[i].p2.Y, model.triangle[i].p2.Z);
                gl.Vertex(model.triangle[i].p3.X, model.triangle[i].p3.Y, model.triangle[i].p3.Z);
                gl.Vertex(model.triangle[i].p3.X, model.triangle[i].p3.Y, model.triangle[i].p3.Z);
            }
        }

        void stlOutputOBJ(OpenGL gl, Model model)
        {
            gl.Color(model.color[0], model.color[1], model.color[2]);
            for (int i = 0; i < model.Vertexes.Count; i += 3)
            {
                gl.Vertex(model.Vertexes[i + 0].X, model.Vertexes[i + 0].Y, model.Vertexes[i + 0].Z);
                gl.Vertex(model.Vertexes[i + 1].X, model.Vertexes[i + 1].Y, model.Vertexes[i + 1].Z);
                gl.Vertex(model.Vertexes[i + 2].X, model.Vertexes[i + 2].Y, model.Vertexes[i + 2].Z);
                gl.Vertex(model.Vertexes[i + 2].X, model.Vertexes[i + 2].Y, model.Vertexes[i + 2].Z);
            }
        }

        public class Model
        {
            public List<Triangle> triangle = new List<Triangle>();
            public List<Vector3> Vertexes = new List<Vector3>();

            public float[] color = new float[3];

            public void coler(float[] colar)
            {
                //цвет
                this.color = colar;
            }

            public void Transformation(float size, Vector3 corner, Vector3 position, float[] colar)
            {
                //матрица масштабирования
                var scaleM = Matrix4x4.CreateScale(size);
                //матрица вращения
                var rotateM = Matrix4x4.CreateFromYawPitchRoll(corner.X, corner.Y, corner.Z);
                //матрица переноса
                var translateM = Matrix4x4.CreateTranslation(position);
                //результирующая матрица
                var m = scaleM * rotateM * translateM;// * paneXY;

                //умножаем вектора на матрицу
                Vertexes = Vertexes.Select(v => Vector3.Transform(v, m)).ToList();
                
                //цвет
                this.color = colar;
            }

            public void LoadFromObj(TextReader tr)
            {
                string line;

                while ((line = tr.ReadLine()) != null)
                {
                    var parts1 = line.Split(' ');
                    if (parts1.Length == 0) continue;
                    if (parts1.Length == 12 && parts1[6] == "vertex")
                    {
                        var parts2 = tr.ReadLine().Split(' ');
                        var parts3 = tr.ReadLine().Split(' ');

                        triangle.Add(new Triangle(
                            new Vector3(float.Parse(parts1[9], CultureInfo.InvariantCulture),
                                float.Parse(parts1[10], CultureInfo.InvariantCulture),
                                float.Parse(parts1[11], CultureInfo.InvariantCulture)),

                            new Vector3(float.Parse(parts2[9], CultureInfo.InvariantCulture),
                                float.Parse(parts2[10], CultureInfo.InvariantCulture),
                                float.Parse(parts2[11], CultureInfo.InvariantCulture)),

                            new Vector3(float.Parse(parts3[9], CultureInfo.InvariantCulture),
                                float.Parse(parts3[10], CultureInfo.InvariantCulture),
                                float.Parse(parts3[11], CultureInfo.InvariantCulture))
                                )
                            );

                        Vertexes.Add(new Vector3(float.Parse(parts1[9], CultureInfo.InvariantCulture),
                                float.Parse(parts1[10], CultureInfo.InvariantCulture),
                                float.Parse(parts1[11], CultureInfo.InvariantCulture)));

                        Vertexes.Add(new Vector3(float.Parse(parts2[9], CultureInfo.InvariantCulture),
                                float.Parse(parts2[10], CultureInfo.InvariantCulture),
                                float.Parse(parts2[11], CultureInfo.InvariantCulture)));

                        Vertexes.Add(new Vector3(float.Parse(parts3[9], CultureInfo.InvariantCulture),
                                float.Parse(parts3[10], CultureInfo.InvariantCulture),
                                float.Parse(parts3[11], CultureInfo.InvariantCulture)));
                    }
                }
            }

            public struct Triangle
            {
                public Vector3 p1, p2, p3;

                public Triangle(Vector3 P1, Vector3 P2, Vector3 P3)
                {
                    p1 = P1;
                    p2 = P2;
                    p3 = P3;
                }
            }

        }

        void Cub(OpenGL gl, float[] coordinates, float size, float[] color)
        {
            float blackout = .2f;
            // рисуем куб
            //gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Begin(OpenGL.GL_QUADS);

            // Top
            gl.Color(color[0], color[1], color[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            // Bottom
            gl.Color(color[0] - blackout, color[1] - blackout, color[2] - blackout);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            // Front
            gl.Color(color[0], color[1], color[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Back
            gl.Color(color[0] - blackout, color[1] - blackout, color[2] - blackout);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            // Left
            gl.Color(color[0], color[1], color[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Right
            gl.Color(color[0] - blackout, color[1] - blackout, color[2] - blackout);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);

            gl.End();
        }

        // Эту функцию используем для задания некоторых значений по умолчанию
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
            gl.PushMatrix();
            gl.Translate(100.0f, 0.0f, 0.0f);
            gl.LoadIdentity();
            float[] pos1 = { 100f, 0f, 0f, 100f };
            gl.Enable(OpenGL.GL_COLOR_MATERIAL);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_LIGHTING);
            gl.Enable(OpenGL.GL_LIGHT0);
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_POSITION, pos1);
            gl.PopMatrix();
        }

        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;

        bool b = false;

        private void button1_Click(object sender, EventArgs e)
        {
        }

        private void numericX_ValueChanged(object sender, EventArgs e)
        { 
        }

        private void numericY_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numericZ_ValueChanged(object sender, EventArgs e)
        {
        }

        private void SIZE_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numeric_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numericCornerX_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numericCornerY_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numericCornerZ_ValueChanged(object sender, EventArgs e)
        {
        }

        private void ColorX_ValueChanged(object sender, EventArgs e)
        {
        }

        private void ColorY_ValueChanged(object sender, EventArgs e)
        {
        }

        private void ColorZ_ValueChanged(object sender, EventArgs e)
        {
        }

        private void numericUpDownSpeed_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            
        }

        private void label43_Click(object sender, EventArgs e)
        {

        }

        private void radioButton6_Click(object sender, EventArgs e)
        {
            //mixture radioButtonMixture
            //currentMixture = int.Parse((sender as RadioButton).Name.Replace("radioButtonMixture", "")) - 1;
            //mixture[i, j].Value
            //numericUpDownTemp.Value = mixture[currentMixture, 1].Value;
            //numericUpDownSpeed.Value = mixture[currentMixture, 2].Value;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            for (int i = 0; i < time.Length; i++)
            {
                time[i]++;
                //timersvaluedetails[i][j]
            }


            for (int i = 0; i < life.Length; i++)
            {
                lifeLabel[i].Text = Convert.ToString(Convert.ToInt32(lifeLabel[i].Text) - (1 * mixture[currentMixture, 0].Value));
                Array.Resize(ref timersvaluedetails[i], time[i] + 1);
                timersvaluedetails[i][time[i]] = Convert.ToInt32(lifeLabel[i].Text);
                
            }


            // хз надо доделать.

        }

        private void label67_Click(object sender, EventArgs e)
        {

        }

        private void label73_Click(object sender, EventArgs e)
        {

        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void radioButtonDetails1_Click(object sender, EventArgs e)
        {
            detail = int.Parse((sender as RadioButton).Name.Replace("radioButtonDetails", "")) - 1;

            GraphPane pane = zedGraphControl1.GraphPane;
            pane.CurveList.Clear();

            PointPairList list = new PointPairList();

            for (int i = 0; i < timersvaluedetails[detail].Length; i++)
            {
                list.Add(i, timersvaluedetails[detail][i]);
            }

            LineItem myCurve = pane.AddCurve("Sinc", list, Color.Blue, SymbolType.None);

            zedGraphControl1.AxisChange();

            zedGraphControl1.Invalidate();
            // currentMixture + lifeLabel[i].Text + i.ToString() // значения
            // "time" + i.ToString() время

            // график и вывод gl
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            

        }

        private void radioButtonMixture11_Click(object sender, EventArgs e)
        {
            currentMixture = int.Parse((sender as RadioButton).Name.Replace("radioButtonMixture1", "")) - 1;
            //mixture[i, j].Value
            numericUpDownTemp.Value = mixture[currentMixture, 1].Value;
            numericUpDownSpeed.Value = mixture[currentMixture, 2].Value;
        }

        private void SharpGLForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            vs.DeleteAll();
            for (int i = 0; i < life.Length; i++)
            {
                //life
                vs.SetSaveValue("life" + i.ToString(), life[i].Value.ToString());
            }

            for (int i = 0; i < mixture.GetLength(0); i++)
            {
                for (int j = 0; j < mixture.GetLength(1); j++)
                {
                    vs.SetSaveValue("mixture" + i.ToString() + j.ToString(), mixture[i, j].Value.ToString());
                }
            }


            for (int i = 0; i < time.Length; i++)
            {
                vs.SetSaveValue("time" + i.ToString(), time[i].ToString());
            }

            for (int i = 0; i < timersvaluedetails.GetLength(0); i++)
            {
                for (int j = 0; j < time[i]; j++)
                {
                    vs.SetSaveValue("timersvaluedetails" + i + "H" + j, timersvaluedetails[i][j].ToString());
                }
            }


            for (int i = 0; i < lifeLabel.Length; i++)
            {
                vs.SetSaveValue("lifeLabel" + i.ToString(), lifeLabel[i].Text);
            }

        }

        private void numericUpDownmeaning1_ValueChanged(object sender, EventArgs e)
        {
            
            //(sender as NumericUpDown).Name
            if (gogo)
            {
                int lifef = int.Parse((sender as NumericUpDown).Name.Replace("numericUpDownmeaning", "")) - 1;

                lifeLabel[lifef].Text = life[lifef].Value.ToString();


                string[] _base = Directory.GetFiles(Environment.CurrentDirectory, "*.ini");
                foreach (string item in _base)
                {
                    if (item.IndexOf("timersvaluedetails" + lifef) >= 0)
                    {
                        File.Delete(item);
                    }
                }

                time[lifef] = 0;

            }
            

            //life
        }

        private void openGLControl1_OpenGLDraw(object sender, RenderEventArgs args)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl1.OpenGL;

            //  Очищаем буфер цвета и глубины
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            //  Загружаем единичную матрицу
            gl.LoadIdentity();


            //Cub(gl, new float[] {1f, 1f, 1f }, 1, new float[] { 1, 0, 0});

            gl.Begin(OpenGL.GL_QUADS);


            //detail
            //for (int j = 0; j < allModels[detail]; j++)
            //{

                allModels[detail].coler(new float[] {
                (Convert.ToInt32(lifeLabel[detail].Text) / (float)life[detail].Value),
                0,
                0
                });
                stlOutputOBJ(gl, allModels[detail]);
            //}



            gl.End();


            // Контроль полной отрисовки следующего изображения
            gl.Flush();
        }

        private void openGLControl1_OpenGLInitialized(object sender, EventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl1.OpenGL;

            //  Фоновый цвет по умолчанию (в данном случае цвет голубой)
            gl.ClearColor(0.1f, 0.5f, 1.0f, 0);
        }

        private void openGLControl1_Resized(object sender, EventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl1.OpenGL;

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
            gl.PushMatrix();
            gl.Translate(100.0f, 0.0f, 0.0f);
            gl.LoadIdentity();
            float[] pos1 = { 100f, 0f, 0f, 100f };
            gl.Enable(OpenGL.GL_COLOR_MATERIAL);
            gl.Enable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_LIGHTING);
            gl.Enable(OpenGL.GL_LIGHT0);
            gl.Light(OpenGL.GL_LIGHT0, OpenGL.GL_POSITION, pos1);
            gl.PopMatrix();
        }

        private void openGLControl1_KeyDown(object sender, KeyEventArgs e)
        {
            double dX, dY, dZ;
            switch (e.KeyCode)
            {
                case Keys.W:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
                case Keys.S:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX -= dX;
                    POSY -= dY;
                    POSZ -= dZ;
                    break;

                case Keys.A:
                    dX = Math.Sin((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSZ += dZ;
                    break;
                case Keys.D:
                    dX = Math.Sin((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSZ += dZ;
                    break;
                case Keys.Space:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    dY = Math.Sin((AngleY - 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
                case Keys.Shift:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    dY = Math.Sin((AngleY + 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
            }
            //openGLControl.Invalidate();
            openGLControl1_Resized(sender, e);
        }

        private void openGLControl1_MouseDown(object sender, MouseEventArgs e) => b = true;

        int lX = -1, lY = -1;

        private void openGLControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (b)
            {
                if (lX != -1)
                {
                    AngleX += (lX - e.X) / 5f;
                }
                if (lY != -1) AngleY += (lY - e.Y) / 5f;
                //Console.WriteLine($"mouse {lX} {lY}");
                openGLControl1_Resized(sender, e);
            }
            lX = e.X;
            lY = e.Y;
        }

        private void openGLControl1_MouseUp(object sender, MouseEventArgs e) => b = false;

        
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
            double dX, dY, dZ;
            switch (e.KeyCode)
            {
                case Keys.W:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
                case Keys.S:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    dY = Math.Sin(AngleY * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);
                    POSX -= dX;
                    POSY -= dY;
                    POSZ -= dZ;
                    break;

                case Keys.A:
                    dX = Math.Sin((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX + 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSZ += dZ;
                    break;
                case Keys.D:
                    dX = Math.Sin((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    dZ = Math.Cos((AngleX - 90) * Rad) * Math.Cos(AngleY * Rad);
                    POSX += dX;
                    POSZ += dZ;
                    break;
                case Keys.Space:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    dY = Math.Sin((AngleY - 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY - 90) * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
                case Keys.Shift:
                    dX = Math.Sin(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    dY = Math.Sin((AngleY + 90) * Rad);
                    dZ = Math.Cos(AngleX * Rad) * Math.Cos((AngleY + 90) * Rad);
                    POSX += dX;
                    POSY += dY;
                    POSZ += dZ;
                    break;
            }
            //openGLControl.Invalidate();
            openGLControl_Resized(sender, e);
        }

    }
}