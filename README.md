# DCad — Function Basket / Geometry Lab

Изначально эта ветка была корзиной отдельных экспериментов: математика, пересечение двух треугольников, проверка точки внутри тела. Эти исходники сохранены без удаления как исторические prototypes.

Теперь основная роль ветки — **регрессионная лаборатория геометрии DCad**.

## ModernGeometryLab

`ModernGeometryLab/` содержит .NET 8 test suite на том же double-precision geometry API и robust CSG backend, которые используются в `V2-Experiment/ModernV2/Production` и `Unified-CAD`.

Вместо тестов вида «на моём примере вроде работает» здесь проверяются свойства, которые обязаны сохраняться для целого класса моделей: площадь после triangulation, boolean volume identities, manifold topology, повторяемый point-in-solid и отказ на некорректных polygons.

```powershell
dotnet test ModernGeometryLab/tests/GeometryLab.Tests/GeometryLab.Tests.csproj -c Release
```

Все новые найденные геометрические баги из V1/V2/VoxelCAD следует сначала фиксировать здесь как regression case, а затем исправлять в общем kernel.
