# DCad — OpenGL viewport foundation

`OpenGL` — базовая ветка визуализации DCad. Раньше это был учебный SharpGL-пример: два захардкоженных куба, `GL_QUADS`, камера в обработчике `Resized` и управление WASD. Теперь ветка оформлена как **reusable CAD viewport**, который должен стать общей визуальной оболочкой для `VoxelСad`, `FEM_Voxel`, `V1-Experiment`, `V2-Experiment` и STL-инструментов.

## Что изменено

- отдельный `Camera3D`: orbit / pan / zoom, perspective + orthographic, стандартные CAD-виды, `Fit`;
- отдельная `Scene3D`: список объектов, selection, scene bounds, CPU ray picking;
- `MeshData` + `MeshFactory`: треугольные mesh вместо логики «нарисовать куб прямо в форме»;
- отдельный `ViewportRenderer`;
- тёмная CAD-сцена, координатная сетка, X/Y/Z axes, depth test, smooth lighting;
- режимы `Shaded`, `ShadedEdges`, `Wireframe`, `XRay`;
- подсветка выбранного объекта;
- scene tree + `PropertyGrid` для редактирования transform/color;
- toolbar: Fit, Isometric, Front, Top, Right, projection, grid, lighting, render mode;
- status bar с режимом камеры/рендера и selection;
- SharpGL обновлён с `2.3.0.1` (2014) до `3.1.1`;
- добавлен Windows CI.

## Управление

| Действие | Управление |
|---|---|
| Select | ЛКМ |
| Orbit | ПКМ drag или `Alt + ЛКМ drag` |
| Pan | СКМ drag или `Shift + ЛКМ drag` |
| Zoom | колесо мыши |
| Fit scene | `F` |
| Perspective / Orthographic | `P` |
| Grid | `G` |
| Lighting | `L` |
| Render mode | `W` |
| Front / Back / Left / Right / Top / Bottom / Iso | `1..7` |
| Deselect | `Esc` |
| Focus selected | double click |
| Local camera pan | `A/D`, `Q/E`, arrows |

## Архитектура

```text
SharpGLForm
   │
   ├── Camera3D              navigation + projection + screen ray
   ├── Scene3D               objects + bounds + selection
   │     └── SceneObject
   │            └── MeshData
   └── ViewportRenderer      grid / axes / shading / wireframe / x-ray
```

Форма теперь не должна знать, является объект воксельной моделью, STL, FEM mesh или polygon CSG. Она получает `SceneObject/MeshData`. Именно это позволяет постепенно объединять исторические ветки.

## Сборка

```powershell
nuget restore OpenGL_lesson_CSharp\OpenGL_lesson_CSharp.sln
msbuild OpenGL_lesson_CSharp\OpenGL_lesson_CSharp.sln /m /p:Configuration=Release /p:Platform=x86
```

Требуется Windows и .NET Framework 4.8.

## Почему пока остаётся SharpGL compatibility rendering

Задача этого этапа — сначала получить **единый интерфейс viewport/scene/camera**, который можно перенести в остальные ветки без их переписывания. Внутренний renderer пока способен работать через compatibility OpenGL, поэтому старые проекты DCad не ломаются.

Следующий renderer backend должен заменить per-frame immediate submission на indexed GPU buffers + shaders. В современном OpenGL vertex data обычно хранится в VBO/VAO, а off-screen passes/picking/anti-aliasing строятся через framebuffer objects. Это будет backend-замена под теми же `Camera3D / Scene3D / MeshData`, а не очередное переписывание UI.

## Следующие этапы

1. `MeshData` adapters для `VoxelСad`, polygon CSG и STL.
2. GPU mesh cache: VBO / index buffers и shader pipeline.
3. integer ID framebuffer picking вместо bounding-sphere picking.
4. MSAA + resolve, silhouette/selection outline pass.
5. section planes, clipping box, explode view.
6. measurement tools: point-point, angle, radius, bounding dimensions.
7. gizmo translate/rotate/scale + snapping.
8. scene layers: geometry / FEM / temperature / stress / voxel fields.
9. large-model chunking, frustum culling и LOD.
10. в итоговом unified app — renderer abstraction, чтобы SharpGL можно было заменить на OpenTK/Silk.NET/Vulkan backend без изменения CAD kernels.

## Техническая база

OpenGL 4.6 остаётся текущей спецификацией Khronos. VAO/VBO являются стандартным механизмом vertex specification, а framebuffer objects — стандартной основой off-screen rendering и multisample pipelines. SharpGL сам содержит Modern OpenGL samples с shaders и vertex buffers, поэтому промежуточная миграция возможна без немедленной смены всего UI stack.
