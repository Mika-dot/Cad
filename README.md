# DCad — Function Basket / Geometry Lab

Изначально эта ветка была корзиной отдельных экспериментов: математика, пересечение двух треугольников, проверка точки внутри тела. Исходники сохранены как historical prototypes, но основная роль ветки теперь — **общая геометрическая математика + регрессионная лаборатория DCad**.

## ModernGeometryLab

`ModernGeometryLab/` содержит .NET 8 geometry core и test suite. Ветка нужна для того, чтобы алгоритмы V2, STL, renderer picking и будущего Unified CAD не копировали собственные версии `epsilon`, ray-triangle и point-in-solid.

Уже есть:

- `GeometryTolerance`: absolute + relative scale-aware tolerance;
- `Vector2d`, `Vector3d`, `Aabb3d`, `Triangle3d`;
- indexed `Mesh3d`;
- polygon validation/triangulation;
- repeatable point-in-solid через solid angle вместо случайного ray;
- mesh validation;
- Manifold Boolean backend regression tests;
- invariants для Boolean volume identities и topology.

## Новое: SpatialQueries

Добавлены reusable spatial primitives:

- `Ray3d` / `RayHit`;
- deterministic Möller–Trumbore ray-triangle query;
- slab ray-AABB intersection;
- closest point on triangle по Voronoi regions;
- 3D Morton code для sparse chunks/BVH ordering.

Это напрямую используется/пригодится в:

```text
OpenGL picking --------+
                       |
V2 BVH / intersections +--> DCad.Core.SpatialQueries
                       |
STL self-intersection -+
                       |
Voxel brick ordering --+
```

## Regression-first правило

Новый геометрический баг должен сначала превращаться в тест здесь, а потом исправляться в kernel.

Например, старый `V2-Experiment` имел point-inside с `new Random()`. В Geometry Lab уже есть test, который 100 раз проверяет одинаковую классификацию одной и той же точки. Такой test не позволяет случайно вернуть старую недетерминированную ошибку.

## Тесты

```powershell
dotnet test ModernGeometryLab/tests/GeometryLab.Tests/GeometryLab.Tests.csproj -c Release
```

Новые spatial regression tests проверяют:

- barycentric hit ray→triangle;
- parallel ray rejection для AABB;
- closest-point regions;
- уникальность Morton code на локальном 8³ block.

## Что развивать дальше

1. SAH BVH builder и traversal;
2. triangle-triangle intersection с coplanar case;
3. segment/triangle and segment/segment distance;
4. signed distance mesh query;
5. winding-number acceleration через BVH;
6. exact/adaptive orientation predicates;
7. robust 2D segment intersections;
8. plane/line/circle/arc primitives;
9. transform/quaternion/matrix shared math;
10. property-based/fuzz tests;
11. adversarial near-coplanar regression corpus;
12. benchmark suite для predicates/BVH/booleans.

## Роль в едином DCad

В конечной структуре это должен быть пакет `DCad.Geometry.Core`, который не зависит ни от UI, ни от SharpGL, ни от FEM, ни от STL parser:

```text
DCad.Geometry.Core
    ^       ^       ^       ^
    |       |       |       |
   V2      STL   Renderer  Volume
```

Так Function-Basket перестаёт быть свалкой функций и становится местом, где математическая корректность проекта закрепляется тестами.
