using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace DCad.Renderer;

public sealed class ShaderProgram : IDisposable
{
    public int Handle { get; }

    public ShaderProgram(string vertexSource, string fragmentSource)
    {
        int vs = Compile(ShaderType.VertexShader, vertexSource);
        int fs = Compile(ShaderType.FragmentShader, fragmentSource);
        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vs);
        GL.AttachShader(Handle, fs);
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(Handle));
        GL.DetachShader(Handle, vs); GL.DetachShader(Handle, fs);
        GL.DeleteShader(vs); GL.DeleteShader(fs);
    }

    static int Compile(ShaderType type, string source)
    {
        int id = GL.CreateShader(type);
        GL.ShaderSource(id, source);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new InvalidOperationException(GL.GetShaderInfoLog(id));
        return id;
    }

    public void Use() => GL.UseProgram(Handle);
    public void Set(string name, Matrix4 value) => GL.UniformMatrix4(GL.GetUniformLocation(Handle, name), false, ref value);
    public void Set(string name, Vector3 value) => GL.Uniform3(GL.GetUniformLocation(Handle, name), value);
    public void Set(string name, float value) => GL.Uniform1(GL.GetUniformLocation(Handle, name), value);
    public void Set(string name, int value) => GL.Uniform1(GL.GetUniformLocation(Handle, name), value);
    public void Dispose() => GL.DeleteProgram(Handle);
}
