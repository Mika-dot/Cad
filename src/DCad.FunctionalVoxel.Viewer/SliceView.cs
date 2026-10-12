using System.Drawing.Drawing2D;
using DCad.FunctionalVoxel;

namespace DCad.FunctionalVoxel.Viewer;

/// <summary>
/// XY slice of F or one of the five M-images; Z is chosen by the TrackBar.
/// Cells are displayed as samples, not false claims of continuous exact values.
/// </summary>
public sealed class SliceView : Panel
{
    private FunctionalVoxelModel? _model;
    private Bitmap? _bitmap;
    private int _slice;
    private int _channel = -1;
    private int _selectedVoxel = -1;
    private Rectangle _imageRect;

    public event Action<int>? VoxelSelected;

    public SliceView()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
    }

    public int SliceIndex => _slice;
    public int Channel => _channel;

    public void SetModel(FunctionalVoxelModel? model)
    {
        _model = model;
        _selectedVoxel = -1;
        _slice = model is null ? 0 : model.Grid.Count(2) / 2;
        RebuildBitmap();
    }

    public void SetSlice(int z)
    {
        if (_model is null) return;
        _slice = Math.Clamp(z, 0, _model.Grid.Count(2) - 1);
        RebuildBitmap();
    }

    public void SetChannel(int channel)
    {
        if (channel < -1 || channel > 4) throw new ArgumentOutOfRangeException(nameof(channel));
        _channel = channel;
        RebuildBitmap();
    }

    public void SelectVoxel(int id)
    {
        if (_model is null) return;
        _selectedVoxel = id;
        var z = _model.Grid.Index(id, 2);
        if (z != _slice) SetSlice(z);
        Invalidate();
    }

    private void RebuildBitmap()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        if (_model is null)
        {
            Invalidate();
            return;
        }
        var grid = _model.Grid;
        int nx = grid.Count(0), ny = grid.Count(1);
        var values = new double[nx * ny];
        var maxAbs = 0.0;
        for (var j = 0; j < ny; j++)
        for (var i = 0; i < nx; i++)
        {
            var id = grid.LinearIndex(i, j, _slice);
            var value = _channel < 0 ? _model.ValueAtCentre(id) : _model.Coefficient(_channel, id);
            values[j * nx + i] = value;
            maxAbs = Math.Max(maxAbs, Math.Abs(value));
        }
        maxAbs = Math.Max(maxAbs, 1e-15);
        _bitmap = new Bitmap(nx, ny);
        for (var j = 0; j < ny; j++)
        for (var i = 0; i < nx; i++)
        {
            var normalized = Math.Clamp(values[j * nx + i] / maxAbs, -1, 1);
            _bitmap.SetPixel(i, ny - j - 1, Palette(normalized));
        }
        Invalidate();
    }

    private static Color Palette(double normalized)
    {
        // Centre (zero) is light neutral; blue means negative, orange positive.
        var k = Math.Pow(Math.Abs(normalized), .65);
        if (normalized >= 0)
            return Color.FromArgb((int)(242 - 38 * k), (int)(242 - 133 * k), (int)(242 - 199 * k));
        return Color.FromArgb((int)(242 - 200 * k), (int)(242 - 124 * k), (int)(242 - 24 * k));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.White);
        if (_bitmap is null)
        {
            TextRenderer.DrawText(g, "Нет поля", Font, ClientRectangle, Color.DimGray,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            return;
        }

        var margin = 18;
        var square = Math.Max(1, Math.Min(Width - 2 * margin, Height - 2 * margin));
        _imageRect = new Rectangle((Width - square) / 2, (Height - square) / 2, square, square);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_bitmap, _imageRect);
        g.DrawRectangle(Pens.Gray, _imageRect);

        if (_model is not null && _selectedVoxel >= 0 &&
            _model.Grid.Index(_selectedVoxel, 2) == _slice)
        {
            var x = _model.Grid.Index(_selectedVoxel, 0);
            var y = _model.Grid.Index(_selectedVoxel, 1);
            var sx = _imageRect.Left + (x + .5f) * _imageRect.Width / _model.Grid.Count(0);
            var sy = _imageRect.Top + (_model.Grid.Count(1) - y - .5f) *
                _imageRect.Height / _model.Grid.Count(1);
            using var pen = new Pen(Color.Black, 1.5f);
            g.DrawLine(pen, sx - 6, sy, sx + 6, sy);
            g.DrawLine(pen, sx, sy - 6, sx, sy + 6);
        }

        TextRenderer.DrawText(g, "+Y", Font, new Point(_imageRect.Left, _imageRect.Top - 18), Color.DimGray);
        TextRenderer.DrawText(g, "+X", Font,
            new Point(_imageRect.Right - 23, _imageRect.Bottom + 1), Color.DimGray);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _model is null ||
            !_imageRect.Contains(e.Location)) return;
        var nx = _model.Grid.Count(0);
        var ny = _model.Grid.Count(1);
        var x = Math.Clamp((int)((e.X - _imageRect.Left) * nx / (double)_imageRect.Width), 0, nx - 1);
        var yFromTop = Math.Clamp((int)((e.Y - _imageRect.Top) * ny / (double)_imageRect.Height), 0, ny - 1);
        var id = _model.Grid.LinearIndex(x, ny - 1 - yFromTop, _slice);
        SelectVoxel(id);
        VoxelSelected?.Invoke(id);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}
