namespace DCad.Core;

public interface ISolid : IDisposable
{
    double Volume { get; }
    double SurfaceArea { get; }
    Mesh3d ToMesh();
}

public interface IModelingKernel
{
    ISolid Box(double x, double y, double z, bool centered = true);
    ISolid Sphere(double radius, int segments = 48);
    ISolid Cylinder(double height, double radius, int segments = 48, bool centered = true);
    ISolid Union(ISolid a, ISolid b);
    ISolid Difference(ISolid a, ISolid b);
    ISolid Intersection(ISolid a, ISolid b);
    ISolid Translate(ISolid solid, double x, double y, double z);
    ISolid RotateDegrees(ISolid solid, double x, double y, double z);
    ISolid Scale(ISolid solid, double x, double y, double z);
}
