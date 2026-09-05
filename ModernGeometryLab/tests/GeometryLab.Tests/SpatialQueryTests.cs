using DCad.Core;
using Xunit;

namespace GeometryLab.Tests;

public sealed class SpatialQueryTests
{
    [Fact]
    public void RayTriangleHitReturnsStableBarycentricCoordinates()
    {
        var tri = new Triangle3d(new(0,0,0), new(2,0,0), new(0,2,0));
        var ray = new Ray3d(new(.5,.5,3), new(0,0,-1));
        Assert.True(SpatialQueries.IntersectRayTriangle(ray, tri, out var hit));
        Assert.InRange(Math.Abs(hit.Distance - 3.0), 0, 1e-12);
        Assert.InRange(Math.Abs(hit.Position.X - .5), 0, 1e-12);
        Assert.InRange(Math.Abs(hit.Position.Y - .5), 0, 1e-12);
        Assert.InRange(Math.Abs(hit.Position.Z), 0, 1e-12);
        Assert.InRange(hit.U, .249999999, .250000001);
        Assert.InRange(hit.V, .249999999, .250000001);
    }

    [Fact]
    public void RayAabbRejectsParallelOutsideRay()
    {
        var box = new Aabb3d(new(-1,-1,-1), new(1,1,1));
        Assert.False(SpatialQueries.IntersectRayAabb(
            new Ray3d(new(2,0,0), new(0,0,1)), box, out _, out _));
        Assert.True(SpatialQueries.IntersectRayAabb(
            new Ray3d(new(0,0,3), new(0,0,-1)), box, out var a, out var b));
        Assert.InRange(Math.Abs(a-2), 0, 1e-12);
        Assert.InRange(Math.Abs(b-4), 0, 1e-12);
    }

    [Fact]
    public void ClosestPointOnTriangleWorksAcrossVoronoiRegions()
    {
        var tri = new Triangle3d(new(0,0,0), new(2,0,0), new(0,2,0));
        Assert.Equal(new Vector3d(0,0,0), SpatialQueries.ClosestPointOnTriangle(new(-3,-2,1), tri));
        var face = SpatialQueries.ClosestPointOnTriangle(new(.5,.5,4), tri);
        Assert.InRange((face-new Vector3d(.5,.5,0)).Length, 0, 1e-12);
    }

    [Fact]
    public void MortonCodeHasDistinctLocalCells()
    {
        var codes = new HashSet<ulong>();
        for(uint x=0;x<8;x++)
            for(uint y=0;y<8;y++)
                for(uint z=0;z<8;z++)
                    Assert.True(codes.Add(SpatialQueries.Morton3D(x,y,z)));
        Assert.Equal(512, codes.Count);
    }
}
