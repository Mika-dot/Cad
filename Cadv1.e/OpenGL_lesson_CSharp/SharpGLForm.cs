using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.SceneGraph.Lighting;
using SharpGL.SceneGraph.Primitives;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        float AngleX = 0, AngleY = 0;
        double POSX = 2, POSY = 0, POSZ = 0;

        const float Rad = 3.14f / 180f;

        int[] worldX = new int[0]; // Значение вокселей
        int[] worldY = new int[0];
        int[] worldZ = new int[0];
        float[] worldID = new float[0];

        int[,] Temperature = new int[,] {
            { 0, 0, 0, -50 },
            { 10, 10, 10, 50 }
        };

        float temp = 0;

        float[,] color = new float[,] {
                    { 0.0f, 1.0f, 0.0f },
                    { 1.0f, 0.5f, 0.0f },
                    { 1.0f, 0.0f, 0.0f },
                    { 1.0f, 1.0f, 0.0f },
                    { 0.0f, 0.0f, 1.0f },
                    { 1.0f, 0.0f, 1.0f },
                };

        public SharpGLForm()
        {
            InitializeComponent();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            AdditionWorld(
                new int[] { 0, 10, 10, 0 }, // контур по Х
                new int[] { 0, 0, 10, 10 }, // контур по У
                10, // экструзия
                new int[] { 0, 0, 0 }, // Кручение вокселей
                new int[] { 0, 0, 0 }, // Перемещение
                1, // тип экструзии

                ref worldX, ref worldY, ref worldZ
            );

            AdditionWorld(
                new int[] { 0, 5, 5, 0 }, // контур по Х
                new int[] { 0, 0, 5, 5 }, // контур по У
                10, // экструзия
                new int[] { 0, 0, 0 }, // Кручение вокселей
                new int[] { 2, 2, 2 }, // Перемещение
                0, // тип экструзии

                ref worldX, ref worldY, ref worldZ
            );

            AdditionWorld(
                new int[] { 0, 10, 10 }, // контур по Х
                new int[] { 10, 0, 10 }, // контур по У
                3, // экструзия
                new int[] { 0, 0, 0 }, // Кручение вокселей
                new int[] { 0, 0, 0 }, // Перемещение
                0, // тип экструзии

                ref worldX, ref worldY, ref worldZ
            );
        }
        private void button2_Click(object sender, EventArgs e)
        {
            temp = TemperatureVoxel(ref worldID, worldX, worldY, worldZ, new int[,] {
                    { 0, 0, 0, -10 },
                    { 10, 10, 10, 10 }
                });
        }

        private void button3_Click(object sender, EventArgs e) 
        { 
            Gcode("test", ref worldX, ref worldY, ref worldZ); 
        }
        private void button5_Click(object sender, EventArgs e)
        {
            SaveGcode("save.txt");
        }
        private void button4_Click(object sender, EventArgs e)
        {
            ReproductionAsync("save.txt");
        }
        private void button6_Click(object sender, EventArgs e)
        {
            SaveTemperatureVoxel(
                                 "save.txt",
                                 new int[,] {
                                 { 0, 0, 0, -10 },
                                 { 10, 10, 10, 10 },
                                 });
        }
        private void button7_Click(object sender, EventArgs e)
        {
            SaveAdditionWorld(
                             "save.txt",
                             new int[] { 0, 10, 10, 0 }, // контур по Х
                             new int[] { 0, 0, 10, 10 }, // контур по У
                             10, // экструзия
                             new int[] { 0, 0, 0 }, // Кручение вокселей
                             new int[] { 0, 0, 0 }, // Перемещение
                             1 // тип экструзии
                             );
        }
        void ReproductionAsync(string path)
        {
            string[] values = new string[0];
            using (StreamReader sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Array.Resize(ref values, values.Length + 1);
                    values[values.Length - 1] = line;
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                if ((i < values.Length) && (values[i] == "AdditionWorld"))
                {
                    int[] x = new int[Convert.ToInt32(values[i + 1])];
                    for (int j = 0; j < Convert.ToInt32(values[i + 1]); j++)
                    {

                        x[j] = Convert.ToInt32(values[i + 2 + j]);
                    }

                    i = i + 1 + Convert.ToInt32(values[i + 1]);
                    int[] y = new int[Convert.ToInt32(values[i + 1])];
                    for (int j = 0; j < Convert.ToInt32(values[i + 1]); j++)
                    {
                        y[j] = Convert.ToInt32(values[i + 2 + j]);
                    }

                    i = i + 2 + Convert.ToInt32(values[i + 1]);

                    int extrusion = Convert.ToInt32(values[i]);

                    i++;

                    int[] torsion = new int[3];
                    for (int j = 0; j < 3; j++)
                    {
                        torsion[j] = Convert.ToInt32(values[i + j]);
                    }

                    i += 3;

                    int[] moving = new int[3];
                    for (int j = 0; j < 3; j++)
                    {
                        moving[j] = Convert.ToInt32(values[i + j]);
                    }

                    i += 3;

                    int type = Convert.ToInt32(values[i]);

                    i++;

                    AdditionWorld(
                    x, // контур по Х
                    y, // контур по У
                    extrusion, // экструзия
                    torsion, // Кручение вокселей
                    moving, // Перемещение
                    type, // тип экструзии

                    ref worldX, ref worldY, ref worldZ
                    );

                }

                if ((i < values.Length) && (values[i] == "TemperatureVoxel"))
                {
                    int[,] temperature = new int[Convert.ToInt32(values[i + 1]), 4];
                    for (int j = 0; j < temperature.GetLength(0); j++)
                    {
                        temperature[j, 0] = Convert.ToInt32(values[i + 2]);
                        temperature[j, 1] = Convert.ToInt32(values[i + 3]);
                        temperature[j, 2] = Convert.ToInt32(values[i + 4]);
                        temperature[j, 3] = Convert.ToInt32(values[i + 5]);
                        i += 4;
                    }
                    temp = TemperatureVoxel(ref worldID, worldX, worldY, worldZ, temperature);
                }

                if ((i < values.Length) && (values[i] == "Gcode"))
                {
                    Gcode("test", ref worldX, ref worldY, ref worldZ);
                }
            }

        }

        void SaveAdditionWorld(string path, int[] x, int[] y, int extrusion, int[] Torsion, int[] moving, int type)
        {
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLineAsync("AdditionWorld");
                writer.WriteLineAsync(Convert.ToString(x.Length));
                for (int i = 0; i < x.Length; i++)
                {
                    writer.WriteLineAsync(Convert.ToString(x[i]));
                }

                writer.WriteLineAsync(Convert.ToString(y.Length));
                for (int i = 0; i < y.Length; i++)
                {
                    writer.WriteLineAsync(Convert.ToString(y[i]));
                }

                writer.WriteLineAsync(Convert.ToString(extrusion));

                for (int i = 0; i < 3; i++)
                {
                    writer.WriteLineAsync(Convert.ToString(Torsion[i]));
                }

                for (int i = 0; i < 3; i++)
                {
                    writer.WriteLineAsync(Convert.ToString(moving[i]));
                }

                writer.WriteAsync(Convert.ToString(type));
            }
        }

        void SaveTemperatureVoxel(string path, int[,] temperature)
        {
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLineAsync("TemperatureVoxel");
                writer.WriteLineAsync(Convert.ToString(temperature.GetLength(0)));
                for (int i = 0; i < temperature.GetLength(0); i++)
                {
                    for (int j = 0; j < temperature.GetLength(1); j++)
                    {
                        writer.WriteLineAsync(Convert.ToString(temperature[i, j]));
                    }
                }
            }
        }

        void SaveGcode(string path)
        {
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLineAsync("Gcode");
            }
        }


        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl.OpenGL;

            //  Очищаем буфер цвета и глубины 
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            
            //  Загружаем единичную матрицу
            gl.LoadIdentity();

            //// Сдвигаем перо вправо от центра и вглубь экрана, но уже дальше
            ////gl.Translate(0.0f, 0.0f, -10.0f);
            //gl.Translate(0, 0, 0);

            ////  Указываем оси вращения (x, y, z)
            //gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);

            for (int i = 0; i < worldX.GetLength(0); i++)
            {
                float size = 0.5f;
                float[] coordinates = new float[3] { worldX[i], worldY[i], worldZ[i] };
                //рисуем куб
                if (temp != 0)
                {
                    float colorV = Math.Abs(worldID[i]) / temp;
                    if (worldID[i] < 0)
                    {
                        color = new float[,] {
                            { 0.0f, 0.0f, colorV },
                            { 0.0f, 0.0f, colorV },
                            { 0.0f, 0.0f, colorV },
                            { 0.0f, 0.0f, colorV },
                            { 0.0f, 0.0f, colorV },
                            { 0.0f, 0.0f, colorV },
                        };
                    }
                    else
                    {
                        color = new float[,] {
                            { colorV, 0.0f, 0.0f },
                            { colorV, 0.0f, 0.0f },
                            { colorV, 0.0f, 0.0f },
                            { colorV, 0.0f, 0.0f },
                            { colorV, 0.0f, 0.0f },
                            { colorV, 0.0f, 0.0f },
                        };
                    }
                }

                Cub(gl, coordinates, size, ref color);
            }

            // Контроль полной отрисовки следующего изображения
            gl.Flush();

        }

        // Эту функцию используем для создания вокселя заданог id
        void Cub(OpenGL gl, float[] coordinates, float size, ref float[,] color)
        {
            // рисуем куб
            //gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Begin(OpenGL.GL_QUADS);

            // Top
            gl.Color(color[0, 0], color[0, 1], color[0, 2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            // Bottom
            gl.Color(color[1, 0], color[1, 1], color[1, 2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            // Front
            gl.Color(color[2, 0], color[2, 1], color[2, 2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Back
            gl.Color(color[3, 0], color[3, 1], color[3, 2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            // Left
            gl.Color(color[4, 0], color[4, 1], color[4, 2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Right
            gl.Color(color[5, 0], color[5, 1], color[5, 2]);
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
            var dX = Math.Sin(AngleX * Rad) * Math.Cos(AngleY * Rad);
            var dY = Math.Sin(AngleY * Rad);
            var dZ = Math.Cos(AngleX * Rad) * Math.Cos(AngleY * Rad);

            gl.LookAt(POSX, POSY, POSZ,    // Позиция самой камеры
                      POSX + dX,
                      POSY + dY,
                      POSZ + dZ,     // Направление, куда мы смотрим
                       0, 1, 0);    // Верх камеры

            //  Зададим модель отображения
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            //Console.WriteLine(POSX + " " + POSY + " " + POSZ + $" {AngleX}");
        }

        bool b = false;
        int lX = -1, lY = -1;

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
            /*
             
            if (b)
            {
                if (lX == -1)
                {
                    lX = e.X;
                    lY = e.Y;
                }
                else
                {
                    AngleX += (lX - e.X) / 5f;
                    AngleY += (lY - e.Y) / 5f;
                    Cursor.Position = CUR_POINT;
                    Console.WriteLine($"mouse {lX} {lY}");
                    openGLControl_Resized(sender, e);
                }
            }
             */
        }

        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;

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




        //-----------------------------------------------------------------------+

        bool Dot(int x, int y, int[] CircuitX, int[] CircuitY)
        {
            int npol = CircuitX.GetLength(0);
            int j = npol - 1;
            bool c = false;
            for (var i = 0; i < npol; i++)
            {
                if ((((CircuitY[i] <= y) && (y < CircuitY[j])) || ((CircuitY[j] <= y) && (y < CircuitY[i]))) &&
                (x > (CircuitX[j] - CircuitX[i]) * (y - CircuitY[i]) / (CircuitY[j] - CircuitY[i]) + CircuitX[i]))
                {
                    c = !c;
                }
                j = i;
            }
            return c;
        } // Точка принадлежности

        int[] Dimensions(int[] CircuitX, int[] CircuitY)
        {
            int[] MinMax = new int[] { int.MaxValue, int.MaxValue, int.MinValue, int.MinValue };

            for (int i = 0; i < CircuitX.GetLength(0); i++)
            {
                if (MinMax[0] > CircuitX[i])
                {
                    MinMax[0] = CircuitX[i];
                }
                if (MinMax[2] < CircuitX[i])
                {
                    MinMax[2] = CircuitX[i];
                }

                if (MinMax[1] > CircuitY[i])
                {
                    MinMax[1] = CircuitY[i];
                }
                if (MinMax[3] < CircuitY[i])
                {
                    MinMax[3] = CircuitY[i];
                }
            }
            return MinMax;
        } // Точки контура мин. и макс.

        void Extrusion(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int HeightExtruder, int[] CircuitX, int[] CircuitY)
        {
            int[] dimensions = Dimensions(CircuitX, CircuitY);

            for (int x = dimensions[0]; x < dimensions[2]; x++)
            {
                for (int y = dimensions[1]; y < dimensions[3]; y++)
                {
                    if (Dot(x, y, CircuitX, CircuitY))
                    {
                        for (int z = 0; z < HeightExtruder; z++)
                        {
                            Array.Resize(ref ExtruderX, ExtruderX.Length + 1);
                            ExtruderX[ExtruderX.Length - 1] = x;

                            Array.Resize(ref ExtruderY, ExtruderY.Length + 1);
                            ExtruderY[ExtruderY.Length - 1] = y;

                            Array.Resize(ref ExtruderZ, ExtruderZ.Length + 1);
                            ExtruderZ[ExtruderZ.Length - 1] = z;
                        }
                    }
                }
            }
        }

        void AxesRotation(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int[] Corners)
        {
            for (int move = 0; move < ExtruderX.GetLength(0); move++)
            {
                int[] dot = new int[] { ExtruderX[move], ExtruderY[move], ExtruderZ[move] };
                dot = MX(dot, Corners[0]);
                dot = MY(dot, Corners[1]);
                dot = MZ(dot, Corners[2]);

                ExtruderX[move] = dot[0];
                ExtruderY[move] = dot[1];
                ExtruderZ[move] = dot[2];
            }
        }

        void MovingPoints(ref int[] ExtruderX, ref int[] ExtruderY, ref int[] ExtruderZ, int[] Moving)
        {
            for (int moving = 0; moving < ExtruderX.GetLength(0); moving++)
            {
                ExtruderX[moving] += Moving[0];
                ExtruderY[moving] += Moving[1];
                ExtruderZ[moving] += Moving[2];
            }
        }


        void AdditionWorld(
                int[] CircuitX, int[] CircuitY, int Height, int[] Corners, int[] Moving, int Type,
                ref int[] worldX, ref int[] worldY, ref int[] worldZ
        )
        {
            int[] ExtruderX = new int[0]; // Значение вокселей
            int[] ExtruderY = new int[0];
            int[] ExtruderZ = new int[0];

            Extrusion(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Height, CircuitX, CircuitY);
            AxesRotation(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Corners);
            MovingPoints(ref ExtruderX, ref ExtruderY, ref ExtruderZ, Moving);

            for (int i = 0; i < ExtruderX.GetLength(0); i++)
            {
                bool Existence = true;
                for (int MATW = 0; MATW < worldX.GetLength(0); MATW++)
                {
                    if (
                        ExtruderX[i] == worldX[MATW] &&
                        ExtruderY[i] == worldY[MATW] &&
                        ExtruderZ[i] == worldZ[MATW]
                        )
                    {
                        Existence = false;
                        if (Type == 0)
                        {
                            Delete(ref worldX, MATW);
                            Delete(ref worldY, MATW);
                            Delete(ref worldZ, MATW);
                        }
                        break;
                    }
                }
                if (Existence && (Type != 0))
                {
                    Array.Resize(ref worldX, worldX.Length + 1);
                    worldX[worldX.Length - 1] = ExtruderX[i];

                    Array.Resize(ref worldY, worldY.Length + 1);
                    worldY[worldY.Length - 1] = ExtruderY[i];

                    Array.Resize(ref worldZ, worldZ.Length + 1);
                    worldZ[worldZ.Length - 1] = ExtruderZ[i];
                }
            }
        }

        float TemperatureVoxel(ref float[] worldID, int[] worldX, int[] worldY, int[] worldZ, int[,] Temperature)
        {
            Array.Resize(ref worldID, worldX.Length);
            float ModuleMore = 0;
            for (int i = 0; i < worldID.GetLength(0); i++)
            {
                float temper = 0;
                for (int t = 0; t < Temperature.GetLength(0); t++)
                {
                    temper += Temperature[t, 3] / GeometricDistance(
                        new int[]
                        {
                Temperature[t, 0] , Temperature[t, 1], Temperature[t, 2]
                        },
                        new int[]
                        {
                worldX[i], worldY[i], worldZ[i]
                        }
                    );
                }
                worldID[i] = temper;
                if (ModuleMore < Math.Abs(temper))
                {
                    ModuleMore = temper;
                }
            }
            return ModuleMore;
        }

        void Gcode(string file, ref int[] worldX, ref int[] worldY, ref int[] worldZ)
        {
            StreamWriter f = new StreamWriter(file + ".gcode");
            f.WriteLine("G21 G40 G49 G53 G80 G90 G17");
            f.WriteLine("G90");
            f.WriteLine("M82");
            f.WriteLine("M140 S80");
            f.WriteLine("M104 S200");
            f.WriteLine("G28");
            f.WriteLine("G29");

            f.WriteLine("G0 X" + worldX[0] + " Y" + worldY[0] + " Z" + worldZ[0] + ";");


            f.WriteLine("G0 X" + worldX[0] + " Y" + worldY[0] + " Z" + worldZ[0] + ";");

            for (int i = 1; i < worldX.GetLength(0); i++)
            {
                int[] Start = new int[] { worldX[i - 1], worldY[i - 1], worldZ[i - 1] };
                int[] end = new int[] { worldX[i], worldY[i], worldZ[i] };

                if (GeometricDistance(Start, end) == 1)
                {
                    f.WriteLine("G1 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + " E2;");
                }
                else
                {
                    f.WriteLine("G0 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + ";");
                    f.WriteLine("G1 X" + worldX[i] + " Y" + worldY[i] + " Z" + worldZ[i] + " E1;");
                }


            }

            f.WriteLine("M107;");
            f.WriteLine("M104 S0;");
            //f.WriteLine("G0 Z" + (worldZ[worldZ.GetLength(0) - 1] + 100) + ";");
            f.WriteLine("M140 S0;");
            f.WriteLine("M84;");
            f.Close();
        }

        //----------------------------------------------------------

        void Delete(ref int[] array, int index)
        {
            if (index < array.Length && index >= 0)
            {
                int[] array2 = new int[array.Length - 1];
                for (int i = 0, j = 0; i < array.Length; i++)
                {
                    if (i == index) continue;
                    array2[j++] = array[i];
                }
                array = array2;
            }
        } // Удаление по индексу






        //----------------------------------------------------------

        int[] MX(int[] A, int angle)
        {
            int[] B = new int[3];
            double q = angle * (Math.PI / 180);

            B[0] = A[0];
            B[1] = (int)(A[1] * Math.Cos(q) + A[2] * Math.Sin(q));
            B[2] = (int)((-1) * A[1] * Math.Sin(q) + A[2] * Math.Cos(q));

            return B;
        }
        int[] MY(int[] A, int angle)
        {
            int[] B = new int[3];
            double q = angle * (Math.PI / 180);

            B[0] = (int)(A[0] * Math.Cos(q) + A[2] * Math.Sin(q));
            B[1] = A[1];
            B[2] = (int)((-1) * A[0] * Math.Sin(q) + A[2] * Math.Cos(q));

            return B;
        }
        int[] MZ(int[] A, int angle)
        {
            int[] B = new int[3];
            double q = angle * (Math.PI / 180);

            B[0] = (int)(A[0] * Math.Cos(q) - A[1] * Math.Sin(q));
            B[1] = (int)(A[0] * Math.Sin(q) + A[1] * Math.Cos(q));
            B[2] = A[2];

            return B;
        }

        float GeometricDistance(int[] dot, int[] temp)
        {
            return (float)Math.Sqrt(Math.Pow(dot[0] - temp[0], 2) + Math.Pow(dot[1] - temp[1], 2) + Math.Pow(dot[2] - temp[2], 2));
        }

        //----------------------------------------------------------

    }
}