using Xunit;
using DCad.Boolean.Manifold;
using DCad.Core;
using DCad.Language;

namespace DCad.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void QuadTriangulatesToExactlyTwoTriangles()
    {
        Vector2d[] p = [new(0, 0), new(2, 0), new(2, 1), new(0, 1)];
        var triangles = PolygonTriangulator.Triangulate(p);
        Assert.Equal(2, triangles.Count);
    }

    [Fact]
    public void ConcavePolygonTriangulatesToNMinusTwo()
    {
        Vector2d[] p = [new(0, 0), new(3, 0), new(3, 3), new(1.5, 1), new(0, 3)];
        var triangles = PolygonTriangulator.Triangulate(p);
        Assert.Equal(p.Length - 2, triangles.Count);
    }

    [Fact]
    public void SelfIntersectingPolygonIsRejected()
    {
        Vector2d[] bowTie = [new(0, 0), new(2, 2), new(0, 2), new(2, 0)];
        Assert.Throws<ArgumentException>(() => PolygonTriangulator.Triangulate(bowTie));
    }

    [Fact]
    public void ManifoldBooleanProducesClosedOrientedMesh()
    {
        var k = new ManifoldKernel();
        using var a = k.Box(10, 10, 10);
        using var rawB = k.Box(10, 10, 10);
        using var b = k.Translate(rawB, 5, 0, 0);
        using var union = k.Union(a, b);
        using var intersection = k.Intersection(a, b);
        using var difference = k.Difference(a, b);

        Assert.InRange(union.Volume, 1499.99, 1500.01);
        Assert.InRange(intersection.Volume, 499.99, 500.01);
        Assert.InRange(difference.Volume, 499.99, 500.01);
        Assert.True(MeshValidator.Validate(union.ToMesh()).IsClosedOrientedManifold);
        Assert.True(MeshValidator.Validate(intersection.ToMesh()).IsClosedOrientedManifold);
        Assert.True(MeshValidator.Validate(difference.ToMesh()).IsClosedOrientedManifold);
    }

    [Fact]
    public void PointClassificationUsesDeterministicSolidAngleNotRandomRays()
    {
        var k = new ManifoldKernel();
        using var cube = k.Box(10, 10, 10);
        var mesh = cube.ToMesh();
        Assert.Equal(PointContainment.Inside, SolidQueries.ClassifyPoint(mesh, new(0, 0, 0)));
        Assert.Equal(PointContainment.Outside, SolidQueries.ClassifyPoint(mesh, new(20, 0, 0)));
        Assert.Equal(PointContainment.Boundary, SolidQueries.ClassifyPoint(mesh, new(5, 0, 0)));
    }

    [Fact]
    public void CadLanguageBuildsBooleanTreeWithUnits()
    {
        const string script = """
            param w = 20mm;
            let base = box(w, 10mm, 10mm);
            let drill = cylinder(20mm, 2mm);
            solid result = base - drill;
            """;
        var k = new ManifoldKernel();
        using var doc = CadScript.Execute(script, k);
        Assert.Equal(20, doc.Parameters["w"]);
        Assert.InRange(doc.Result.Volume, 1800, 1950);
        Assert.True(MeshValidator.Validate(doc.Result.ToMesh()).IsClosedOrientedManifold);
    }
}
