# DCad unification plan

Цель репозитория — собрать сильные части исторических веток в одно CAD/CAE-приложение, а не поддерживать набор несвязанных WinForms/SharpGL демонстраторов.

Активная интеграционная ветка: **`Unified-CAD`**.

## Что берём из каждой ветки

| Ветка | Современная часть | Целевой модуль |
|---|---|---|
| `Unified-CAD` | общий .NET 8 kernel API, DSL, robust polygon CSG, CLI, OpenTK app | интеграционная база |
| `OpenGL` | Camera/Scene UX + `ModernRenderer` VBO/VAO/shaders/field scalar | `DCad.Viewport` |
| `VoxelСad` | sparse voxel/implicit CSG, primitives, morphology, TPMS/lattice, greedy meshing | `DCad.Geometry.Voxel` |
| `FEM_Voxel` | voxel FEM, SIMP/OC, density/stress fields, field exchange | `DCad.Analysis`, `DCad.Optimization` |
| `V1-Experiment` | modern voxel engine, operation history, extrusion/G-code/temperature ideas | `DCad.Document`, `DCad.Manufacturing`, fields |
| `V2-Experiment` | `ModernV2/Production`: indexed mesh, robust CSG, language and viewer | `DCad.Geometry.Mesh` |
| `Rendering-stl` | `ModernStl`: STL validation/import/export | `DCad.IO` |
| `Function-Basket` | `ModernGeometryLab`: deterministic geometry/CSG regressions | `DCad.Tests` |

## Target solution

```text
DCad.sln
  DCad.App
  DCad.Document
  DCad.Language
  DCad.Viewport
  DCad.Geometry.Core
  DCad.Geometry.Mesh
  DCad.Geometry.Voxel
  DCad.Analysis
  DCad.Optimization
  DCad.Manufacturing
  DCad.IO
  DCad.Tests
```

## Главный принцип

UI, renderer, geometry, analysis и IO разделяются интерфейсами.

```text
.dcad / project
      │
Document + Operation Graph
      │
Geometry Kernel API
  ┌───┼─────────────┐
  │   │             │
Mesh  Voxel/SDF     B-Rep
  │   │             │
  └───┴─────┬───────┘
            │
     Mesh / Field contracts
      ┌─────┼──────────┐
      │     │          │
 Viewport   IO   FEM / Optimization
```

Ни FEM, ни voxel engine, ни polygon CSG не должны напрямую владеть OpenGL/WinForms state.

## Инварианты общей системы

- длина по умолчанию: mm;
- сила: N;
- stress / Young modulus: N/mm² (MPa);
- geometry algorithms работают в `double`, если backend не требует другое;
- tolerance policy централизована и зависит от масштаба;
- solid mesh проходит topology validation;
- замкнутый solid не имеет boundary/non-manifold edges;
- geometry bugs превращаются в regression tests;
- operation history сериализуема;
- renderer принимает mesh/field packets, а не вызывает geometry code;
- analysis fields имеют explicit origin/grid/unit metadata.

## Migration status

### Phase 0 — inventory and research baseline
- [x] branch inventory
- [x] main README as project map
- [x] VoxelCAD expanded beyond box-only occupancy
- [x] FEM/topology optimization baseline
- [x] modern viewport experiments

### Phase 1 — common geometry foundation
- [x] .NET 8 integration branch `Unified-CAD`
- [x] double-precision vectors / triangles / AABB
- [x] indexed `Mesh3d`
- [x] mesh topology validator
- [x] deterministic concave polygon triangulation
- [x] deterministic point-in-solid (no RNG rays)
- [x] replace legacy V2 boolean path with production Manifold adapter
- [x] CSG regression tests
- [x] basic modeling-kernel abstraction
- [ ] exact/B-Rep adapter for STEP/NURBS-class geometry

### Phase 2 — modeling language and document
- [x] first `.dcad` parser/evaluator
- [x] scalar parameters + explicit units
- [x] primitives / transforms / union / difference / intersection
- [ ] persistent AST / operation graph
- [ ] stable object IDs and named selections
- [ ] undo / redo
- [ ] project save/load
- [ ] sketches + geometric constraints
- [ ] extrude / revolve / sweep / loft
- [ ] arrays, patterns, mirrors

### Phase 3 — viewport convergence
- [x] reusable Camera/Scene UX in `OpenGL`
- [x] VBO/VAO/index-buffer shader renderer in `OpenGL/ModernRenderer`
- [x] OpenTK production viewer in `Unified-CAD`
- [x] scalar/heatmap experiment in renderer branch
- [ ] common `ScenePacket` / `FieldLayer` contracts
- [ ] GPU ID picking
- [ ] selection outline
- [ ] gizmo + snapping
- [ ] orthographic CAD views in unified viewer
- [ ] clipping/section planes
- [ ] measurements
- [ ] large model chunking/culling/LOD

### Phase 4 — voxel / field convergence
- [x] VoxelCAD primitives, implicit rasterization, CSG and morphology
- [x] TPMS/lattice experiments
- [x] greedy mesh export
- [x] FEM field exchange convention
- [ ] common C# `IFieldSource`
- [ ] mesh → voxel/SDF
- [ ] voxel/SDF → mesh adapter into common `Mesh3d`
- [ ] persistent narrow-band SDF
- [ ] adaptive sparse bricks / VDB-class backend
- [ ] Dual Contouring/QEF surface extraction

### Phase 5 — CAE / generative
- [x] SIMP + Optimality Criteria
- [x] filtering/projection/continuation
- [x] sparse regular-grid FEM
- [x] density field output
- [x] shared mm/N/MPa convention documented
- [ ] formal CAD→analysis request schema
- [ ] loads/supports/materials stored in common document
- [ ] stress/displacement/density rendered as field layers
- [ ] result probing and legends
- [ ] density/stress-driven lattice/TPMS
- [ ] parameter sweeps and variant comparison

### Phase 6 — IO and manufacturing
- [x] modern STL toolkit experiment exists in `Rendering-stl`
- [x] historical G-code generator preserved in V1
- [ ] move STL/OBJ import/export into `DCad.IO`
- [ ] mesh repair report on import
- [ ] PLY / 3MF
- [ ] STEP via B-Rep bridge
- [ ] slicing and G-code as `DCad.Manufacturing`
- [ ] printability / minimum-wall checks

### Phase 7 — tests / robustness
- [x] `Function-Basket` converted to `ModernGeometryLab`
- [x] triangulation area/n−2 invariants
- [x] CSG volume identities
- [x] deterministic point classification tests
- [x] manifold topology checks
- [ ] triangle/triangle pathological corpus
- [ ] coplanar/touching boolean corpus
- [ ] property-based randomized geometry tests
- [ ] fuzzing parsers/importers
- [ ] performance benchmarks and large-model datasets

## What not to merge literally

Не переносятся как архитектурная основа:

- копии `SharpGLForm` из веток;
- `worldX/worldY/worldZ` как глобальное состояние;
- branch-specific `save.txt`;
- UI event handlers, внутри которых живут CSG/FEM algorithms;
- random ray casting и fixed magic epsilons;
- mixed metre/Pascal vs millimetre/MPa units;
- per-triangle / per-voxel immediate OpenGL submission;
- бинарники и NuGet packages, закоммиченные в старые каталоги.

Они сохраняются как история, но современная реализация строится вокруг общих contracts и regression tests.

## Merge policy

Старые ветки не должны механически сливаться в `main`. Сначала полезная подсистема получает современный isolated implementation + CI в своей ветке, затем интерфейс стабилизируется в `Unified-CAD`, и только после этого она переносится в итоговый application solution.
