# Быстрый вход — ФВМ + FEM + генеративное проектирование


> **ПРОВЕРЕННЫЙ НОВЫЙ ЭТАП 14.10.2026 — независимый
> тетраэдральный FEM по ЭКСПОРТИРОВАННОЙ STL.**
> Добавлены `experiments/FvmGenerativeDemo/independent_tetra_fem.py`
> и `compare_fem_discretizations.py`.
> В отличие от C# Hex8/FVM, solver реализует собственную
> 3D линейную T4 матрицу, свои STL-ray проверки фактического
> материала, реальные pressure STL patches, sparse FEM через
> SciPy, проверку реакций, материальной связности load↔fixed.
> Вход и аналитический самотест — в
> [FVM_INDEPENDENT_T4_FEM_VERIFICATION.md](FVM_INDEPENDENT_T4_FEM_VERIFICATION.md).
> Верификация [CI #38525508905](https://github.com/Mika-dot/Cad/actions/runs/38525508905)
> **PASS**, включая реальную STL, аналитический стержень
> на 3 сетках, два T4 разбиения с 1 и 4 материальными
> квадратурными точками, отдельные ошибки нагрузок на
> ersatz-узлы и NEGATIVE 2-несвязных-тела тест.
> **НЕ СЕРТИФИЦИРОВАНО:** бинарный C# Hex8 maxVM=1.446 МПа,
> Python T4 4point initial maxVM=1.459 МПа,
> Python T4 4point refined maxVM=2.701 МПа
> (более подробный отличается от Hex8 на +86.83%).
> Строгий независимый `--require-agreement` отвергает
> такой проект. Уровень расхождений требует последующего
> построения surface-conforming T4 tetra volume mesh
> и исследования сеточной сходимости реальных stress hot spots.
>

> **АКТУАЛЬНО — 13.10.2026, ИСПРАВЛЕНО STL→Hex8 PRESSURE И ЗАЩИТА ОТ ЛОЖНОЙ СХОДИМОСТИ.**
> В `HexFemSurfacePressure.cs` произвольный STL-треугольник
> теперь отсекается границами пересекаемых фоновых Hex8;
> внутри каждого полигона применяется **7-point degree-5**
> квадратура, а не прежняя 3-point без пересечений.
> Аналитический контроль давления/момента и сравнение
> одного наклонного треугольника с **1024** треугольниками
> дали относительную L2 ошибку вектора узловых сил
> **1.738e−15**: [CI Windows/Linux #38428415781](https://github.com/Mika-dot/Cad/actions/runs/38428415781).
> Трёхуровневый FEM теперь требует **обоих** относительных
> уточнений в пределах порогов; давление в сцене
> `multi_scene_surface_pressure.json` показало +57.33%
> VM на первом и +3.76% на втором переходе, поэтому
> `meshConvergenceScreenPassed=false`.
> При `--require-mesh-convergence` повторный запуск
> **не выдаёт optimized.stl**;
> [CI Windows/Linux #38429308621](https://github.com/Mika-dot/Cad/actions/runs/38429308621) PASS.
> При безусловном экспериментальном режиме выходной STL
> может генерироваться, но **прочность не подтверждена**.
> Исходники, тесты и математическое описание:
> [FVM_SURFACE_PRESSURE_IMPLEMENTATION.md](FVM_SURFACE_PRESSURE_IMPLEMENTATION.md),
> [FVM_FREE_TOPOLOGY_SIMP_OC.md](FVM_FREE_TOPOLOGY_SIMP_OC.md),
> [TASK_JOURNAL.md](../experiments/FvmGenerativeDemo/TASK_JOURNAL.md).
>

> **Актуальное дополнение: СВОБОДНАЯ ТОПОЛОГИЯ С АВТОМАТИЧЕСКИМ КОНТАКТНЫМ КАНАЛОМ.**
> В `FreeTopologyField.Value` и `DesignIsActive` включён
> новый контактный CSG-фильтр `scene.PressureContactVoid`.
> Он автоматически резервирует наружную свободную область
> перед любыми выбранными STL pressure faces. Иначе
> свободная плотностная модель могла физически неверно
> закрыть грани нагруженного исходного тела.
> [Рабочий pressure+free-topology CI #38422695754](https://github.com/Mika-dot/Cad/actions/runs/38422695754):
> Windows/Ubuntu PASS; 3 тела, p=0.5 МПа × 256 мм² = 128 Н;
> **−12.107% FEM-объёма, −16.636% STL**, два принятых шага,
> бинарный FEM 1.454 МПа. По 3 M/FEM параметрам совпали
> конечные разности. `"pressureContactVoid":false` в той же
> сцене специально отвергается как заглублённая внутренняя
> нагрузка. Следующий приоритет — независимая STL pressure
> и mesh-convergence в контактных участках.
>
> **Новое 13.10.2026 — ПОВЕРХНОСТНЫЕ НАГРУЗКИ:**
> [FVM_SURFACE_PRESSURE_IMPLEMENTATION.md](FVM_SURFACE_PRESSURE_IMPLEMENTATION.md)
> описывает настоящие Hex8 Gauss surface traction и
> произвольные ориентированные ASCII/Binary STL pressure patch.
> В `UniversalScene` появился массив `surfacePressurePatches`:
> `loadCase`, `bodyId`, `stlFile`, `pressureMpa`;
> он передаётся через `HexFem` и `UniversalDesign`, включая
> независимый расчёт прямого F; нагрузка от PDF/STL
> не подменяется объёмным распределением силы.
> Реально выполнено: стержень p=10 МПа,
> A=100 мм², L=100 мм, E=200000 МПа → F=1000 Н,
> Rx=−1000 Н, u=0.005 мм, σ=10 МПа; Linux/Windows
> [#38420106507](https://github.com/Mika-dot/Cad/actions/runs/38420106507)
> 2/3/32 STL грани и несколько FEM сеток PASS.
> CSG pressure-only: `multi_scene_surface_pressure.json`,
> нагружена поверхность бокса из двух STL треугольников,
> p=0.5 МПа, A=256 мм², F=128 Н,
> [CI #38421658244](https://github.com/Mika-dot/Cad/actions/runs/38421658244)
> PASS Windows/Linux и 6 негативных тестов.
> Особо: в `FreeTopologyDesign` такая же STL-поверхностная
> нагрузка математически может быть передана HexFem,
> но если свободный density design **закрывает** её новым
> материалом, схема физически недостоверна — добавлен явный
> отказ без экспорта. Следующий шаг: гарантировать
> свободную внешнюю контактную полость/плоскость на стадии
> синтеза, а не разрешать внутреннее «давление».
>

> **АКТУАЛЬНОЕ СОСТОЯНИЕ ПОСЛЕ ДОПОЛНИТЕЛЬНОГО АУДИТА (13.10.2026).**
> Все изменения в ветке `feature/fvm-generative-mechanics`.
> Основная точка входа: `experiments/FvmGenerativeDemo/FreeTopologyDesign.cs`.
> Запуск: `dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_6_bodies.json --iterations 3 --gradient-check --self-test --out output/design`.
> Методика и математика: [FVM_FREE_TOPOLOGY_SIMP_OC.md](FVM_FREE_TOPOLOGY_SIMP_OC.md);
> подробные фактические тесты: [TASK_JOURNAL.md](../experiments/FvmGenerativeDemo/TASK_JOURNAL.md).
> Есть опциональный `projectionBeta` для настоящей гладкой Heaviside и точная обратная производная через 5 M и плотностный фильтр. Импортируется binary STL как обязательный объект; CSG union(эллипсоид,box) также используется без вручную заданных связей.
> Проверены [STL #38395542709](https://github.com/Mika-dot/Cad/actions/runs/38395542709) −22.604% FEM/−33.046% STL; [CSG #38396009551](https://github.com/Mika-dot/Cad/actions/runs/38396009551) −21.358% FEM; [β=3 #38396472796](https://github.com/Mika-dot/Cad/actions/runs/38396472796) −25.469% FEM; [6-body #38386836326](https://github.com/Mika-dot/Cad/actions/runs/38386836326) −17.848% FEM и [multiphysics #38386829988](https://github.com/Mika-dot/Cad/actions/runs/38386829988) −5.972% FEM. Независимо проверены экспортированная STL, исходные геометрии, физические зазоры и бинарный FEM; три пространственно независимые проверки FEM-градиента.
> ИСПРАВЛЕН ВАЖНЫЙ FEM-БАГ: термонагрузка должна масштабироваться тем же `rho^p`, что и матрица жёсткости — подтверждена инвариантность однородной термоупругости для p=1/3.
> **Не утверждать промышленную физическую точность:** на уточнённой сетке пиковый VM отличается на ~35%; нужен convergence proof/поверхностный FEM/реальные pressure traction BC, STEP/BREP и контакты. Физические `fixed`/loads задаёт инженер; только форму соединителей система проектирует сама.
>


> **АКТУАЛЬНЫЙ РЕЗУЛЬТАТ 13.10.2026 — СВОБОДНАЯ ТОПОЛОГИЯ.**
> Главный запуск: `dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_3_bodies.json --iterations 3 --gradient-check --self-test --out output/free`.
> Математика и ограниченная физическая обоснованность:
> [FVM_FREE_TOPOLOGY_SIMP_OC.md](FVM_FREE_TOPOLOGY_SIMP_OC.md).
> Рабочая программа `FreeTopologyDesign.cs` содержит отдельные
> плотностные переменные по разрешённым ячейкам, 3×3×3 нормированный фильтр,
> 5 M-образов, SIMP p=3 и OC с обратным градиентом через пространственную
> производную всех М-гиперплоскостей; пробует новые 3D-пустоты
> независимо от графа маршрутов. Кандидат только при
> M+directF+binaryFVM/Hex8 прочности/перемещениях, связности,
> keepouts и снижении массы. Сцены 3 тела и 6 тел с двумя препятствиями
> проверены в GitHub Actions: [#38385288100](https://github.com/Mika-dot/Cad/actions/runs/38385288100).
> Численные градиенты дC/dρ и дV/dρ проверены независимыми
> FEM центральными разностями до порядка 1e-7 относительного уровня.
> **Не перепутать**: это работающая свободная топологическая
> плотностная оптимизация, но не гарантированно глобальный
> оптимум, не точный surface FEM, не утверждение, что деталь выдержит
> нагрузки в производственных испытаниях. Физическую заделку
> пользователь по-прежнему задаёт как BC: определить реальный
> крепёж по внешней форме однозначно невозможно.
> Подробные числа и обнаруженные ошибки добавлены в TASK_JOURNAL.md.
>


> **Дополнение 13.10.2026 — проверка инженерной корректности и автосинтез раскосов.**
> Подробный [аудит](FVM_ENGINEERING_AUDIT_2026_10_13.md), ключевой тест:
> [Actions #38379745918](https://github.com/Mika-dot/Cad/actions/runs/38379745918).
> `autoTruss=true` добавляет 3D-раскосы на безопасных участках уже
> найденных маршрутов; реальные 6 базовых тел → 5 основных рёбер
> + 10 дополнительных раскосов, четыре auto-seed,
> FEM volume −8.11%, STL volume −8.645%, Windows/Linux/Python PASS.
> Исправлены ошибки связности нескольких `fixed`, повторной direct-F
> геометрии, нагрузки на зафиксированные DOFs, динамики по всем
> loadcases, mass density, STL winding и округления координат.
> Прежний оригинальный `FEM_Voxel` делает SIMP/OC внутри проектной области,
> а новый C# пока только строит дерево+диагонали и изменяет их радиусы.
> **Следующий P0 — настоящая свободная топологическая оптимизация F,
> а не дальнейшее усложнение рисунка заранее созданных балок.**
> Физические BC (`fixed`/внешние `load`) нельзя угадывать по форме
> изделия. Всё менять только в `feature/fvm-generative-mechanics`.
>

> **АКТУАЛЬНЫЙ РЕЗУЛЬТАТ 13.10.2026: УНИВЕРСАЛЬНЫЙ FVM/FEM GENERATOR.**
> Предыдущие ограничения «только две сферы» уже исторические:
> теперь реализован C# `--multi-scene` с **2–32 CSG или watertight STL
> обязательными телами**, несколькими закреплениями и нагрузками,
> `obstacles`/clearance, Dijkstra 3D маршрутизацией и независимой
> M/direct-F FEM-приёмкой по нескольким физическим критериям.
> Рабочий код: `UniversalScene.cs`, `ImplicitMesh.cs`,
> `UniversalDesign.cs`, `HexFemMultiphysics.cs`,
> `MultiphysicsSelfTest.cs`, `validate_keepouts.py`.
> Подробности и точные команды:
> [FVM_UNIVERSAL_MULTIPHYSICS_IMPLEMENTATION.md](FVM_UNIVERSAL_MULTIPHYSICS_IMPLEMENTATION.md).
> Фактические результаты и ошибки:
> [TASK_JOURNAL.md](../experiments/FvmGenerativeDemo/TASK_JOURNAL.md).
>
> **Численно подтверждено:** 
> [Actions #38374129417](https://github.com/Mika-dot/Cad/actions/runs/38374129417)
> (Windows+Linux+независимый Python FEM PASS);
> 6 объектов и 2 forbidden regions: −6.68% регуляризованного FEM-объёма,
> принудительный обход: −12.26%, импорт binary STL: −8.56%.
> Пользовательские keepout clearances дополнительно
> проверяются на экспортированной STL.
> Физика: реальный static Hex8, first-mode eigensolve,
> half-sine Newmark impulse, steady Fourier, thermoelastic,
> одномодовая FRF и screening S–N Basquin.
> Не реализованы STEP/BREP без STL, многоосный rainflow,
> контактный удар, buckling, нелинейная thermo-mechanics,
> глобально оптимальная topology.
> ОЧЕНЬ ВАЖНО: не выдавать линейные приближённые расчёты
> за сертифицированную безопасность. Не трогать исходные
> ветки main / FEM_Voxel / functional-voxel-kernel.
>


> **Дополнение 13.10.2026:** предложена мультифизическая математика
> [FVM_MULTIPHYSICS_GENERATIVE_DESIGN.md](FVM_MULTIPHYSICS_GENERATIVE_DESIGN.md):
> удары, спектральные колебания, температура, усталость, устойчивость,
> риски неопределённости и проекция множества shape-gradients на **одно F / 5M**.
> Особая идея: геометрический коридор погрешности реконструкции M±e_F
> с адаптацией сетки по достоверности физического отклика.
> **Это исследовательский план, а не реализованные и проверенные на CI модули.**
>

> **АКТУАЛИЗАЦИЯ 12.10.2026.** Приведённые далее исходный P0 и статусы «только план» —
> это **историческое состояние** на момент создания handoff.
> **Сегодня уже реализован** .NET 8 генератор двух произвольно размещённых сфер
> с JSON, 54 3D базисными функциями F, 5 М-образами,
> сопряжённой чувствительностью compliance, Hex8 FEM, двойной
> механической приёмкой по M/direct-F и экспортом STL.
> Проверка Windows/Linux/Python: [Actions #38287713233](https://github.com/Mika-dot/Cad/actions/runs/38287713233).
> Основной актуальный материал: **[ФВМ+FEM: как работает программа](FVM_FEM_GENERATIVE_DESIGN_GUIDE.md)**,
> детальный научный статус — [masterplan §18](FVM_GENERATIVE_MECHANICS_MASTERPLAN.md),
> фактический журнал — [TASK_JOURNAL](../experiments/FvmGenerativeDemo/TASK_JOURNAL.md).
> Последний испытанный результат: обычные сферы **4 шага, −11.30% регуляризованного объёма**;
> произвольно повернутые R10/R11 — **1 шаг, −22.42%**.
> В отличие от старой версии, пятый шаг стандартной сцены **отклонён**
> прямым FEM по F как нарушающий лимит смещения.
> При этом FEM остаётся сглаженным и не прошёл полный mesh-convergence —
> это демонстратор, не сертифицированная расчётная САПР.
>
> **Следующий приоритет** — уточнение FEM/M-сеток, подлинное поверхностное давление,
> независимая верификация напряжений, сопоставимый Python SIMP/OC baseline
> и свободная topology optimization. Все изменения — исключительно в
> `feature/fvm-generative-mechanics`.


**Сначала прочитать:** [FVM_GENERATIVE_MECHANICS_MASTERPLAN.md](FVM_GENERATIVE_MECHANICS_MASTERPLAN.md). Это основной документ, математический и архитектурный «источник истины» нового исследовательского проекта. Этот файл — короткая карта для нового чата, не заменяет полное ТЗ.

## Что пользователь хочет получить

Не «перерисовать воксели» и не «удалить 18% слабонагруженных кубов», а систему, в которой пользователь задаёт несколько соединяемых конструктивных поверхностей, закрепления, нагрузки, материал и запрещённые зоны; программа решает упругость, определяет **механические чувствительности изменения формы** и формирует минимально материалоёмкий конструктивный коннектор. Геометрия изменяется как неявное поле `F(x,y,z)`; поверхность `F=0`, материал `F>0`. Итоговый объект представлен **пятью нормированными М-образами**, без шестого канала и без обязательной бинаризации геометрии.

## Где исходные проекты

| Ветка | Код | Реальное состояние |
|---|---|---|
| [`feature/functional-voxel-kernel`](https://github.com/Mika-dot/Cad/tree/feature/functional-voxel-kernel) | `src/DCad.FunctionalVoxel/`, `src/DCad.FunctionalVoxel.Viewer/` | .NET8 ядро М-образов, R-функции, tests, Viewer; **нет механики/генератора** |
| [`FEM_Voxel`](https://github.com/Mika-dot/Cad/tree/FEM_Voxel) | `openscad_gen/fem.py`, `optimizer.py`, `geometry.py` | Python FEM на Hex1, SIMP+OC, `target_volume_ratio`, CSV/NPZ/SCAD; **нет ФВ-оптимизации** |
| [`feature/fvm-generative-mechanics`](https://github.com/Mika-dot/Cad/tree/feature/fvm-generative-mechanics) | `docs/FVM_GENERATIVE_MECHANICS_MASTERPLAN.md` | Рабочая изолированная ветка от ФВ-ядра; **на этапе планирования** |

## Чего строго не делать

- Не смешивать `M0...M4` с временными физическими полями `σ`, `u`, `∇σ` и не менять размер геометрического формата.
- Не выдавать процедуру «SIMP с новой картинкой» за новую геометрическую оптимизацию.
- Не утверждать, что пять М-коэффициентов непосредственно вычисляют напряжение: нужна задача упругости `K(F)u=f`.
- Не выдавать градиент напряжений `∇σ` за shape sensitivity `dJ/dF`.
- Не менять все коэффициенты М-образов независимо; менять F, согласованно вычислять `∇F`, затем упаковывать 5 коэффициентов.
- Не путать фоновые гексаэдры FEM с бинарными геометрическими вокселями. Выходная поверхность берётся из F=0.
- Не объявлять уровень научной новизны доказанным: R-функции, level-set, adjoint shape optimization и FEM — известные методы.
- Не сливать оригинальные ветки в `main` до проверки; всё исследование вести в `feature/fvm-generative-mechanics`.

## Конкретный алгоритм, к которому движемся

1. Геометрия `F>0` по локальным М-образам и правилам R-композиции с `preserve/avoid`.
2. `χ_F=Hε(F/||∇F||)` **лишь около регулярной границы**; для FEM — quadrature внутри элементов, не occupancy одного центра.
3. `K(F)u=f`, вычисление `u`, `ε`, `σ`, compliance, реакции, residual.
4. Adjoint/shape sensitivity геометрии; отдельно ограничение напряжений/деформаций; цель `min V(F)` при допустимом `σ, u`, а не `target_volume_ratio`.
5. Нормальная скорость поверхности `V_n` из механических чувствительностей/ограничений, проверенный шаг `F_t=V_n|∇F|`.
6. Согласованное восстановление `F, Fx,Fy,Fz` и формирование **M0...M4** как нового геометрического состояния.
7. Функциональные полости при физически оправданной смене топологии, проверка креплений, повторный FEM.
8. Сравнение с исходным SIMP и стандартным level-set/FEM — требуется доказать **конкретное преимущество**.

## Текущий ближайший шаг (P0)

Создать обмен ФВ-поля C#↔Python, однозначно сохранить оси, масштаб, центры ячеек, результаты расчёта и физические единицы. **Есть несовпадение linear indices:** в C# `(i*ny+j)*nz+k`, в Python `k*nx*ny+i*ny+j`. Обязательный тест `shape=(5,4,3)`.

Затем на маленькой эталонной призме запустить Python FEM по неявной геометрии, не начиная с полной топологической оптимизации. Эталон одноосного растяжения для соответствующих граничных условий: `L=100 мм, A=100 мм², P=1000 Н, E=200000 МПа`; `σ=10 МПа`, `ΔL=0,005 мм`. Задать реальные закрепления и распределение силы; проверить `Ku-f` и реакции.

После успешного P0 обновить **основной masterplan**: выполненные пункты, тесты, коммиты, решения, ошибки и точку продолжения.

## Полезные команды

~~~powershell
git clone https://github.com/Mika-dot/Cad.git
cd Cad
git fetch --all
git switch feature/fvm-generative-mechanics
dotnet test tests/DCad.FunctionalVoxel.Tests/DCad.FunctionalVoxel.Tests.csproj -c Release
git show origin/FEM_Voxel:openscad_gen/optimizer.py
git show origin/FEM_Voxel:openscad_gen/fem.py
~~~

**Исходные материалы:** [ФВ-документация](FUNCTIONAL_VOXEL_KERNEL.md), [исходный FEM](https://github.com/Mika-dot/Cad/tree/FEM_Voxel), [основной научно-технический план](FVM_GENERATIVE_MECHANICS_MASTERPLAN.md).

## Текст следующего задания

> Прочитай `docs/FVM_GENERATIVE_MECHANICS_MASTERPLAN.md` полностью, проверь текущие коды веток `FEM_Voxel` и `feature/functional-voxel-kernel`, работай только в `feature/fvm-generative-mechanics`. Выполни P0 из masterplan: воспроизводимый FEM baseline, транспорт пяти C# М-образов в Python, строгая проверка индексации и статический FEM по F-геометрии, с тестами. Не выдавай задуманные алгоритмы за готовые; обнови masterplan новыми коммитами и фактическими результатами.
