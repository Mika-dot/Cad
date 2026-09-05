# DCad — Unified CAD/CAE application

`Unified-CAD` — интеграционная ветка репозитория. Здесь исторические направления DCad перестают быть отдельными демонстраторами и собираются в одну модульную систему.

Цель: один документ, один язык/operation graph, один viewport и несколько взаимозаменяемых geometry/analysis backends.

## Что уже работает

На первом интеграционном этапе собран production polygon-CAD vertical slice:

```text
.dcad source
    ↓
DCad.Language
    ↓
IModelingKernel
    ↓
Manifold polygon CSG
    ↓
indexed Mesh3d + topology validation
    ├──→ DCad.Cli → OBJ
    └──→ DCad.App → OpenTK viewport
```

Реализовано:

- .NET 8 solution;
- double-precision geometry primitives;
- indexed triangle mesh;
- bounds, surface area, signed volume;
- topology audit: degenerate triangles, boundary edges, non-manifold edges, inconsistent winding;
- deterministic simple-polygon triangulation с поддержкой concave polygons;
- deterministic point-in-solid через generalized solid angle;
- production mesh booleans через `ManifoldNET`;
- `union`, `difference`, `intersection`;
- box / sphere / cylinder;
- translate / rotate / scale;
- собственный текстовый CAD language;
- headless CLI;
- modern OpenTK 4 shader renderer;
- wireframe / orbit / zoom;
- regression tests, выросшие из проблем старого `V2-Experiment`;
- GitHub Actions build + tests + real language→mesh smoke test.

## Язык проектирования

Пример `examples/bracket.dcad`:

```text
param width = 60mm;
param depth = 40mm;
param height = 8mm;

let base = box(width, depth, height);
let hole = cylinder(20mm, 5mm);

let h1 = translate(hole, -20mm, -10mm, 0mm);
let h2 = translate(hole,  20mm, -10mm, 0mm);
let h3 = translate(hole, -20mm,  10mm, 0mm);
let h4 = translate(hole,  20mm,  10mm, 0mm);

solid result = base - h1 - h2 - h3 - h4;
```

Поддерживаются:

- `param` — числовые параметры;
- `let` — промежуточные solids;
- `solid` — результирующий solid;
- `mm`, `cm`, `m`, `deg`;
- `+` — union;
- `-` — difference;
- `&` — intersection;
- `box`, `sphere`, `cylinder`;
- `translate`, `rotate`, `scale`;
- функции `union(...)`, `difference(...)`, `intersection(...)`.

Это первый слой будущего языка. Следующий этап — сохраняемый AST/operation graph, constraints, sketches, arrays/patterns, named selections и связь команд с undo/redo.

## Проекты solution

```text
DCad.sln
├── DCad.Core
│   ├── math / tolerance policy
│   ├── Mesh3d
│   ├── mesh validation
│   ├── polygon triangulation
│   └── solid queries
├── DCad.Boolean.Manifold
│   └── production triangle-solid CSG adapter
├── DCad.Language
│   └── parser / evaluator for .dcad
├── DCad.Cli
│   └── headless modeling + mesh validation + OBJ
├── DCad.App
│   └── OpenTK / OpenGL 3.3+ viewport
└── DCad.Tests
    └── geometry / CSG / language regressions
```

`IModelingKernel` специально отделяет язык и UI от конкретной реализации booleans. В дальнейшем рядом могут появиться:

```text
IModelingKernel
├── ManifoldMeshKernel        current
├── VoxelSdfKernel            from VoxelСad
└── OpenCascadeBRepKernel     future exact/B-Rep path
```

## Почему старый V2 не используется как production kernel

В исходном `V2-Experiment` были хорошие идеи, но вычислительная геометрия строилась на наборе локальных эвристик:

- slope/intercept пересечения отрезков;
- точные сравнения floating-point с нулём;
- фиксированные epsilon, не зависящие от масштаба модели;
- принудительное округление intersection points;
- ad-hoc triangulation наборов точек;
- random ray casting для point-in-solid;
- отсутствие обязательной проверки manifold topology после CSG.

Такой код полезен как история разработки и regression corpus, но его нельзя делать базой объединённого CAD. В `V2-Experiment/ModernV2/Production` теперь лежит та же исправленная production architecture, а старый BSP оставлен как readable reference implementation.

## Как сюда входят остальные ветки

| Ветка | Что переносится | Целевой модуль |
|---|---|---|
| `V1-Experiment` | operation history, voxel extrusion, G-code, temperature-field concept | `DCad.Document`, `DCad.Manufacturing`, fields |
| `V2-Experiment` | explicit polygon/mesh representation | `DCad.Geometry.Mesh` — уже начато |
| `VoxelСad` | sparse voxels, implicit primitives, morphology, TPMS/lattice, voxel↔mesh | `DCad.Geometry.Voxel` |
| `FEM_Voxel` | FEM, SIMP/OC, density/stress/displacement fields | `DCad.Analysis`, `DCad.Optimization` |
| `OpenGL` | camera/viewport UX, render modes, field heatmap | `DCad.Viewport` |
| `Rendering-stl` | STL validation/import/export and application workflow | `DCad.IO` |
| `Function-Basket` | pathological geometry examples | `DCad.Tests` / fuzz/regression corpus |

## Общая архитектура

```text
                         DCad.App
                            │
                   Document / Commands
                            │
                    .dcad / project file
                            │
                    Geometry Kernel API
                 ┌──────────┼──────────┐
                 │          │          │
             Polygon      Voxel/SDF   B-Rep
                 │          │          │
                 └──────┬───┴────┬─────┘
                        │        │
                   Mesh/Field contracts
                    ┌───┴───┐  ┌─┴──────────┐
                    │       │  │            │
                 Viewport   IO FEM/Optimization
                                │
                     stress/density/displacement
                                │
                         lattice / TPMS
                                │
                          manufacturing
```

Критический принцип: geometry, analysis и IO модули не владеют UI и не рисуют OpenGL сами. Renderer получает mesh/field packets; FEM получает geometry/field data; language создаёт operation graph через kernel interfaces.

## Запуск

```powershell
dotnet restore DCad.sln
dotnet build DCad.sln -c Release
dotnet test tests/DCad.Tests/DCad.Tests.csproj -c Release
```

CLI:

```powershell
dotnet run --project src/DCad.Cli/DCad.Cli.csproj -- examples/bracket.dcad result.obj
```

Viewer:

```powershell
dotnet run --project src/DCad.App/DCad.App.csproj -- examples/bracket.dcad
```

Управление viewer:

- стрелки — orbit;
- `PageUp/PageDown` — zoom;
- `F1` — solid/wireframe;
- `Esc` — exit.

## Инварианты geometry kernel

Для solid-операций вводится контракт, которого не было в старых экспериментах:

1. floating-point tolerance задаётся централизованно;
2. входной indexed mesh можно проверить до операции;
3. финальный solid обязан иметь согласованный winding;
4. у замкнутого solid не должно быть boundary edges;
5. non-manifold edges не принимаются молча;
6. triangulation простого polygon даёт `n-2` triangles;
7. point-in-solid не зависит от RNG;
8. найденный geometry bug превращается в regression test в `Function-Basket/ModernGeometryLab`.

## Следующие интеграционные этапы

1. `DCad.Document`: persistent operation graph, object IDs, parameters, undo/redo.
2. `DCad.IO`: единый STL/OBJ/PLY/3MF layer и mesh repair report.
3. `DCad.Viewport`: перенести лучшие camera/selection/render-mode функции из `OpenGL` и объединить с OpenTK backend.
4. `DCad.Geometry.Voxel`: подключить `VoxelСad` через общий geometry/field contract.
5. mesh ↔ voxel/SDF conversion.
6. `DCad.Analysis.Protocol`: связать C# document с `FEM_Voxel` без OpenSCAD/Streamlit state.
7. stress/density/displacement overlays в одном viewport.
8. FEM/topology result → variable lattice/TPMS geometry.
9. sketches + constraints + extrusion/revolve/sweep.
10. B-Rep/STEP bridge для точной инженерной геометрии, где triangle/voxel representation недостаточно.

## Текущий статус

Первый vertical slice уже проходит CI целиком: restore → Release build → geometry/CSG/language regression tests → выполнение `.dcad` → CSG → topology validation → OBJ. Это базовая точка, от которой имеет смысл дальше присоединять остальные ветки, а не ещё раз переписывать их UI независимо друг от друга.

> `ManifoldNET` сейчас используется как практический mesh-CSG backend и остаётся заменяемым adapter-слоем. Для долгосрочного промышленного CAD отдельно потребуется B-Rep/STEP backend; mesh booleans не должны притворяться заменой точной NURBS/B-Rep геометрии.
