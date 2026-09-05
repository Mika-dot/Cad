# VoxelCAD roadmap: от occupancy grid к field-based CAD

Этот документ фиксирует техническое направление `VoxelСad` после обновления 2026. Цель — развивать проект как исследовательский field/voxel CAD, а не пытаться копировать классический B-Rep CAD на кубиках.

## 1. Текущее ядро: sparse binary occupancy

Сейчас координата `(i,j,k)` означает занятый voxel cell. `HashSet` хорош как минимальный reference backend: отрицательные координаты, отсутствие глобального bounding box, O(1)-подобный доступ и простые CSG/morphology операции.

Проблема — overhead одного элемента `HashSet` велик, а соседние воксели физически не лежат рядом в памяти. На моделях с миллионами/десятками миллионов voxels это становится главным bottleneck.

### Следующий backend: sparse bricks

Разбить пространство на chunks/bricks 8³ или 16³. Ключ chunk хранится в hash map, occupancy внутри chunk — bitset.

Для 8³ chunk требуется 512 bits = 64 bytes чистой occupancy, после чего:

- neighbour queries становятся cache-friendly;
- morphology может работать целыми bit words;
- surface extraction идёт chunk-wise;
- пустые chunks не существуют;
- Morton/Hilbert ordering улучшает locality при batch traversal.

На этом уровне стоит сделать интерфейс `IVoxelGrid`, чтобы `HashSetVoxelGrid`, `BrickVoxelGrid` и будущий VDB backend были взаимозаменяемы.

## 2. Не occupancy, а sparse signed distance field

Binary occupancy всегда квантует поверхность до voxel boundary. Современный field CAD должен хранить хотя бы narrow-band SDF вокруг поверхности.

Минимальная структура cell/voxel data:

```text
active / topology
signed_distance
material_id or material fractions
optional density
optional temperature / stress / design variables
```

SDF открывает:

- sub-voxel surface reconstruction;
- smooth union / smooth subtraction;
- offset/thickness как `d' = d - offset`;
- shell как диапазон расстояний;
- fillet/blend через field operators;
- нормали из `grad(SDF)`;
- geometry-aware sampling для FEM/optimization.

Для CAD-операций нельзя после каждого шага бездумно бинаризовать field. Boolean/offset/blend должны работать над distance values, а rasterized occupancy использоваться как derived representation.

## 3. Adaptive hierarchy: octree / VDB

Фиксированный voxel size не нужен в пустом объёме и избыточен на плоских участках. Нужен adaptive representation:

- coarse cells в однородных областях;
- refinement около zero level set;
- дополнительное refinement по curvature, stress gradient или manufacturing constraints.

Историческая база: Adaptive Distance Fields → sparse octrees → VDB. Для практического промышленного backend наиболее зрелая точка входа — OpenVDB.

OpenVDB отделяет tree topology от transform/world space и уже содержит production-grade level set, CSG, morphology, filters, mesh-to-volume и volume-to-mesh операции.

## 4. Surface extraction: три режима вместо одного

### 4.1 Greedy voxel meshing — implemented

Используется, когда нужна намеренно блочная геометрия или максимально простой STL. Соседние копланарные voxel faces объединяются в прямоугольники.

### 4.2 Marching Cubes

Baseline для scalar/SDF grid. Даёт гладкую поверхность, широко поддерживается, легко параллелится. Недостаток — sharp CAD features могут размываться и требует аккуратной обработки topology ambiguities.

### 4.3 Dual Contouring + QEF

При наличии Hermite data (edge intersections + normals) Dual Contouring лучше подходит CAD-подобной геометрии: может сохранять углы/рёбра и естественно работает на adaptive octree.

Production target: adaptive Dual Contouring/QEF с crack-free stitching между уровнями LOD.

## 5. CSG как field algebra

Для exact binary occupancy достаточно set operations. Для field CAD нужны функции:

```text
union:        min(dA, dB)
intersection: max(dA, dB)
difference:   max(dA, -dB)
offset:       d - r
shell:        abs(d) - t/2
```

Дополнительно — polynomial/exponential smooth-min для blends. При этом важно различать CAD semantics и purely visual blending: smooth CSG меняет размеры и должен контролироваться параметрически.

## 6. Lattice / TPMS как first-class geometry

Gyroid и Schwarz-P уже добавлены в occupancy baseline. Следующий уровень:

- Diamond / I-WP / Neovius TPMS;
- BCC / FCC / octet-truss / Kelvin lattices;
- spatially varying period, thickness and orientation;
- mapping lattice density from FEM stress / topology optimization field;
- boundary-conformal lattice: cell field следует distance-to-boundary/parameterization, а не тупо обрезается box'ом;
- minimum printable feature constraints.

Практический pipeline:

```text
load/design outer SDF
        ↓
FEM / topology density field
        ↓
map density -> lattice thickness / TPMS offset
        ↓
intersect with outer SDF
        ↓
manufacturing cleanup
        ↓
adaptive meshing
```

## 7. Связка с FEM и веткой FEM_Voxel

Текущий `VoxelСad` FEM bridge соединяет центры соседних voxels стержнями. Это полезно как быстрый visual demonstrator, но это не voxel solid FEM.

Нормальная инженерная линия:

1. 8-node hexahedral element per active design voxel или matrix-free stencil formulation;
2. sparse global stiffness matrix;
3. PCG/MINRES + multigrid/preconditioner;
4. SIMP/RAMP material interpolation;
5. sensitivity filtering + Heaviside projection;
6. compliance / displacement / stress constraints;
7. connectivity and manufacturability constraints;
8. density field → SDF/lattice geometry conversion.

Ветка `FEM_Voxel` уже содержит SIMP + OC направление и должна в перспективе использовать общий geometry/grid backend с `VoxelСad`.

## 8. GPU architecture

CPU C# `HashSet` не должен быть конечным compute backend.

### Вариант A — C++ core + C# shell

```text
C# UI / scripting
      ↓ P/Invoke / C API
C++ geometry kernel
      ↓
OpenVDB (CPU) / NanoVDB (GPU)
```

Плюсы: можно сохранить WinForms/новый .NET UI и при этом использовать зрелую volumetric ecosystem.

### Вариант B — modern .NET + compute shaders

Перенести frontend на .NET 8+, рендер на OpenGL/Vulkan/Direct3D abstraction. Sparse bricks можно хранить в SSBO/storage buffers, morphology/meshing делать compute shaders.

### Вариант C — research Python/CUDA sidecar

fVDB/PyTorch используется для differentiable sparse operations, neural priors, point/mesh reconstruction, generative experiments. CAD kernel остаётся deterministic; ML не должен становиться единственным источником геометрической истины.

## 9. Differentiable / learned CAD — где это действительно полезно

Не надо заменять CSG нейросетью. Полезные зоны:

- reconstruction scan/point cloud → SDF;
- proposal generation для topology/lattice;
- learned surrogate FEM для ранжирования вариантов;
- differentiable optimization of field parameters;
- semantic selection/segmentation volumetric parts;
- defect healing suggestions.

Результат обязательно проходит deterministic geometry validation: connectivity, minimum wall, collisions, dimensions, watertight mesh.

## 10. Multi-material and property fields

Voxel CAD становится особенно интересным, когда voxel — не bool.

План cell attributes:

```text
material_id
volume_fraction[materials]
density
E, nu or material reference
thermal_conductivity
temperature
stress / strain derived fields
manufacturing mask
```

Для multi-material interface нужен multi-label level set или vector/material field, а mesher должен уметь строить согласованные interfaces без gaps/overlaps.

## 11. Import/export

Приоритет:

1. STL/OBJ/PLY mesh voxelization;
2. STL/OBJ/PLY surface export;
3. OpenVDB `.vdb` volumes;
4. 3MF для additive manufacturing и material metadata;
5. point clouds / scan data;
6. heightmaps / image stacks / CT volumes.

STEP/IGES import не следует реализовывать вручную: при необходимости использовать OpenCASCADE для tessellation/B-Rep bridge, затем переводить результат в field representation.

## 12. Manufacturing-aware operators

Field representation позволяет сделать CAD «знающим о производстве»:

- minimum wall / minimum channel checks;
- morphological compensation for printer/process resolution;
- trapped-volume detection;
- overhang field for additive manufacturing;
- tool accessibility field for subtractive manufacturing;
- anisotropic dilation/erosion;
- support volume estimation;
- print-direction dependent lattice constraints.

## 13. Benchmark suite

Любой новый backend надо сравнивать не по ощущениям, а по:

- active voxels / occupied volume;
- peak RAM;
- build/CSG/morphology time;
- random access ns/query;
- STL triangle count and export time;
- Hausdorff/RMS surface error against analytic SDF;
- connectivity preservation;
- FEM solve time / residual;
- GPU transfer and kernel time where applicable.

Наборы размеров: 64³, 128³, 256³, 512³ и sparse world с одинаковым числом active voxels при разных extents.

## 14. Этапы реализации

### Phase 1 — завершить usable voxel kernel

- [x] implicit primitive rasterization
- [x] sphere/cylinder/torus/capsule
- [x] boolean model ops
- [x] morphology 6/18/26
- [x] connectivity cleanup
- [x] Gyroid / Schwarz-P / BCC
- [x] greedy STL
- [x] JSON DSL + headless CLI
- [ ] unit tests for analytic primitives
- [ ] benchmark runner
- [ ] mesh voxelizer

### Phase 2 — sparse brick backend

- [ ] `IVoxelGrid`
- [ ] 8³/16³ bit bricks
- [ ] Morton/Hilbert chunk ordering
- [ ] parallel surface extraction
- [ ] bit-parallel morphology

### Phase 3 — SDF/level set

- [ ] narrow-band float/half SDF
- [ ] redistance / fast sweeping
- [ ] field CSG / offsets / shell / blends
- [ ] Marching Cubes
- [ ] normals/curvature

### Phase 4 — adaptive geometry

- [ ] octree/VDB backend
- [ ] adaptive refinement
- [ ] Dual Contouring + QEF
- [ ] LOD / crack-free transitions

### Phase 5 — engineering + optimization

- [ ] common grid backend with `FEM_Voxel`
- [ ] hexa FEM / matrix-free solver
- [ ] density-to-lattice mapping
- [ ] print constraints
- [ ] multi-material fields

### Phase 6 — GPU / differentiable research

- [ ] NanoVDB backend
- [ ] CUDA/Vulkan compute kernels
- [ ] fVDB bridge
- [ ] differentiable field parameter optimization
- [ ] learned reconstruction/proposal modules with deterministic validation

## References

- S. Frisken et al., *Adaptively Sampled Distance Fields*, SIGGRAPH 2000 — https://doi.org/10.1145/344779.344899
- T. Ju et al., *Dual Contouring of Hermite Data*, SIGGRAPH 2002 — https://doi.org/10.1145/566570.566586
- K. Museth, *VDB: High-Resolution Sparse Volumes with Dynamic Topology*, ACM TOG 2013 — https://doi.org/10.1145/2487228.2487235
- V. Kämpe et al., *High Resolution Sparse Voxel DAGs*, ACM TOG 2013 — https://doi.org/10.1145/2461912.2462024
- F. Williams et al., *fVDB*, ACM TOG / SIGGRAPH 2024 — https://doi.org/10.1145/3658226
- OpenVDB documentation / releases — https://www.openvdb.org/documentation/doxygen/
