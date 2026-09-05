# DCad V2 — modern mesh/triangle CAD kernel

`ModernV2` — новый reference backend для полигонального направления DCad. Старый `VARcad` сохранён для истории и сравнения, но новые эксперименты не должны зависеть от случайной классификации point-inside и ручного набора epsilon-условий.

## Что реализовано

- геометрия в `double`, а не `float`;
- детерминированный BSP CSG;
- `Union`, `Subtract`, `Intersect`;
- convex polygon splitting плоскостью;
- box/cylinder primitives;
- triangulation polygon fan для экспорта;
- surface area, signed volume и bounds;
- ASCII STL export;
- standalone .NET 8 CLI без WinForms/SharpGL зависимостей.

Пример:

```csharp
var body = MeshFactory.Box(
    new Vec3d(-8, -8, -4),
    new Vec3d( 8,  8,  4));

var hole = MeshFactory.Cylinder(
    new Vec3d(0, 0, 0),
    radius: 5.2,
    height: 14,
    segments: 64);

var result = body.Subtract(hole);
var stats = MeshAnalysis.Analyze(result);
StlWriter.WriteAscii("result.stl", result);
```

## Запуск

```bash
dotnet run --project ModernV2/DCad.MeshKernel.csproj -- result.stl
```

## Почему BSP, если существуют готовые kernels

BSP здесь нужен как небольшой понятный reference implementation: его удобно тестировать, профилировать и использовать для проверки API. Это **не заявление о production-grade exact CAD kernel**.

Для будущего единого DCad один интерфейс mesh booleans должен иметь несколько backend'ов:

```text
DCad.Geometry.Mesh
        |
        +-- ManagedBspBackend       <- этот эксперимент
        +-- ManifoldBackend         <- быстрые manifold booleans
        +-- CgalExactBackend        <- robust/exact predicates & constructions
        +-- OpenCascadeBridge       <- B-Rep / STEP bridge
```

Это позволит выбирать быстрый backend для интерактивной работы и exact/repair backend для проблемных моделей.

## Следующие шаги

- indexed half-edge mesh;
- BVH/AABB tree для spatial queries;
- coplanar polygon merge;
- degeneracy/self-intersection audit;
- normal/winding repair;
- connected components;
- remeshing and simplification;
- feature-edge preservation;
- STL/OBJ/PLY/3MF import-export;
- mesh -> SDF/voxel bridge;
- SDF/voxel -> mesh bridge;
- exact backend adapter;
- renderer data packets shared with `OpenGL` branch.

## Ключевая идея V2

V1/VoxelCAD отвечают на вопрос «как редактировать объёмное поле», а V2 — «как работать с явной поверхностью». В финальном DCad это не конкурирующие версии, а два представления одной сцены:

```text
Analytic/B-Rep
     | tessellate
     v
Triangle Mesh <----> SDF / Sparse Volume
     |                    |
 rendering             topology/FEM
     |                    |
     +------ export -------+
```
