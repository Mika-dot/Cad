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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace OpenGL_lesson_CSharp
{
    public partial class SharpGLForm : Form
    {
        float rotation = 0.0f;
        float AngleX = 30;
        float AngleY = 30;
        float AngleZ = 0;

        const float AngleDl = 1;

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
            gl.LookAt(AngleX, AngleY, AngleZ,    // Позиция самой камеры
                       0, 0, 0,     // Направление, куда мы смотрим
                       0, 1, 0);    // Верх камеры

            //  Зададим модель отображения
            gl.MatrixMode(OpenGL.GL_MODELVIEW);

            Console.WriteLine(AngleX + " " + AngleY + " " + AngleZ);
        }

        private void openGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                    AngleX += AngleDl;
                    break;
                case Keys.S:
                    AngleX -= AngleDl;
                    break;
                case Keys.A:
                    AngleY += AngleDl;
                    break;
                case Keys.D:
                    AngleY -= AngleDl;
                    break;
                case Keys.Q:
                    AngleZ += AngleDl;
                    break;
                case Keys.E:
                    AngleZ -= AngleDl;
                    break;
            }
            //openGLControl.Invalidate();
            openGLControl_Resized(sender, e);
        }


    }
}