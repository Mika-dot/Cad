# DCad — V2 Experiment / polygon & mesh CAD

`V2-Experiment` — полигональная линия DCad. В отличие от V1/VoxelCAD, здесь тело описывается явной треугольной поверхностью и булевы операции выполняются над mesh.

Исторический код `GL/OpenGL_lesson_CSharp/VARcad` сохранён как исходный эксперимент. В нём были реализованы `OR`, `XOR`, разбиение пересекающихся треугольников и ручная триангуляция. Этот код важен как история проекта, но его математическую модель нельзя считать надёжным production kernel: классификация `Triangle.IsInside(...)` использует случайно выбранный луч, геометрия хранится в `float`, а большое количество частных epsilon-проверок делает сложные касания и почти копланарные случаи нестабильными.

## ModernV2

Ветка теперь содержит отдельный современный проект:

```text
ModernV2/
├── DCad.MeshKernel.csproj
├── MeshKernel.cs
├── Program.cs
└── README.md
```

Это новый deterministic reference backend на .NET 8:

- double precision geometry;
- BSP-based CSG;
- union / subtraction / intersection;
- polygon splitting against planes;
- box/cylinder primitives;
- surface area, signed volume, bounding box;
- STL export;
- headless CLI для тестирования геометрии без UI.

Запуск:

```bash
dotnet run --project ModernV2/DCad.MeshKernel.csproj -- result.stl
```

Подробности: [`ModernV2/README.md`](ModernV2/README.md).

## Что исправляет новый подход

Старый алгоритм пытался вручную решать одновременно четыре разные задачи: triangle-triangle intersection, topology reconstruction, inside/outside classification и triangulation. Это сильно усложняет обработку касаний, совпадающих рёбер и копланарных поверхностей.

BSP-версия разделяет эти операции через классификацию polygon относительно плоскости и работает детерминированно. Она остаётся reference implementation, а не конечным промышленным ядром.

## Целевая архитектура mesh CAD

В финальном приложении DCad mesh API не должен быть привязан к одному алгоритму:

```text
                     DCad.Geometry.Mesh API
                               |
           +-------------------+-------------------+
           |                   |                   |
      Managed BSP        Manifold backend      CGAL backend
      reference          fast robust mesh      exact/repair
                                                   |
                                             OpenCASCADE
                                             STEP / B-Rep
```

Так можно использовать быстрый backend во viewport/interactive CSG, а сложные случаи отправлять в robust/exact backend.

## Что следует добавить дальше

1. **Half-edge indexed mesh** вместо независимых triangles. Это даст явную топологию vertices/edges/faces и дешёвый adjacency.
2. **BVH/AABB tree** для broad phase. Triangle operations не должны каждый раз перебирать все пары `N × M`.
3. **Robust predicates** для orientation/intersection и единая tolerance policy.
4. **Mesh audit/repair**: degenerate faces, non-manifold edges, flipped normals, duplicate vertices, holes, disconnected shells.
5. **Coplanar merge** после Boolean, чтобы поверхность не распадалась на сотни микротреугольников.
6. **Remeshing/simplification** с сохранением feature edges.
7. **Picking / selection / face IDs**, чтобы mesh был пригоден для CAD UI, а не только экспорта.
8. **Mesh ↔ SDF/Voxel** конвертер для связи с `V1-Experiment` и `VoxelСad`.
9. **Material/field attributes** на vertex/face/cell.
10. **STEP/B-Rep bridge** через OpenCASCADE вместо попытки самостоятельно реализовать NURBS/STEP kernel.

## Представления в едином DCad

V2 теперь рассматривается как surface-geometry backend:

```text
Parametric / B-Rep
        |
        v
 Triangle Mesh <----------> Sparse SDF / Voxels
        |                        |
        |                        +--> topology optimization / fields
        |
        +--> rendering / picking / STL / 3MF
```

Поэтому V1, V2 и VoxelCAD не нужно сливать в одну гигантскую структуру данных. Им нужен общий scene/geometry interface и явные конвертеры между представлениями.

## Legacy

Старый демонстратор по-прежнему находится в `GL/`. Он сохранён для сравнения алгоритмов и визуальной истории проекта.
