# DCad
 Сделан двумя студентами:
* [Пересторонин Аким](https://github.com/Mika-dot)
* [Трушин Владислав](https://github.com/TrushinVlad)

##  Добавление произвольной фигуры.
### Это кад полигональный.

Для добавления произвольной фигуры необходимо вызвать функцию генерации этой фигуры.

Класс CADMath, функции XOR и OR. Структура полизна представлена CreateComplexFigure.

Способ задать полигоны для фигуры.
```C#
Figures[i] = new List<Triangle>();
var link = Figures[i];

link.Add(new Triangle(
                      new Vector3(tr.Vertex1.X, tr.Vertex1.Y, tr.Vertex1.Z),
                      new Vector3(tr.Vertex2.X, tr.Vertex2.Y, tr.Vertex2.Z),
                      new Vector3(tr.Vertex3.X, tr.Vertex3.Y, tr.Vertex3.Z)
                     ));
```

Для того что бы сложить необходимо использовать следующий код.
```C#
Figures[i] = new List<Triangle>();
Figures[ID1] = CADMath.OR(Figures[ID1], Figures[ID2]);
```

Для того что бы вычисть необходимо использовать следующий код.
```C#
Figures[i] = new List<Triangle>();
Figures[ID1] = CADMath.XOR(Figures[ID1], Figures[ID2]);
```

Пример работы када целеком вместе с интерфейсом в качестве демонстрации.
<p align="center">
  <img src="https://github.com/Mika-dot/Cad/blob/V2-Experiment/media/xor.png?raw=true" alt="ConsoleWriteImage"/>
</p>
