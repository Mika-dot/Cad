# DCad unification plan

Цель репозитория — не сохранить восемь независимых демонстраторов, а собрать их идеи в одно CAD/CAE-приложение. Ветки рассматриваются как prototypes модулей.

## Что брать из каждой ветки

| Ветка | Сильная часть | Куда переносить |
|---|---|---|
| `OpenGL` | viewport, camera, interaction, scene visualization | `DCad.Viewport` |
| `VoxelСad` | sparse voxel/implicit CSG, TPMS/lattice, morphology | `DCad.Geometry.Voxel` |
| `FEM_Voxel` | voxel FEM, SIMP/OC topology optimization | `DCad.Analysis` / `DCad.Optimization` |
| `V1-Experiment` | contour extrusion, operation history, G-code, temperature field | `DCad.Geometry.Commands`, `DCad.Manufacturing`, `DCad.Fields` |
| `V2-Experiment` | triangle/polygon representation and CSG experiments | `DCad.Geometry.Mesh` |
| `Rendering-stl` | STL/application visualization ideas | `DCad.IO`, `DCad.Viewport.Adapters` |
| `Function-Basket` | historical algorithms | test/reference archive; useful functions migrate with tests |

## Target solution

```text
DCad.sln
  DCad.App                 desktop shell / docking / commands
  DCad.Viewport            camera, scene, picking, render abstraction
  DCad.Geometry.Core       vectors, transforms, bounds, IDs, operation graph
  DCad.Geometry.Mesh       triangle meshes, mesh repair, polygon CSG
  DCad.Geometry.Voxel      sparse voxels, SDF, TPMS, morphology
  DCad.Analysis            FEM, thermal and result fields
  DCad.Optimization        SIMP/topology/generative workflows
  DCad.Manufacturing       slicing, G-code, print/process checks
  DCad.IO                  STL/OBJ/PLY/3MF/VDB/project format
  DCad.Tests               geometry/math/regression tests
```

## Главный принцип интеграции

Ни voxel engine, ни FEM, ни polygon CSG не должны напрямую рисовать OpenGL.

Каждый kernel выдаёт данные через adapter:

```text
VoxelModel ─────┐
TriangleSolid ──┼──> SceneObject / MeshData / FieldLayer ──> Viewport
FEM Result ─────┤
STL/OBJ ────────┘
```

Так renderer можно менять независимо от математики.

## Общая модель документа

Вместо `save.txt` разных веток нужен operation/document model:

```text
Document
 ├─ Objects
 │   ├─ Geometry source
 │   ├─ Transform
 │   ├─ Appearance
 │   └─ Parameters
 ├─ Operation graph / history
 ├─ Analysis cases
 ├─ Result fields
 └─ Manufacturing jobs
```

Команды `AddBox`, `Extrude`, `Boolean`, `Voxelize`, `Optimize`, `GenerateLattice` должны быть сериализуемыми. Undo/redo строится на command/history layer, а не на копировании всего мира.

## Migration order

### Phase 0 — foundation
- [x] repository branch inventory
- [x] main README as project map
- [x] reusable OpenGL camera/scene/renderer shell
- [x] advanced VoxelCAD baseline
- [x] FEM/topology optimization baseline

### Phase 1 — common contracts
- [ ] вынести math/transform/bounds из OpenGL в `Geometry.Core`
- [ ] общий `IMeshSource` / `IFieldSource`
- [ ] adapters из `VoxelСad`, V2 mesh, STL, FEM
- [ ] единая система единиц: mm, N, MPa by default + explicit unit metadata

### Phase 2 — desktop CAD shell
- [ ] document tree
- [ ] property inspector
- [ ] command palette / toolbar
- [ ] undo/redo
- [ ] project save/load
- [ ] transform gizmo + snapping
- [ ] measurement and section tools

### Phase 3 — geometry convergence
- [ ] mesh import/repair
- [ ] mesh ↔ voxel/SDF conversion
- [ ] robust boolean pipeline
- [ ] SDF offsets/shell/blends
- [ ] adaptive meshing

### Phase 4 — CAE/generative
- [ ] FEM cases displayed as viewport field layers
- [ ] topology density preview in same viewport
- [ ] density/stress → lattice/TPMS
- [ ] parameter sweeps and compare variants

### Phase 5 — manufacturing
- [ ] slicing/G-code moved from V1 into separate service
- [ ] minimum wall / printability checks
- [ ] 3MF export and process metadata

### Phase 6 — renderer backend
- [ ] VBO/indexed mesh cache
- [ ] shader materials
- [ ] FBO ID picking
- [ ] MSAA
- [ ] clipping/section pass
- [ ] optional backend migration away from SharpGL

## What not to merge literally

- `SharpGLForm` copies from each branch;
- raw `worldX/worldY/worldZ` arrays;
- branch-specific save files;
- UI event handlers containing geometry algorithms;
- FEM units mixed between metres/Pascal and millimetres/MPa;
- per-voxel/per-triangle draw calls in application code.

Эти части являются историческим прототипом и заменяются общими contracts.
