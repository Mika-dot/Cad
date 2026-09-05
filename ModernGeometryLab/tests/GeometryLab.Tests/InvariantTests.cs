using DCad.Boolean.Manifold;
using DCad.Core;
using Xunit;

namespace GeometryLab.Tests;

public sealed class InvariantTests
{
    [Fact]
    public void ConcaveTriangulationPreservesAreaAndNMinusTwo()
    {
        Vector2d[] p = [new(0,0), new(5,0), new(5,4), new(3,2), new(1,4), new(0,3)];
        var tris = PolygonTriangulator.Triangulate(p);
        Assert.Equal(p.Length - 2, tris.Count);
        var triangulatedArea = tris.Sum(t => Math.Abs(Vector2d.Cross(p[t.B] - p[t.A], p[t.C] - p[t.A])) * .5);
        Assert.InRange(Math.Abs(triangulatedArea - Math.Abs(PolygonTriangulator.SignedArea(p))), 0, 1e-10);
    }

    [Fact]
    public void BooleanVolumeIdentitiesHoldAcrossManyOverlaps()
    {
        var k = new ManifoldKernel();
        for (var i = 0; i < 21; i++)
        {
            var tx = -4.25 + i * 0.425;
            using var a = k.Box(10, 10, 10);
            using var b0 = k.Box(6, 7, 8);
            using var b = k.Translate(b0, tx, 0.37, -0.61);
            using var u = k.Union(a, b);
            using var n = k.Intersection(a, b);
            using var d = k.Difference(a, b);

            Assert.InRange(Math.Abs((u.Volume + n.Volume) - (a.Volume + b.Volume)), 0, 2e-3);
            Assert.InRange(Math.Abs((d.Volume + n.Volume) - a.Volume), 0, 2e-3);
            Assert.True(MeshValidator.Validate(u.ToMesh()).IsClosedOrientedManifold);
            Assert.True(MeshValidator.Validate(n.ToMesh()).IsClosedOrientedManifold || n.Volume < 1e-8);
            Assert.True(MeshValidator.Validate(d.ToMesh()).IsClosedOrientedManifold || d.Volume < 1e-8);
        }
    }

    [Fact]
    public void PointInsideClassificationIsRepeatable()
    {
        var k = new ManifoldKernel();
        using var cube = k.Box(10, 10, 10);
        var mesh = cube.ToMesh();
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(PointContainment.Inside, SolidQueries.ClassifyPoint(mesh, new(1.234, -2.345, 3.456)));
            Assert.Equal(PointContainment.Outside, SolidQueries.ClassifyPoint(mesh, new(7, 7, 7)));
            Assert.Equal(PointContainment.Boundary, SolidQueries.ClassifyPoint(mesh, new(5, 1, -2)));
        }
    }

    [Fact]
    public void BowTieAndZeroLengthPolygonEdgesAreRejected()
    {
        Vector2d[] bowTie = [new(0,0), new(2,2), new(0,2), new(2,0)];
        Vector2d[] duplicate = [new(0,0), new(2,0), new(2,0), new(0,2)];
        Assert.Throws<ArgumentException>(() => PolygonTriangulator.Triangulate(bowTie));
        Assert.Throws<ArgumentException>(() => PolygonTriangulator.Triangulate(duplicate));
    }
}
