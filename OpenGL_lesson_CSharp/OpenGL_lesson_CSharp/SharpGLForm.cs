using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph.Primitives;
using SharpGL.SceneGraph.Raytracing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        float AngleX = 0, AngleY = 0;
        double POSX = 2, POSY = 0, POSZ = 0;

        const float Rad = 3.14f / 180f;

        public SharpGLForm()
        {
            InitializeComponent();
        }

        private void openGLControl_OpenGLDraw(object sender, RenderEventArgs e)
        {
            //  Возьмём OpenGL объект
            OpenGL gl = openGLControl.OpenGL;

            //  Очищаем буфер цвета и глубины
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            
            //  Загружаем единичную матрицу
            gl.LoadIdentity();

            // Сдвигаем перо вправо от центра и вглубь экрана, но уже дальше
            //gl.Translate(0.0f, 0.0f, -10.0f);
            gl.Translate(0.0f, 0.0f, 0.0f);

            //  Указываем оси вращения (x, y, z)
            //gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);

            float size = 10.0f;
            float[] coordinates = new float[3] {0, 0, 0};
            // рисуем куб
            Cub(gl, coordinates, size);

            coordinates[0] = 20;
            coordinates[1] = 20;
            coordinates[2] = 0;
            size = 1;
            Cub(gl, coordinates, size);

            // Контроль полной отрисовки следующего изображения
            gl.Flush();
        }

        void Cub(OpenGL gl, float[] coordinates, float size)
        {
            // рисуем куб
            //gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Begin(OpenGL.GL_QUADS);

            // Top
            gl.Color(0.0f, 1.0f, 0.0f);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            // Bottom
            gl.Color(1.0f, 0.5f, 0.0f);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            // Front
            gl.Color(1.0f, 0.0f, 0.0f);
            gl.Vertex(size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Back
            gl.Color(1.0f, 1.0f, 0.0f);
            gl.Vertex(size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            // Left
            gl.Color(0.0f, 0.0f, 1.0f);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], -size + coordinates[2]);
            gl.Vertex(-size + coordinates[0], -size + coordinates[1], size + coordinates[2]);
            // Right
            gl.Color(1.0f, 0.0f, 1.0f);
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
        }

        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;

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