using System.Drawing.Drawing2D;
using System.Numerics;
using DCad.Core;
using CoreVector = DCad.Core.Vector3d;

namespace DCad.FunctionalVoxel.Viewer;

/// <summary>
/// Minimal interactive orthographic 3D CAD viewport using Windows GDI.
/// No extra rendering package or GPU requirement. All geometry comes from the
/// functional-voxel kernel's exported Mesh3d.
/// </summary>
public sealed class Viewport3D : Panel
{
    private readonly SolidBrush[] _shadedBrushes;
    private Mesh3d? _mesh;
    private float _yaw = .85f;
    private float _pitch = .42f;
    private float _zoom = 1;
    private PointF _pan = new(0, 0);
    private Vector3 _centre;
    private Vector3 _right;
    private Vector3 _up;
    private Vector3 _eyeDirection;
    private float _scale;
    private bool _dragging;
    private bool _moved;
    private MouseButtons _dragButton;
    private Point _lastMouse;
    private Point _downMouse;
    private List<Face2D> _visibleFaces = new();
    private int _selectedFace = -1;

    private readonly record struct Face2D(int Index, PointF A, PointF B, PointF C, float Depth, int Shade);

    public event Action<CoreVector>? PointSelected;

    public bool Wireframe
    {
        get; set;
    }

    public Viewport3D()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        BackColor = Color.White;
        _shadedBrushes = Enumerable.Range(0, 32).Select(i =>
        {
            var t = i / 31f;
            return new SolidBrush(Color.FromArgb(
                (int)(42 + 62 * t), (int)(78 + 106 * t), (int)(111 + 108 * t)));
        }).ToArray();
    }

    public void SetMesh(Mesh3d? mesh)
    {
        _mesh = mesh;
        _visibleFaces.Clear();
        _selectedFace = -1;
        ResetCamera();
    }

    public void ResetCamera()
    {
        _yaw = .85f;
        _pitch = .42f;
        _zoom = 1;
        _pan = PointF.Empty;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.White);
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.SmoothingMode = SmoothingMode.None;
        if (_mesh is null)
        {
            TextRenderer.DrawText(g, "Нулевая поверхность отсутствует", Font,
                ClientRectangle, Color.DimGray, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter);
            _visibleFaces.Clear();
            return;
        }

        UpdateCamera();

        var points = new PointF[_mesh.Vertices.Count];
        var depths = new float[_mesh.Vertices.Count];
        for (var i = 0; i < points.Length; i++)
            (points[i], depths[i]) = Project(_mesh.Vertices[i]);

        var faces = new List<Face2D>(_mesh.Triangles.Count);
        var light = Vector3.Normalize(new Vector3(.5f, -.2f, 1.0f));
        for (var i = 0; i < _mesh.Triangles.Count; i++)
        {
            var f = _mesh.Triangles[i];
            var a = ToVector(_mesh.Vertices[f.A]);
            var b = ToVector(_mesh.Vertices[f.B]);
            var c = ToVector(_mesh.Vertices[f.C]);
            var cross = Vector3.Cross(b - a, c - a);
            if (cross.LengthSquared() < 1e-12f) continue;
            var normal = Vector3.Normalize(cross);
            // Single-sided CAD surface. Backfaces are not filled.
            if (Vector3.Dot(normal, _eyeDirection) <= 0) continue;
            var lightFactor = Math.Clamp(.34f + .66f * Math.Max(0, Vector3.Dot(normal, light)), 0, 1);
            faces.Add(new Face2D(i, points[f.A], points[f.B], points[f.C],
                (depths[f.A] + depths[f.B] + depths[f.C]) / 3,
                Math.Clamp((int)Math.Round(lightFactor * 31), 0, 31)));
        }
        // Painter's algorithm is sufficient for a simple, non-intersecting
        // isosurface. It is NOT a replacement for a hardware depth buffer.
        faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        _visibleFaces = faces;

        using var edges = new Pen(Color.FromArgb(115, 57, 74, 86), 1f);
        foreach (var f in faces)
        {
            var polygon = new[] { f.A, f.B, f.C };
            if (!Wireframe) g.FillPolygon(_shadedBrushes[f.Shade], polygon);
            else g.DrawPolygon(edges, polygon);
        }

        if (_selectedFace >= 0)
        {
            var found = faces.FindLastIndex(f => f.Index == _selectedFace);
            if (found >= 0)
            {
                var f = faces[found];
                using var selectionPen = new Pen(Color.DarkOrange, 2f);
                g.DrawPolygon(selectionPen, new[] { f.A, f.B, f.C });
            }
        }

        DrawAxes(g);
        using var labelFont = new Font(Font.FontFamily, 8);
        g.DrawString($"Граней: {_mesh.Triangles.Count:N0}    Вращение: ЛКМ    Перемещение: ПКМ    Масштаб: колесо",
            labelFont, Brushes.DimGray, 9, Height - 24);
    }

    private void UpdateCamera()
    {
        _eyeDirection = Vector3.Normalize(new Vector3(
            MathF.Cos(_pitch) * MathF.Cos(_yaw),
            MathF.Cos(_pitch) * MathF.Sin(_yaw),
            MathF.Sin(_pitch)));
        _right = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, _eyeDirection));
        _up = Vector3.Normalize(Vector3.Cross(_eyeDirection, _right));
        var bounds = _mesh!.Bounds;
        _centre = ToVector((bounds.Min + bounds.Max) / 2);
        _scale = Math.Max(1f, Math.Min(Width, Height) * .79f /
            Math.Max(.001f, (float)bounds.Diagonal)) * _zoom;
    }

    private static Vector3 ToVector(CoreVector v) =>
        new((float)v.X, (float)v.Y, (float)v.Z);

    private (PointF Point, float Depth) Project(CoreVector point)
    {
        var delta = ToVector(point) - _centre;
        var x = Width / 2f + _pan.X + Vector3.Dot(delta, _right) * _scale;
        var y = Height / 2f + _pan.Y - Vector3.Dot(delta, _up) * _scale;
        var depth = Vector3.Dot(delta, _eyeDirection);
        return (new PointF(x, y), depth);
    }

    private void DrawAxes(Graphics g)
    {
        if (_mesh is null) return;
        var o = _centre;
        var length = Math.Max(.1f, (float)_mesh.Bounds.Diagonal * .18f);
        var origin = Project(new CoreVector(o.X, o.Y, o.Z)).Point;
        using var font = new Font(Font.FontFamily, 8, FontStyle.Bold);
        void Axis(Vector3 direction, Color color, string caption)
        {
            var end = o + direction * length;
            var pt = Project(new CoreVector(end.X, end.Y, end.Z)).Point;
            using var pen = new Pen(color, 2);
            g.DrawLine(pen, origin, pt);
            using var brush = new SolidBrush(color);
            g.DrawString(caption, font, brush, pt);
        }
        Axis(Vector3.UnitX, Color.Firebrick, "X");
        Axis(Vector3.UnitY, Color.ForestGreen, "Y");
        Axis(Vector3.UnitZ, Color.RoyalBlue, "Z");
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
        Focus();
        Capture = true;
        _dragButton = e.Button;
        _dragging = true;
        _moved = false;
        _downMouse = e.Location;
        _lastMouse = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        var dx = e.X - _lastMouse.X;
        var dy = e.Y - _lastMouse.Y;
        if (Math.Abs(e.X - _downMouse.X) + Math.Abs(e.Y - _downMouse.Y) > 4)
            _moved = true;
        if (_dragButton == MouseButtons.Left)
        {
            _yaw += dx * .012f;
            _pitch = Math.Clamp(_pitch - dy * .012f, -1.48f, 1.48f);
        }
        else if (_dragButton == MouseButtons.Right)
        {
            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
        }
        _lastMouse = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        var pick = e.Button == MouseButtons.Left && !_moved;
        _dragging = false;
        Capture = false;
        if (pick) SelectFace(e.Location);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12f : 1 / 1.12f), .12f, 25f);
        Invalidate();
    }

    private static bool TryBarycentric(PointF p, Face2D f, out float u, out float v, out float w)
    {
        var denominator = (f.B.Y - f.C.Y) * (f.A.X - f.C.X) +
                          (f.C.X - f.B.X) * (f.A.Y - f.C.Y);
        u = v = w = 0;
        if (Math.Abs(denominator) < 1e-7) return false;
        u = ((f.B.Y - f.C.Y) * (p.X - f.C.X) + (f.C.X - f.B.X) * (p.Y - f.C.Y)) / denominator;
        v = ((f.C.Y - f.A.Y) * (p.X - f.C.X) + (f.A.X - f.C.X) * (p.Y - f.C.Y)) / denominator;
        w = 1 - u - v;
        return u >= -1e-5 && v >= -1e-5 && w >= -1e-5;
    }

    private void SelectFace(Point location)
    {
        if (_mesh is null) return;
        // Iterate front to back: last painted (nearest) triangle is selected.
        for (var i = _visibleFaces.Count - 1; i >= 0; i--)
        {
            var projected = _visibleFaces[i];
            if (!TryBarycentric(location, projected, out var u, out var v, out var w)) continue;
            var triangle = _mesh.Triangles[projected.Index];
            var a = _mesh.Vertices[triangle.A];
            var b = _mesh.Vertices[triangle.B];
            var c = _mesh.Vertices[triangle.C];
            var worldPoint = a * u + b * v + c * w;
            _selectedFace = projected.Index;
            Invalidate();
            PointSelected?.Invoke(worldPoint);
            return;
        }
        _selectedFace = -1;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var brush in _shadedBrushes) brush.Dispose();
        base.Dispose(disposing);
    }
}
