# DCad unification plan — 2026

Цель репозитория — собрать сильные части исторических веток в одно CAD/CAE-приложение, а не поддерживать набор несвязанных WinForms/SharpGL/Python демонстраторов.

**Активная интеграционная ветка: `Unified-CAD`.**

Ветки продолжают использоваться как research tracks: новая математика сначала обкатывается изолированно, получает CI/regression tests и только затем переносится в единый solution через стабильный контракт.

## Что берём из каждой ветки

| Ветка | Современная роль | Целевой модуль |
|---|---|---|
| `Unified-CAD` | document/feature graph, common kernel contracts, `.dcad`, Manifold mesh CSG, fields, CLI, app | интеграционная база |
| `OpenGL` | OpenTK renderer, picking math, scalar/CAE visualization | `DCad.Viewport` |
| `VoxelСad` | sparse voxel/implicit CSG, morphology, TPMS/lattice, meshing | `DCad.Geometry.Voxel` |
| `FEM_Voxel` | voxel FEM, SIMP/OC, robust/multi-load topology optimization, fields | `DCad.Analysis`, `DCad.Optimization` |
| `V1-Experiment` | operation history, sparse voxel manufacturing/toolpath ideas | `DCad.Document`, `DCad.Manufacturing` |
| `V2-Experiment` | explicit mesh/CSG research, deterministic solid queries, legacy failure corpus | `DCad.Geometry.Mesh`, regressions |
| `Rendering-stl` | STL IO, topology/quality audit, repair diagnostics | `DCad.IO` |
| `Function-Basket` | robust predicates and pathological geometry tests | `DCad.Tests`, geometry lab |

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
  DCad.Geometry.BRep
  DCad.Fields
  DCad.Analysis.Protocol
  DCad.Analysis
  DCad.Optimization
  DCad.Manufacturing
  DCad.IO
  DCad.Tests
  DCad.Benchmarks
```

## Главный принцип

Один документ и feature graph, но несколько geometry backends.

```text
.dcad / project / UI commands
            │
      Document + Feature Graph
            │
      Modeling capability API
    ┌───────┼─────────────┐
    │       │             │
 Mesh/CSG  Voxel/SDF     B-Rep
    │       │             │
    └───────┴──────┬──────┘
                   │
          Mesh / Field contracts
       ┌───────────┼───────────────┐
       │           │               │
   Viewport       IO        FEM / Optimization
                               │
                    density/stress/displacement
                               │
                     lattice / TPMS / manufacturing
```

Ни FEM, ни voxel engine, ни polygon CSG не должны владеть OpenGL/WinForms/Streamlit state.

## Общие инженерные инварианты

- length: mm;
- force: N;
- stress / Young modulus: N/mm² = MPa;
- geometry math — `double` по умолчанию;
- tolerance policy зависит от масштаба модели;
- найденная почти-вырожденная геометрия не «лечится» округлением координат;
- closed mesh не имеет boundary/non-manifold edges;
- geometry bugs становятся regression tests;
- document history сериализуема и воспроизводима;
- backend capability определяется явно;
- renderer получает mesh/field packets, а не вызывает CAD/FEM algorithms;
- analysis fields имеют explicit grid/origin/units/schema metadata;
- CAD→CAE обмен versioned, поэтому Python/C++/GPU backend можно менять независимо от UI.

# Migration status

## Phase 0 — inventory and research baseline

- [x] inventory всех активных веток;
- [x] `main` как карта проекта;
- [x] `Unified-CAD` как отдельная integration branch;
- [x] VoxelCAD expanded beyond box-only occupancy;
- [x] FEM/topology optimization baseline;
- [x] modern OpenTK viewport experiments.

## Phase 1 — common geometry foundation

- [x] .NET 8 `Unified-CAD` solution;
- [x] double-precision vectors / triangles / AABB;
- [x] indexed `Mesh3d`;
- [x] mesh topology validator;
- [x] deterministic concave polygon triangulation;
- [x] deterministic point-in-solid;
- [x] Manifold mesh-CSG adapter;
- [x] CSG regression tests;
- [x] modeling-kernel abstraction;
- [x] explicit backend capability model (`KernelCapabilities`);
- [ ] exact/B-Rep adapter for STEP/NURBS-class geometry;
- [ ] common tessellation policy between B-Rep and renderer;
- [ ] persistent topological naming / lineage for faces and edges.

## Phase 2 — document, feature tree and modeling language

- [x] first `.dcad` parser/evaluator;
- [x] scalar parameters + explicit units;
- [x] primitives / transforms / union / difference / intersection;
- [x] persistent in-memory `DocumentGraph`;
- [x] stable UUID object IDs;
- [x] dependency validation and deterministic topological rebuild order;
- [x] deterministic document fingerprint;
- [x] transactional undo / redo snapshots;
- [ ] save/load project package;
- [ ] named selections tied to topology lineage;
- [ ] sketches + geometric constraints;
- [ ] extrude / revolve / sweep / loft;
- [ ] arrays, patterns, mirrors;
- [ ] feature suppression/configurations;
- [ ] incremental dirty-subgraph rebuild and cache.

## Phase 3 — viewport convergence

- [x] reusable Camera/Scene UX in `OpenGL`;
- [x] VBO/VAO/index-buffer shader renderer;
- [x] OpenTK production viewer in `Unified-CAD`;
- [x] scalar/heatmap vertex channel;
- [x] reusable CPU ray/triangle picking math in `OpenGL/ModernRenderer`;
- [x] robust scalar normalization: linear/log/symmetric + quantile clipping;
- [ ] common `ScenePacket` / `FieldLayer` contracts;
- [ ] GPU ID-buffer picking for large scenes;
- [ ] hover/selection outline;
- [ ] transform gizmo + snapping;
- [ ] orthographic CAD views;
- [ ] clipping/section planes;
- [ ] measurement tools;
- [ ] large-model chunking/frustum culling/LOD;
- [ ] vertex/face/edge selection with stable object IDs.

## Phase 4 — voxel / field convergence

- [x] sparse occupancy baseline;
- [x] implicit primitive rasterization;
- [x] voxel CSG + morphology;
- [x] Gyroid / Schwarz-P / BCC experiments;
- [x] greedy mesh export;
- [x] FEM field interchange convention;
- [x] common C# structured scalar/mask fields;
- [ ] `IVolumeField` / `IFieldSampler` common interfaces;
- [ ] mesh → voxel/SDF;
- [ ] voxel/SDF → common `Mesh3d`;
- [ ] persistent narrow-band SDF;
- [ ] chunked sparse bit-bricks;
- [ ] adaptive octree / OpenVDB-class backend;
- [ ] Marching Cubes baseline;
- [ ] Dual Contouring + QEF for feature-preserving surface extraction;
- [ ] material/property fields, not only occupancy.

## Phase 5 — CAE / generative engineering

- [x] SIMP + Optimality Criteria;
- [x] filtering/projection/continuation;
- [x] sparse regular-grid FEM;
- [x] matrix-free large-system path;
- [x] multi-load shared FEM context;
- [x] density/stress field output;
- [x] reusable robust three-field projection helpers;
- [x] KS and p-norm aggregation helpers;
- [x] generalized OC update;
- [x] overhang and minimum-feature diagnostics;
- [x] versioned C# `analysis.request/1` and `analysis.result/1` contracts;
- [ ] use multi-load aggregated sensitivity directly inside optimizer iteration;
- [ ] robust eroded/nominal/dilated objective end-to-end;
- [ ] displacement/stress constraints through KS/augmented Lagrangian;
- [ ] MMA/GCMMA backend;
- [ ] matrix-free CPU equivalence tests;
- [ ] GPU element-by-element operator;
- [ ] MGPCG/geometric multigrid;
- [ ] multiple RHS/block solve for load cases;
- [ ] modal/eigenfrequency and buckling constraints;
- [ ] nonlinear/material validation modes;
- [ ] density/stress-driven variable lattice/TPMS.

## Phase 6 — IO and manufacturing

- [x] modern STL reader/writer/audit toolkit;
- [x] STL topology component diagnostics;
- [x] triangle quality metrics: angle/aspect ratio/edge scales;
- [x] boundary loop/open-chain extraction;
- [x] scale-aware STL tolerance helper;
- [x] V1 operation-history research;
- [x] V1 run-compressed layer/toolpath planner + manufacturing estimates;
- [ ] move STL/OBJ functionality into `DCad.IO`;
- [ ] structured mesh repair actions with before/after report;
- [ ] PLY / 3MF;
- [ ] STEP through B-Rep adapter;
- [ ] project package embedding source + artifacts + analysis results;
- [ ] real slicer model: contours, offsets, infill, supports, process profiles;
- [ ] printability/minimum-wall/clearance checks.

## Phase 7 — numerical robustness and testing

- [x] `Function-Basket/ModernGeometryLab`;
- [x] triangulation area and n-2 invariants;
- [x] CSG volume identities;
- [x] deterministic point classification;
- [x] manifold topology checks;
- [x] adaptive 2D/3D orientation predicates with high-precision fallback;
- [x] deterministic segment intersection classification;
- [x] near-degenerate regression tests;
- [x] V2 generalized solid-angle point classification reference;
- [ ] exact/adaptive expansion predicates for cases beyond decimal range;
- [ ] triangle/triangle pathological corpus;
- [ ] coplanar/touching boolean corpus;
- [ ] property-based randomized geometry tests;
- [ ] parser/importer fuzzing;
- [ ] benchmark corpus: tiny/huge scales, degeneracies, millions of triangles/voxels.

## Phase 8 — performance architecture

- [ ] benchmark harness with wall-time + allocation + peak memory;
- [ ] operation-result cache by deterministic feature fingerprint;
- [ ] incremental rebuild only downstream of dirty feature;
- [ ] mesh BVH for selection/intersections;
- [ ] parallel tessellation and mesh validation;
- [ ] sparse brick traversal / SIMD morphology;
- [ ] GPU field visualization and compute kernels;
- [ ] out-of-core project/field data for very large scans and optimization grids.

# What not to merge literally

Не переносятся как архитектурная основа:

- копии `SharpGLForm`;
- global `worldX/worldY/worldZ` state;
- branch-specific `save.txt`;
- UI handlers, внутри которых живут CSG/FEM algorithms;
- random ray casting;
- fixed magic epsilons как единственная tolerance policy;
- mixed metre/Pascal and millimetre/MPa units;
- per-triangle/per-voxel immediate OpenGL submission;
- committed `bin`, `obj`, `__pycache__`, old NuGet binaries;
- OpenSCAD/Streamlit state as application document format;
- STL as internal canonical CAD model.

Они сохраняются только как история и regression material.

# Merge policy

Старые ветки не должны механически сливаться в `main`.

Правильный путь:

```text
research branch
      ↓
small isolated implementation
      ↓
unit/regression/CI
      ↓
stable contract
      ↓
port/adapt into Unified-CAD
      ↓
integration test
      ↓
main documents current product state
```

`Unified-CAD` должен постепенно стать единственным запускаемым приложением. Остальные ветки после переноса сильных частей остаются research/reference tracks, а не отдельными продуктами.
