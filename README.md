# DCad — experimental CAD / CAE platform

DCad — исследовательский CAD/CAE-проект, который вырос из нескольких независимых экспериментов: voxel solid modeling, polygon CSG, OpenGL rendering, STL tooling, operation history, thermal fields, G-code, FEM и topology optimization.

С 2026 года цель репозитория другая: **не поддерживать ветки как отдельные программы, а собрать их сильные идеи в одно приложение**.

> Активная интеграционная ветка: **[`Unified-CAD`](https://github.com/Mika-dot/Cad/tree/Unified-CAD)**  
> Подробный checklist объединения: [`docs/UNIFICATION_PLAN.md`](docs/UNIFICATION_PLAN.md)

## Что уже объединено

Первый рабочий vertical slice единого приложения уже существует:

```text
.dcad modeling language
          ↓
     DCad.Language
          ↓
   IModelingKernel
          ↓
 robust polygon CSG
          ↓
 indexed mesh + validation
      ┌───────┴────────┐
      ↓                ↓
   DCad.Cli         DCad.App
     OBJ         OpenTK viewport
```

В `Unified-CAD` сейчас есть:

- .NET 8 solution;
- double-precision geometry core;
- indexed triangle mesh;
- centralized tolerance policy;
- surface area / signed volume / AABB;
- topology validation: degenerate, boundary, non-manifold and winding errors;
- deterministic triangulation простых concave polygons;
- deterministic point-in-solid без случайного ray casting;
- robust triangle-solid CSG adapter;
- box / sphere / cylinder;
- union / difference / intersection;
- translate / rotate / scale;
- собственный `.dcad` language с параметрами и единицами;
- CLI → OBJ;
- OpenTK shader viewer;
- regression tests;
- Windows CI: build → tests → language → CSG → validated mesh.

Это не замена всех веток сразу. Это **общий каркас**, в который они постепенно подключаются.

---

# Карта репозитория

| Ветка | Основная идея | Что уже модернизировано | Роль в едином DCad |
|---|---|---|---|
| **[`Unified-CAD`](https://github.com/Mika-dot/Cad/tree/Unified-CAD)** | единое приложение | Core + mesh CSG + DSL + CLI + OpenTK + tests | **integration branch** |
| **[`V2-Experiment`](https://github.com/Mika-dot/Cad/tree/V2-Experiment)** | polygon / triangle CAD | `ModernV2/Production` + managed BSP reference | `DCad.Geometry.Mesh` |
| **[`VoxelСad`](https://github.com/Mika-dot/Cad/tree/Voxel%D0%A1ad)** | voxel / implicit / field CAD | primitives, CSG, morphology, TPMS, lattice, greedy STL, CLI | `DCad.Geometry.Voxel` |
| **[`FEM_Voxel`](https://github.com/Mika-dot/Cad/tree/FEM_Voxel)** | FEM + topology optimization | SIMP/OC, sparse FEM, field exchange | `DCad.Analysis`, `DCad.Optimization` |
| **[`OpenGL`](https://github.com/Mika-dot/Cad/tree/OpenGL)** | CAD viewport / renderer | reusable Camera/Scene + `ModernRenderer` VBO/VAO/shaders | `DCad.Viewport` |
| **[`V1-Experiment`](https://github.com/Mika-dot/Cad/tree/V1-Experiment)** | first voxel CAD | `Modern/VoxelEngine`, `OperationHistory` + legacy G-code/temp | `DCad.Document`, Manufacturing, fields |
| **[`Rendering-stl`](https://github.com/Mika-dot/Cad/tree/Rendering-stl)** | STL / visualization / application prototype | `ModernStl` toolkit | `DCad.IO` |
| **[`Function-Basket`](https://github.com/Mika-dot/Cad/tree/Function-Basket)** | старые math experiments | `ModernGeometryLab` regression suite | `DCad.Tests` |

---

# V2-Experiment — explicit polygon geometry

Изначальный V2 был одним из самых интересных экспериментов проекта: CAD-тело представлялось набором треугольников, а над meshes выполнялись boolean operations.

Но старая реализация содержала фундаментальные numerical-geometry проблемы:

- line intersection через slope/intercept;
- деление на ноль для вертикальных/почти параллельных линий;
- fixed epsilon без учёта масштаба модели;
- rounding intersection points;
- ad-hoc triangulation;
- random ray casting для проверки точки внутри solid;
- отсутствие строгой проверки manifold topology результата.

Теперь ветка разделена:

```text
V2-Experiment
├── GL/                       historical implementation
└── ModernV2/
    ├── *.cs                  readable managed BSP reference
    └── Production/           current production polygon path
```

`ModernV2/Production` содержит тот же подход, который уже проверяется в `Unified-CAD`:

- indexed `Mesh3d`;
- double precision;
- mesh validator;
- deterministic polygon triangulation;
- deterministic point-in-solid;
- production CSG adapter;
- `.dcad` language;
- OpenTK application;
- tests and CI.

Managed BSP оставлен намеренно: он полезен как понятная исследовательская реализация CSG, но production app не должен зависеть от него на сложной геометрии.

---

# VoxelСad — volumetric / implicit CAD

`VoxelСad` — второй полноценный geometry backend. Он работает не с явной triangle surface, а с occupancy/implicit field.

Реализовано:

### Geometry

- sparse voxel occupancy;
- box;
- sphere;
- cylinder;
- torus;
- arbitrary-axis capsule/strut;
- generic implicit field rasterization.

### CSG

- union;
- difference;
- intersection.

### Morphology

- dilation;
- erosion;
- opening;
- closing;
- 6/18/26-neighbourhood;
- majority smoothing;
- largest connected component cleanup.

### Architected materials

- Gyroid TPMS;
- Schwarz-P TPMS;
- BCC lattice.

### Output / engineering

- volume and surface metrics;
- surface voxels;
- greedy STL meshing;
- JSON operation scene;
- headless scene → STL CLI;
- FEM bridge;
- corrected mm / N / MPa convention;
- CI smoke test.

Дальнейшее направление:

```text
binary occupancy
      ↓
sparse bricks/chunks
      ↓
narrow-band SDF / level set
      ↓
adaptive octree / VDB
      ↓
Dual Contouring / QEF
      ↓
GPU sparse fields
```

В едином приложении mesh и voxel — не конкурирующие CAD версии. Это разные представления одной geometry graph:

```text
analytic / operations
        │
    ┌───┴────┐
    │        │
 Triangle   SDF/Voxel
   Mesh       Field
    │        │
    └───↔────┘
```

---

# FEM_Voxel — CAE / topology optimization

Ветка выросла из voxel FEM эксперимента в отдельный analysis backend.

Реализовано:

- regular voxel/hexa grid;
- sparse FEM assembly;
- density-based SIMP;
- Optimality Criteria update;
- sensitivity/density filtering;
- projection and continuation;
- connectivity-aware final geometry;
- multi-load experiments;
- density/stress/result fields;
- artifacts and metrics;
- field interchange format;
- единицы mm / N / MPa.

Целевая связь:

```text
CAD object
    ↓
voxel / FEM domain
    ↓
loads + supports + material
    ↓
FEM / SIMP
    ↓
stress / displacement / density
    ├──→ viewport overlays
    └──→ lattice / TPMS generator
```

То есть FEM должен стать не отдельным Python UI, а analysis service общего документа.

---

# OpenGL — viewport and rendering research

Эта ветка начиналась с учебного SharpGL cube demo.

Сейчас здесь два уровня:

### Compatibility viewport

- `Camera3D`;
- orbit / pan / zoom;
- perspective / orthographic;
- standard CAD views;
- scene objects;
- selection;
- grid / axes;
- shaded / edges / wireframe / x-ray;
- property editing.

### `ModernRenderer`

- OpenTK 4;
- OpenGL core profile;
- VBO / VAO / EBO;
- indexed triangle drawing;
- vertex + fragment shaders;
- normals;
- lighting;
- scalar field / heatmap attribute;
- wireframe mode;
- modern camera interaction.

В `Unified-CAD` уже используется тот же современный OpenTK direction. Следующий этап — перенести лучшие CAD interaction-функции из compatibility viewport в общий renderer, а не поддерживать два UI.

---

# V1-Experiment — operation history, manufacturing and fields

V1 не нужно выбрасывать из-за возраста. В ней появились идеи, которые важны для полноценного CAD:

- contour → extrusion;
- voxel solid editing;
- сохранение операций, а не только финальной геометрии;
- transforms;
- G-code;
- temperature field.

В ветке уже появился modern layer:

- `Modern/VoxelEngine`;
- `Modern/OperationHistory`;
- modern demo UI.

Главная ценность V1 для unified app теперь не её renderer, а концепция:

```text
Document
  └── Operation History
        ├── Create
        ├── Extrude
        ├── Transform
        ├── Boolean
        ├── Voxelize
        ├── Analyze
        └── Manufacturing
```

Именно отсюда должен вырасти persistent operation graph + undo/redo.

---

# Rendering-stl — IO layer

Историческая часть ветки — большой WinForms/machine hackathon prototype.

Полезный современный результат — `ModernStl`, который должен быть перенесён в `DCad.IO`.

Итоговый IO layer должен отвечать за:

- STL binary / ASCII;
- OBJ;
- PLY;
- 3MF;
- mesh validation/repair report;
- единицы и metadata;
- позже STEP через B-Rep adapter;
- project files.

Renderer не должен сам загружать STL, а geometry kernel не должен сам писать файл на диск.

---

# Function-Basket — geometry regression lab

Старые каталоги «точка внутри», «пересечения двух треугольников» и другие experiments сохранены как исторический corpus.

Добавлен `ModernGeometryLab`:

- .NET 8 tests;
- concave triangulation area invariant;
- `n-2` polygon triangulation invariant;
- rejection of self-intersecting polygons;
- CSG volume identities;
- closed/oriented manifold checks;
- repeatable point-in-solid tests.

Это будущий `DCad.Tests`: любой новый geometry bug должен сначала появляться здесь как воспроизводимый test case.

---

# Целевая архитектура

```text
                         DCad.App
                            │
                    DCad.Document
                            │
                    DCad.Language
                            │
                    Operation Graph
                            │
                 Geometry Kernel API
            ┌───────────────┼──────────────┐
            │               │              │
        Mesh/Polygon     Voxel/SDF       B-Rep
            │               │              │
            └───────────┬───┴──────────────┘
                        │
                Mesh / Field contracts
          ┌─────────────┼───────────────┐
          │             │               │
     DCad.Viewport   DCad.IO       DCad.Analysis
                                      │
                                DCad.Optimization
                                      │
                               lattice / TPMS
                                      │
                             DCad.Manufacturing
```

## Почему несколько geometry backends

Одного универсального представления недостаточно:

- B-Rep лучше для точной инженерной parametric geometry, STEP/NURBS;
- triangle mesh удобен для imported geometry, rendering, repair и быстрых surface operations;
- voxel/SDF удобен для topology, scans, morphology, lattices, complex booleans и fields.

Задача DCad — дать им **один document/operation layer**, а не выбрать одно представление и заставить его решать все задачи.

---

# Modeling language

Уже работающий первый вариант:

```text
param width = 60mm;
param depth = 40mm;
param height = 8mm;

let body = box(width, depth, height);
let hole = cylinder(20mm, 5mm);
let left  = translate(hole, -20mm, 0mm, 0mm);
let right = translate(hole,  20mm, 0mm, 0mm);

solid result = body - left - right;
```

Current operators:

| Syntax | Operation |
|---|---|
| `+` | union |
| `-` | difference |
| `&` | intersection |
| `box(...)` | box |
| `sphere(...)` | sphere |
| `cylinder(...)` | cylinder |
| `translate(...)` | translation |
| `rotate(...)` | rotation |
| `scale(...)` | scale |

Units: `mm`, `cm`, `m`, `deg`.

Дальше язык должен получить:

- persistent AST / operation graph;
- sketches;
- constraints;
- extrude / revolve / sweep / loft;
- arrays / patterns / mirror;
- named selections;
- analysis cases;
- manufacturing commands;
- reusable functions/components.

---

# Инженерные правила проекта

Чтобы старые numerical bugs не вернулись в общий kernel:

1. geometry math — `double` по умолчанию;
2. tolerance задаётся централизованно и зависит от масштаба;
3. нельзя округлять geometry coordinates как способ «починить» intersection;
4. random ray casting не является production point-in-solid algorithm;
5. solid mesh проходит topology validation;
6. boundary/non-manifold errors не скрываются;
7. units хранятся явно;
8. UI не содержит geometry/FEM algorithms;
9. renderer получает готовый mesh/field packet;
10. каждый найденный баг получает regression test.

---

# Ближайшие задачи

1. Persistent `DCad.Document` + operation graph.
2. Undo / redo + project save/load.
3. Общий `ScenePacket / FieldLayer` между geometry/FEM/renderer.
4. `VoxelСad` adapter в `Unified-CAD`.
5. mesh ↔ voxel/SDF conversion.
6. перенести STL toolkit в `DCad.IO`.
7. подключить FEM request/result protocol.
8. stress/displacement/density overlays в общем viewport.
9. sketch/constraint subsystem.
10. B-Rep/STEP backend.
11. FEM → stress-driven lattice/TPMS workflow.
12. manufacturing/slicing/G-code из V1.

Репозиторий теперь рассматривается как **эволюция одного CAD/CAE-движка**, где старые ветки сохраняют историю алгоритмов, а современные каталоги и `Unified-CAD` постепенно формируют единое приложение.
