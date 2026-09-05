# DCad — experimental multi-backend CAD / CAE platform

DCad вырос из нескольких независимых экспериментов: polygon CSG, sparse voxels, implicit geometry, OpenGL rendering, STL tooling, operation history, G-code, FEM и topology optimization.

Сейчас цель репозитория одна: **собрать сильные части всех веток в одно CAD/CAE-приложение**, сохранив разные geometry representations как взаимозаменяемые backends.

> **Интеграционная ветка:** [`Unified-CAD`](https://github.com/Mika-dot/Cad/tree/Unified-CAD)  
> **Полный roadmap:** [`docs/UNIFICATION_PLAN.md`](docs/UNIFICATION_PLAN.md)

## Концепция

Один document/feature graph должен управлять несколькими представлениями геометрии:

```text
                       DCad.App
                          │
                 Document / Feature Graph
                          │
                 modeling capability API
            ┌─────────────┼─────────────┐
            │             │             │
      triangle mesh    voxel / SDF   exact B-Rep
            │             │             │
            └──────────┬──┴───────┬─────┘
                       │          │
                 Mesh / Field contracts
             ┌─────────┼──────────┼────────────┐
             │         │          │            │
          Viewport     IO       FEM/TO    Manufacturing
                                  │
                        stress/density/displacement
                                  │
                         variable lattice / TPMS
```

Не нужно выбирать между «полигональным CAD» и «воксельным CAD». У них разные сильные стороны. Mesh удобен для rendering/import/repair и быстрых surface operations; voxel/SDF — для morphology, scans, topology optimization, fields и architected materials; B-Rep нужен для точных инженерных surfaces, sketches, fillets и STEP.

# Текущее состояние по веткам

| Ветка | Что в ней развивается | Что переносится в единое приложение |
|---|---|---|
| **[`Unified-CAD`](https://github.com/Mika-dot/Cad/tree/Unified-CAD)** | .NET 8 integration solution, feature graph, language, mesh kernel, fields, analysis protocol | каркас приложения |
| **[`VoxelСad`](https://github.com/Mika-dot/Cad/tree/Voxel%D0%A1ad)** | sparse voxel/implicit CAD, CSG, morphology, TPMS/lattice, greedy meshing | `DCad.Geometry.Voxel` |
| **[`FEM_Voxel`](https://github.com/Mika-dot/Cad/tree/FEM_Voxel)** | hexa/voxel FEM, SIMP/OC, robust/multi-load optimization, field exchange | `DCad.Analysis` + `DCad.Optimization` |
| **[`V2-Experiment`](https://github.com/Mika-dot/Cad/tree/V2-Experiment)** | explicit mesh/CSG experiments, deterministic solid queries, production migration | `DCad.Geometry.Mesh` + regression corpus |
| **[`OpenGL`](https://github.com/Mika-dot/Cad/tree/OpenGL)** | modern OpenTK renderer, camera, heatmaps, picking | `DCad.Viewport` |
| **[`V1-Experiment`](https://github.com/Mika-dot/Cad/tree/V1-Experiment)** | operation history, sparse voxel workflow, layer/toolpath research | `DCad.Document` + `DCad.Manufacturing` |
| **[`Rendering-stl`](https://github.com/Mika-dot/Cad/tree/Rendering-stl)** | STL reader/writer/audit, topology and mesh-quality diagnostics | `DCad.IO` |
| **[`Function-Basket`](https://github.com/Mika-dot/Cad/tree/Function-Basket)** | robust geometry predicates and pathological tests | `DCad.Tests` / geometry lab |

# Unified-CAD — конечная точка объединения

`Unified-CAD` уже не demo-ветка. Это начало общего application architecture.

Сейчас там есть:

- .NET 8 solution;
- `DCad.Core` с double-precision geometry;
- indexed `Mesh3d`;
- topology validation;
- deterministic concave polygon triangulation;
- deterministic point-in-solid;
- Manifold-based triangle solid CSG;
- `IModelingKernel`;
- явные `KernelCapabilities` для mesh / voxel-SDF / будущего B-Rep;
- `.dcad` language;
- CLI;
- OpenTK application;
- structured scalar/mask field model;
- FEM field reader;
- **persistent `DocumentGraph`** с UUID объектов и dependency DAG;
- deterministic topological rebuild order;
- deterministic SHA-256 document fingerprint;
- transactional undo/redo;
- **versioned CAD→CAE protocol** `dcad.analysis.request/1` / `dcad.analysis.result/1`;
- xUnit regression tests и CI.

Ключевая идея feature graph: изменение параметра не должно означать «нарисовать всё заново руками». Feature знает inputs и parameters, а document знает dependency graph. Дальше можно добавить dirty-subgraph rebuild/cache, stable named selections и feature suppression.

# VoxelСad — volumetric / field geometry

Ветка уже вышла далеко за первоначальные `HashSet<(x,y,z)> + AddBox`.

Реализовано:

- sparse occupancy;
- box / sphere / cylinder / torus / capsule;
- generic implicit-function rasterization;
- union / difference / intersection;
- dilation / erosion / opening / closing;
- 6/18/26 neighbourhood;
- majority smoothing;
- connected-component cleanup;
- Gyroid TPMS;
- Schwarz-P TPMS;
- BCC lattice;
- volume/surface metrics;
- greedy STL meshing;
- JSON scene DSL;
- headless JSON→STL pipeline;
- FEM bridge с согласованными mm/N/MPa units;
- CI smoke test.

Следующая архитектура:

```text
HashSet occupancy
      ↓
chunked sparse bit-bricks
      ↓
narrow-band signed distance field
      ↓
field CSG / offset / shell / blend
      ↓
adaptive octree / VDB-class storage
      ↓
Marching Cubes + Dual Contouring/QEF
      ↓
GPU sparse field backend
```

# FEM_Voxel — CAE и generative engineering

Ветка должна стать headless computational backend, а не отдельным CAD UI.

Базовый pipeline уже содержит:

- regular hexa/voxel FEM;
- sparse stiffness assembly;
- matrix-free path для больших систем;
- SIMP;
- Optimality Criteria;
- density/sensitivity filtering;
- Heaviside projection + continuation;
- connectivity-aware postprocessing;
- multi-load shared FEM context;
- density/stress/displacement/compliance fields;
- `final_fields.npz` interchange;
- mm / N / MPa convention.

Новый advanced optimization layer добавляет:

- continuation schedule для `p`, `beta`, move limit;
- robust **eroded / nominal / dilated** projections;
- KS smooth-max aggregation;
- p-norm aggregation;
- weighted/max/p-norm aggregation of per-element multi-load energies;
- generalized OC update;
- voxel overhang diagnostics;
- minimum-feature diagnostics through Euclidean distance transform.

Дальше:

```text
assembled sparse FEM
      ↓
matrix-free element-by-element operator
      ↓
MGPCG / geometric multigrid
      ↓
GPU compute
      ↓
MMA/GCMMA + stress/displacement/buckling constraints
      ↓
robust manufacturing-aware topology optimization
      ↓
stress/density field → variable TPMS/lattice
```

Подробно: [`FEM_Voxel/docs/ADVANCED_OPTIMIZATION.md`](https://github.com/Mika-dot/Cad/blob/FEM_Voxel/docs/ADVANCED_OPTIMIZATION.md).

# V2-Experiment — mesh geometry research

Старый V2 важен как история mesh/boolean CAD, но его ad-hoc numerical geometry нельзя делать production kernel.

В современной части сохранены/добавлены:

- readable BSP CSG reference;
- mesh validation;
- primitives/transforms/export;
- deterministic generalized solid-angle point classification;
- scale-aware tolerance reference;
- production implementation в `ModernV2/Production`, совпадающая по направлению с `Unified-CAD`.

Старые алгоритмы slope/intercept, fixed epsilon, rounding intersections и random ray casting должны оставаться regression material, а не архитектурной основой.

# OpenGL — viewport research

`OpenGL/ModernRenderer` используется как лаборатория renderer/interaction функций:

- OpenTK 4 / OpenGL core;
- VBO / VAO / EBO;
- shaders;
- normals + lighting;
- scalar heatmaps;
- orbit/zoom/wireframe;
- screen→world ray construction;
- Möller–Trumbore triangle picking;
- barycentric hit result;
- scalar normalization: linear / log / symmetric;
- robust quantile clipping для stress/CAE heatmaps.

Цель — перенести interaction слой в единый viewport, затем перейти от CPU O(N triangles) picking к BVH/GPU ID-buffer для больших сцен.

# V1-Experiment — document/manufacturing research

V1 ценна не старым SharpGL UI, а идеями history/rebuild/manufacturing.

Современный слой содержит:

- sparse voxel engine;
- serializable operation history;
- undo/redo через deterministic rebuild;
- box/sphere/cylinder/extruded polygon operations;
- scalar field in voxel cells;
- historical G-code path;
- новый **run-compressed layer planner**: contiguous voxels объединяются в layer runs;
- manufacturing estimates: layers, runs, active cells, deposition length, volume;
- research G-code output с существенно меньшим количеством motion primitives, чем «одна команда на voxel».

Финальный slicer всё равно должен стать отдельным `DCad.Manufacturing`: contours → offsets → infill/supports → process profile → toolpath.

# Rendering-stl — IO / mesh quality

`ModernStl` теперь содержит не только чтение/запись STL:

- binary/ASCII STL reader;
- STL/OBJ writer;
- degenerate / boundary / non-manifold / duplicate audit;
- connected-component/topology diagnostics;
- vertex welding/basic repair;
- triangle angle/aspect-ratio quality;
- median edge scale;
- boundary-loop/open-chain extraction;
- scale-aware tolerance suggestion.

Это должно переехать в `DCad.IO`, где import возвращает не просто mesh, а `mesh + validation/repair report`.

# Function-Basket — numerical geometry lab

Эта ветка превращается из архива в corpus для geometry invariants.

`ModernGeometryLab` содержит:

- modern .NET tests;
- triangulation invariants;
- manifold/CSG tests;
- spatial queries;
- adaptive 2D/3D orientation predicates: быстрый double path + high-precision fallback около cancellation;
- deterministic segment-intersection classification;
- near-degenerate regression cases.

Правило проекта: **любой найденный geometry bug сначала превращается в минимальный тест, и только потом исправляется kernel**.

# Modeling language и будущий CAD UX

Текущий `.dcad` уже умеет параметры, units, primitives, transforms и booleans:

```text
param width = 60mm;
param depth = 40mm;
param height = 8mm;

let body = box(width, depth, height);
let hole = cylinder(20mm, 5mm);
let left = translate(hole, -20mm, 0mm, 0mm);
let right = translate(hole, 20mm, 0mm, 0mm);

solid result = body - left - right;
```

Следующий modeling layer:

- sketch entities + geometric constraints;
- extrude / revolve / sweep / loft;
- arrays / patterns / mirror;
- feature suppression/configurations;
- named selections;
- stable face/edge lineage;
- project save/load;
- analysis cases directly in document;
- manufacturing operations;
- reusable components/functions.

# Что принципиально не переносится

Не нужно механически объединять исходники веток.

В итоговый продукт не должны попасть как архитектурные основы:

- копии `SharpGLForm`;
- UI event handlers с CSG/FEM logic;
- global camera/geometry state;
- random ray casting;
- fixed magic epsilon как единственная tolerance strategy;
- округление coordinates для «починки» booleans;
- mixed SI Pa and mm/MPa models;
- per-triangle/per-voxel immediate OpenGL;
- STL как canonical editable CAD document;
- Streamlit/OpenSCAD internal state как project format;
- committed `bin`, `obj`, `__pycache__` и старые binary packages.

# Приоритет дальнейшей разработки

1. `Unified-CAD`: project persistence + incremental feature rebuild/cache.
2. Sketch solver + constraints + extrude/revolve/sweep.
3. `VoxelСad` adapter → common `Mesh3d`/field contracts; mesh↔SDF.
4. `FEM_Voxel`: real multi-load optimizer + robust three-field objective + KS constraints.
5. Matrix-free CPU reference → GPU + MGPCG.
6. Unified viewport: picking, selections, gizmo, sections, measurements, field probe.
7. `Rendering-stl` → `DCad.IO`, затем 3MF/PLY и STEP через B-Rep backend.
8. Exact B-Rep adapter и stable topological naming.
9. Density/stress-driven lattice/TPMS generation.
10. Benchmarks/fuzzing/property tests на всех geometry representations.

Главное: `Unified-CAD` должен постепенно стать **единственным приложением**, а остальные ветки — хорошо тестируемыми research tracks, из которых в него переносится только зрелая функциональность.
