# Сфера минус цилиндр — следующий шаг

Тот же **один `Program.cs`** и тот же класс `FunctionPixels`. Просто добавили в `Main` вторую формулу: из сферы радиусом 3 мм вырезаем бесконечный цилиндр радиусом 1 мм вдоль оси Z.

```csharp
Func<double, double, double, double> sphere =
    (x, y, z) => 9 - x * x - y * y - z * z;

Func<double, double, double, double> cylinder =
    (x, y, z) => 1 - x * x - y * y;

Func<double, double, double, double> sphereMinusCylinder = (x, y, z) =>
{
    double sphereValue = sphere(x, y, z);
    double cylinderValue = cylinder(x, y, z);
    double outsideCylinder = -cylinderValue;
    return sphereValue + outsideCylinder -
           Math.Sqrt(sphereValue * sphereValue +
                     outsideCylinder * outsideCylinder);
};
```

Это **R-разность**: положительное поле остаётся только внутри сферы и вне цилиндра. Не обычное вычитание значений двух функций — иначе отверстие получится неверным.

## Запуск (Windows Terminal / PowerShell, нужен .NET 8 SDK)

```powershell
git clone https://github.com/Mika-dot/Cad.git
cd Cad
git switch assembly/sphere-from-zero
dotnet run
```

При уже имеющемся клоне:

```powershell
git fetch origin
git switch assembly/sphere-from-zero
git pull
dotnet run
```

Для каждого слоя Z программа печатает **два сечения рядом**, чтобы было видно отверстие: `##` — внутри, `oo` — поверхность, `..` — снаружи. Затем цветом выводит по **четыре карты M0–M3** для исходной сферы и для сферы с отверстием.

В `output/` сохраняются **56 BMP**: 7 Z-слоёв × 2 фигуры × 4 М-образа. Имена: `sphere_Z03_M0.bmp` и `sphere_minus_cylinder_Z03_M0.bmp` для центрального среза Z=0. Каждый файл — 7×7 пикселей, в Paint используйте увеличение.

**Математическая оговорка.** Эти четыре коэффициента — локальная модель **двумерного XY-среза** при фиксированном Z, а не полное пятиканальное 3D-представление. Производные вычисляются численно; в точках пересечения нулевых поверхностей сферы и цилиндра производная R-функции может быть не определена. Отображаемые цвета — RGB24-кодировка коэффициентов, а не материал детали.
