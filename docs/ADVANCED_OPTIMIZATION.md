# Расширенные функции оптимизации FEM_Voxel

`openscad_gen/advanced_optimization.py` — библиотека численных политик и
диагностик. Модуль не заменяет FEM-решатель и пока не подключён к основному
циклу `optimize_connector`.

## Что реализовано

| API | Назначение | Ограничение |
|---|---|---|
| `ContinuationSchedule.state()` | Расписание SIMP-параметра, Heaviside `beta` и OC move limit | Зависит только от номера итерации |
| `heaviside_projection()` | Сглаженная проекция плотности и её производная | Вход не ограничивается автоматически диапазоном `[0, 1]` |
| `robust_projection_triplet()` | Eroded, nominal и dilated реализации | Возвращает поля и производные, но не вычисляет objective |
| `ks_aggregate()` | Гладкое приближение максимума | Возвращает только значение, без градиента |
| `pnorm_aggregate()` | p-норма неотрицательных ограничений | Отрицательные значения обнуляются |
| `aggregate_element_energies()` | Weighted sum, max или p-norm для нескольких нагрузок | Все массивы должны иметь одинаковую форму |
| `oc_update()` | OC-обновление с `fixed_solid` и произвольной производной объёма | Предназначено для density-based оптимизации |
| `overhang_violation_mask()` | Воксельная диагностика опор в направлении печати | Это эвристика, а не проверка траектории печати |
| `minimum_feature_violation()` | Поиск твёрдых ячеек с малым внутренним радиусом | Проверяет только твёрдую фазу |

## Используемые формулы

SIMP-интерполяция:

```text
E(rho) = E_min + rho^p (E_0 - E_min)
```

Три реализации одной отфильтрованной плотности:

```text
eroded  = H(rho_tilde; beta, eta + delta)
nominal = H(rho_tilde; beta, eta)
dilated = H(rho_tilde; beta, eta - delta)
```

KS-агрегация значений `g_i`:

```text
KS(g) = g_max + log(sum(exp(rho * (g_i - g_max)))) / rho
```

Вычитание `g_max` в показателе экспоненты защищает расчёт от переполнения.

## Проверка

GitHub Actions:

1. устанавливает зависимости из `requirements.txt`;
2. компилирует Python-модули;
3. проверяет continuation schedule, projection, KS, multi-load aggregation,
   OC update и две manufacturing-диагностики на малых массивах.

Эти smoke-тесты не подтверждают сходимость полной FEM-оптимизации, точность
напряжений или масштабируемость на больших сетках.

Локальный запуск:

```bash
python -m compileall openscad_gen app
python -m openscad_gen.selftest
```

## Порядок интеграции

1. Подключить `ContinuationSchedule` к `optimize_connector`.
2. Передавать несколько load cases в один цикл решения и sensitivities.
3. Добавить robust triplet в objective и проверить градиенты конечными
   разностями.
4. Добавить KS-ограничения перемещений/напряжений вместе с их производными.
5. Описать версионированный headless-контракт для `Unified-CAD`.
6. Только после численной верификации рассматривать другой solver backend.

Этап считается завершённым, когда есть воспроизводимый пример, эталонное
значение objective, проверка градиента и тест повторяемости результата.

## Контракт с Unified-CAD

Worker должен получать запрос с версией схемы, единицами, сеткой, материалом,
закреплениями и нагрузками. В ответе нужны:

- версия схемы и fingerprint исходной геометрии;
- статус решения и residual;
- summary в JSON;
- density/stress/displacement fields с shape и порядком элементов.

UI не должен импортировать внутреннее состояние Python-оптимизатора.
