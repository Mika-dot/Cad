# Генеративное проектирование ФВМ + FEM (.NET 8)

**Актуальная реализация — свободная топологическая оптимизация ФВМ × FEM.**
На входе 2–32 обязательных CSG/STL-тела, физические граничные условия,
нагрузки, свойства материала и запретные области; пользователь **не
рисует соединительные балки или фермы**. Программа создаёт поле материала
во всём разрешённом 3D-пространстве, решает Hex8 FEM, вычисляет производные
через пять М-образов, выполняет SIMP/OC, может создавать новые пустоты,
проверяет бинарную геометрию и экспортирует STL. Есть опциональная
гладкая проекция Хевисайда `projectionBeta`. Режимы `--two-spheres`
и `--multi-scene` сохранены как независимые проверяемые прототипы
с ограниченной параметризацией рёбер.

Базовый проверенный сценарий:
```powershell
git switch feature/fvm-generative-mechanics
dotnet build experiments/FvmGenerativeDemo/FvmGenerativeDemo.csproj -c Release --warnaserror

# Быстро проверить, что обязательные тела не пересекают запретные зоны:
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --validate-scene experiments/FvmGenerativeDemo/multi_scene_6_bodies.json

# Выполнить свободную топологическую оптимизацию и три независимые
# проверки FEM-градиента:
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_6_bodies.json --iterations 3 --gradient-check --self-test --out output/free-six

python experiments/FvmGenerativeDemo/validate_stl.py output/free-six/initial.stl output/free-six/optimized.stl
python experiments/FvmGenerativeDemo/validate_free_topology.py experiments/FvmGenerativeDemo/free_topology_6_bodies.json output/free-six
```

[Математика свободной топологии](../../docs/FVM_FREE_TOPOLOGY_SIMP_OC.md)
и [журнал подтверждённых тестов](TASK_JOURNAL.md).

**Подтверждённая корректная постановка 6 тел после устранения
пересечения обязательной детали с препятствием:**
[FVM topology CI #38398019577](https://github.com/Mika-dot/Cad/actions/runs/38398019577):
6 обязательных объектов, 2 запретные зоны, 2 нагрузочных случая,
775 проектных переменных, 6 принятых изменений, 3 новых пустоты,
**−21.485%** сглаженного FEM-объёма, **−29.036%**
геометрического STL-объёма.
[Полный CI Windows, Linux и оригинальный FEM Python](https://github.com/Mika-dot/Cad/actions/runs/38398027920): 3/3 PASS.
[Проверка согласованности входных условий](https://github.com/Mika-dot/Cad/actions/runs/38398736352):
положительные и отрицательные сцены PASS.

**Ограничение:** это численно протестированный исследовательский
прототип, а не подтверждение промышленной несущей способности.
Даже при успешном FEM допустима существенная зависимость напряжений
от дискретизации. Требуются уточнённая сетка, реальные поверхностные
граничные условия, технологические ограничения и независимое
физическое испытание изделия.

Основное пошаговое описание — **[FVM_FEM_GENERATIVE_DESIGN_GUIDE.md](../../docs/FVM_FEM_GENERATIVE_DESIGN_GUIDE.md)**.
Полный научный план — [MASTERPLAN](../../docs/FVM_GENERATIVE_MECHANICS_MASTERPLAN.md).
Фактические испытания — [TASK_JOURNAL.md](TASK_JOURNAL.md).

## Быстрый запуск на Windows или Linux

Нужен SDK **.NET 8**, Python 3 используется только для независимой
проверки STL; сама оптимизация выполняется на C# без Python.

```powershell
git clone https://github.com/Mika-dot/Cad.git
cd Cad
git switch feature/fvm-generative-mechanics

dotnet build experiments/FvmGenerativeDemo/FvmGenerativeDemo.csproj -c Release --warnaserror

# Основные FEM, ФВМ и математические эталоны:
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --self-test

# Две сферы, контроль FEM/adjoint и экспорта STL:
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --two-spheres --self-test --out output/selftest

# Реальная пространственная генерация (до 10 попыток):
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --two-spheres --iterations 10 --out output/two-spheres

# Проверить замкнутость STL независимо от C#:
python experiments/FvmGenerativeDemo/validate_stl.py output/two-spheres/initial.stl output/two-spheres/optimized.stl
```

Пример пользовательской сцены с разными радиусами и **повёрнутой в 3D
осью между сферами**:

```powershell
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --two-spheres --scene experiments/FvmGenerativeDemo/two_spheres_example.json --out output/rotated
```

В JSON: `sphereA.center`, `sphereB.center` и радиусы (мм),
`loadN=[Fx,Fy,Fz]` (Н), `youngMpa`, `poisson`,
`maxVonMisesMpa`, `maxDisplacementMm`, `iterations`.
Переопределения CLI: `--force-x`, `--force-y`, `--force-z`,
`--young`, `--stress-limit`, `--displacement-limit`,
`--iterations` и `--out`.

Если исходная форма нарушает ограничения, расчёт завершится ошибкой.
Если следующий облегчённый вариант не проходит повторный FEM, шаг
отклоняется и уменьшается. Число принятых шагов может быть меньше
`--iterations`.

## Что рассчитывается

- **Геометрия:** `F>0` — материал, `F=0` — поверхность. В
  каждом центре ФВ-сетки записываются 5 коэффициентов нормированной
  локальной гиперплоскости одной функции `F`. 54 пространственных
  гауссовых базиса изменяют `F` и её производные согласованно,
  не удаляя определённый процент FEM-элементов.
- **Механика:** 3D изотропный Hex8, восемь точек Гаусса, матрично-свободный
  Jacobi-PCG, напряжения/деформации, реакции `Rx,Ry,Rz`, податливость,
  объём и условная масса стали (ρ=7850 кг/м³).
- **Оптимизация:** adjoint `dC/dF_g=-uᵀ(dK/dF_g)u`, производная объёма,
  chain rule через все пять М-каналов, line search и полный FEM
  для каждой принятой формы. Пределы по Мизесу и перемещению
  проверяются непосредственно, но их отдельные adjoint-производные
  **пока не реализованы**.
- **Геометрическая проверка:** опора и нагруженные узлы остаются в
  реконструированном материале; между сферами есть 6-соседний путь
  по положительным ФВ-ячейкам. STL строится marching tetrahedra;
  общие вершины пересечений переиспользуются, направление граней
  согласуется по рёбрам и ориентируется наружу по объёму оболочки.
- **Контроль аппроксимации:** `fvm_vs_analytic_f.csv` сравнивает
  объёмы, напряжения и перемещения на *одинаковой FEM-сетке*
  при прямом исходном `F` и при интерполяции из пяти M.

## Файлы результата

| Файл | Назначение |
|---|---|
| `initial.stl`, `optimized.stl` | Начальная и облегчённая замкнутая поверхность |
| `metrics.csv` | Физика каждого принятого шага и невязка |
| `fvm_vs_analytic_f.csv` | Оценка ошибки восстановления по М-образам |
| `summary.txt` | Сцена, итоговые значения и ограничения |
| `geometry_*_z*.bmp` | Срезы геометрии до/после и различия |
| `M0_*.bmp` ... `M4_*.bmp` | Пять слоёв ФВМ до/после |

Также сохранён прежний **двухпараметрический эталон** без `--two-spheres`:
`--self-test`, `--iterations 3`, `--export-element-ke`.
Он нужен для регрессии, а не является основным режимом генеративного
проектирования.

## Реально подтверждённые испытания

GitHub Actions **Windows + Ubuntu + независимый Python FEM**:
[успешный run #38287145669](https://github.com/Mika-dot/Cad/actions/runs/38287145669).

В начальной сцене: 5 принятых 3D FEM-шагов; объём с
19383.06 до 17193.23 мм³ (−11.30% *регуляризованного FEM-объёма*) после четырёх принятых шагов. Пятый был отклонён при независимой проверке прямого F.
Отдельно проверена замкнутость STL: геометрический объём с
18952.234 до 17088.043 мм³ (−9.836%).

В повернутой JSON-сцене: 1 принятый шаг, FEM-объём
20689.09 → 16050.95 мм³ (−22.42%); замкнутые
STL 19727.932 → 15095.696 мм³ (−23.481%).
Оба STL проходят независимую проверку рёбер и ориентации.
У каждого предлагаемого улучшения ограничения дополнительно
проверяются на **прямом исходном F** (при той же FEM-сетке),
чтобы ошибка интерполяции М-образов не создавала ложный результат PASS.
Для повернутой сцены остаточное отклонение М-напряжений от прямого F
в конце составляет −10.09%, перемещений −8.65% — это пока
существенная численная неопределённость, а не прецизионный расчёт.
Ни одна из этих цифр не должна интерпретироваться как
точный объём отливки или гарантированная прочность.

## Ограничения

Это **экспериментальная оптимизация формы**, не промышленная
валидация FEM. Переход материал/пустота сглажен (`εF=2 мм`),
«пустота» имеет искусственную жёсткость `Emin/E=1e-4`,
а предел напряжения оценивается по выбранным Gauss-точкам.
Нет полной сходимости по размеру элемента/регуляризации,
точной CutFEM, давления на заданную поверхность,
произвольного импорта STEP и GUI. Одна сцена — одна система
сил, не множество эксплуатационных случаев. 54 базисные
функции **не гарантируют произвольную топологию или
глобальный минимум массы**.

Рекомендации по исследованию и воспроизводимые данные —
в [руководстве](../../docs/FVM_FEM_GENERATIVE_DESIGN_GUIDE.md)
и [журнале](TASK_JOURNAL.md).


## Предлагаемое расширение: несколько физических воздействий

Математическая концепция в отдельном [FVM_MULTIPHYSICS_GENERATIVE_DESIGN.md](../../docs/FVM_MULTIPHYSICS_GENERATIVE_DESIGN.md): не только статическая сила, но также вибрации, удар, нагрев, термоупругость, усталость, потеря устойчивости, сценарная надёжность, связанная тепло-динамическая физика, единый adjoint-подход и геометрические «конверты» неопределённости пяти М-образов. **Это ещё НЕ функции данной версии C#**, а план расширения с формулами и критериями численных доказательств.


## Универсальный режим: много тел, избегание, STL и мультифизика (13.10.2026)

**Новый основной запуск:** [полное практическое руководство](../../docs/FVM_UNIVERSAL_MULTIPHYSICS_IMPLEMENTATION.md).
Существующий `--two-spheres` сохранён для регрессии; новая точка входа
`--multi-scene <json>`.

```powershell
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --multiphysics-self-test

dotnet run --project experiments/FvmGenerativeDemo -c Release -- --multi-scene experiments/FvmGenerativeDemo/multi_scene_6_bodies.json --iterations 2 --out output/multi-six

dotnet run --project experiments/FvmGenerativeDemo -c Release -- --multi-scene experiments/FvmGenerativeDemo/multi_scene_forced_detour.json --iterations 1 --out output/detour

python experiments/FvmGenerativeDemo/validate_stl.py output/detour/initial.stl output/detour/optimized.stl
python experiments/FvmGenerativeDemo/validate_keepouts.py experiments/FvmGenerativeDemo/multi_scene_forced_detour.json output/detour/routes.csv output/detour/initial.stl output/detour/optimized.stl
```

Поддержаны 2–32 отдельных CSG/STL тела, несколько фиксированных и нагружаемых
областей, произвольные обязательные якоря, forbidden CSG-объёмы и
заданные зазоры. CSG: sphere, box, ellipsoid, capsule, cylinder, torus,
union/intersection/difference, входной замкнутый ASCII/binary STL.
Автоматически строится дерево соединений с обходом препятствий.
Пять M создаются из одного неявного поля F и его градиентов.

**Реально исполняемые физические блоки**:
статические Hex8 FEM-режимы, первая собственная частота, первомодовый
гармонический отклик, линейная переходная реакция на импульс силы Newmark,
3D стационарная теплопроводность, последовательная термоупругость,
проверка усталости по упрощённой Basquin S–N. Это **не** реализация
контактного удара, rainflow по реальному спектру, многомодовой FRF,
buckling, STEP-BREP или полного нелинейного многополевого решателя.
Результаты проверяются на M и прямом F с **той же** FEM сеткой,
что не гарантирует сходимость физической задачи.


## Аудит и автоматическая ферменная геометрия (13.10.2026)

**[Аудит формул, исправленных ошибок и сравнение с FEM_Voxel/SIMP](../../docs/FVM_ENGINEERING_AUDIT_2026_10_13.md)**.

Теперь доступен режим `autoTruss=true`: пользователь задаёт физически закреплённые/нагруженные/обязательные тела и запрещённые объёмы, **не рисуя соединительные балки и раскосы**. Программа сама строит сеть, добавляет пространственные диагонали и FEM-оптимизирует их толщину. `seed` для CSG/STL можно опустить: программа попытается найти внутреннюю точку, но для тонкой и сложной формы может потребоваться ручная точка.

Проверенная сцена `multi_scene_auto_truss.json`: **6 CSG-объектов, 5 маршрутов, 10 самостоятельно построенных раскосов, 4 автоматически выведенные опорные точки, один принятый FEM-шаг**. Объём FEM −8.11%, STL −8.645%. Проверка Windows/Ubuntu/Python: [Actions #38379745918](https://github.com/Mika-dot/Cad/actions/runs/38379745918).

Это инженерная эвристика начальной пространственной фермы, а НЕ полная свободная топологическая оптимизация SIMP/OC. `fixed` и внешние силы остаются инженерными граничными условиями, которые нужно задать явно, а не угадывать по геометрии.


## Основной исследовательский режим: свободная топология ФВМ × FEM

**Теперь не требуется рисовать ни одну соединительную балку**. При `--free-topology <scene.json>` начальный проектный объём определяется автоматически из bounding box, обязательных CSG/STL-тел и keepout-зон. Каждая свободная ячейка ФВМ получает собственную проектную переменную, а 3D Hex8/SIMP и OC изменяют форму вплоть до зарождения новых пустот. Итоговые STL, density.csv, metrics.csv, summary.json и опциональный mesh_refinement.json создаются из согласованной геометрии пяти M.

В отличие от старого `--multi-scene --autoTruss`, новый режим **не задаёт предварительно граф фермы** и позволяет сформировать собственную топологию распределения материала. Реализовано не глобальное доказательство минимальной массы, а локальная плотностная оптимизация с FEM-обратной связью и проверкой условий.

**Запуск** (из корня репозитория):

```powershell
dotnet build experiments/FvmGenerativeDemo/FvmGenerativeDemo.csproj -c Release --warnaserror
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_3_bodies.json --iterations 3 --self-test --gradient-check --refinement-report --out output/free
python experiments/FvmGenerativeDemo/validate_stl.py output/free/initial.stl output/free/optimized.stl
python experiments/FvmGenerativeDemo/validate_free_topology.py experiments/FvmGenerativeDemo/free_topology_3_bodies.json output/free --require-void
```

**Документация математики:** [FVM_FREE_TOPOLOGY_SIMP_OC.md](../../docs/FVM_FREE_TOPOLOGY_SIMP_OC.md).

**Численно проверено (Linux CI):** [run #38385288100](https://github.com/Mika-dot/Cad/actions/runs/38385288100): на 3 CSG-телах −21.65% FEM-объёма, на 6 CSG-телах −17.85%; созданы новые пустоты, экспортированные STL замкнуты и выдерживают требуемые зазоры до запретных областей. Градиент проверен через полный независимый пересчёт FEM в двух сдвинутых пяти-M геометриях (совпадение примерно до 1e-7 по относительной величине).

Пока остаются ограничения: наложенные пользователем физические закрепления/силы, линейная упругость, ersatz-материал фоновой сетки, зависимость результата от шага FEM/FVM, отсутствие точного STEP/BREP (STL поддержан), глобально оптимальной формы, гарантированной печатаемости/прочности изделия. Существуют отдельные дополнительные проверки first-mode, impulse/Newmark, steady thermal, thermoelastic, harmonic и fatigue-screen; результаты этих режимов нельзя приравнивать к реальному контактному удару или промышленной сертификации.


### FEM-сходимость и предупреждение для реального производства

```powershell
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_multiphysics.json --iterations 1 --gradient-check --refinement-report --out output/validated

# Строгий режим: отказ при недоказанной сходимости
dotnet run --project experiments/FvmGenerativeDemo -c Release -- --free-topology experiments/FvmGenerativeDemo/free_topology_multiphysics.json --iterations 1 --refinement-report --require-mesh-convergence --out output/strict
```

В первом случае создаётся `mesh_refinement.json` с
тремя сетками и процентами изменения. Во втором на текущей
грубой тестовой сцене система **должна отказать**:
максимальное напряжение менялось на +35.03% и +13.69%
при последовательных уточнениях. Это не ошибка компиляции,
а нормальная проверка достоверности. Ни один данный PASS
не подтверждает промышленную безопасность изделия.
