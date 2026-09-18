using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace AFVMDemo;

public sealed class ViewerWindow : GameWindow
{
    private readonly (string name, RenderMesh mesh, Vector3 color)[] _models;
    private readonly float[] _gridLines;
    private readonly int[] _vaos, _vbos, _ebos;
    private int _gridVao, _gridVbo, _meshProgram, _lineProgram, _active;
    private bool _wireframe, _showGrid = true, _heatmap;
    private float _yaw = -35f, _pitch = 24f, _distance = 5.3f;
    private bool _dragging; private Vector2 _lastMouse;

    public ViewerWindow((string name, RenderMesh mesh, Vector3 color)[] models, float[] gridLines)
        : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new Vector2i(1360, 850), Title = "AFVM research demo",
            APIVersion = new Version(3, 3), Profile = ContextProfile.Core, Flags = ContextFlags.ForwardCompatible
        })
    {
        _models = models; _gridLines = gridLines;
        _vaos = new int[models.Length]; _vbos = new int[models.Length]; _ebos = new int[models.Length];
    }

    protected override void OnLoad()
    {
        base.OnLoad(); GL.ClearColor(0.025f, 0.032f, 0.045f, 1f); GL.Enable(EnableCap.DepthTest); GL.Enable(EnableCap.Multisample);
        _meshProgram = CreateProgram(MeshVs, MeshFs); _lineProgram = CreateProgram(LineVs, LineFs);
        for (int i = 0; i < _models.Length; i++) UploadMesh(i, _models[i].mesh);
        _gridVao = GL.GenVertexArray(); _gridVbo = GL.GenBuffer(); GL.BindVertexArray(_gridVao); GL.BindBuffer(BufferTarget.ArrayBuffer, _gridVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _gridLines.Length * sizeof(float), _gridLines, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0); GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float)); GL.EnableVertexAttribArray(1);
        UpdateTitle();
    }

    private void UploadMesh(int i, RenderMesh mesh)
    {
        _vaos[i] = GL.GenVertexArray(); _vbos[i] = GL.GenBuffer(); _ebos[i] = GL.GenBuffer(); GL.BindVertexArray(_vaos[i]);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbos[i]); GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(float), mesh.Vertices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebos[i]); GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices, BufferUsageHint.StaticDraw);
        int stride = 7 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0); GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float)); GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float)); GL.EnableVertexAttribArray(2);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e); GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        var eye = Eye(); var model = Matrix4.Identity; var view = Matrix4.LookAt(eye, Vector3.Zero, Vector3.UnitZ);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(48f), Size.X / (float)Math.Max(1, Size.Y), 0.02f, 100f);
        GL.PolygonMode(TriangleFace.FrontAndBack, _wireframe ? PolygonMode.Line : PolygonMode.Fill);
        GL.UseProgram(_meshProgram); SetMatrix(_meshProgram, "uModel", model); SetMatrix(_meshProgram, "uView", view); SetMatrix(_meshProgram, "uProjection", projection);
        GL.Uniform3(GL.GetUniformLocation(_meshProgram, "uEye"), eye); GL.Uniform3(GL.GetUniformLocation(_meshProgram, "uBaseColor"), _models[_active].color);
        GL.Uniform1(GL.GetUniformLocation(_meshProgram, "uHeatmap"), _heatmap ? 1 : 0); GL.BindVertexArray(_vaos[_active]);
        GL.DrawElements(PrimitiveType.Triangles, _models[_active].mesh.Indices.Length, DrawElementsType.UnsignedInt, 0);

        if (_showGrid)
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill); GL.Disable(EnableCap.DepthTest); GL.UseProgram(_lineProgram);
            var mvp = model * view * projection; SetMatrix(_lineProgram, "uMvp", mvp); GL.BindVertexArray(_gridVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _gridLines.Length / 4); GL.Enable(EnableCap.DepthTest);
        }
        SwapBuffers();
    }

    private Vector3 Eye()
    {
        float yr = MathHelper.DegreesToRadians(_yaw), pr = MathHelper.DegreesToRadians(_pitch), cp = MathF.Cos(pr);
        return new Vector3(_distance * cp * MathF.Cos(yr), _distance * cp * MathF.Sin(yr), _distance * MathF.Sin(pr));
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Keys.Escape) Close();
        if (e.Key is Keys.D1 or Keys.KeyPad1) _active = 0;
        if (e.Key is Keys.D2 or Keys.KeyPad2) _active = Math.Min(1, _models.Length - 1);
        if (e.Key is Keys.D3 or Keys.KeyPad3) _active = Math.Min(2, _models.Length - 1);
        if (e.Key is Keys.D4 or Keys.KeyPad4) _active = Math.Min(3, _models.Length - 1);
        if (e.Key == Keys.W) _wireframe = !_wireframe;
        if (e.Key == Keys.G) _showGrid = !_showGrid;
        if (e.Key == Keys.H) _heatmap = !_heatmap;
        if (e.Key == Keys.F) { _yaw = -35; _pitch = 24; _distance = 5.3f; }
        UpdateTitle();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButton.Left) { _dragging = true; _lastMouse = MousePosition; } }
    protected override void OnMouseUp(MouseButtonEventArgs e) { base.OnMouseUp(e); if (e.Button == MouseButton.Left) _dragging = false; }
    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e); if (!_dragging) return; var cur = MousePosition; var d = cur - _lastMouse; _lastMouse = cur;
        _yaw += d.X * .35f; _pitch = Math.Clamp(_pitch - d.Y * .35f, -85f, 85f);
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { base.OnMouseWheel(e); _distance = Math.Clamp(_distance * MathF.Pow(.88f, e.OffsetY), 2.0f, 30f); }
    protected override void OnResize(ResizeEventArgs e) { base.OnResize(e); GL.Viewport(0, 0, e.Width, e.Height); }

    private void UpdateTitle() => Title = $"AFVM demo — {_models[_active].name} | 1 exact  2 uniform FV  3 adaptive direct  4 adaptive R-algebra | G grid W wire H normals-color";

    protected override void OnUnload()
    {
        for (int i = 0; i < _models.Length; i++) { GL.DeleteBuffer(_vbos[i]); GL.DeleteBuffer(_ebos[i]); GL.DeleteVertexArray(_vaos[i]); }
        GL.DeleteBuffer(_gridVbo); GL.DeleteVertexArray(_gridVao); GL.DeleteProgram(_meshProgram); GL.DeleteProgram(_lineProgram); base.OnUnload();
    }

    private static void SetMatrix(int program, string name, Matrix4 m) => GL.UniformMatrix4(GL.GetUniformLocation(program, name), false, ref m);
    private static int CreateProgram(string vsText, string fsText)
    {
        int vs = Compile(ShaderType.VertexShader, vsText), fs = Compile(ShaderType.FragmentShader, fsText), p = GL.CreateProgram();
        GL.AttachShader(p, vs); GL.AttachShader(p, fs); GL.LinkProgram(p); GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(p));
        GL.DetachShader(p, vs); GL.DetachShader(p, fs); GL.DeleteShader(vs); GL.DeleteShader(fs); return p;
    }
    private static int Compile(ShaderType type, string text)
    {
        int s = GL.CreateShader(type); GL.ShaderSource(s, text); GL.CompileShader(s); GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(s)); return s;
    }

    private const string MeshVs = """
    #version 330 core
    layout(location=0) in vec3 aPosition; layout(location=1) in vec3 aNormal; layout(location=2) in float aScalar;
    uniform mat4 uModel,uView,uProjection; out vec3 vNormal; out vec3 vWorld; out float vScalar;
    void main(){ vec4 w=uModel*vec4(aPosition,1.0); vWorld=w.xyz; vNormal=mat3(transpose(inverse(uModel)))*aNormal; vScalar=aScalar; gl_Position=uProjection*uView*w; }
    """;
    private const string MeshFs = """
    #version 330 core
    in vec3 vNormal; in vec3 vWorld; in float vScalar; uniform vec3 uEye; uniform vec3 uBaseColor; uniform int uHeatmap; out vec4 FragColor;
    vec3 turbo(float x){ x=clamp(x,0.0,1.0); return clamp(vec3(1.15*(1.0-abs(2.0*x-1.0))+.1,.9*x+.08,1.05*(1.0-x)+.05),0.0,1.0); }
    void main(){ vec3 base=uHeatmap==1?turbo(vScalar):uBaseColor; vec3 N=normalize(vNormal),L=normalize(vec3(.4,.8,.55)),V=normalize(uEye-vWorld),H=normalize(L+V); float d=max(dot(N,L),0.0),s=pow(max(dot(N,H),0.0),48.0); FragColor=vec4(base*(.22+.78*d)+vec3(.22)*s,1.0); }
    """;
    private const string LineVs = """
    #version 330 core
    layout(location=0) in vec3 aPosition; layout(location=1) in float aLevel; uniform mat4 uMvp; out float vLevel;
    void main(){ vLevel=aLevel; gl_Position=uMvp*vec4(aPosition,1.0); }
    """;
    private const string LineFs = """
    #version 330 core
    in float vLevel; out vec4 FragColor; void main(){ FragColor=vec4(.22+.7*vLevel,.82-.35*vLevel,1.0-.55*vLevel,.82); }
    """;
}
