# DCad — Rendering-STL / mesh I/O & manufacturing geometry

Эта ветка исторически появилась как часть хакатонного проекта: STL-модели оборудования, SharpGL viewer, графики и ПО станка. Исторические материалы (`Rendering stl/`, `body/`, `bunker hydrodynamics/`, `media/`) сохранены, но ветка теперь получает вторую роль — **общий STL/mesh I/O и validation модуль** будущего DCad.

## ModernStl

Современная часть находится в:

```text
ModernStl/
├── DCad.StlToolkit.csproj
├── StlToolkit.cs
├── StlToolkit.Advanced.cs
└── Program.cs
```

Это standalone .NET 8 toolkit без зависимости от старого WinForms UI.

### Поддерживается

- auto-detect binary/ASCII STL;
- binary + ASCII STL write;
- OBJ export;
- surface area;
- enclosed absolute volume;
- bounding box;
- degenerate triangle detection;
- duplicate triangle detection;
- boundary edge audit;
- non-manifold edge audit;
- **unique welded vertex count**;
- **connected shell/component count**;
- **isolated triangle components**;
- **directed edge/winding consistency diagnostics**;
- tolerance-based vertex welding;
- basic repair = weld + remove degenerate + remove duplicate faces;
- scale transform;
- built-in closed-cube round-trip self-test.

## CLI

```bash
dotnet run --project ModernStl/DCad.StlToolkit.csproj -- model.stl
```

Repair:

```bash
dotnet run --project ModernStl/DCad.StlToolkit.csproj -- \
  broken.stl --repair repaired.stl --weld 0.000001
```

OBJ conversion:

```bash
dotnet run --project ModernStl/DCad.StlToolkit.csproj -- model.stl --obj model.obj
```

Self-test:

```bash
dotnet run --project ModernStl/DCad.StlToolkit.csproj -- --self-test
```

## Почему эта ветка нужна, если V2 уже работает с triangles

`V2-Experiment` исследует **создание/изменение** mesh и Boolean operations.

`Rendering-stl` должен отвечать за **границу с внешними файлами**:

```text
external STL/OBJ/3MF
        |
        v
  import + audit + repair
        |
        v
 shared indexed mesh
    |             |
    v             v
 V2 CSG        OpenGL renderer
    |
    v
 voxel/SDF conversion
```

Это позволяет не размазывать STL parser, normal repair и manifold checks по нескольким CAD engines.

## Следующие задачи

1. перейти с triangle soup на общий indexed half-edge mesh DTO;
2. consistent face orientation propagation по connected shells;
3. hole boundary loop extraction;
4. small-hole filling;
5. self-intersection broad-phase через BVH;
6. component filtering по volume/area;
7. mesh simplification;
8. remeshing и feature-edge preservation;
9. normal generation: flat/smooth/crease angle;
10. PLY support;
11. 3MF support с units/material metadata;
12. mesh voxelization и SDF conversion;
13. glTF как viewport/cache формат;
14. direct adapter в `OpenGL/MeshData`;
15. manufacturing checks: minimum wall, trapped shells, watertightness, build dimensions.

## Хакатонная часть

Старый проект по станку и бункеру не удаляется: он остаётся примером того, откуда выросли STL/rendering эксперименты. Но README теперь отделяет историческое приложение от reusable geometry tooling.

## Роль в едином DCad

```text
DCad.IO.Mesh
   ├── STL import/export       <- эта ветка
   ├── OBJ/PLY/3MF
   ├── audit / repair
   └── mesh normalization

DCad.Geometry.Mesh             <- V2
DCad.Rendering                 <- OpenGL
DCad.Geometry.Volume           <- V1/VoxelCAD
```

Именно так старый `Rendering-stl` превращается в полезный самостоятельный subsystem вместо ещё одного отдельного viewer.
