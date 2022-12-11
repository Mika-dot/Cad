using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace VARCad
{
    public class CADMath
    {

        public static Triangle[] CreateComplexFigure(Vector2[] PointsIN, float Zin, Vector2[] PointsOUT, float Zout) => FiguresCreator.CreateComplexFigure(PointsIN, Zin, PointsOUT, Zout);
        public static List<Triangle> XOR(List<Triangle> FigureFrom, List<Triangle> FigureWhat)
        {
            Console.WriteLine("начали");
            var FIGURE1 = FiguresCreator.IntersectThem(FigureFrom, FigureWhat);
            Console.WriteLine("начали 2");
            var FIGURE2 = FiguresCreator.IntersectThem(FigureWhat, FigureFrom);
            Console.WriteLine("начали 3");
            FigureWhat = FIGURE1;
            FigureFrom = FIGURE2;

            var COPY = new List<Triangle>(FigureFrom);

            // Вырезаем часть из From
            for (int i = 0; i < FigureFrom.Count; i++)
            {
                if (FigureFrom[i].IsInside(FigureWhat))
                {
                    FigureFrom.RemoveAt(i);
                    i--;
                }
            }

            // Удаляем оставшиеся What
            for (int i = 0; i < FigureWhat.Count; i++)
            {
                if (!FigureWhat[i].IsInside(COPY))
                {
                    FigureWhat.RemoveAt(i);
                    i--;
                }
            }

            FigureFrom.AddRange(FigureWhat);
            return FigureFrom;

            //Тут я думаю все понятно без слов
        }
        public static List<Triangle> OR(List<Triangle> FigureFrom, List<Triangle> FigureWhat)
        {
            Console.WriteLine("начали");
            var FIGURE1 = FiguresCreator.IntersectThem(FigureFrom, FigureWhat);
            Console.WriteLine("начали 2");
            var FIGURE2 = FiguresCreator.IntersectThem(FigureWhat, FigureFrom);
            Console.WriteLine("начали 3");
            FigureWhat = FIGURE1;
            FigureFrom = FIGURE2;

            var COPY = new List<Triangle>(FigureFrom);

            // Вырезаем часть из From
            for (int i = 0; i < FigureFrom.Count; i++)
            {
                if (FigureFrom[i].IsInside(FigureWhat))
                {
                    FigureFrom.RemoveAt(i);
                    i--;
                }
            }

            // Удаляем оставшиеся What
            for (int i = 0; i < FigureWhat.Count; i++)
            {
                if (FigureWhat[i].IsInside(COPY))
                {
                    FigureWhat.RemoveAt(i);
                    i--;
                }
            }

            //Тут я думаю все понятно без слов
            FigureFrom.AddRange(FigureWhat);
            return FigureFrom;
        }


    }

    class FiguresCreator
    {
        // Нахождения пересечений
        internal static List<Triangle> IntersectThem(List<Triangle> figure1, List<Triangle> figure2)
        {
            List<Triangle> Final = new List<Triangle>(figure2);
            for (int i = 0; i < figure1.Count; i++)
            {
                for (int j = 0; j < Final.Count; j++)
                {
                    if (Final[j].p1 == Vector3.Zero && Final[j].p2 == Vector3.Zero && Final[j].p3 == Vector3.Zero) continue;

                    if (TrianglesIntersection.Intersection(figure1[i], Final[j], out List<Vector3> res))
                    {
                        Final.RemoveAt(j);
                        Final.InsertRange(j, Triangulation.Triangulate(res));
                        j--;
                    }
                }
            }
            return Final.Distinct().Where(x => x.p1 != Vector3.Zero || x.p2 != Vector3.Zero || x.p3 != Vector3.Zero).ToList();
        }

        // Вырезать пересечение 
        private static bool CutIntersection(Vector2 L1p1, Vector2 L1p2, Vector2 L2p1, Vector2 L2p2, out float X, out float Y)
        {
            bool K1IsInf = false;
            bool K2IsInf = false;
            bool K1IsZero = false;
            bool K2IsZero = false;
            float K1 = (L1p1.Y - L1p2.Y) / (L1p1.X - L1p2.X);
            float B1 = L1p1.Y - K1 * L1p1.X;
            if (L1p1.Y == L1p2.Y) K1IsZero = true;
            if (L1p1.X == L1p2.X) K1IsInf = true;
            float K2 = (L2p1.Y - L2p2.Y) / (L2p1.X - L2p2.X);
            float B2 = L2p1.Y - K2 * L2p1.X;
            if (L2p1.X == L2p2.X) K2IsInf = true;
            if (L2p1.Y == L2p2.Y) K2IsZero = true;

            X = 0;
            Y = 0;
            if (K1 == K2)
            {
                //Console.WriteLine("Они параллельны.");
                return false;
            }
            else
            {
                if (K1IsInf)
                {
                    if (K2IsInf)
                    {
                        //Оба вертикальные
                        if (L1p1.X == L2p1.X)
                        {
                            //Console.WriteLine("Они друг в друге.");
                            X = L1p1.X;
                            Y = L1p1.Y;
                        }
                        else
                        {
                            //Console.WriteLine("Они вертикальные и не пересекаются.");
                            //return false;
                        }
                    }
                    else
                    {
                        X = L1p1.X;
                        Y = K2 * X + B2;
                    }
                }
                else
                {
                    if (K2IsInf)
                    {
                        X = L2p1.X;
                        Y = K1 * X + B1;
                    }
                    else
                    {
                        X = (B2 - B1) / (K1 - K2);
                        Y = K1 * X + B1;
                    }
                }

                if (X > Math.Max(L1p1.X, L1p2.X) + 0.05) return false;
                if (X > Math.Max(L2p1.X, L2p2.X) + 0.05) return false;
                if (X < Math.Min(L1p1.X, L1p2.X) - 0.05) return false;
                if (X < Math.Min(L2p1.X, L2p2.X) - 0.05) return false;
                if (Y > Math.Max(L1p1.Y, L1p2.Y) + 0.05) return false;
                if (Y > Math.Max(L2p1.Y, L2p2.Y) + 0.05) return false;
                if (Y < Math.Min(L1p1.Y, L1p2.Y) - 0.05) return false;
                if (Y < Math.Min(L2p1.Y, L2p2.Y) - 0.05) return false;

                return true;
            }
        }

        // Создать сложную фигуру
        public static Triangle[] CreateComplexFigure(Vector2[] PointsIN, float Zin, Vector2[] PointsOUT, float Zout)
        {

            /*
             * Тут был смысл в том что .....
             * короче щас картинка разъясню.
               :..............:.  :......::......:..^:.....::.....:^ .^:::::.::.:::::^.  .!~~!!!77??77777~     
               .^              ::  ^    ..:.:.    .:.:..  .:^:.. ...^..^..  .::::. ...^.  :??7!!!!~7JYYYYJ!     
               .^              ::  ^ ..:.    ..:. .:.: .::. .  :::. ^..:..::.    .::..:.  :!?J!:.    :!JY!~     
               .^              ::  ^ ^     .    .^.:.::^....::....~.^..^::          ^:^.  :~!:          7?J     
               .^              ::  ^ ::    ^    ^..:.: ^   :^^:  .: :..:.^         .^ :.  :^^~         :J5Y     
               .^              ::  ^  ^.       :: .:.: .^.:.  ::.^  :..: :^        ~. :.  :^:~^        J5PY     
               .^              ::  ^  .:.......:  .:.: .^^.....:^:. :..: .^:......:^. :.  :^:~7^^:::::!PGPY     
                ^..............:.  ^....:::::::...::.^:::.:::::.::.:^..^:::::::::::^::^.  :~~!77777!!!~?5Y?     
                        ..                                                                ^YJJJJJJJJJJJ??J?     
                      .:..:.                                                              ~GP55YYYYYYYYYYYJ     
                   .:..    .::.                                                           ~PPPP5YYYYYYYYYYJ     
                  ::          ^.                                                          ~PPPPPP55YYYYYYYJ     
                   ^         .:                                                           ~PPPPPPPP55YYYYYJ     
                   .^        ^                                                            ~PPPPPPPPPP55YYYJ     
                    ::......:.                                                            ~GPPPPPPPPPPPP55Y     
                                                                                          ^JJJJJJJJJJJJJJJ7     
                                                                                                  ::            
                                                                                               :!J?^^^:.        
                                                                                            :!?YYJ^:::^^^:      
                                                                                            !YJJJ~::::^~77      
                                                                                            .JYY7::^~!7??.      
                                                                                             ^YJ^^!7??7?~       
                                                                                              !7!777777!        
                                                                                               ........ 
            * О как ну кто понял тот понял
            * А для остальных поясьню 
            * 
            * Надо найти центра фигур и совместить их
            * После проести лучи от центров до углов фигуры
            * далее полученые отрезки триангулировать
             */

            float R = 0;
            Vector2 CenterIN = Vector2.Zero;
            Vector2 CenterOUT = Vector2.Zero;

            //Внутреняя функция Пересечение внутреннего
            Vector2 IntersectInner(int n)//, ref int WhereGot)//Выдает точку пересечения [центр - точка внут. фигуры - ТОЧКА НА ВНЕШ. ЛИНИИ]
            {
                for (int i = 0; i < PointsOUT.Length; i++)//Так как мы не знаем, с какой линией мы пересечемся
                {
                    if (CutIntersection(
                        CenterIN, PointsIN[n] + Vector2.Normalize(PointsIN[n] - CenterIN) * R,
                        PointsOUT[i], PointsOUT[(i < PointsOUT.Length - 1) ? (i + 1) : 0],
                        out float x, out float y
                        ))
                    {
                        return new Vector2(x, y);
                    }
                }
                return Vector2.Zero;
            }

            //Внутреняя функция Пересекать внешний
            Vector2 IntersectOuter(int n)//, ref int WhereGot)//Выдает точку пересечения [центр - точка внут. фигуры - ТОЧКА НА ВНЕШ. ЛИНИИ]
            {
                for (int i = 0; i < PointsIN.Length; i++)//Так как мы не знаем, с какой линией мы пересечемся
                {
                    if (CutIntersection(CenterIN,
                        PointsOUT[n],
                        PointsIN[i],
                        PointsIN[(i < PointsIN.Length - 1) ? (i + 1) : 0],
                        out float x, out float y))
                    {
                        return new Vector2(x, y);
                    }
                }
                return Vector2.Zero;
            }

            float AngleOfReference(Vector2 v) => NormalizeAngle((float)(Math.Atan2(v.Y, v.X) / Math.PI * 180));
            float AngleOfVectors(Vector2 first, Vector2 second) => NormalizeAngle(AngleOfReference(first) - AngleOfReference(second));

            // Нормализация угла, для мнимого прохода по лучам
            float NormalizeAngle(float angle)
            {
                bool CheckBottom(float a) => a >= 0;
                bool CheckTop(float a) => a < 360;

                float turn = CheckBottom(angle) ? -360 : 360;
                while (!(CheckBottom(angle) && CheckTop(angle))) angle += turn;
                return angle;
            }
            Vector3 v3(Vector2 v, float Z) => new Vector3(v.X, v.Y, Z);

            List<(Vector2, Vector2, float)> Lines = new List<(Vector2, Vector2, float)>();
            // Выводим их. + Находим собственно центр.
            for (int i = 0; i < PointsIN.Length; i++) CenterIN += PointsIN[i];
            for (int i = 0; i < PointsOUT.Length; i++) CenterOUT += PointsOUT[i];
            CenterIN /= PointsIN.Length;
            CenterOUT /= PointsOUT.Length;
            Vector2 DIFFER = CenterIN - CenterOUT;

            // Находим радиус описанной окружности (нужен, чтоб гарантировать, что отрезок будет норм длины)
            for (int i = 0; i < PointsOUT.Length; i++)
            {
                PointsOUT[i] += DIFFER;
                float dist = Vector2.Distance(CenterIN, PointsOUT[i]);
                if (dist > R) R = dist;
            }
            Vector2 VEC0 = Vector2.UnitX;
            for (int i = 0; i < PointsIN.Length; i++)
            {
                Lines.Add((IntersectInner(i), PointsIN[i], AngleOfVectors(PointsIN[i] - CenterIN, VEC0)));
            }
            for (int i = 0; i < PointsOUT.Length; i++)
            {
                Lines.Add((PointsOUT[i], IntersectOuter(i), AngleOfVectors(PointsOUT[i] - CenterIN, VEC0)));
            }
            (Vector2, Vector2, float)[] arr = Lines.Distinct().ToArray();
            Array.Sort(arr, (a, b) => a.Item3.CompareTo(b.Item3));
            int p = arr.Length;
            Triangle[] TR1 = TriangulateFigure(PointsIN, Zout).Distinct().Where(x => (x.p1 != Vector3.Zero || x.p2 != Vector3.Zero || x.p3 != Vector3.Zero)
            && !Triangulation.BelongingToAStraightLine(x)).ToArray();
            Triangle[] TR2 = TriangulateFigure(PointsOUT, Zin).Distinct().Where(x => (x.p1 != Vector3.Zero || x.p2 != Vector3.Zero || x.p3 != Vector3.Zero)
            && !Triangulation.BelongingToAStraightLine(x)).ToArray();
            Triangle[] trs = new Triangle[p * 2 + TR1.Length + TR2.Length];
            Array.Copy(TR1, 0, trs, p * 2, TR1.Length);
            Array.Copy(TR2, 0, trs, p * 2 + TR1.Length, TR2.Length);

            for (int i = 0; i < p; i++)
            {
                trs[i * 2] = new Triangle(
                    v3(arr[i].Item1, Zin),
                    v3(arr[i].Item2, Zout),
                    v3(arr[i + 1 < p ? i + 1 : 0].Item2, Zout)
                    );
                trs[i * 2 + 1] = new Triangle(
                    v3(arr[i].Item1, Zin),
                    v3(arr[i + 1 < p ? i + 1 : 0].Item1, Zin),
                    v3(arr[i + 1 < p ? i + 1 : 0].Item2, Zout)
                    );
            }

            return trs;
        }

        static Triangle[] TriangulateFigure(Vector2[] points, float Z) //триангуляция
        {
            /*
             * Это треагуляция до которой допрет школьник
             * Но мы не школьники и не доперли
             * Так что взяли с сайта https://grafika.me/node/457
             * 
             * Это на какмом дели просто работает
             */

            Triangle[] triangles = new Triangle[points.Length];
            bool[] taken = new bool[points.Length];
            int findNextNotTaken(int startPos) //найти следущую нерассмотренную вершину
            {
                startPos %= points.Length;
                if (!taken[startPos])
                    return startPos;

                int i = (startPos + 1) % points.Length;
                while (i != startPos)
                {
                    if (!taken[i])
                        return i;
                    i = (i + 1) % points.Length;
                }

                return -1;
            }
            bool isLeft(Vector2 a, Vector2 b, Vector2 c) //левая ли тройка векторов
            {
                float abX = b.X - a.X;
                float abY = b.Y - a.Y;
                float acX = c.X - a.X;
                float acY = c.Y - a.Y;
                return abX * acY - acX * abY < 0;
            }
            bool isPointInside(Vector2 a, Vector2 b, Vector2 c, Vector2 p) //находится ли точка p внутри треугольника abc
            {
                float ab = (a.X - p.X) * (b.Y - a.Y) - (b.X - a.X) * (a.Y - p.Y);
                float bc = (b.X - p.X) * (c.Y - b.Y) - (c.X - b.X) * (b.Y - p.Y);
                float ca = (c.X - p.X) * (a.Y - c.Y) - (a.X - c.X) * (c.Y - p.Y);
                return (ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0);
            }
            bool canBuildTriangle(int AI, int BI, int CI) //false - если внутри есть вершина
            {
                for (int i = 0; i < points.Length; i++) //рассмотрим все вершины многоугольника
                    if (i != AI && i != BI && i != CI) //кроме троих вершин текущего треугольника
                        if (isPointInside(points[AI], points[BI], points[CI], points[i]))
                            return false;
                return true;
            }
            Vector3 v3(Vector2 p) => new Vector3(p.X, p.Y, Z);

            int trainPos = 0; //
            int leftPoints = points.Length; //сколько осталось рассмотреть вершин

            //текущие вершины рассматриваемого треугольника
            int ai = findNextNotTaken(0);
            int bi = findNextNotTaken(ai + 1);
            int ci = findNextNotTaken(bi + 1);

            int count = 0; //количество шагов

            while (leftPoints > 3) //пока не остался один треугольник
            {
                if (isLeft(points[ai], points[bi], points[ci]) && canBuildTriangle(ai, bi, ci)) //если можно построить треугольник
                {
                    triangles[trainPos++] = new Triangle(v3(points[ai]), v3(points[bi]), v3(points[ci])); //новый треугольник
                    taken[bi] = true; //исключаем вершину b
                    leftPoints--;
                    bi = ci;
                    ci = findNextNotTaken(ci + 1); //берем следующую вершину
                }
                else
                { //берем следующие три вершины
                    ai = findNextNotTaken(ai + 1);
                    bi = findNextNotTaken(ai + 1);
                    ci = findNextNotTaken(bi + 1);
                }

                if (count > points.Length * points.Length)
                { //если по какой-либо причине (например, многоугольник задан по часовой стрелке) триангуляцию провести невозможно, выходим
                    triangles = null;
                    break;
                }

                count++;
            }

            if (triangles != null) //если триангуляция была проведена успешно
                triangles[trainPos] = new Triangle(v3(points[ai]), v3(points[bi]), v3(points[ci]));

            return triangles;
        }

    }

    class Triangulation
    {
        // Константа ошибки
        const double Epsilon = 0.01d;

        //Принадлежность к прямой линии
        internal static bool BelongingToAStraightLine(Triangle finalList)
        {
            float ConstX = (finalList.p3.X - finalList.p1.X) / (finalList.p2.X - finalList.p1.X);
            float ConstY = (finalList.p3.Y - finalList.p1.Y) / (finalList.p2.Y - finalList.p1.Y);
            float ConstZ = (finalList.p3.Z - finalList.p1.Z) / (finalList.p2.Z - finalList.p1.Z);

            if (float.IsNaN(ConstX)) ConstX = (float.IsNaN(ConstY)) ? ConstZ : ConstY;
            if (float.IsNaN(ConstY)) ConstY = (float.IsNaN(ConstZ)) ? ConstX : ConstZ;
            if (float.IsNaN(ConstZ)) ConstZ = (float.IsNaN(ConstX)) ? ConstY : ConstX;


            if ((Math.Abs(ConstX - ConstY) < Epsilon) && (Math.Abs(ConstX - ConstZ) < Epsilon))
            {
                return true;
            }
            return false;
        }


        internal static List<Triangle> Triangulate(in List<Vector3> points)
        {
            List<Triangle> finalList = new List<Triangle>();

            // Если на входе 3 точки => триангуляция не нужна
            if (points.Count == 3)
            {
                Triangle triangle = new Triangle(points[0], points[1], points[2]);
                finalList.Add(triangle);
                return finalList;
            }

            // Если на входе 4 точки => трангуляция простая с разбиением на 3 триугольника
            if (points.Count == 4)
            {
                Triangle triangle1Old = new Triangle(points[1], points[3], points[0]);
                Triangle triangle2Old = new Triangle(points[2], points[3], points[0]);
                Triangle triangle3Old = new Triangle(points[1], points[3], points[2]);
                finalList.Add(triangle1Old);
                finalList.Add(triangle2Old);
                finalList.Add(triangle3Old);

                Clear();

                return finalList;

                /*
                                                                                                                               
                                                   @3                                                         
                                                  @@3@                                                        
                                                  @ 3@                                                        
                                                 @  3 @                                                       
                                                @@  3 @@                                                      
                                                @   3  @                                                      
                                               @    3   @                                                     
                                               @    3   @                                                     
                                              @     3    @                                                    
                                             @      3     @                                                   
                                            @@      3     @@                                                  
                                            @       33     @                                                  
                                           @        33      @                                                 
                                          @@        33      @@                                                
                                          @         33       @                                                
                                         @          33        @                                               
                                         @          33        @                                               
                                        @           33         @                                              
                                       @            33          @                                             
                                       @            33          @@                                            
                                      @             33           @                                            
                                     @              33            @                                           
                                    @               33            @@                                          
                                    @               33             @                                          
                                   @                33              @                                         
                                  @@                33              @                                         
                                  @                 33               @                                        
                                 @                  33                @                                       
                                 @                  33                @@                                      
                                @                   333                @                                      
                               @                3333333333              @                                     
                              @                333333333333             @@                                    
                              @               33333333333333             @                                    
                             @                33333333333333              @                                   
                            @                 33333333333333              @                                   
                            @                 33333333333333               @                                  
                           @                  33333333333333                @                                 
                           @                33333333333333333               @@                                
                          @              333     33333333    33              @                                
                         @             333                     333            @                               
                        @@           333                         333          @                               
                        @          33                              333         @                              
                       @        333                                  333        @                             
                      @       333                                       33      @                             
                      @     333                                           33     @                            
                     @    333                                               33    @                           
                    @@ 333                                                    333 @                           
                    @333                                                        333@                          
                   333                             @@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@333                         
                  33@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@                                                           
                                                                                        
                 
                 */
            }


            // Если 5 точек => сложный

            Triangle triangle1 = new Triangle(points[1], points[3], points[0]);
            Triangle triangle2 = new Triangle(points[2], points[3], points[0]);
            Triangle triangle3 = new Triangle(points[1], points[3], points[2]);
            finalList.Add(triangle1);
            finalList.Add(triangle2);
            finalList.Add(triangle3);


            Clear();

            for (int i = 0; i < finalList.Count; i++)
            {
                if (TrianglesIntersection.PointInTriangleNEW(finalList[i], points[4]))
                {
                    Triangle triangle11 = new Triangle(finalList[i].p1, finalList[i].p2, points[4]);
                    Triangle triangle21 = new Triangle(finalList[i].p3, finalList[i].p2, points[4]);
                    Triangle triangle31 = new Triangle(finalList[i].p1, finalList[i].p3, points[4]);
                    finalList.Add(triangle11);
                    finalList.Add(triangle21);
                    finalList.Add(triangle31);

                    finalList.RemoveAt(i);
                    //триангулируем
                    break;
                }

                // Без картинки, так как это сложно (попытаюсь обяснить)
                /*
                 * Мы создаем из первых 3 точек триугольник. так как они являются точками изночального триугольника.
                 * Дальне 4 точку (зная что она внутри точек (1-2-3) образованных в треугольник) вставляем внуть.
                 * создаем 3 треугольника как с 4 точками
                 * После надо удалить вырожденые треугольники
                 * Далее проверяем точку 5 внутри какого из 3
                 * Если нашли процес анологичен как для 4, только для этого треугольника
                 */
            }
            Clear();


            void Clear() // Вырожденные треугольники (отчистка)
            {
                for (int i = 0; i < finalList.Count; i++)
                {
                    if (BelongingToAStraightLine(finalList[i]))
                    {
                        finalList.RemoveAt(i);
                        i--;
                        // Да дешево, но эфективно
                    }
                }
            }

            return finalList;

            /*
             * Это штука работает безупречно.
             * Однако если есть подозрени, то пересмотри пересечения труегольников
             * Это другой класс
             * Если все же здесь, то BelongingToAStraightLine.
             * Это хрень нас никогда не подводила, но она супер старая и может устареть где то.
             * В пересечении треугольников этот метод другой
             */
        }
    }
    class TrianglesIntersection
    {

        // Расчет нормали к треугольнику
        internal static Vector3 CalculateNormals(Vector3 a, Vector3 b, Vector3 c)
        {
            float wrki;
            Vector3 v1, v2;

            v1.X = a.X - b.X;
            v1.Y = a.Y - b.Y;
            v1.Z = a.Z - b.Z;

            v2.X = b.X - c.X;
            v2.Y = b.Y - c.Y;
            v2.Z = b.Z - c.Z;

            wrki = (float)Math.Sqrt(Math.Pow(v1.Y * v2.Z - v1.Z * v2.Y, 2) + Math.Pow(v1.Z * v2.X - v1.X * v2.Z, 2) + Math.Pow(v1.X * v2.Y - v1.Y * v2.X, 2));
            Vector3 n = new Vector3();
            n.X = (v1.Y * v2.Z - v1.Z * v2.Y) / wrki;
            n.Y = (v1.Z * v2.X - v1.X * v2.Z) / wrki;
            n.Z = (v1.X * v2.Y - v1.Y * v2.X) / wrki;

            return n;
        }

        // Пересечения прямой и плоскости
        internal static bool linePlaneIntersection(Vector3 rayEnd, Vector3 rayOrigin, Vector3 normal, Vector3 coord, out Vector3 contact)
        {

            contact = Vector3.Zero;
            // get d value
            float d = Vector3.Dot(normal, coord);
            Vector3 ray = rayEnd - rayOrigin;

            if (Vector3.Dot(normal, ray) == 0)
            {
                return false;
            }

            float x = (d - Vector3.Dot(normal, rayOrigin)) / Vector3.Dot(normal, ray);
            contact = rayOrigin + ray * x;
            contact.X = (float)Math.Round(contact.X, 5);
            contact.Y = (float)Math.Round(contact.Y, 5);
            contact.Z = (float)Math.Round(contact.Z, 5);
            return (
                contact.X <= Math.Max(rayOrigin.X, rayEnd.X) && contact.X >= Math.Min(rayOrigin.X, rayEnd.X)
             && contact.Y <= Math.Max(rayOrigin.Y, rayEnd.Y) && contact.Y >= Math.Min(rayOrigin.Y, rayEnd.Y)
             && contact.Z <= Math.Max(rayOrigin.Z, rayEnd.Z) && contact.Z >= Math.Min(rayOrigin.Z, rayEnd.Z)
            );
        }

        //Поиск в треугольнике новой точки
        internal static bool PointInTriangleNEW(Triangle triangle, Vector3 P)
        {
            Vector3 A = triangle.p1, B = triangle.p2, C = triangle.p3;
            if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
            {
                Vector3 vc1 = Vector3.Cross(Vector3.Subtract(A, B), Vector3.Subtract(A, C));
                if (Math.Abs(Vector3.Dot(Vector3.Subtract(A, P), vc1)) <= 0.1f)
                    return true;
            }

            return false;
        }

        //Поиск точки внутри труегольника через вектора
        private static bool SameSide(Vector3 p1, Vector3 p2, Vector3 A, Vector3 B)
        {
            Vector3 cp1 = Vector3.Cross(Vector3.Subtract(B, A), Vector3.Subtract(p1, A));
            Vector3 cp2 = Vector3.Cross(Vector3.Subtract(B, A), Vector3.Subtract(p2, A));
            if (Vector3.Dot(cp1, cp2) >= 0) return true;
            return false;

            /*
             * 
                                                             333                 3                                        
                                                             3333                33                                       
                                                             33333               333                                      
                                                             3 33               33333                                     
                                                               @3               33333                                     
                                                              @@33                33                                      
                                                              @  33               33                                      
                                                             @    3                3                                      
                                                            @@    33               33                                     
                                                            @      33              33                                     
                                                           @       33               3                                     
                                                           @        33              3                                     
                                                          @          33             33                                    
                                                         @           33             33                                    
                                                        @@            33             3                                    
                                                        @              3             3                                    
                                                       @               33            33                                   
                                                      @@                33           33                                   
                                                      @                  3            3                                   
                                                     @                   33           33                                  
                                333                  @                    33          33                                  
                                3333333             @                      33         33                                  
                                 3333              @                       33          3                                  
                                  33 33            @                        33         33                                 
                                   3   33         @                          3         33                                 
                                         33      @                           33        33                                 
                                           33   @                             33        33                                
                                             33 @                              3        33                                
                                               33                              33       33                                
                                              @@ 33                             33      33                                
                                              @    33                           33       33                               
                                             @       33                          33      33                               
                                             @        333                         33     33                               
                                            @           333                       33      3                               
                                           @               3333333                 33     33                              
                                          @                3333  3                  33    33                              
                                          @                33 3333                  33    33                              
                                         @                   333333                  33    3                              
                                        @                         333                 3    33                             
                                        @                           333               33   33                             
                                       @                              333              33  33                             
                                       @                                333             3@  3                             
                                      @                                   333           33  33                            
                                     @                                      333          33 33                            
                                    @@                                        333         3 33                            
                                    @                                           33        33 3                            
                                   @                                              333      3333                           
                                  @                                                 333     333                           
                                  @                                                   33    333                           
                                 @                                                      333  33                           
                                @@                                                        333 33                          
                      3333      @                                                           3333                          
                333333         @                                                              333                         
                333333333333333333333333333333333333333333333333333333333333333333333333333333333                         
                       333                                                                                                                                                                     
             */
        }

        //Получить точку треугольника
        static bool GetTrianglePoint(Triangle Tri, Vector3 D, Vector3 E, out Vector3 res)
        {
            /*
             * https://gamedev.ru/code/forum/?id=41118
             * Тут норм разьяснение есть
             */

            /*
             * Раньше мы использовали только Intersection
             * Но теперь она устарели и эта основныя
             * Так что все коменты и лавры переходят сюда
             * 
             * P.S. Не забывай про народное голосование "+" или "-"
             */

            res = Vector3.Zero;
            Vector3 A = Tri.p1;
            Vector3 B = Tri.p2;
            Vector3 C = Tri.p3;
            Vector3 p1 = D - A;
            Vector3 p2 = D - E;
            Vector3 p3 = B - A;
            Vector3 p4 = C - A;
            float delta = Vector3.Dot(p2, Vector3.Cross(p3, p4));
            if (delta == 0) return false;//delta = 0.01f;
            float t = Vector3.Dot(p1, Vector3.Cross(p3, p4)) / delta;
            if (t <= 0 || t >= 1) return false;
            float u = Vector3.Dot(p2, Vector3.Cross(p1, p4)) / delta;
            if (u <= 0 || u >= 1) return false;
            float v = Vector3.Dot(p2, Vector3.Cross(p3, p1)) / delta;
            if (v <= 0 || v >= 1) return false;
            if (u + v >= 1) return false;
            res = A + p3 * u + p4 * v;
            res.X = (float)Math.Round(res.X, 3);//тут хорошо работает 3
            res.Y = (float)Math.Round(res.Y, 3);
            res.Z = (float)Math.Round(res.Z, 3);
            return true;
        }

        //Проверка точки внутри
        private static Vector3[] PointCheckInside(Triangle triangle1, Triangle triangle2)
        {
            List<Vector3> pointTriangle = new List<Vector3>();

            if (GetTrianglePoint(triangle1, triangle2.p1, triangle2.p2, out Vector3 V1))
            {
                pointTriangle.Add(V1);
            }
            if (GetTrianglePoint(triangle1, triangle2.p1, triangle2.p3, out Vector3 V2))
            {
                pointTriangle.Add(V2);
            }
            if (GetTrianglePoint(triangle1, triangle2.p2, triangle2.p3, out Vector3 V3))
            {
                pointTriangle.Add(V3);
            }

            return pointTriangle.ToArray();
            /*
             * Вроде норм работала, покрайне мери когда я тестил ошибки были не сдесь
             */
        }

        //Не помнью что это такое
        internal static bool Intersection(Triangle triangle1, Triangle triangle2, out List<Vector3> res)
        {
            res = new List<Vector3>();
            Vector3[] PointTriangle1 = PointCheckInside(triangle1, triangle2).ToArray();
            Vector3[] PointTriangle2 = PointCheckInside(triangle2, triangle1).ToArray();

            /*
             * Смотри тут выдаются точки
             * Они вроде как правильные, ОДНАКО
             * Такое большое ОДНАКО
             * надо их правильно интропретировать 
             * И тут проблемы для примера приведу тестовые триугольнике которые ты должен проыверить
             */

            /*
             * 
             * 
             * 1 0 0
             * 0 0 0
             * 0 0 1
             * 
             * 0 0 0
             * 0 1 0
             * 0 0 1
             * 
             * Нету
             * -----------
             * 
             * 1 0 0
             * 0 0 0
             * 0 0 0,5
             * 
             * 0 0 0
             * 0 1 0
             * 0 0 1
             * 
             * 0 0 0,5
             * -----------
             * 
             * 1 0 0
             * 0 0 0,25
             * 0 0 0,75
             * 
             * 0 0 0
             * 0 1 0
             * 0 0 1
             * 
             * 0 0 0,25
             * 0 0 0,75
             * -----------
             * 
             * 1 0 1
             * 1 0 0
             * 0 0 0,5
             * 
             * 0 1 1
             * 0 1 0
             * 0 0 0,5
             * 
             * Нету
             * -----------
             * 
             * 1 0,5 1
             * 1 0,5 1
             * 0 0,5 0,5
             * 
             * 0 1 1
             * 0 1 0
             * 0 0 0,5
             * 
             * 0 0,5 0,5
             * -----------
             * 
             * 0 1 0
             * 0 1 1
             * 0 0 0,5
             * 
             * 1 0,75 0,5
             * 0 0,75 0,25
             * 0 0,75 0,75
             * 
             * 0 0,75 0,25
             * 0 0,75 0,75
             * -----------
             * 
             * Так если ты досуда дошол
             * Иди своей дорогой сталкер
             */

            PointTriangle1 = PointTriangle1.Concat(PointTriangle2).ToArray();
            res.Add(triangle2.p1);
            res.Add(triangle2.p2);
            res.Add(triangle2.p3);
            for (int i = 0; i < PointTriangle1.Length; i++)
            {
                bool b = true;
                for (int j = 0; j < res.Count; j++)
                {
                    var n = 0;
                    if (Math.Abs(res[j].X - PointTriangle1[i].X) < 0.1) n++;
                    if (Math.Abs(res[j].Y - PointTriangle1[i].Y) < 0.1) n++;
                    if (Math.Abs(res[j].Z - PointTriangle1[i].Z) < 0.1) n++;//тут хорошо работет 0.05
                    if (n == 3)
                    {
                        b = false;
                        break;
                    }
                }
                if (b)
                    res.Add(PointTriangle1[i]);
            }
            return res.Count > 3;

            /*
             * Если тебе сказали переделывать, так как ошибки то смотри тут
             * Эта хрень весьма вероятно сломалось
             * 
             * Так что предыстория
             * 
             * Смотри путрик это злой драко
             * Который не одного славного рыцаря сжег
             * Как физически так и морально
             * Это как неприступная блятская крепость
             * Если ты не знаешь что за ошибка 1/3 то скоро познаешь
             * А если познал то поставь "+" если смог и +1 в "-" если этот дракон забрал еще одного бойца
             * 
             * +: 
             * -: 5
             */

        }

    }

    //Структура треугольника
    public struct Triangle
    {
        public Vector3 p1, p2, p3;


        internal Triangle(Vector3 P1, Vector3 P2, Vector3 P3)
        {
            p1 = P1;
            p2 = P2;
            p3 = P3;
            /*
             *Ели будешь это тюнить, то такой подход дэбильный и переделай эту структуру
             *Она часто даставляет дискомфорт
             */
        }

        // Погрешность вычислений 
        const double Epsilon = 0.000001d;
        /*
         * Необходимость так как 1/3 это мать его 0,333!, а математика говорит хрен тебе
         */

        // Алгоритм Молера Трумена
        internal Vector3? InterPoint(Vector3 point, Vector3 direction)
        {
            Vector3 P = Vector3.Cross(direction, p3 - p1);
            float det = Vector3.Dot(p2 - p1, P);

            if (det > -Epsilon && det < Epsilon)
            {
                return null;
            }

            double invDet = 1d / det;
            double u = Vector3.Dot(point - p1, P) * invDet;
            if (u < 0 || u > 1)
            {
                return null;
            }

            Vector3 qvec = Vector3.Cross(point - p1, p2 - p1);
            double v = Vector3.Dot(direction, qvec) * invDet;
            if (v < 0 || u + v > 1)
            {
                return null;
            }

            double t = Vector3.Dot(p3 - p1, qvec) * invDet;
            if (t < 0 || t > 1)
            {
                return null;
            }

            return point + direction * (float)t;
        }

        // Нахождение точки внутри треугольника
        internal bool IsInside(List<Triangle> polygon)
        {
            const double disp = 100.0;
            Random random = new Random();
            int count = 0;
            double min = 10000.0;

            Vector3 center = new Vector3(
                (p1.X + p2.X + p3.X) / 3,
                (p1.Y + p2.Y + p3.Y) / 3,
                (p1.Z + p2.Z + p3.Z) / 3);

            Vector3 point = new Vector3(
                (float)(random.NextDouble() * disp + min),
                (float)(random.NextDouble() * disp + min),
                (float)(random.NextDouble() * disp + min));
            foreach (Triangle side in polygon)
            {
                if (side.InterPoint(center, point) != null) count++;
            }

            return count % 2 != 0;
        }

    }

}
