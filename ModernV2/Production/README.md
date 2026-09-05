# V2 Production polygon CAD

Это production-направление `V2-Experiment`. Старый `GL/` оставлен как историческая версия, а `ModernV2/*.cs` — как небольшой managed BSP reference backend. Для реального приложения используется этот каталог.

## Архитектура

- `DCad.Core` — double-precision geometry, indexed triangle mesh, tolerance policy, mesh topology validation, triangulation and point/solid queries;
- `DCad.Boolean.Manifold` — robust manifold triangle-solid CSG backend;
- `DCad.Language` — детерминированный язык параметрического моделирования;
- `DCad.Cli` — headless execution and OBJ export;
- `DCad.App` — OpenTK 4 renderer (VBO/VAO, shaders, depth/culling, solid/wireframe);
- `DCad.Tests` — regression tests for the failure modes of the original V2.

## CAD language

```text
param width = 60mm;
param depth = 40mm;
param height = 8mm;

let base = box(width, depth, height);
let hole = cylinder(20mm, 5mm);
let h1 = translate(hole, -20mm, 0mm, 0mm);
let h2 = translate(hole,  20mm, 0mm, 0mm);
solid result = base - h1 - h2;
```

Supported length units: `mm`, `cm`, `m`; rotations use `deg`. Boolean operators are `+` union, `-` difference and `&` intersection.

## Run

```powershell
cd ModernV2/Production
dotnet restore DCad.sln
dotnet test tests/DCad.Tests/DCad.Tests.csproj -c Release
dotnet run --project src/DCad.Cli/DCad.Cli.csproj -- examples/bracket.dcad result.obj
dotnet run --project src/DCad.App/DCad.App.csproj -- examples/bracket.dcad
```

Viewer controls: arrows orbit, PageUp/PageDown zoom, F1 wireframe, Esc exit.

## Why the original mathematics was replaced

The legacy V2 performed triangle splitting and solid classification using scale-independent epsilons, rounded intersection coordinates, slope/intercept line intersections and a randomized ray test. Those methods are retained only for comparison. The production path uses an indexed mesh, deterministic solid-angle classification, explicit manifold validation and a dedicated CSG backend.

The invariant after every production boolean is: no degenerate topology accepted silently, closed oriented output where a solid is expected, and no boundary/non-manifold edges in the final mesh.
