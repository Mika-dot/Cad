# V2 integration prototype

![Место интеграционного прототипа в V2](../../docs/images/v2-geometry.svg)

Этот каталог — .NET 8 полигональный vertical slice: язык `.dcad`, ManifoldNET CSG, `Mesh3d`, CLI, OpenTK viewer и xUnit. Название папки `Production` историческое; код остаётся прототипом и теперь дублируется в ветке `Unified-CAD`.

## Сборка

Из `ModernV2/Production`:

```powershell
dotnet restore DCad.sln
dotnet build DCad.sln -c Release --no-restore
dotnet test tests/DCad.Tests/DCad.Tests.csproj -c Release --no-build
dotnet run --project src/DCad.Cli/DCad.Cli.csproj -c Release -- examples/bracket.dcad result.obj
dotnet run --project src/DCad.App/DCad.App.csproj -c Release -- examples/bracket.dcad
```

## Состав

| Проект | Назначение |
|---|---|
| `DCad.Core` | double geometry, indexed mesh, triangulation, point-in-solid, audit |
| `DCad.Boolean.Manifold` | примитивы, transforms и CSG через ManifoldNET |
| `DCad.Language` | parser/evaluator `.dcad` |
| `DCad.Cli` | выполнение модели и OBJ |
| `DCad.App` | простой OpenTK viewer |
| `DCad.Tests` | geometry/CSG/language regressions |

## Поддерживаемый язык

`param`, `let`, `solid`; units `mm`, `cm`, `m`, `deg`; примитивы `box`, `sphere`, `cylinder`; `translate`, `rotate`, `scale`; операции `+`, `-`, `&`.

## Ограничения

- ManifoldNET `1.0.7-alpha` — внешняя alpha-зависимость;
- используется triangle mesh, не B-Rep/STEP;
- UI показывает только итоговую модель;
- нет документа, undo/redo, выбора объектов и сохранения проекта;
- этот код не должен развиваться независимо от `Unified-CAD`.
