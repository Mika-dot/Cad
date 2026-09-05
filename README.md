# DCad — OpenGL viewport foundation

`OpenGL` — базовая ветка визуализации DCad. Раньше это был учебный SharpGL-пример: два захардкоженных куба, `GL_QUADS`, камера в обработчике `Resized` и управление WASD. Теперь ветка оформлена как **reusable CAD viewport**, который должен стать общей визуальной оболочкой для `VoxelСad`, `FEM_Voxel`, `V1-Experiment`, `V2-Experiment` и STL-инструментов.

## CAD viewport layer

Уже реализованы:

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
- SharpGL обновлён с `2.3.0.1` до `3.1.1`;
- Windows CI.

## Новое: ModernRenderer GPU backend experiment

Добавлен второй renderer path:

```text
ModernRenderer/
├── DCad.Renderer.csproj
├── MeshData.cs
├── ShaderProgram.cs
├── ViewerWindow.cs
└── Program.cs
```

Это .NET 8 + **OpenTK 4.9.4 / OpenGL 3.3 Core**. Он нужен не вместо текущего богатого CAD UI, а как prototype будущей GPU-части:

- VAO / VBO / EBO;
- indexed triangles;
- GLSL vertex/fragment shaders;
- per-vertex scalar values;
- scalar heatmap;
- normal-based lighting;
- depth + culling;
- orbit/zoom;
- wireframe toggle.

Запуск:

```bash
dotnet run --project ModernRenderer/DCad.Renderer.csproj
```

На 2026 год stable OpenTK 4.x остаётся более консервативным выбором для этого prototype: OpenTK 5 доступен как prerelease. Поэтому backend закреплён на `4.9.4`, а не на pre-release API.

## Управление legacy/CAD viewport

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

## Целевая архитектура

Нельзя выбирать между «текущим SharpGL viewport» и «новым OpenTK viewer» как между двумя приложениями. Их надо скрестить по слоям:

```text
DCad UI / tools
      |
      +-- Scene3D
      +-- Camera3D
      +-- selection / properties / gizmos
      |
      v
IRenderBackend
      |
      +-- SharpGLCompatibilityBackend   <- текущий CAD viewport
      +-- OpenTkGpuBackend              <- ModernRenderer
             |
             +-- VBO/EBO cache
             +-- shaders
             +-- ID framebuffer picking
             +-- field visualization
```

То есть UI/scene logic сохраняется, а непосредственный GPU backend становится заменяемым.

## Geometry contract

Renderer не должен знать, как была создана фигура:

```text
V2 triangle mesh --------+
                         |
Rendering-STL ------------+--> indexed MeshData --> renderer
                         |
VoxelCAD surface mesher --+
                         |
FEM density/stress -------+--> vertex/cell scalar attributes
```

Именно поэтому `MeshData` должен стать общим DTO: positions, normals, triangle indices и optional scalar/material/object IDs.

## Сборка существующего CAD viewport

```powershell
nuget restore OpenGL_lesson_CSharp\OpenGL_lesson_CSharp.sln
msbuild OpenGL_lesson_CSharp\OpenGL_lesson_CSharp.sln /m /p:Configuration=Release /p:Platform=x86
```

Требуется Windows и .NET Framework 4.8.

## Следующие этапы

1. общий `IRenderBackend`;
2. adapters `MeshData` для V2, STL и VoxelCAD;
3. persistent GPU mesh cache вместо upload каждый кадр;
4. integer object/face ID framebuffer picking;
5. silhouette selection outline pass;
6. MSAA + resolve;
7. clipping/section planes и clipping box;
8. measurement tools: distance, angle, radius, dimensions;
9. translate/rotate/scale gizmo + snapping;
10. scene layers: geometry / FEM / temperature / stress / density;
11. large-model chunks, frustum culling и LOD;
12. offscreen screenshot/report renderer;
13. voxel instancing/indirect draw для debug volume mode;
14. PBR/environment lighting для нормального CAD viewport.

## Роль ветки

`OpenGL` становится **единственным rendering/view layer** будущего приложения. Ни V1, ни V2, ни FEM не должны иметь собственную долгоживущую систему камер, selection и визуальных режимов — они должны отдавать геометрию/fields этому модулю.
