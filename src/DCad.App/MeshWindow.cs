using DCad.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using DVector3d = DCad.Core.Vector3d;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace DCad.App;

public sealed class MeshWindow : GameWindow
{
    private readonly float[] _vertices;
    private int _vao, _vbo, _program, _mvpLocation, _modelLocation;
    private float _yaw = 35f, _pitch = 25f, _distance;
    private Vector3 _center;
    private bool _wireframe;

    public MeshWindow(GameWindowSettings game, NativeWindowSettings native, Mesh3d mesh) : base(game, native)
    {
        var list = new List<float>(mesh.Triangles.Count * 18);
        foreach (var ti in mesh.Triangles)
        {
            var a = mesh.Vertices[ti.A]; var b = mesh.Vertices[ti.B]; var c = mesh.Vertices[ti.C];
            var n = DVector3d.Cross(b - a, c - a).Normalized();
            Add(a, n); Add(b, n); Add(c, n);
        }
        _vertices = list.ToArray();
        var bounds = mesh.Bounds;
        _center = new((float)((bounds.Min.X + bounds.Max.X) * .5), (float)((bounds.Min.Y + bounds.Max.Y) * .5), (float)((bounds.Min.Z + bounds.Max.Z) * .5));
        _distance = (float)Math.Max(10, bounds.Diagonal * 1.8);

        void Add(DVector3d p, DVector3d n)
        {
            list.Add((float)p.X); list.Add((float)p.Y); list.Add((float)p.Z);
            list.Add((float)n.X); list.Add((float)n.Y); list.Add((float)n.Z);
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.055f, 0.065f, 0.08f, 1f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);

        _program = CreateProgram(VertexShader, FragmentShader);
        _mvpLocation = GL.GetUniformLocation(_program, "uMvp");
        _modelLocation = GL.GetUniformLocation(_program, "uModel");
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
    }

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        if (KeyboardState.IsKeyDown(Keys.Escape)) Close();
        var step = (float)(70 * args.Time);
        if (KeyboardState.IsKeyDown(Keys.Left)) _yaw -= step;
        if (KeyboardState.IsKeyDown(Keys.Right)) _yaw += step;
        if (KeyboardState.IsKeyDown(Keys.Up)) _pitch = Math.Clamp(_pitch + step, -89, 89);
        if (KeyboardState.IsKeyDown(Keys.Down)) _pitch = Math.Clamp(_pitch - step, -89, 89);
        if (KeyboardState.IsKeyDown(Keys.PageUp)) _distance *= (float)Math.Pow(0.2, args.Time);
        if (KeyboardState.IsKeyDown(Keys.PageDown)) _distance *= (float)Math.Pow(5.0, args.Time);
        if (KeyboardState.IsKeyPressed(Keys.F1)) _wireframe = !_wireframe;
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        GL.PolygonMode(TriangleFace.FrontAndBack, _wireframe ? PolygonMode.Line : PolygonMode.Fill);

        var yr = MathHelper.DegreesToRadians(_yaw);
        var pr = MathHelper.DegreesToRadians(_pitch);
        var direction = new Vector3(MathF.Cos(pr) * MathF.Cos(yr), MathF.Sin(pr), MathF.Cos(pr) * MathF.Sin(yr));
        var eye = _center + direction * _distance;
        var view = Matrix4.LookAt(eye, _center, Vector3.UnitZ);
        var aspect = Math.Max(1f, Size.X / (float)Math.Max(1, Size.Y));
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45f), aspect, Math.Max(.01f, _distance / 1000f), _distance * 10f);
        var model = Matrix4.Identity;
        var mvp = model * view * projection;

        GL.UseProgram(_program);
        GL.UniformMatrix4(_mvpLocation, false, ref mvp);
        GL.UniformMatrix4(_modelLocation, false, ref model);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _vertices.Length / 6);
        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnUnload()
    {
        GL.DeleteBuffer(_vbo); GL.DeleteVertexArray(_vao); GL.DeleteProgram(_program);
        base.OnUnload();
    }

    private static int CreateProgram(string vertex, string fragment)
    {
        var vs = Compile(ShaderType.VertexShader, vertex);
        var fs = Compile(ShaderType.FragmentShader, fragment);
        var p = GL.CreateProgram(); GL.AttachShader(p, vs); GL.AttachShader(p, fs); GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out var ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(p));
        GL.DetachShader(p, vs); GL.DetachShader(p, fs); GL.DeleteShader(vs); GL.DeleteShader(fs);
        return p;
    }

    private static int Compile(ShaderType type, string source)
    {
        var s = GL.CreateShader(type); GL.ShaderSource(s, source); GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out var ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(s));
        return s;
    }

    private const string VertexShader = """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        layout(location=1) in vec3 aNormal;
        uniform mat4 uMvp;
        uniform mat4 uModel;
        out vec3 vNormal;
        void main(){ gl_Position = vec4(aPosition,1.0) * uMvp; vNormal = normalize(mat3(uModel) * aNormal); }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec3 vNormal;
        out vec4 color;
        void main(){
            vec3 light = normalize(vec3(0.4,0.6,0.7));
            float diffuse = max(dot(normalize(vNormal), light), 0.0);
            float rim = pow(1.0 - abs(vNormal.z), 2.0) * 0.12;
            vec3 base = vec3(0.25,0.62,0.92);
            color = vec4(base * (0.24 + 0.76*diffuse) + rim, 1.0);
        }
        """;
}
