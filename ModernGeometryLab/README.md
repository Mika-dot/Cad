# DCad Geometry Regression Lab

Эта папка превращает старую Function-Basket из набора одноразовых экспериментов в тестовую лабораторию геометрического ядра.

Проверяются не только отдельные ответы, но и математические инварианты:

- triangulation простого вогнутого polygon: `n-2` triangles и сохранение площади;
- запрет self-intersection/zero-length edges;
- `V(A ∪ B) + V(A ∩ B) = V(A) + V(B)`;
- `V(A \\ B) + V(A ∩ B) = V(A)`;
- closed/oriented manifold topology после boolean;
- детерминированность point-in-solid без случайных лучей.

Запуск:

```powershell
dotnet test ModernGeometryLab/tests/GeometryLab.Tests/GeometryLab.Tests.csproj -c Release
```

Исторические каталоги на русском сохранены как исходный corpus идей и случаев, но новые regressions должны добавляться сюда.
