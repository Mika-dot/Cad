# План объединения DCad

Интеграционная ветка: `Unified-CAD`. Остальные ветки используются как источники кода и регрессионных примеров; механическое слияние веток не применяется.

## Общие правила

- длина: мм;
- сила: Н;
- модуль Юнга и напряжение: Н/мм² (МПа);
- геометрические вычисления: `double`;
- один `Mesh3d`, один набор math types и одна tolerance policy;
- UI не выполняет CSG/FEM внутри event handlers;
- renderer получает готовые mesh/field data;
- найденная ошибка сначала оформляется как воспроизводимый тест;
- формат документа и расчётного обмена имеет номер версии.

## Что переносить

| Источник | Полезная часть | Куда |
|---|---|---|
| `Unified-CAD` | `DCad.Core`, `.dcad`, ManifoldNET adapter, CLI, App, Fields | основа |
| `Function-Basket` | predicates, spatial queries и regression cases | `DCad.Core`, `DCad.Tests` |
| `Rendering-stl` | STL reader/writer и audit | новый `DCad.IO` |
| `OpenGL` | CAD camera, scene tree, picking и scalar mapping | `DCad.App`/`DCad.Viewport` |
| `V1-Experiment` | сериализуемые операции и manufacturing runs | `DCad.Document`, позднее `DCad.Manufacturing` |
| `VoxelСad` | occupancy operations, morphology, TPMS, JSON cases | новый `DCad.Geometry.Voxel` |
| `FEM_Voxel` | field archive, FEM/SIMP и расчётные cases | внешний analysis worker |
| `V2-Experiment` | случаи отказа старых алгоритмов | `DCad.Tests` |

Не переносить: копии `SharpGLForm`, глобальные массивы `worldX/worldY/worldZ`, случайный ray casting, fixed magic epsilon, закоммиченные `bin`, `.suo` и NuGet package caches.

## Уже есть в Unified-CAD

- [x] .NET 8 solution;
- [x] math types и indexed `Mesh3d`;
- [x] topology audit;
- [x] triangulation простого polygon;
- [x] детерминированный point-in-solid;
- [x] `IModelingKernel` и ManifoldNET adapter;
- [x] capability profiles для mesh, voxel/SDF и будущего B-Rep backend;
- [x] язык `.dcad`;
- [x] CLI → OBJ;
- [x] базовый OpenTK viewer;
- [x] `DocumentGraph` и библиотечный undo/redo;
- [x] structured scalar/mask fields;
- [x] reader формата `final_fields.npz`;
- [x] records `AnalysisRequest` / `AnalysisResultSummary`;
- [x] xUnit и smoke-test CLI.

## Этап 1. Удалить дубли общего ядра

Работа:

1. перенести недостающие tests/predicates из `Function-Basket`;
2. перенести regression cases из `V2-Experiment`;
3. запретить появление новых копий `DCad.Core` в экспериментальных ветках.

Готово, когда один solution содержит все общие geometry tests, а `rg --files` не находит дополнительных актуальных копий `Geometry.cs`, `Mesh3d.cs`, `PolygonTriangulator.cs`.

## Этап 2. Документ и язык

Работа:

1. parser создаёт `DocumentGraph`, а evaluator исполняет его;
2. добавить сохранение проекта с номером схемы;
3. сохранить параметры, object IDs, входы узлов и suppression;
4. подключить undo/redo к UI.

Готово, когда файл можно открыть, изменить параметр, перестроить, сохранить и повторно открыть с тем же fingerprint и объёмом.

## Этап 3. STL и общий mesh I/O

Работа:

1. перенести binary/ASCII STL reader/writer;
2. адаптировать audit к общему `Mesh3d`;
3. добавить JSON-отчёт импорта;
4. покрыть тестами holes, duplicate faces, bad winding и degenerate triangles.

Готово, когда CLI выполняет STL → audit → OBJ и каждый дефект имеет отдельный тестовый файл.

## Этап 4. Один viewport

Работа:

1. перенести camera math и unit-тесты;
2. добавить scene objects и selection IDs;
3. подключить tree/properties;
4. вывести scalar field с легендой;
5. добавить orthographic views и fit.

Готово, когда одна и та же `Mesh3d` открывается из `.dcad` и STL, выбирается мышью и показывает поле из NPZ.

## Этап 5. Voxel backend

Работа:

1. определить `IVoxelGrid` и metadata сетки;
2. перенести операции из `VoxelСad`;
3. добавить строгий parser и unit-тесты;
4. реализовать mesh → voxel и voxel → `Mesh3d`;
5. затем заменить `HashSet` на bricks/chunks.

Готово, когда одна операция документа может исполняться mesh- или voxel-backend и сравниваться по bounds/volume с заданным допуском.

## Этап 6. FEM worker

Работа:

1. сериализовать `AnalysisRequest` из документа;
2. принять его в Python без Streamlit;
3. вернуть `AnalysisResultSummary` и NPZ fields;
4. проверить fingerprint, units, shape и element ordering;
5. добавить одну эталонную FEM-задачу в CI.

Готово, когда CLI запускает расчёт, проверяет версию схемы и открывает density/stress в viewport.

## После объединения

Эскизы с ограничениями, STEP/B-Rep, 3MF, slicing и другие крупные функции добавляются только после завершения этапов 1–6. До этого они увеличивают число несовместимых типов и интерфейсов.
