# FEM_Voxel — topology optimization & engineering fields for DCad

Эта ветка отвечает за расчётное направление будущего DCad: voxel/hexahedral FEM, density-based topology optimization и преобразование расчётных полей обратно в редактируемую геометрию.

Главное изменение предыдущей версии — старый жадный алгоритм поштучного удаления вокселей заменён на **SIMP + Optimality Criteria**. В текущем обновлении ветка дополнительно подготовлена к объединению с V1/VoxelCAD и общим renderer.

## Что уже есть

- density-based SIMP + OC;
- sensitivity/density filtering;
- Heaviside projection + continuation;
- регулярная hexahedral FEM grid;
- sparse matrix assembly;
- matrix-free large-system solve;
- CG / BiCGSTAB / ILU fallback;
- post-processing с сохранением связности;
- stress/displacement/compliance metrics;
- Streamlit UI и CLI;
- OpenSCAD input/output pipeline;
- согласованные единицы: mm, N, N/mm² (MPa).

## Новое: DCad field interchange

После штатного расчёта теперь сохраняется:

```text
final_fields.npz
```

Это не картинка и не OpenSCAD script, а машинно-читаемое volumetric state:

- grid origin;
- voxel size;
- grid shape;
- design / anchor / load / obstacle / preserve masks;
- connector mask;
- density field;
- von Mises stress field;
- FEM element ids;
- JSON manifest с единицами и конфигурацией.

Формат описан в [`docs/DCAD_FIELD_FORMAT.md`](docs/DCAD_FIELD_FORMAT.md).

Именно этот файл должен стать мостом:

```text
FEM_Voxel
    |
    | final_fields.npz
    v
VoxelCAD / V1 --------> OpenGL renderer
    |                         |
    +---- geometry edits -----+
```

То есть результат topology optimization теперь можно передавать в общий CAD без парсинга картинок, `final_connector.scad` или внутреннего состояния Streamlit.

## Новое: multi-load API

Добавлен `openscad_gen/multiload.py`.

Он позволяет решать несколько load cases на **одном построенном FEM context**:

```python
from openscad_gen.multiload import LoadCase, solve_load_cases

cases = [
    LoadCase("nominal", force_nominal, 1.0),
    LoadCase("side", force_side, 0.5),
    LoadCase("reverse", force_reverse, 0.25),
]

result = solve_load_cases(
    voxel_scene,
    ctx,
    rho_phys,
    penal=3.0,
    cases=cases,
    logger=logger,
)
```

Получаются:

- weighted compliance;
- worst displacement;
- worst connector von Mises;
- aggregated design energy для последующего multi-load sensitivity update.

Это foundation для optimization не под один идеальный случай нагрузки, а под реальное семейство режимов.

## Установка

```bash
python -m pip install -r requirements.txt
```

## CLI

```bash
python main.py --scene test.scad
```

## UI

```bash
streamlit run ui_app.py
```

## Основные параметры

- `voxel_size`
- `target_volume_ratio`
- `max_displacement`
- `filter_radius`
- `penal_max`
- `density_threshold`
- `solver`
- matrix-free thresholds / tolerances

Для крупных сеток оставляйте `solver: "auto"`; exploratory runs разумно начинать с voxel 1.5–2.0 mm, затем делать refinement.

## Артефакты запуска

- `final_connector.scad`
- `final_scene_preview.scad`
- **`final_fields.npz`**
- `metrics.csv`
- `summary.json`
- `animation.gif`
- PNG stress/geometry frames

## Important note on units

OpenSCAD geometry здесь считается в **millimeters**, forces — **newtons**, а material stiffness/yield — **N/mm² (MPa)**. Передача SI pascal values в mm-model завышает stiffness примерно на `10^6`.

## Что ещё развивать

### Optimization

1. подключить `MultiLoadResult.aggregated_design_energy` непосредственно в OC/MMA iteration;
2. добавить MMA/GCMMA backend для нескольких ограничений;
3. p-norm/KS stress aggregation;
4. buckling/eigenfrequency constraints;
5. robust design: erosion/intermediate/dilation projections;
6. passive solid/void masks как first-class constraints;
7. symmetry / extrusion / draw-direction manufacturing constraints;
8. continuation по mesh refinement, а не только penal/beta.

### Solver

1. AMG/multigrid preconditioner;
2. GPU sparse/matrix-free backend;
3. domain decomposition;
4. reuse/preconditioner warm-start между соседними optimization iterations;
5. multi-right-hand-side solve для load cases.

### Geometry coupling

1. density → smooth SDF;
2. stress/density → variable-thickness lattice/TPMS;
3. final field → VDB/3MF;
4. mesh/VoxelCAD boundary conditions;
5. shared material database.

## Роль в едином приложении

`FEM_Voxel` не должен становиться ещё одним CAD UI. Его конечная форма — headless analysis/optimization service/library:

```text
DCad.App
   |
   +-- Geometry.Volume / Mesh
   +-- Analysis.FEM
   +-- Analysis.Topology      <- FEM_Voxel
   +-- Fields                 <- density/stress/displacement
   +-- Rendering
```

UI лишь задаёт boundary/load/design regions и показывает fields; FEM остаётся отдельным вычислительным модулем.
