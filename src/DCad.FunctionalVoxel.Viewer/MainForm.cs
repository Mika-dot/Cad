using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using DCad.Core;
using DCad.FunctionalVoxel;

namespace DCad.FunctionalVoxel.Viewer;

/// <summary>
/// Plain engineering layout: inputs at left, 3D working area in the centre,
/// functional-voxel slices and measured values at right.
/// </summary>
public sealed class MainForm : Form
{
    private readonly ComboBox _shape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
    private readonly ComboBox _fieldMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
    private readonly ComboBox _resolution = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 232 };
    private readonly NumericUpDown _radius = new()
    {
        Width = 232, Minimum = 1, Maximum = 200, DecimalPlaces = 1, Increment = .5m, Value = 10
    };
    private readonly NumericUpDown _secondary = new()
    {
        Width = 232, Minimum = .5m, Maximum = 100, DecimalPlaces = 1, Increment = .5m, Value = 3
    };
    private readonly Button _build = new() { Text = "Построить / рассчитать", Width = 232, Height = 35 };
    private readonly Button _saveObj = new() { Text = "Экспорт OBJ (3D)", Width = 232, Height = 30, Enabled = false };
    private readonly Button _saveCsv = new() { Text = "Экспорт CSV (срез)", Width = 232, Height = 30, Enabled = false };
    private readonly Viewport3D _viewport = new() { Dock = DockStyle.Fill };
    private readonly SliceView _slice = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _channel = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TrackBar _z = new()
    {
        Dock = DockStyle.Fill, TickStyle = TickStyle.None, Minimum = 0, Maximum = 31
    };
    private readonly TextBox _pointInfo = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f),
        BackColor = Color.White
    };
    private readonly TextBox _metrics = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f),
        BackColor = Color.White
    };
    private readonly ToolStripStatusLabel _status = new() { Text = "Готово" };
    private SceneResult? _scene;
    private bool _busy;

    public MainForm()
    {
        Text = "DCad — функционально-воксельное моделирование";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1440;
        Height = 860;
        MinimumSize = new Size(1080, 650);
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(242, 242, 242);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);
        statusStrip.Dock = DockStyle.Bottom;
        Controls.Add(statusStrip);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(5)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 253));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 361));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        layout.BringToFront();

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(6, 2, 2, 2),
            BackColor = Color.FromArgb(246, 246, 246)
        };
        layout.Controls.Add(left, 0, 0);

        left.Controls.Add(Heading("ГЕОМЕТРИЯ"));
        left.Controls.Add(Caption("Примитив / операция R"));
        _shape.Items.AddRange(new object[]
        {
            "Сфера", "Параллелепипед", "Цилиндр", "Сфера − цилиндр",
            "Объединение двух сфер", "Пересечение двух сфер"
        });
        _shape.SelectedIndex = 3;
        left.Controls.Add(_shape);
        left.Controls.Add(Caption("Наружный радиус, мм"));
        left.Controls.Add(_radius);
        left.Controls.Add(Caption("Радиус отверстия, мм"));
        left.Controls.Add(_secondary);

        left.Controls.Add(Heading("ВЫЧИСЛЕНИЕ"));
        left.Controls.Add(Caption("Сетка (по каждой оси)"));
        _resolution.Items.AddRange(new object[] { "16", "24", "32", "40", "48", "64" });
        _resolution.SelectedIndex = 2;
        left.Controls.Add(_resolution);

        left.Controls.Add(Caption("Операция над ФВ-полем"));
        _fieldMode.Items.AddRange(new object[]
        {
            "F (исходное поле)", "∂F / ∂x", "∂F / ∂y", "∂F / ∂z", "∫ F dx"
        });
        _fieldMode.SelectedIndex = 0;
        left.Controls.Add(_fieldMode);
        left.Controls.Add(VerticalGap(6));
        left.Controls.Add(_build);
        left.Controls.Add(VerticalGap(2));

        left.Controls.Add(Heading("ПРОСМОТР / ФАЙЛЫ"));
        var wireframe = new CheckBox { Text = "Каркасный режим", Width = 232, Height = 29 };
        wireframe.CheckedChanged += (_, _) =>
        {
            _viewport.Wireframe = wireframe.Checked;
            _viewport.Invalidate();
        };
        left.Controls.Add(wireframe);
        var cameraButton = new Button { Text = "Сброс вида", Width = 232, Height = 30 };
        cameraButton.Click += (_, _) => _viewport.ResetCamera();
        left.Controls.Add(cameraButton);
        left.Controls.Add(_saveObj);
        left.Controls.Add(_saveCsv);
        left.Controls.Add(VerticalGap(10));
        left.Controls.Add(new Label
        {
            Text = "ЛКМ — вращать / выбрать грань\n" +
                   "ПКМ — переместить вид\n" +
                   "Колесо — масштаб\n" +
                   "Справа — M0…M4 и срез XY",
            AutoSize = false, Width = 234, Height = 92, ForeColor = Color.DimGray
        });

        var workArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        workArea.Controls.Add(_viewport);
        layout.Controls.Add(workArea, 1, 0);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8,
            BackColor = Color.FromArgb(246, 246, 246), Padding = new Padding(5, 2, 5, 2)
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.Controls.Add(right, 2, 0);

        right.Controls.Add(Heading("Срез XY: значение или M-образ"), 0, 0);
        right.Controls.Add(_slice, 0, 1);
        right.Controls.Add(_z, 0, 2);
        _channel.Items.AddRange(new object[]
        {
            "F — значение поля", "M0 — коэффициент x", "M1 — коэффициент y",
            "M2 — коэффициент z", "M3 — коэффициент F", "M4 — свободный член"
        });
        _channel.SelectedIndex = 0;
        right.Controls.Add(_channel, 0, 3);
        right.Controls.Add(Heading("Выбранный воксель / точка"), 0, 4);
        right.Controls.Add(_pointInfo, 0, 5);
        right.Controls.Add(Heading("Параметры и проверка"), 0, 6);
        right.Controls.Add(_metrics, 0, 7);

        _viewport.PointSelected += OnWorldPointSelected;
        _slice.VoxelSelected += DisplayVoxel;
        _z.ValueChanged += (_, _) =>
        {
            _slice.SetSlice(_z.Value);
            UpdateSliceLabel();
        };
        _channel.SelectedIndexChanged += (_, _) => _slice.SetChannel(_channel.SelectedIndex - 1);
        _build.Click += async (_, _) => await RebuildAsync();
        _saveObj.Click += (_, _) => SaveObj();
        _saveCsv.Click += (_, _) => SaveCsv();
        Shown += async (_, _) => await RebuildAsync();
    }

    private static Label Heading(string text) => new()
    {
        Text = text, AutoSize = false, Width = 236, Height = 26,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 8, 0, 0)
    };

    private static Label Caption(string text) => new()
    {
        Text = text, AutoSize = false, Width = 236, Height = 22,
        TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(3, 3, 0, 0)
    };

    private static Panel VerticalGap(int height) => new() { Width = 230, Height = height };

    private SceneSettings CurrentSettings() => new(
        (ShapeKind)_shape.SelectedIndex,
        (FieldView)_fieldMode.SelectedIndex,
        (double)_radius.Value,
        (double)_secondary.Value,
        int.Parse(_resolution.SelectedItem!.ToString()!, CultureInfo.InvariantCulture));

    private async Task RebuildAsync()
    {
        if (_busy) return;
        var settings = CurrentSettings();
        _busy = true;
        _build.Enabled = false;
        _saveObj.Enabled = false;
        _saveCsv.Enabled = false;
        _status.Text = "Вычисление М-образов, производных и изоповерхности...";
        try
        {
            var scene = await Task.Run(() => SceneBuilder.Build(settings));
            if (IsDisposed || Disposing) return;
            _scene = scene;
            _viewport.SetMesh(scene.Mesh);
            _slice.SetModel(scene.Model);
            _z.Minimum = 0;
            _z.Maximum = scene.Model.Grid.Count(2) - 1;
            _z.Value = _slice.SliceIndex;
            _channel.SelectedIndex = 0;
            _saveObj.Enabled = scene.Mesh is not null;
            _saveCsv.Enabled = true;
            var grid = scene.Model.Grid;
            DisplayVoxel(grid.LinearIndex(grid.Count(0) / 2,
                grid.Count(1) / 2, grid.Count(2) / 2));
            DisplayMetrics(scene);
            _status.Text = $"Готово: {grid.CellCount:N0} узлов; " +
                $"{scene.Mesh?.Triangles.Count ?? 0:N0} треугольников; " +
                $"{scene.Model.ChannelCount} М-образов";
        }
        catch (Exception ex)
        {
            _status.Text = "Ошибка расчёта: " + ex.Message;
            MessageBox.Show(this, ex.ToString(), "Ошибка функционально-воксельного расчёта",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            if (!IsDisposed && !Disposing) _build.Enabled = true;
        }
    }

    private void UpdateSliceLabel()
    {
        if (_scene is null) return;
        var g = _scene.Model.Grid;
        var z = g.Centre(g.LinearIndex(0, 0, _z.Value))[2];
        _status.Text = $"Срез Z={z:0.###} мм; слой {_z.Value + 1}/{g.Count(2)}";
    }

    private static string N(double value) => value.ToString("G9", CultureInfo.InvariantCulture);

    private void DisplayVoxel(int id)
    {
        var scene = _scene;
        if (scene is null) return;
        if ((uint)id >= (uint)scene.Model.VoxelCount) return;
        _slice.SelectVoxel(id);
        _z.Value = scene.Model.Grid.Index(id, 2);
        var p = scene.Model.Grid.Centre(id);
        var jet = scene.Model.JetAtCentre(id);
        var lines = new List<string>
        {
            $"i,j,k: {scene.Model.Grid.Index(id,0)}, {scene.Model.Grid.Index(id,1)}, {scene.Model.Grid.Index(id,2)}",
            $"X={N(p[0])}, Y={N(p[1])}, Z={N(p[2])} мм",
            $"F={N(jet.Value)}",
            $"dF/dX={N(jet.Gradient(0))}",
            $"dF/dY={N(jet.Gradient(1))}",
            $"dF/dZ={N(jet.Gradient(2))}"
        };
        for (var m = 0; m < scene.Model.ChannelCount; m++)
            lines.Add($"M{m}={N(scene.Model.Coefficient(m, id))}  #{scene.Packed.Rgb24(m,id):X6}");
        _pointInfo.Lines = lines.ToArray();
    }

    private void OnWorldPointSelected(Vector3d point)
    {
        if (_scene is null) return;
        var grid = _scene.Model.Grid;
        int Cell(int axis, double coordinate) =>
            Math.Clamp((int)Math.Floor((coordinate - grid.Minimum(axis)) / grid.Step(axis)),
                0, grid.Count(axis) - 1);
        var id = grid.LinearIndex(Cell(0, point.X), Cell(1, point.Y), Cell(2, point.Z));
        DisplayVoxel(id);
        _status.Text = $"3D: ({N(point.X)}, {N(point.Y)}, {N(point.Z)}) мм — ближайший воксель #{id}";
    }

    private void DisplayMetrics(SceneResult scene)
    {
        var g = scene.Model.Grid;
        var audit = scene.MeshAudit;
        var q = scene.Quantization;
        _metrics.Lines = new[]
        {
            $"Фигура: {scene.Settings.Shape}",
            $"Поле: {scene.Settings.View}",
            $"Сетка: {g.Count(0)} × {g.Count(1)} × {g.Count(2)}",
            $"Шаг: {N(g.Step(0))} мм",
            $"Каналов (double): {scene.Model.ChannelCount}",
            $"Объём по центрам: {N(scene.PositiveVoxelVolume)} мм³",
            $"Треугольников: {scene.Mesh?.Triangles.Count ?? 0}",
            $"Площадь mesh: {N(scene.Mesh?.SurfaceArea() ?? 0)} мм²",
            $"Объём mesh: {N(scene.Mesh?.SignedVolume() ?? 0)} мм³",
            $"Замкнутая mesh: {(audit?.IsClosedOrientedManifold == true ? "да" : "нет")}",
            $"Граничные рёбра: {audit?.BoundaryEdges ?? 0}",
            $"Невырожд. триангуляция: {audit?.DegenerateTriangles ?? 0}",
            "",
            "RGB24 — ошибка восстановления:",
            $"max |ΔF|: {N(q.MaxValueAbsoluteError)}",
            $"max |ΔF/scale|: {N(q.MaxValueRelativeError)}",
            $"max |Δ∇F|: {N(q.MaxGradientAbsoluteError)}",
            $"худший воксель: #{q.WorstVoxelId}"
        };
    }

    private void SaveObj()
    {
        if (_scene?.Mesh is null) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "Wavefront OBJ (*.obj)|*.obj", FileName = "functional_voxel.obj"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SceneExports.WriteObj(_scene, dialog.FileName);
            _status.Text = "Экспортировано: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка OBJ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveCsv()
    {
        if (_scene is null) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv", FileName = $"functional_voxel_Z{_z.Value}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            SceneExports.WriteSliceCsv(_scene, _z.Value, dialog.FileName);
            _status.Text = "Экспортировано: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
