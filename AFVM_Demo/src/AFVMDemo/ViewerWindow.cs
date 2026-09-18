using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace AFVMDemo;

public sealed class ViewerWindow : GameWindow
{
    private sealed class GpuMesh
    {
        public int Vao, Vbo, Ebo, IndexCount;
    }

    private sealed class GpuLines
    {
        public int Vao, Vbo, VertexCount;
    }

    private sealed class GpuModel
    {
        public required ResearchModel Model { get; init; }
        public required GpuMesh Surface { get; init; }
        public required GpuMesh Planes { get; init; }
        public required GpuLines Grid { get; init; }
        public required GpuLines Normals { get; init; }
    }

    private readonly ResearchModel[] _models;
    private readonly Box3d _domain;
    private readonly double _tolerance;
    private GpuModel[] _gpu = [];

    private int _meshProgram, _lineProgram;
    private int _active = 3;
    private bool _overview = true;
    private bool _showSurface = true;
    private bool _showGrid = true;
    private bool _showPlanes = true;
    private bool _showNormals;
    private bool _heatmap = true;
    private bool _wireframe;

    private float _yaw = -38f, _pitch = 25f, _distance = 5.6f;
    private Vector3 _target = Vector3.Zero;
    private bool _rotateDrag, _panDrag;
    private Vector2 _lastMouse;

    public ViewerWindow(ResearchModel[] models, Box3d domain, double tolerance)
        : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new Vector2i(1500, 940),
            MinimumClientSize = new Vector2i(900, 600),
            Title = "AFVM research demonstrator",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible
        })
    {
        _models = models;
        _domain = domain;
        _tolerance = tolerance;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Multisample);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);

        _meshProgram = CreateProgram(MeshVertexShader, MeshFragmentShader);
        _lineProgram = CreateProgram(LineVertexShader, LineFragmentShader);

        _gpu = _models.Select(m => new GpuModel
        {
            Model = m,
            Surface = UploadMesh(m.Surface),
            Planes = UploadMesh(m.Visual.PlaneMesh),
            Grid = UploadLines(m.Visual.GridLines),
            Normals = UploadLines(m.Visual.NormalLines)
        }).ToArray();

        ResetCamera();
        UpdateTitle();
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        if (_overview) RenderOverview();
        else RenderSingle(_active, 0, 0, Size.X, Size.Y);
        GL.Disable(EnableCap.ScissorTest);
        SwapBuffers();
    }

    private void RenderOverview()
    {
        int leftWidth = Size.X / 2;
        int rightWidth = Size.X - leftWidth;
        int bottomHeight = Size.Y / 2;
        int topHeight = Size.Y - bottomHeight;

        RenderSingle(0, 0, bottomHeight, leftWidth, topHeight);
        RenderSingle(1, leftWidth, bottomHeight, rightWidth, topHeight);
        RenderSingle(2, 0, 0, leftWidth, bottomHeight);
        RenderSingle(3, leftWidth, 0, rightWidth, bottomHeight);
    }

    private void RenderSingle(int modelIndex, int x, int y, int width, int height)
    {
        var gpu = _gpu[modelIndex];
        var tint = gpu.Model.BaseColor;
        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(x, y, Math.Max(1, width), Math.Max(1, height));
        GL.Viewport(x, y, Math.Max(1, width), Math.Max(1, height));
        GL.ClearColor(0.018f + tint.X * 0.020f, 0.022f + tint.Y * 0.020f, 0.032f + tint.Z * 0.020f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var eye = Eye();
        var model = Matrix4.Identity;
        var view = Matrix4.LookAt(eye, _target, Vector3.UnitZ);
        float aspect = Math.Max(0.05f, width / (float)Math.Max(1, height));
        float near = Math.Max(0.01f, _distance / 500f);
        float far = Math.Max(50f, _distance * 20f);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(46f), aspect, near, far);

        if (_showSurface && gpu.Surface.IndexCount > 0)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.PolygonMode(TriangleFace.FrontAndBack, _wireframe ? PolygonMode.Line : PolygonMode.Fill);
            UseMeshProgram(model, view, projection, eye, gpu.Model.BaseColor, 1f, _heatmap && modelIndex != 0, lighting: true);
            GL.BindVertexArray(gpu.Surface.Vao);
            GL.DrawElements(PrimitiveType.Triangles, gpu.Surface.IndexCount, DrawElementsType.UnsignedInt, 0);
        }

        if (_showPlanes && gpu.Planes.IndexCount > 0)
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(false);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(-1.2f, -1.2f);
            UseMeshProgram(model, view, projection, eye, new Vector3(.15f, .92f, .95f), .34f, _heatmap, lighting: false);
            GL.BindVertexArray(gpu.Planes.Vao);
            GL.DrawElements(PrimitiveType.Triangles, gpu.Planes.IndexCount, DrawElementsType.UnsignedInt, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.DepthMask(true);
        }

        if (_showGrid && gpu.Grid.VertexCount > 0)
        {
            UseLineProgram(model, view, projection, new Vector3(.12f, .56f, 1f), new Vector3(.96f, .22f, .82f), .68f);
            GL.BindVertexArray(gpu.Grid.Vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, gpu.Grid.VertexCount);
        }

        if (_showNormals && gpu.Normals.VertexCount > 0)
        {
            UseLineProgram(model, view, projection, new Vector3(1f, .86f, .12f), new Vector3(1f, .18f, .08f), .92f);
            GL.BindVertexArray(gpu.Normals.Vao);
            GL.DrawArrays(PrimitiveType.Lines, 0, gpu.Normals.VertexCount);
        }

        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
    }

    private void UseMeshProgram(Matrix4 model, Matrix4 view, Matrix4 projection, Vector3 eye, Vector3 color, float alpha, bool heatmap, bool lighting)
    {
        GL.UseProgram(_meshProgram);
        SetMatrix(_meshProgram, "uModel", model);
        SetMatrix(_meshProgram, "uView", view);
        SetMatrix(_meshProgram, "uProjection", projection);
        GL.Uniform3(GL.GetUniformLocation(_meshProgram, "uEye"), eye);
        GL.Uniform3(GL.GetUniformLocation(_meshProgram, "uBaseColor"), color);
        GL.Uniform1(GL.GetUniformLocation(_meshProgram, "uAlpha"), alpha);
        GL.Uniform1(GL.GetUniformLocation(_meshProgram, "uHeatmap"), heatmap ? 1 : 0);
        GL.Uniform1(GL.GetUniformLocation(_meshProgram, "uLighting"), lighting ? 1 : 0);
    }

    private void UseLineProgram(Matrix4 model, Matrix4 view, Matrix4 projection, Vector3 low, Vector3 high, float alpha)
    {
        GL.UseProgram(_lineProgram);
        var mvp = projection * view * model;
        SetMatrix(_lineProgram, "uMvp", mvp);
        GL.Uniform3(GL.GetUniformLocation(_lineProgram, "uLowColor"), low);
        GL.Uniform3(GL.GetUniformLocation(_lineProgram, "uHighColor"), high);
        GL.Uniform1(GL.GetUniformLocation(_lineProgram, "uAlpha"), alpha);
    }

    private GpuMesh UploadMesh(RenderMesh mesh)
    {
        if (mesh.Vertices.Length == 0 || mesh.Indices.Length == 0) return new GpuMesh();
        var gpu = new GpuMesh
        {
            Vao = GL.GenVertexArray(),
            Vbo = GL.GenBuffer(),
            Ebo = GL.GenBuffer(),
            IndexCount = mesh.Indices.Length
        };
        GL.BindVertexArray(gpu.Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, gpu.Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, mesh.Vertices.Length * sizeof(float), mesh.Vertices, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, gpu.Ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, mesh.Indices.Length * sizeof(uint), mesh.Indices, BufferUsageHint.StaticDraw);
        int stride = 7 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        return gpu;
    }

    private GpuLines UploadLines(float[] vertices)
    {
        if (vertices.Length == 0) return new GpuLines();
        var gpu = new GpuLines
        {
            Vao = GL.GenVertexArray(),
            Vbo = GL.GenBuffer(),
            VertexCount = vertices.Length / 4
        };
        GL.BindVertexArray(gpu.Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, gpu.Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        int stride = 4 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        return gpu;
    }

    private Vector3 Eye()
    {
        float yr = MathHelper.DegreesToRadians(_yaw);
        float pr = MathHelper.DegreesToRadians(_pitch);
        float cp = MathF.Cos(pr);
        return _target + new Vector3(_distance * cp * MathF.Cos(yr), _distance * cp * MathF.Sin(yr), _distance * MathF.Sin(pr));
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Keys.Escape) Close();
        else if (e.Key == Keys.F1) _overview = !_overview;
        else if (e.Key is Keys.D1 or Keys.KeyPad1) Select(0);
        else if (e.Key is Keys.D2 or Keys.KeyPad2) Select(1);
        else if (e.Key is Keys.D3 or Keys.KeyPad3) Select(2);
        else if (e.Key is Keys.D4 or Keys.KeyPad4) Select(3);
        else if (e.Key == Keys.Tab) { _active = (_active + 1) % _models.Length; _overview = false; }
        else if (e.Key == Keys.G) _showGrid = !_showGrid;
        else if (e.Key == Keys.P) _showPlanes = !_showPlanes;
        else if (e.Key == Keys.N) _showNormals = !_showNormals;
        else if (e.Key == Keys.H) _heatmap = !_heatmap;
        else if (e.Key == Keys.W) _wireframe = !_wireframe;
        else if (e.Key == Keys.Space) _showSurface = !_showSurface;
        else if (e.Key == Keys.F) ResetCamera();
        UpdateTitle();
    }

    private void Select(int index)
    {
        _active = Math.Clamp(index, 0, _models.Length - 1);
        _overview = false;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _lastMouse = MousePosition;
        if (e.Button == MouseButton.Left) _rotateDrag = true;
        if (e.Button == MouseButton.Right) _panDrag = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButton.Left) _rotateDrag = false;
        if (e.Button == MouseButton.Right) _panDrag = false;
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        var cur = MousePosition;
        var d = cur - _lastMouse;
        _lastMouse = cur;

        if (_rotateDrag)
        {
            _yaw += d.X * .32f;
            _pitch = Math.Clamp(_pitch - d.Y * .32f, -88f, 88f);
        }
        if (_panDrag)
        {
            var eye = Eye();
            var forward = (_target - eye).Normalized();
            var right = Vector3.Cross(forward, Vector3.UnitZ).Normalized();
            if (right.LengthSquared < 1e-8f) right = Vector3.UnitX;
            var up = Vector3.Cross(right, forward).Normalized();
            float scale = _distance * .0018f;
            _target += (-d.X * right + d.Y * up) * scale;
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _distance = Math.Clamp(_distance * MathF.Pow(.86f, e.OffsetY), 1.8f, 40f);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, Math.Max(1, e.Width), Math.Max(1, e.Height));
    }

    private void ResetCamera()
    {
        _yaw = -38f;
        _pitch = 25f;
        _target = Vector3.Zero;
        _distance = (float)Math.Max(5.2, _domain.Diagonal * 1.03);
    }

    private void UpdateTitle()
    {
        string overlay = $"G:grid={OnOff(_showGrid)}  P:planes={OnOff(_showPlanes)}  N:normals={OnOff(_showNormals)}  H:error={OnOff(_heatmap)}  W:wire={OnOff(_wireframe)}";
        if (_overview)
        {
            Title = $"AFVM | OVERVIEW: TL Exact | TR Uniform | BL Adaptive-direct | BR Adaptive-R | {overlay} | F1 inspect/overview";
            return;
        }

        var m = _models[_active];
        string error = m.Metrics.Samples == 0
            ? "analytic reference"
            : $"RMS={m.Metrics.RmsDistance:F5}, max={m.Metrics.MaxDistance:F5}, normal={m.Metrics.MeanNormalAngleDeg:F2} deg";
        Title = $"AFVM | {_active + 1}:{m.ShortName} | {error} | cells={m.Visual.CellCount:N0}, plane polygons={m.Visual.PlanePolygonCount:N0} | tol={_tolerance:G4} | {overlay}";
    }

    private static string OnOff(bool value) => value ? "on" : "off";

    protected override void OnUnload()
    {
        foreach (var model in _gpu)
        {
            Delete(model.Surface);
            Delete(model.Planes);
            Delete(model.Grid);
            Delete(model.Normals);
        }
        if (_meshProgram != 0) GL.DeleteProgram(_meshProgram);
        if (_lineProgram != 0) GL.DeleteProgram(_lineProgram);
        base.OnUnload();
    }

    private static void Delete(GpuMesh mesh)
    {
        if (mesh.Vbo != 0) GL.DeleteBuffer(mesh.Vbo);
        if (mesh.Ebo != 0) GL.DeleteBuffer(mesh.Ebo);
        if (mesh.Vao != 0) GL.DeleteVertexArray(mesh.Vao);
    }

    private static void Delete(GpuLines lines)
    {
        if (lines.Vbo != 0) GL.DeleteBuffer(lines.Vbo);
        if (lines.Vao != 0) GL.DeleteVertexArray(lines.Vao);
    }

    private static void SetMatrix(int program, string name, Matrix4 matrix)
    {
        int location = GL.GetUniformLocation(program, name);
        GL.UniformMatrix4(location, false, ref matrix);
    }

    private static int CreateProgram(string vertexText, string fragmentText)
    {
        int vs = Compile(ShaderType.VertexShader, vertexText);
        int fs = Compile(ShaderType.FragmentShader, fragmentText);
        int program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(program));
        GL.DetachShader(program, vs);
        GL.DetachShader(program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        return program;
    }

    private static int Compile(ShaderType type, string text)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, text);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(shader));
        return shader;
    }

    private const string MeshVertexShader = """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in float aScalar;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        out vec3 vNormal;
        out vec3 vWorld;
        out float vScalar;
        void main(){
            vec4 world = uModel * vec4(aPosition,1.0);
            vWorld = world.xyz;
            vNormal = mat3(transpose(inverse(uModel))) * aNormal;
            vScalar = aScalar;
            gl_Position = uProjection * uView * world;
        }
        """;

    private const string MeshFragmentShader = """
        #version 330 core
        in vec3 vNormal;
        in vec3 vWorld;
        in float vScalar;
        uniform vec3 uEye;
        uniform vec3 uBaseColor;
        uniform float uAlpha;
        uniform int uHeatmap;
        uniform int uLighting;
        out vec4 FragColor;
        vec3 turbo(float x){
            x=clamp(x,0.0,1.0);
            float r=clamp(1.5-abs(4.0*x-3.0),0.0,1.0);
            float g=clamp(1.5-abs(4.0*x-2.0),0.0,1.0);
            float b=clamp(1.5-abs(4.0*x-1.0),0.0,1.0);
            return vec3(r,g,b);
        }
        void main(){
            vec3 base = uHeatmap==1 ? turbo(vScalar) : uBaseColor;
            vec3 color = base;
            if(uLighting==1){
                vec3 N=normalize(vNormal);
                vec3 L=normalize(vec3(.45,.70,.62));
                vec3 V=normalize(uEye-vWorld);
                vec3 H=normalize(L+V);
                float diffuse=max(dot(N,L),0.0);
                float spec=pow(max(dot(N,H),0.0),42.0);
                float rim=pow(1.0-max(dot(N,V),0.0),2.0);
                color=base*(.22+.78*diffuse)+vec3(.20)*spec+base*.12*rim;
            }
            FragColor=vec4(color,uAlpha);
        }
        """;

    private const string LineVertexShader = """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        layout(location=1) in float aScalar;
        uniform mat4 uMvp;
        out float vScalar;
        void main(){ vScalar=aScalar; gl_Position=uMvp*vec4(aPosition,1.0); }
        """;

    private const string LineFragmentShader = """
        #version 330 core
        in float vScalar;
        uniform vec3 uLowColor;
        uniform vec3 uHighColor;
        uniform float uAlpha;
        out vec4 FragColor;
        void main(){ FragColor=vec4(mix(uLowColor,uHighColor,clamp(vScalar,0.0,1.0)),uAlpha); }
        """;
}
