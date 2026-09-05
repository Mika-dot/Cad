using DCad.Core;
using NativeManifold = ManifoldNET.Manifold;

namespace DCad.Boolean.Manifold;

public sealed class ManifoldKernel : ICapabilityModelingKernel
{
    public KernelCapabilities Description => StandardKernelProfiles.ManifoldMesh;

    public ISolid Box(double x, double y, double z, bool centered = true) =>
        new ManifoldSolid(NativeManifold.Cube(F(x), F(y), F(z), centered));

    public ISolid Sphere(double radius, int segments = 48) =>
        new ManifoldSolid(NativeManifold.Sphere(F(radius), Math.Max(4, segments)));

    public ISolid Cylinder(double height, double radius, int segments = 48, bool centered = true) =>
        new ManifoldSolid(NativeManifold.Cylinder(F(height), F(radius), F(radius), Math.Max(4, segments), centered));

    public ISolid Union(ISolid a, ISolid b) =>
        new ManifoldSolid(NativeManifold.Union(Get(a), Get(b)));

    public ISolid Difference(ISolid a, ISolid b) =>
        new ManifoldSolid(NativeManifold.Difference(Get(a), Get(b)));

    public ISolid Intersection(ISolid a, ISolid b) =>
        new ManifoldSolid(NativeManifold.Intersection(Get(a), Get(b)));

    public ISolid Translate(ISolid solid, double x, double y, double z) =>
        new ManifoldSolid(Get(solid).Translate(F(x), F(y), F(z)));

    public ISolid RotateDegrees(ISolid solid, double x, double y, double z) =>
        new ManifoldSolid(Get(solid).Rotate(F(x), F(y), F(z)));

    public ISolid Scale(ISolid solid, double x, double y, double z) =>
        new ManifoldSolid(Get(solid).Scale(F(x), F(y), F(z)));

    private static NativeManifold Get(ISolid solid) => solid is ManifoldSolid m
        ? m.Native
        : throw new ArgumentException("Solid was created by a different modeling kernel.", nameof(solid));

    private static float F(double value)
    {
        if (!double.IsFinite(value) || value < -float.MaxValue || value > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value));
        return (float)value;
    }
}

internal sealed class ManifoldSolid : ISolid
{
    private bool _disposed;
    internal NativeManifold Native { get; }

    public ManifoldSolid(NativeManifold native)
    {
        Native = native ?? throw new ArgumentNullException(nameof(native));
        if (native.Status != ManifoldNET.ManifoldError.NoError)
            throw new InvalidOperationException($"Manifold rejected the mesh/operation: {native.Status}.");
    }

    public double Volume => Native.Properties.volume;
    public double SurfaceArea => Native.Properties.surface_area;

    public Mesh3d ToMesh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var mesh = Native.MeshGL;
        var props = mesh.VerticesProperties ?? throw new InvalidOperationException("Manifold returned no vertex properties.");
        var indices = mesh.TriangleVertices ?? throw new InvalidOperationException("Manifold returned no triangles.");
        var stride = mesh.PropertiesNumber;
        if (stride < 3) throw new InvalidOperationException("Mesh vertex stride is less than XYZ.");

        var vertices = new Vector3d[props.Length / stride];
        for (var i = 0; i < vertices.Length; i++)
        {
            var j = i * stride;
            vertices[i] = new(props[j], props[j + 1], props[j + 2]);
        }

        var triangles = new TriangleIndex[indices.Length / 3];
        for (var i = 0; i < triangles.Length; i++)
        {
            var j = i * 3;
            triangles[i] = new(indices[j], indices[j + 1], indices[j + 2]);
        }
        return new(vertices, triangles);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Native.Dispose();
        _disposed = true;
    }
}
