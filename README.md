# DCad
 Сделан двумя студентами:
* [Пересторонин Аким](https://github.com/Mika-dot)
* [Трушин Владислав](https://github.com/TrushinVlad)

##  Добавление произвольной фигуры.

Для добавления произвольной фигуры необходимо вызвать функцию генерации этой фигуры.

```C#
            AdditionWorld(
                new int[] { 0, 5, 5, 0 }, // контур по Х
                new int[] { 0, 0, 5, 5 }, // контур по У
                10, // экструзия
                new int[] { 0, 0, 0 }, // Кручение вокселей
                new int[] { 2, 2, 2 }, // Перемещение
                0, // тип экструзии

                ref worldX, ref worldY, ref worldZ
            );
```

Первый массив содержит точки по часовой стрелки оси X.
Второй массив содержит точки соответствующие массиву X, однако по кординатной прямой Y.

Далее параметр экструзия и он выдавливает воксели по контуру определенного до этого.

Следующий массив это массив углов поворота по трем осям фигуры относительно нулевых координат контура.
Этот пораметр предназначен для вращения воксельного тела.

Массив перемещения служит для перемещения тела фигуры в пространстве и задается так же тремя координатоми соответствующих осям.

Тип экструзии обозначает материал фигуры.
В данный момент всего 2 типа.
1. Значения 1 это существования вокселя.
2. Значение 0 это отсутствия вокселя.
Если поставить фигуру 0 и она войдет в фигуру со значением 1, то произойдет онулирования положительной фигуры и тем смым процес буленовского вычитания. (Твердотельное моделирование)

![Пример генерации фигуры](https://github.com/Mika-dot/Cad/blob/V1-Experiment/media/1.PNG)

##  Gcode.

```C#
Gcode("test", ref worldX, ref worldY, ref worldZ); 
```

Передаете параметр названия сохраняемого файла.

![Пример генерации gcode](https://github.com/Mika-dot/Cad/blob/V1-Experiment/media/3.PNG)
![Пример генерации gcode напечатоного](https://github.com/Mika-dot/Cad/blob/V1-Experiment/media/4.JPG)


## Температура

```C#
            temp = TemperatureVoxel(ref worldID, worldX, worldY, worldZ, 
                new int[,] {
                    { 0, 0, 0, -10 },
                    { 10, 10, 10, 10 }
                });
```

Температуру можно задать с помощью функции "TemperatureVoxel". TemperatureVoxel принимает на вход следующие аргументы:
* worldID, worldX, worldY, worldZ - это ссылки на массивы, которые и представляют мир.
* Последним идёт двумерный массив. В нём перечислено, в какую точку какая температура идёт. Первые три целых числа - это координаты в пространстве (необязательно, чтобы в этих координатах был объект), четвёртое число - это сама температура.
На выход выдаётся дробное число. Это технический параметр для отладки.

![Пример температуры](https://github.com/Mika-dot/Cad/blob/V1-Experiment/media/2.PNG)

# Компиляция

Программа компилируется под Windows. Язык программирования C#. Файл .sln - основной файл проекта. Проект можно загрузить в Visual Studio (или любой другой редактор). Загружать дополнительные библиотеки не требуется, они идут вместе с проектом. Проект можно сразу же собрать и начать отлаживать.
Программа основана на Windows Forms и производит рендер объектов с помощью OpenGL. Визуализатор OpenGL (стороннее ПО) был получен и загружен с помощью NuGet.
Программа имеет примитивный интерфейс и состоит из одной формы - на ней расположено само окно для отображения модели, а также кнопки создания, сохранения и загрузки.

## Сохранение
Вся информация сохраняется в файл save.txt, расположенный в корневой директории программы.
На форме есть кнопки сохранения мира, температуры и G-Code. Последовательно нажимая их, можно сгенерировать файл save.txt, содержащий всю необходимую информацию для её последующей загрузки. Программа сохраняет не все воксели, а информацию о выдавливании и вырезании.

## Загрузка
При нажатии на кнопку "Воспроизвести" программа загружает информацию из save.txt и восстанавливает форму объекта и его температуру.

P.S. Это эксперимент по создания воксельного САПР.
