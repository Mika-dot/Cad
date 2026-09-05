# ModernV2 — managed BSP CSG

![Место ModernV2 в ветке](../docs/images/v2-geometry.svg)

Самодостаточный .NET 8 console project без внешнего геометрического пакета. Он нужен как читаемая реализация BSP-булевых операций и не заявляется как промышленное CAD-ядро.

## Запуск

Из корня ветки:

```bash
dotnet build ModernV2/DCad.MeshKernel.csproj -c Release
dotnet run --project ModernV2/DCad.MeshKernel.csproj -c Release -- --self-test
dotnet run --project ModernV2/DCad.MeshKernel.csproj -c Release -- output.stl --obj
```

Без пути программа пишет `v2-modern-demo.stl`. `--ascii` выбирает ASCII STL, `--obj` дополнительно создаёт OBJ.

## Файлы

| Файл | Содержимое |
|---|---|
| `MeshKernel.cs` | BSP types, box/cylinder, CSG, анализ, ASCII STL |
| `MeshUtilities.cs` | validation, transforms, sphere, picking, binary STL, OBJ |
| `Program.cs` | встроенная demo-модель и CLI |
| `SelfTests.cs` | проверки cube, transforms, sphere, picking, subtraction и STL layout |

## Честные границы

- fixed epsilon `1e-7` не зависит от масштаба;
- splitter не содержит exact/adaptive predicates;
- topology определяется квантованием координат;
- BSP может создавать длинные и плохо обусловленные polygons;
- нет BVH, coplanar cleanup, remeshing и тестов на большой набор STL;
- результат следует валидировать перед использованием вне эксперимента.

Каталог `Production/` исключён из compilation glob этого проекта и собирается отдельным solution.
