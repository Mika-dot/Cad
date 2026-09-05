# DCad V2 — polygon / triangle CAD

`V2-Experiment` теперь имеет два современных слоя рядом с исходным историческим `GL/`.

## Что использовать

### `Production/` — основной V2

Это текущая рабочая реализация для будущего единого DCad: double precision, indexed mesh, topology validation, Manifold CSG, язык моделирования, CLI, OpenTK viewer и regression tests.

Запуск:

```powershell
cd ModernV2/Production
dotnet test tests/DCad.Tests/DCad.Tests.csproj -c Release
dotnet run --project src/DCad.App/DCad.App.csproj -- examples/bracket.dcad
```

### Файлы `ModernV2/*.cs` — managed BSP reference

Небольшой полностью управляемый BSP-CSG оставлен как исследовательская реализация. Его удобно читать, профилировать и сравнивать с production backend, но он не является источником истины для сложных CAD boolean cases.

### `../GL/` — legacy V2

Старая WinForms/SharpGL реализация сохранена для истории. Именно в ней были обнаружены математические проблемы: slope/intercept intersection, fixed epsilon thresholds, принудительное округление intersection points, случайный ray casting, некорректная triangulation для некоторых наборов точек и отсутствие строгой topology validation.

## Роль V2 в общем DCad

V2 отвечает за explicit surface / polygon representation. В объединённом приложении этот слой должен работать рядом с voxel/SDF и FEM:

```text
CAD language / operation graph
            |
            v
     geometry kernel API
       /      |       \
 polygon   voxel/SDF   B-Rep
    |          |         |
    +----------+---------+
               |
       renderer / FEM / IO
```

Production V2 уже использует тот же интерфейс и архитектурную модель, что и интеграционная ветка `Unified-CAD`, поэтому перенос в общее приложение не требует повторного переписывания геометрической математики.
