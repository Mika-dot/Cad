# DCad field interchange v1

`final_fields.npz` — промежуточный формат между topology/FEM, voxel CAD и renderer.

## Координаты и единицы

- длина: mm;
- сила: N;
- напряжение/модуль: N/mm² (MPa);
- grid indexing: `(i, j, k)`;
- world point of cell centre: `origin + (ijk + 0.5) * voxel_size`.

## Обязательные массивы

- `format_version`;
- `origin[3]`;
- `voxel_size[1]`;
- `shape[3]`;
- `anchor_mask[nx,ny,nz]`;
- `load_mask[nx,ny,nz]`;
- `obstacle_mask[nx,ny,nz]`;
- `preserve_mask[nx,ny,nz]`;
- `design_mask[nx,ny,nz]`;
- `connector_mask[nx,ny,nz]`;
- `manifest_json`.

## Опциональные поля

- `density[nx,ny,nz]` — topology/design density 0..1;
- `stress[nx,ny,nz]` — scalar von Mises field in MPa;
- `displacement` — vector field / nodal result when exported;
- `field_linear_ids` — original FEM design element ids for traceability.

## Linear element id

FEM currently uses:

```text
linear_id = k * nx * ny + i * ny + j
```

`field_exchange.linear_field_to_grid(...)` reconstructs `(i,j,k)` without depending on Python object state.

## Why NPZ first

This is a lightweight exchange format, not the final storage technology. It provides a stable bridge while the project experiments with different engines. Later backends can add OpenVDB/NanoVDB/3MF, but algorithms should not depend directly on OpenSCAD files or Streamlit state.
