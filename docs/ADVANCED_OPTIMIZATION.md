# FEM_Voxel — advanced optimization track

`FEM_Voxel` больше не должен быть только демонстрацией SIMP+OC. Цель ветки — стать вычислительным CAE/optimization backend для `Unified-CAD`.

## Уже добавлено

Модуль `openscad_gen/advanced_optimization.py` добавляет reusable building blocks, не привязанные к UI:

- deterministic continuation schedule для SIMP `p`, Heaviside `beta` и OC move limit;
- smooth Heaviside projection + производная;
- robust three-field projection: eroded / nominal / dilated;
- KS aggregation для локальных stress/displacement constraints;
- p-norm aggregation;
- multi-load aggregation of element strain energies: weighted sum / max / p-norm;
- generalized OC update с fixed-solid design variables;
- voxel AM overhang diagnostic;
- minimum-feature diagnostic на Euclidean distance transform.

Это намеренно отдельный слой: solver/FEM и optimizer policy должны быть независимыми.

## Математическая модель

### SIMP

Для design density `rho_e`:

```text
E_e(rho_e) = E_min + rho_e^p (E_0 - E_min)
```

Compliance для load case `l`:

```text
C_l = f_l^T u_l = sum_e E_e * u_e^T K0 u_e
K(rho) u_l = f_l
```

Для нескольких нагрузок базовый objective:

```text
J = sum_l w_l C_l
```

Для worst-case поведения вместо жёсткого `max` можно использовать p-norm или KS aggregation.

### Robust three-field design

Одна filtered density `rho_tilde` проецируется тремя порогами:

```text
rho_eroded  = H(rho_tilde; beta, eta + delta)
rho_nominal = H(rho_tilde; beta, eta)
rho_dilated = H(rho_tilde; beta, eta - delta)
```

Оптимизация может минимизировать worst-case compliance по трём реализациям. Это даёт геометрию, которая меньше разваливается при вариации фактической толщины стенки/струта после печати.

### KS stress aggregation

Для нормированных ограничений `g_i = sigma_i / sigma_allow - 1`:

```text
KS(g) = g_max + (1/rho) log(sum_i exp(rho*(g_i-g_max)))
```

Это smooth approximation к локальному max и удобнее для gradient-based optimization, чем одно глобальное `max(stress)`.

## Следующий production-level solver stack

### 1. Matrix-free structured hexa FEM

Для регулярной voxel-grid нет необходимости собирать глобальную CSR stiffness matrix на каждой итерации. Нужен operator вида:

```text
q = K(rho) p
```

с element-by-element gather → local `Ke @ ue` → scatter. На GPU это главный путь масштабирования к миллионам элементов.

### 2. MGPCG / geometric multigrid

Jacobi-PCG недостаточно стабилен на высоком SIMP contrast. Целевая схема:

```text
PCG
 └─ geometric multigrid preconditioner
     ├─ voxel restriction/prolongation
     ├─ weighted Jacobi/Chebyshev smoother
     └─ coarse solve
```

### 3. Multiple load cases as first-class data

`multiload.py` уже умеет переиспользовать FEM context. Следующий шаг — parser/data model для именованных load cases и combinations:

```text
loadcase service {
  force clamp_tip = [0N, -120N, 0N];
  weight = 0.6;
}

loadcase impact {
  force clamp_tip = [80N, -40N, 0N];
  weight = 0.4;
}
```

### 4. Constraints, а не только compliance

Production target:

- volume fraction;
- displacement constraints;
- local/von-Mises stress via KS or augmented Lagrangian;
- eigenfrequency / buckling constraints;
- minimum member size;
- symmetry / passive-solid / passive-void regions;
- AM overhang/build-direction constraints.

### 5. MMA/GCMMA alongside OC

OC хорош для compliance + volume. Когда появляются несколько nonlinear constraints, нужен MMA/GCMMA backend. OC остаётся быстрым baseline и regression reference.

### 6. Nonlinear and anisotropic extensions

Дальнейшие режимы:

- geometric nonlinearity;
- elastoplastic material validation;
- orthotropic/composite material tensors;
- spatially varying material orientation;
- thermoelastic coupling;
- modal / buckling analysis.

## Связь с VoxelСad

Целевой pipeline:

```text
Voxel/SDF CAD domain
       ↓
material/design field
       ↓
FEM_Voxel optimization
       ↓
density / stress / displacement fields
       ↓
field exchange
       ↓
VoxelСad lattice/TPMS generator
       ↓
printable geometry
```

То есть `FEM_Voxel` не должен сам генерировать финальные STL кубиками. Он должен выдавать физические поля и optimization state через стабильный field protocol.

## Связь с Unified-CAD

В едином приложении Python backend должен запускаться headless, получать versioned analysis request и возвращать versioned field/result package. UI не должен импортировать optimizer state напрямую.

Target contract:

```text
analysis.request.json
geometry/field payload
        ↓
FEM_Voxel worker
        ↓
analysis.result.json
rho.npz
stress.npz
displacement.npz
```

Это позволит позже заменить Python FEM на GPU/C++ backend без изменения document/UI layer.

## Современные ориентиры

- Matrix-free 3D SIMP с fused gather/GEMM/scatter kernels показывает, что global stiffness assembly можно убрать из hot path и получить большой выигрыш на consumer GPU: https://arxiv.org/abs/2604.18020
- Для large-scale 3D topology optimization практический путь — GPU + multigrid-preconditioned iterative solve; открытые реализации уже демонстрируют десятки миллионов элементов: https://doi.org/10.1016/j.cma.2023.116473
- Stress-based TO в 2026 активно развивается в сторону local constraint enforcement / augmented Lagrangian вместо одного грубого global stress max: https://doi.org/10.1016/j.cma.2025.118692
- Manufacturing-aware TO уже включает build process/residual-stress constraints, а не только geometry overhang check: https://doi.org/10.1016/j.cma.2025.117913

## Приоритет реализации

1. Integrate `ContinuationSchedule` into `optimize_connector`.
2. Turn `multiload.py` into optimizer-native multi-load sensitivity path.
3. Add robust three-field objective.
4. Add KS displacement/stress constraints.
5. Add MMA backend.
6. Matrix-free CPU reference operator + equivalence tests against assembled CSR.
7. GPU operator (CuPy/CUDA) with CPU fallback.
8. MGPCG.
9. Analysis protocol for `Unified-CAD`.
10. Density/stress-driven lattice mapping back into `VoxelСad`.
