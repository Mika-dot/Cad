using DCad.Core;
using Xunit;

namespace GeometryLab.Tests;

public sealed class RobustPredicateTests
{
    [Fact]
    public void Orient2d_ResolvesTinyOffsetAtLargeCoordinate()
    {
        var a=new Vector2d(1_000_000.0,1_000_000.0);
        var b=new Vector2d(1_000_001.0,1_000_001.0);
        var c=new Vector2d(1_000_002.0,1_000_002.0000000001);
        Assert.Equal(OrientationSign.Positive,RobustPredicates.Orient2d(a,b,c));
    }

    [Fact]
    public void SegmentIntersection_ClassifiesProperCrossing()
    {
        var r=RobustPredicates.IntersectSegments(
            new Vector2d(0,0),new Vector2d(10,10),
            new Vector2d(0,10),new Vector2d(10,0));
        Assert.True(r.Intersects);
        Assert.True(r.Proper);
        Assert.False(r.Collinear);
        Assert.True(Math.Abs(r.Point.X-5)<1e-10);
        Assert.True(Math.Abs(r.Point.Y-5)<1e-10);
    }

    [Fact]
    public void SegmentIntersection_DetectsCollinearOverlap()
    {
        var r=RobustPredicates.IntersectSegments(
            new Vector2d(0,0),new Vector2d(10,0),
            new Vector2d(5,0),new Vector2d(15,0));
        Assert.True(r.Intersects);
        Assert.True(r.Collinear);
    }

    [Fact]
    public void Orient3d_DetectsCoplanarTetrahedron()
    {
        var s=RobustPredicates.Orient3d(
            new Vector3d(0,0,0),new Vector3d(1,0,0),new Vector3d(0,1,0),new Vector3d(0.2,0.2,0));
        Assert.Equal(OrientationSign.Zero,s);
    }
}
