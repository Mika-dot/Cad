# ModernGeometryLab

![Путь от старого случая к регрессионному тесту](../docs/images/geometry-lab.svg)

Набор .NET 8 библиотек и xUnit-тестов для геометрических функций DCad.

## Проекты

| Проект | Назначение |
|---|---|
| `src/DCad.Core` | типы геометрии, triangulation, mesh audit, point-in-solid, spatial queries, predicates |
| `src/DCad.Boolean.Manifold` | примитивы и булевы операции через ManifoldNET |
| `tests/GeometryLab.Tests` | проверка численных и топологических инвариантов |

## Запуск

Из корня ветки:

```powershell
dotnet test ModernGeometryLab/tests/GeometryLab.Tests/GeometryLab.Tests.csproj -c Release
```

## Наборы тестов

- `InvariantTests`: площадь triangulation, булевы тождества объёмов, manifold topology, повторяемость point-in-solid;
- `SpatialQueryTests`: ray/triangle, ray/AABB, closest point, Morton code;
- `RobustPredicateTests`: знак orientation и классификация пересечения отрезков.

Тесты фиксируют известные случаи, но пока не покрывают fuzzing, экстремальные диапазоны координат, самопересечения mesh и производительность.
