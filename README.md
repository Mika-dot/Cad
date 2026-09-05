# DCad — V1 Experiment / sparse voxel CAD baseline

`V1-Experiment` — первая линия DCad: CAD как дискретный объём. Исторический прототип строил тело через три параллельных массива `worldX/worldY/worldZ`, экструзию 2D-контура, булево вычитание, температурное поле и генерацию G-code.

В обновлении 2026 ветка сохранена как отдельный эксперимент, но идея доведена до более пригодной архитектуры. Старый интерфейс и алгоритмы не удалены: их можно запустить с `--legacy` и напрямую сравнить с новым ядром.

## Что изменено

### Sparse voxel storage

Вместо обязательного использования трёх синхронных массивов добавлен `Modern/SparseVoxelGrid`:

```csharp
var grid = new SparseVoxelGrid();
grid.AddBox(0, 0, 0, 20, 15, 8);
grid.SubtractSphere(10, 7.5, 5, 4);
grid.AddCylinderZ(10, 7.5, 0, 14, 2, material: 2);
```

Пустое пространство не хранится. Ключом является одна координата `VoxelKey`, а значение `VoxelCell` содержит `Material` и произвольное scalar field. Это устраняет главный класс ошибок старой схемы — рассинхронизацию `worldX`, `worldY`, `worldZ`, `worldID`.

### CSG и примитивы

Реализованы:

- `UnionWith`, `Subtract`, `IntersectWith`;
- box add/subtract;
- sphere add/subtract;
- Z-cylinder;
- polygon extrusion add/subtract;
- translate;
- arbitrary scalar field.

2D polygon extrusion проверяет принадлежность центра voxel-cell, а не целочисленной точки на границе, поэтому поведение стабильнее на контуре.

### Поля внутри CAD-модели

Voxel теперь может быть не только `есть/нет`:

```csharp
grid.SetScalar((x, y, z) =>
    (float)(z + 2.0 * Math.Sin(x * 0.4)));
```

На одном backend можно хранить температуру, плотность topology optimization, напряжение, material id и в дальнейшем другие расчётные поля. Это важная точка объединения с веткой `FEM_Voxel`.

### Surface-only rendering

Старый viewer рисовал куб для каждого voxel и отправлял в OpenGL все шесть граней, включая полностью внутренние. Новое ядро отдаёт только `SurfaceFaces()`: если два voxels соседние, общая грань вообще не попадает в renderer.

Это уменьшает число реально рисуемых полигонов без изменения геометрии.

### Новый viewer

По умолчанию приложение запускает `ModernVoxelDemoForm`:

- orbit camera мышью;
- wheel zoom;
- perspective camera;
- depth test и back-face culling;
- координатные оси и reference grid;
- material coloring;
- scalar/temperature heatmap;
- optional wire overlay;
- статистика `voxels / exposed faces`;
- интерактивное добавление и вычитание primitives.

Историческое окно:

```powershell
OpenGL_lesson_CSharp.exe --legacy
```

### G-code baseline

`VoxelGCodePlanner` выдаёт layer-wise serpentine path вместо произвольного прохода по массивам. Это исследовательский baseline, не замена полноценному slicer, но travel-path теперь детерминирован и локален по слоям.

## API

```text
Modern/
├── VoxelEngine.cs
│   ├── VoxelKey
│   ├── VoxelCell
│   ├── SparseVoxelGrid
│   ├── VoxelFace
│   └── VoxelGCodePlanner
└── ModernVoxelDemoForm.cs
```

Пример polygon extrusion:

```csharp
var x = new double[] { 0, 20, 20, 4, 4, 0 };
var y = new double[] { 0, 0, 6, 6, 18, 18 };

grid.ExtrudePolygon(x, y, 0, 8, material: 1);
grid.ExtrudePolygon(
    new double[] { 7, 13, 13, 7 },
    new double[] { 2, 2, 5, 5 },
    0, 8,
    subtract: true);
```

## Зачем сохранять V1 отдельно

Эта ветка полезна как простая reference implementation volume CAD. В ней удобно проверять алгоритм до переноса в более тяжёлое ядро:

- voxel CSG;
- morphology;
- scalar/material fields;
- manufacturing compensation;
- scan-to-volume;
- topology field post-processing;
- G-code/slicing experiments.

Она не должна оставаться финальным storage backend: `Dictionary/HashSet` удобен для правильности алгоритма, но следующий уровень — sparse bricks 8³/16³, затем narrow-band SDF/VDB.

## Следующий этап V1

1. `IVoxelGrid` и 8³ bit-brick backend.
2. Morton ordering chunks.
3. greedy meshing прямоугольных поверхностей.
4. narrow-band SDF поверх sparse chunks.
5. morphology 6/18/26 neighbourhood.
6. import mesh → voxel/SDF.
7. 3MF export с material ids.
8. соединение scalar field с FEM/topology optimization.

## Сборка

Windows + .NET Framework 4.8:

```powershell
nuget restore Cadv1.e\OpenGL_lesson_CSharp.sln
msbuild Cadv1.e\OpenGL_lesson_CSharp.sln /m /p:Configuration=Release /p:Platform=x86
```

## Место V1 в будущем общем приложении

```text
Unified DCad
   │
   ├─ Geometry.Core
   │    ├─ Polygon/BRep adapter      <- V2
   │    ├─ Sparse voxel/field        <- V1 + VoxelСad
   │    └─ Mesh/STL                  <- Rendering-stl
   │
   ├─ Analysis                       <- FEM_Voxel
   ├─ Rendering                      <- OpenGL
   └─ Geometry.Math                  <- Function-Basket
```

V1 теперь рассматривается не как «старая версия CAD», а как компактная лаборатория sparse volumetric geometry.
