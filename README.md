# DCad — experimental CAD / CAE platform

DCad — исследовательский репозиторий, в котором за несколько поколений появилось несколько независимых подходов к CAD: voxel solid modeling, polygon/triangle CSG, OpenGL viewport, STL tooling, thermal fields, G-code, voxel FEM и topology optimization.

Теперь цель репозитория — **не развивать эти ветки как отдельные приложения, а постепенно собрать их сильные части в один CAD/CAE продукт**.

Полный план объединения: [`docs/UNIFICATION_PLAN.md`](docs/UNIFICATION_PLAN.md).

## Текущая архитектурная идея

```text
                    DCad.App
                       │
        ┌──────────────┼──────────────┐
        │              │              │
  DCad.Viewport   Document/Commands   DCad.IO
        │              │              │
        ├──────────────┼──────────────┤
        │              │              │
 Geometry.Mesh   Geometry.Voxel   Analysis / FEM
        │              │              │
        └──────────────┼──────────────┘
                       │
                 Optimization
                       │
                 Manufacturing
```

Главный принцип: **ни voxel engine, ни FEM, ни polygon CSG не должны сами рисовать OpenGL или владеть UI**. Они должны отдавать `MeshData`, volumetric field или analysis layer в общий viewport.

## Карта веток

| Ветка | Что в ней ценного | Роль в будущем приложении | Текущий статус |
|---|---|---|---|
| **[`OpenGL`](https://github.com/Mika-dot/Cad/tree/OpenGL)** | camera, viewport interaction, scene rendering | `DCad.Viewport` | **обновлена: reusable CAD viewport** |
| **[`VoxelСad`](https://github.com/Mika-dot/Cad/tree/Voxel%D0%A1ad)** | sparse voxels, implicit CSG, TPMS/lattice, morphology, STL | `DCad.Geometry.Voxel` | **основная volumetric ветка** |
| **[`FEM_Voxel`](https://github.com/Mika-dot/Cad/tree/FEM_Voxel)** | voxel FEM, SIMP + OC, topology optimization | `DCad.Analysis`, `DCad.Optimization` | **основная CAE/optimization ветка** |
| [`V1-Experiment`](https://github.com/Mika-dot/Cad/tree/V1-Experiment) | contour extrusion, boolean subtraction, history, G-code, temperature | commands, fields, manufacturing | историческая, но содержит полезные подсистемы |
| [`V2-Experiment`](https://github.com/Mika-dot/Cad/tree/V2-Experiment) | triangle representation и polygon CSG | `DCad.Geometry.Mesh` | экспериментальная mesh-ветка |
| [`Rendering-stl`](https://github.com/Mika-dot/Cad/tree/Rendering-stl) | STL/application visualization, machine UI ideas | `DCad.IO`, viewport adapters | archive / source of features |
| [`Function-Basket`](https://github.com/Mika-dot/Cad/tree/Function-Basket) | старые алгоритмы и функции | reference/test archive | archive |

---

# OpenGL — общий viewport foundation

`OpenGL` раньше был простым SharpGL demo: два куба, fixed-function drawing и камера, логика которой находилась непосредственно в WinForms events.

В обновлении 2026 ветка перестроена в reusable viewport:

- отдельный `Camera3D`;
- orbit / pan / zoom;
- Perspective и Orthographic;
- стандартные Front/Back/Left/Right/Top/Bottom/Isometric views;
- `Fit scene` / focus selected;
- `Scene3D` и `SceneObject`;
- `MeshData` как общий triangle-mesh contract;
- CPU ray picking;
- scene tree + `PropertyGrid`;
- grid + XYZ axes;
- lighting;
- `Shaded`, `ShadedEdges`, `Wireframe`, `XRay`;
- selection outline;
- toolbar/status UI;
- SharpGL `2.3.0.1` → `3.1.1`;
- Windows CI.

Эта ветка должна стать визуальным фундаментом для остальных модулей, а не очередным отдельным CAD demo.

Следующий шаг renderer-а: GPU mesh cache через VBO/index buffers, shaders, framebuffer ID picking, MSAA и section/clipping passes. API `Camera3D / Scene3D / MeshData` при этом должен сохраниться, чтобы backend можно было заменить независимо от CAD kernels.

---

# VoxelСad — volumetric geometry kernel

`VoxelСad` — направление для geometry-as-volume / field CAD.

Реализовано:

- sparse occupancy grid;
- CSG union / difference / intersection;
- implicit/SDF-like rasterization API;
- box, sphere, cylinder, torus, arbitrary-axis capsule;
- Gyroid и Schwarz-P TPMS;
- BCC lattice;
- dilation / erosion / opening / closing;
- majority smoothing;
- connected-component cleanup;
- volume / surface metrics;
- greedy STL meshing;
- JSON scene DSL;
- headless JSON → STL pipeline;
- FEM bridge с согласованными mm / N / MPa единицами;
- CI smoke pipeline.

Roadmap ветки: binary occupancy → sparse bricks → persistent SDF/level set → adaptive VDB/octree → Marching Cubes / Dual Contouring → GPU/NanoVDB.

---

# FEM_Voxel — CAE и topology optimization

`FEM_Voxel` содержит уже не просто voxel visualization, а расчётное направление:

- density-based SIMP;
- Optimality Criteria update;
- density/sensitivity filtering;
- projection/continuation;
- sparse regular-grid FEM assembly;
- connectivity-aware final geometry;
- OpenSCAD pipeline;
- metrics and optimization artifacts.

Целевая интеграция:

```text
CAD geometry / voxel field
          ↓
       FEM solve
          ↓
 density / stress / displacement fields
          ↓
 Viewport field layer
          ↓
 topology / lattice / TPMS generation
```

В итоге stress/density должны не только показываться картинкой, а напрямую управлять толщиной lattice/TPMS или локальным материалом.

---

# V1-Experiment — не выбрасывать

Несмотря на возраст, `V1-Experiment` содержит несколько вещей, которых нет в более новых ветках:

- contour → extrusion;
- сохранение не только результата, а операций построения;
- boolean subtraction через voxel representation;
- temperature field;
- G-code generation.

Из этой ветки стоит переносить **концепции**, а не старые массивы и UI:

```text
ExtrudeCommand
BooleanCommand
TemperatureField
ManufacturingJob / GCodeExporter
```

Это потенциально станет началом history/command system итогового CAD.

---

# V2-Experiment — polygon / mesh kernel

`V2-Experiment` нужен как отдельное направление, потому что не всё стоит переводить в voxels.

Его задача в unified DCad:

- triangle mesh representation;
- mesh transforms;
- mesh repair;
- polygon boolean experiments;
- mesh ↔ voxel/SDF conversion;
- импорт/экспорт STL/OBJ/PLY.

В перспективе DCad должен уметь одновременно держать:

```text
B-Rep / imported mesh
        ↕
 triangle mesh
        ↕
 voxel / SDF field
```

и выбирать представление под конкретную задачу.

---

# Что должно получиться в итоге

Целевая solution structure:

```text
DCad.sln
  DCad.App
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

## Общий document model

Вместо отдельных `save.txt`, локальных массивов и branch-specific state нужен единый документ:

```text
Document
 ├─ Objects
 │   ├─ Geometry source
 │   ├─ Transform
 │   ├─ Parameters
 │   └─ Appearance
 ├─ Operation history
 ├─ Analysis cases
 ├─ Result fields
 └─ Manufacturing jobs
```

Так появятся нормальные:

- undo / redo;
- parametric operation history;
- project save/load;
- object tree;
- common selection;
- one viewport for mesh/voxel/FEM;
- reproducible calculations;
- batch/headless processing.

## Ближайший порядок объединения

1. Общие math/transform/bounds/contracts.
2. `VoxelСad → MeshData` adapter.
3. `V2 → MeshData` adapter.
4. STL loader → `MeshData`.
5. FEM result → viewport field layer.
6. Document + command history.
7. Undo/redo + project format.
8. Measurement, snapping, section/clipping.
9. Mesh ↔ voxel/SDF conversion.
10. Unified generative workflow: FEM → density/stress → lattice/TPMS → export.

---

# Современные технические ориентиры

Для volumetric части наиболее логичны OpenVDB / NanoVDB и в research-сценариях fVDB. Для surface extraction — Marching Cubes и Dual Contouring/QEF. Для render backend текущий SharpGL используется как compatibility bridge, но архитектура viewport должна позволить перейти на современный VBO/shader/FBO pipeline или другой .NET graphics backend без изменения geometry kernels.

Базовые работы:

- Frisken et al., *Adaptively Sampled Distance Fields*, SIGGRAPH 2000 — https://doi.org/10.1145/344779.344899
- Ju et al., *Dual Contouring of Hermite Data*, SIGGRAPH 2002 — https://doi.org/10.1145/566570.566586
- Museth, *VDB: High-Resolution Sparse Volumes with Dynamic Topology*, TOG 2013 — https://doi.org/10.1145/2487228.2487235
- Kämpe et al., *High Resolution Sparse Voxel DAGs*, TOG 2013 — https://doi.org/10.1145/2461912.2462024
- Williams et al., *fVDB*, SIGGRAPH 2024 — https://doi.org/10.1145/3658226

DCad теперь рассматривается не как набор старых экспериментов, а как **набор уже проверенных идей, которые постепенно сводятся к общей CAD/CAE архитектуре**.
