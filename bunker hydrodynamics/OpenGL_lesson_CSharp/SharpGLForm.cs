using System;
using System.Drawing;
using System.Windows.Forms;
using SharpGL;
using SharpGL.SceneGraph.Primitives;


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
        public class Bernoulli
        {
            readonly float g = 9.8f;
            readonly float pressure = (float)Math.Pow(10, 5);

            public float Square(float r, float h) => r * h;

            public float Speed(float v, float[] s1, float[] s2) => Square(s1[0], s1[1]) * v / Square(s2[0], s2[1]);

            public float BernoulliConst( float density, float v, float[] s1, float[] s2, float h) => (float)(density * Math.Pow(Speed(v, s1, s2), 2) / 2) + density * g * h + pressure;

            public float BernoulliP( float density, float v, float[] s1, float[] s2, float h) => (float)(density * Math.Pow(Speed(v, s1, s2), 2) / 2) + density * g * h;

        }

        public class Triangle
        {
            public float Corner(float Mu) => (float)Math.Tan(Mu);

            public float TriangleHeight(float Mu, float a) => Mu * a;

            public float Square(float Mu, float a) => TriangleHeight(Mu, a) * a / 2;

            // ------- 

            public float PointsK( float Mu, float a ) => (-1) * (TriangleHeight(Mu, a) / a);

            public float PointsB( float Mu, float a ) => TriangleHeight(Mu, a);

        }

        public class Length
        {

            public float Straight(float y) => y;

            public float Linear(float y, float Mu, float a) => (y - new Triangle().PointsB(Mu, a)) / new Triangle().PointsK(Mu, a);

            public float Quadratic(float y, float a, float b, float c) => (float)((-1) * (b + Math.Sqrt(Math.Pow(b, 2) + 4 * a * y - 4 * a * c)) / (2 * a));

            public float TotalSettlement(float y, float Mu, float hz, float[] abc, float w) => y >= new Triangle().TriangleHeight(Mu, hz) ? Math.Abs(0 - (Quadratic(y, abc[0], abc[1], abc[2]) + w)) : Math.Abs(Linear(y, Mu, hz) - (Quadratic(y, abc[0], abc[1], abc[2]) + w));

            public float RightBorder(float y, float[] abc, float w) => Quadratic(y, abc[0], abc[1], abc[2]) + w;

            public float LeftBorder(float y, float Mu, float hz) => y >= new Triangle().TriangleHeight(Mu, hz) ? 0 : Linear(y, Mu, hz);

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

            float Mu = 1f; // трение
            float Pu = 1000; // плотность
            float Vn = 10; // скорость шнека


            float hz = 2f; // растояние механизма нижней крышки
            float[] abc = new float[] { 0.5f, 0, 0 }; // функция правой паработы

            float h = 4;
            float w = 5;
            float l = 1;


            float delta = 0.1f; // шаг по горизонту


            Triangle tr = new Triangle();
            Bernoulli brConst = new Bernoulli();
            Length functions = new Length();

            // константа 
            float constBer = brConst.BernoulliConst(
                    Pu, Vn,
                    new float[] { functions.TotalSettlement(0, Mu, hz, abc, w), l }, new float[] { functions.TotalSettlement(h, Mu, hz, abc, w), l}, 
                    h
                );


            // расчет давления в n точке
            // решим что давление в диапазоне 0-2 //  / (float)Math.Pow(10, 5) / 2
            float min = int.MaxValue;
            float max = int.MinValue;

            for (float j = 0; j < h; j += delta)
            {
                float Hn = j; // высота
                float Ber = constBer - brConst.BernoulliP(
                    Pu, Vn,
                    new float[] { functions.TotalSettlement(0, Mu, hz, abc, w), l }, new float[] { functions.TotalSettlement(Hn, Mu, hz, abc, w), l },
                    h
                );
                if (min > Ber)
                {
                    min = Ber;
                }
                if (max < Ber)
                {
                    max = Ber;
                }
            }

            
            for (float j = 0, r = 0; j < h; j += delta, r++)
            {
                float Hn = j; // высота
                float Ber = constBer - brConst.BernoulliP(
                    Pu, Vn,
                    new float[] { functions.TotalSettlement(0, Mu, hz, abc, w), l }, new float[] { functions.TotalSettlement(Hn, Mu, hz, abc, w), l },
                    h
                );

                for (float i = functions.LeftBorder(Hn, Mu, hz); i < functions.RightBorder(Hn, abc, w); i += delta)
                {
                    for (float n = 0; n < l; n += delta)
                    {
                        // Ber // x y z
                        Cub(gl, new float[] { i, Hn, n }, delta / 3, new float[] { (Ber - min) / (max - min) , 0, 0 });
                        
                    }
                }
            }
            


            // Контроль полной отрисовки следующего изображения
            gl.Flush();
        }

        void line(OpenGL gl, float[] Point1, float[] Point2, float size, float[] color)
        {
            float blackout = .2f;

            gl.Color(color[0], color[1], color[2]);
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(Point1[0], Point1[1], 0);
            gl.Vertex(Point2[0], Point2[1], 0);
            gl.Vertex(Point2[0], Point2[1], size);
            gl.Vertex(Point2[0], Point2[1], size);
            gl.End();
            gl.Color(color[0] - blackout, color[1] - blackout, color[2] - blackout);
            gl.Begin(OpenGL.GL_TRIANGLES);
            gl.Vertex(Point1[0], Point1[1], 0);
            gl.Vertex(Point1[0], Point1[1], size);
            gl.Vertex(Point2[0], Point2[1], size);
            gl.Vertex(Point2[0], Point2[1], size);
            gl.End();
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
            gl.ClearColor(1f, 1f, 1f, 0);
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
        }

        private void openGLControl_MouseDown(object sender, MouseEventArgs e) => b = true;
        private void openGLControl_MouseUp(object sender, MouseEventArgs e) => b = false;

        bool b = false;

        private void button1_Click(object sender, EventArgs e)
        {
        }

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