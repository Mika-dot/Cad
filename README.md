# openscad-gen-live-ui

Обновлённый проект для воксельной генеративной/топологической оптимизации коннектора между телами из OpenSCAD.

## Что изменено в этой версии

Главное изменение: старый жадный алгоритм поштучного удаления вокселей заменён на **density-based оптимизатор SIMP + OC**.

Что это даёт:

- больше **нет ручного шага удаления** — обновление дизайна считает сам оптимизатор;
- форма стала заметно ближе к классической topology optimization;
- добавлены **фильтрация чувствительностей / плотностей** и **projection/continuation** для более чистой геометрии;
- FEM переписан в стиле регулярной гекса-сетки с **предвычисленной сборкой sparse-матриц**, без повторного построения mesh на каждой итерации;
- визуализация ускорена: при больших масках используется scatter-визуализация вместо тяжёлого `ax.voxels(...)` на каждом кадре;
- финальная бинаризация плотностей сопровождается постобработкой и попыткой сохранить связность.

## Установка

```bash
python -m pip install -r requirements.txt
```

## CLI-режим

```bash
python main.py --scene test.scad
```

## UI-режим

```bash
streamlit run ui_app.py
```

## Ключевые параметры сцены

Теперь пользователю обычно достаточно задавать:

- `voxel_size`
- `target_volume_ratio`
- `max_displacement`
- `filter_radius`
- `penal_max`
- `density_threshold`

Ручные параметры удаления (`initial_removal_fraction`, `removal_fraction_step`, `min_removal_fraction`) больше не используются.

## Артефакты запуска

После расчёта сохраняются:

- `final_connector.scad`
- `final_scene_preview.scad`
- `metrics.csv`
- `summary.json`
- `animation.gif`


## Important note on units

This project assumes OpenSCAD geometry is authored in **millimeters** and loads are specified in **newtons**. Material stiffness and yield values are therefore stored in **N/mm^2 (MPa)**, not in pascals. If you put SI pascal values directly into a millimeter model, the structure becomes about 10^6 times too stiff and the optimizer appears to stall.

For large voxel grids, keep `solver: "auto"` and prefer `voxel_size` 1.5-2.0 for exploratory runs. After the shape looks reasonable, decrease the voxel size for a refinement run.


## Что было исправлено после повторной проверки

- разделён **нижний предел плотности** (`min_density`) и **жёсткость пустоты** (`void_stiffness_ratio`); раньше это был один и тот же параметр, из-за чего "пустой" материал оставался слишком жёстким и оптимизатор не формировал реальный мост между деталями;
- бинарная геометрия теперь строится не простым порогом по `density_threshold`, а через **связный путь по полю плотностей** с последующим наращиванием объёма, поэтому финальный коннектор не распадается на изолированные куски;
- continuation по `penal/beta` сделан заметно агрессивнее, чтобы за разумное число итераций поле плотностей переходило к чёрно-белой топологии.
