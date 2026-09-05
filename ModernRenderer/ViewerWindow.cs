using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace DCad.Renderer;

public sealed class ViewerWindow : GameWindow
{
    int vao, vbo, ebo;
    ShaderProgram? shader;
    readonly MeshData mesh;
    float yaw = -35f, pitch = 24f, distance = 5.5f;
    Vector3 target = Vector3.Zero;
    bool dragging;
    Vector2 lastMouse;
    bool wireframe;
    bool heatmap = true;

    const string VertexShader = """
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
        vec4 world=uModel*vec4(aPosition,1.0);
        vWorld=world.xyz;
        vNormal=mat3(transpose(inverse(uModel)))*aNormal;
        vScalar=aScalar;
        gl_Position=uProjection*uView*world;
    }
    """;

    const string FragmentShader = """
    #version 330 core
    in vec3 vNormal;
    in vec3 vWorld;
    in float vScalar;
    uniform vec3 uEye;
    uniform int uHeatmap;
    out vec4 FragColor;
    vec3 turbo(float x){
        x=clamp(x,0.0,1.0);
        vec3 c=vec3(1.0-abs(x*2.0-1.0), x, 1.0-x);
        return clamp(vec3(c.x*1.15+0.1,c.y*0.9+0.08,c.z*1.05+0.05),0.0,1.0);
    }
    void main(){
        vec3 base=uHeatmap==1?turbo(vScalar):vec3(0.58,0.68,0.80);
        vec3 N=normalize(vNormal);
        vec3 L=normalize(vec3(0.4,0.8,0.55));
        vec3 V=normalize(uEye-vWorld);
        vec3 H=normalize(L+V);
        float diffuse=max(dot(N,L),0.0);
        float spec=pow(max(dot(N,H),0.0),48.0);
        vec3 color=base*(0.22+0.78*diffuse)+vec3(0.22)*spec;
        FragColor=vec4(color,1.0);
    }
    """;

    public ViewerWindow(MeshData mesh)
        : base(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new Vector2i(1280, 800),
            Title = "DCad Modern Renderer — OpenTK 4 / OpenGL 3.3",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible
        }) => this.mesh = mesh;

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.025f,0.032f,0.045f,1f);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(CullFaceMode.Back);
        GL.Enable(EnableCap.Multisample);

        shader = new ShaderProgram(VertexShader,FragmentShader);
        vao=GL.GenVertexArray();vbo=GL.GenBuffer();ebo=GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer,vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,mesh.Vertices.Length*sizeof(float),mesh.Vertices,BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer,ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,mesh.Indices.Length*sizeof(uint),mesh.Indices,BufferUsageHint.StaticDraw);
        int stride=7*sizeof(float);
        GL.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,stride,0);GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1,3,VertexAttribPointerType.Float,false,stride,3*sizeof(float));GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2,1,VertexAttribPointerType.Float,false,stride,6*sizeof(float));GL.EnableVertexAttribArray(2);
        GL.BindVertexArray(0);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);GL.Viewport(0,0,Size.X,Size.Y);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
        GL.PolygonMode(MaterialFace.FrontAndBack,wireframe?PolygonMode.Line:PolygonMode.Fill);

        Vector3 eye=Eye();
        Matrix4 model=Matrix4.Identity;
        Matrix4 view=Matrix4.LookAt(eye,target,Vector3.UnitY);
        Matrix4 projection=Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(50f),Size.X/(float)Math.Max(1,Size.Y),0.05f,1000f);
        shader!.Use();shader.Set("uModel",model);shader.Set("uView",view);shader.Set("uProjection",projection);shader.Set("uEye",eye);shader.Set("uHeatmap",heatmap?1:0);
        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Triangles,mesh.Indices.Length,DrawElementsType.UnsignedInt,0);
        GL.BindVertexArray(0);
        SwapBuffers();
    }

    Vector3 Eye()
    {
        float yr=MathHelper.DegreesToRadians(yaw),pr=MathHelper.DegreesToRadians(pitch),cp=MathF.Cos(pr);
        return target+new Vector3(distance*cp*MathF.Cos(yr),distance*MathF.Sin(pr),distance*cp*MathF.Sin(yr));
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button==MouseButton.Left){dragging=true;lastMouse=MousePosition;}
    }
    protected override void OnMouseUp(MouseButtonEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButton.Left)dragging=false;}
    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);if(!dragging)return;Vector2 cur=MousePosition;Vector2 d=cur-lastMouse;lastMouse=cur;yaw+=d.X*.35f;pitch=Math.Clamp(pitch-d.Y*.35f,-85f,85f);
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e){base.OnMouseWheel(e);distance=Math.Clamp(distance*MathF.Pow(.88f,e.OffsetY),1.2f,500f);}
    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Key==Keys.Escape)Close();
        if(e.Key==Keys.W)wireframe=!wireframe;
        if(e.Key==Keys.H)heatmap=!heatmap;
        if(e.Key==Keys.F){yaw=-35;pitch=24;distance=5.5f;target=Vector3.Zero;}
    }

    protected override void OnUnload()
    {
        GL.DeleteBuffer(vbo);GL.DeleteBuffer(ebo);GL.DeleteVertexArray(vao);shader?.Dispose();base.OnUnload();
    }
}
