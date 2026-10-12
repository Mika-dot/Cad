using DCad.FunctionalVoxel;
using Xunit;

namespace DCad.FunctionalVoxel.Tests;

public sealed class FunctionalVoxelTests
{
    private static FunctionalGrid CubicGrid(int size = 8, double step = .5) =>
        new(new[] { -size * step / 2, -size * step / 2, -size * step / 2 },
            new[] { step, step, step }, new[] { size, size, size });

    [Fact]
    public void AnalyticSphereHasExactValueGradientAndHessian()
    {
        var j = FunctionalField.Sphere(0, 0, 0, 5).Evaluate(1, 2, 3);
        Assert.Equal(11.0, j.Value, 12);
        Assert.Equal(-2.0, j.Gradient(0), 12);
        Assert.Equal(-4.0, j.Gradient(1), 12);
        Assert.Equal(-6.0, j.Gradient(2), 12);
        Assert.Equal(-2.0, j.Hessian(0, 0), 12);
        Assert.Equal(-2.0, j.Hessian(2, 2), 12);
        Assert.Equal(0.0, j.Hessian(0, 1), 12);
    }

    [Fact]
    public void LocalPlaneIsTangentAndHasUnitExtendedNormal()
    {
        var p = new[] { 1.0, 2.0, 3.0 };
        var j = FunctionalField.Sphere(0, 0, 0, 5).Evaluate(p);
        var plane = LocalFunctionalPlane.FromJet(p, j);
        Assert.Equal(5, plane.ChannelCount);
        Assert.Equal(11.0, plane.ValueAt(p), 11);
        Assert.Equal(-2.0, plane.Gradient(0), 11);
        Assert.Equal(-4.0, plane.Gradient(1), 11);
        var length = Math.Sqrt(Enumerable.Range(0, 4).Sum(i => Math.Pow(plane.Coefficient(i), 2)));
        Assert.Equal(1.0, length, 12);
        var q = new[] { 1.01, 2.0, 3.0 };
        Assert.Equal(.0001, plane.ValueAt(q) - FunctionalField.Sphere(0, 0, 0, 5).Evaluate(q).Value, 8);
    }

    [Fact]
    public void SampleBuildsFiveIndependentMImagesForThreeArguments()
    {
        var model = FunctionalVoxelModel.Sample(FunctionalField.Sphere(0, 0, 0, 3),
            new FunctionalGrid(new[] { -3.0, -3.0, -3.0 },
                new[] { 2.0, 2.0, 2.0 }, new[] { 3, 3, 3 }));
        Assert.Equal(5, model.ChannelCount);
        Assert.Equal(27, model.VoxelCount);
        var middle = model.Grid.LinearIndex(1, 1, 1);
        Assert.Equal(9.0, model.ValueAtCentre(middle), 11);
        Assert.Equal(0.0, model.JetAtCentre(middle).Gradient(0), 11);
    }

    [Fact]
    public void AlphaRAlgebraRespectsInteriorSign()
    {
        var a = FunctionalField.Sphere(0, 0, 0, 2);
        var b = FunctionalField.Sphere(3, 0, 0, 2);
        Assert.True(a.Union(b).Evaluate(0, 0, 0).Value > 0);
        Assert.True(a.Intersection(b).Evaluate(0, 0, 0).Value < 0);
        Assert.True(a.Difference(b).Evaluate(0, 0, 0).Value > 0);
        Assert.True(a.Intersection(b).Evaluate(1.5, 0, 0).Value > 0);
        Assert.True(a.Union(b, alpha: .4).Evaluate(1.5, 0, 0).Value > 0);
        Assert.True(a.Difference(b).Evaluate(3, 0, 0).Value < 0);
    }

    [Fact]
    public void RCompositionFromMImagesMatchesAnalyticChainRuleAtCentres()
    {
        var g = CubicGrid(6, .7);
        var a = FunctionalField.Sphere(-.5, 0, 0, 2);
        var b = FunctionalField.Sphere(.5, 0, 0, 2);
        var model = FunctionalVoxelModel.Sample(a, g).Combine(FunctionalVoxelModel.Sample(b, g), ROperation.Intersection);
        var expected = a.Intersection(b);
        for (var id = 0; id < g.CellCount; id++)
        {
            var p = g.Centre(id);
            var actual = model.JetAtCentre(id);
            var reference = expected.Evaluate(p);
            Assert.Equal(reference.Value, actual.Value, 9);
            for (var axis = 0; axis < 3; axis++)
                Assert.Equal(reference.Gradient(axis), actual.Gradient(axis), 9);
        }
    }

    [Fact]
    public void CompositionDoesNotRequireOriginalAnalyticFunction()
    {
        var evaluations = 0;
        var source = FunctionalField.Analytic(2, p =>
        {
            evaluations++;
            var x = FunctionalJet.Variable(p, 0);
            var y = FunctionalJet.Variable(p, 1);
            return x * x + y * y;
        });
        var grid = new FunctionalGrid(new[] { -1.0, -1.0 }, new[] { .25, .25 }, new[] { 8, 8 });
        var model = FunctionalVoxelModel.Sample(source, grid);
        Assert.Equal(64, evaluations);
        var doubled = model.Combine(model, ROperation.Add);
        Assert.Equal(64, evaluations);
        var id = grid.LinearIndex(3, 4);
        Assert.Equal(2 * model.ValueAtCentre(id), doubled.ValueAtCentre(id), 11);
    }

    [Fact]
    public void RGB24RoundTripPreservesLocalsWithinQuantizationBounds()
    {
        var grid = CubicGrid(6, 1.0);
        var model = FunctionalVoxelModel.Sample(FunctionalField.Sphere(.1, .2, -.3, 2), grid);
        var packed = model.EncodeRgb24();
        var restored = packed.ToModel();
        for (var channel = 0; channel < 5; channel++)
        {
            var tolerance = (packed.Maximum(channel) - packed.Minimum(channel)) / EncodedMImages.MaxRgb;
            for (var id = 0; id < grid.CellCount; id++)
            {
                Assert.InRange(packed.Rgb24(channel, id), 0u, EncodedMImages.MaxRgb);
                Assert.InRange(Math.Abs(model.Coefficient(channel, id) - restored.Coefficient(channel, id)),
                    0, tolerance + 1e-13);
            }
        }
        var centre = grid.LinearIndex(2, 2, 2);
        Assert.InRange(Math.Abs(model.ValueAtCentre(centre) - restored.ValueAtCentre(centre)), 0, 1e-4);
    }

    [Fact]
    public void UniformChannelsEncodeWithoutDivisionByZero()
    {
        var g = new FunctionalGrid(new[] { 0.0 }, new[] { 1.0 }, new[] { 4 });
        var model = FunctionalVoxelModel.Sample(FunctionalField.Constant(1, 7), g);
        var packed = model.EncodeRgb24();
        for (var i = 0; i < g.CellCount; i++)
        {
            Assert.Equal(0u, packed.Rgb24(0, i));
            Assert.Equal(7.0, packed.ToModel().ValueAtCentre(i), 11);
        }
    }

    [Fact]
    public void AffineLiftAddsZeroGradientAlongOmittedAxis()
    {
        var source = FunctionalField.Disk(0, 0, 2);
        var lifted = source.Lift(3, 0, 2);
        var j = lifted.Evaluate(1, 200, 0.5);
        Assert.Equal(source.Evaluate(1, .5).Value, j.Value, 12);
        Assert.Equal(0.0, j.Gradient(1), 12);
        Assert.Equal(0.0, j.Hessian(1, 1), 12);
        Assert.Equal(-2.0, j.Gradient(0), 12);
    }

    [Fact]
    public void AffineArgumentMapUsesJacobianAndSecondChainRule()
    {
        var x = FunctionalField.Coordinate(1, 0);
        var q = x * x;
        var transformed = q.MapArguments(new double[,] { { 0, 2 } }, new[] { 3.0 });
        var j = transformed.Evaluate(12, 4);
        Assert.Equal(121.0, j.Value, 10);
        Assert.Equal(0.0, j.Gradient(0), 10);
        Assert.Equal(44.0, j.Gradient(1), 10);
        Assert.Equal(8.0, j.Hessian(1, 1), 10);
    }

    [Fact]
    public void AnalyticDerivativeRetainsExactFirstDerivativeButNotThirdOrder()
    {
        var partial = FunctionalField.Sphere(0, 0, 0, 2).Partial(0);
        var result = partial.Evaluate(1, 2, 3);
        Assert.Equal(-2.0, result.Value, 12);
        Assert.Equal(-2.0, result.Gradient(0), 12);
        Assert.False(result.HasHessian);
        Assert.Throws<InvalidOperationException>(() => result.Hessian(0, 0));
    }

    [Fact]
    public void SampledPartialRecoversPolynomialDerivative()
    {
        var x = FunctionalField.Coordinate(1, 0);
        var g = new FunctionalGrid(new[] { 0.0 }, new[] { 1.0 }, new[] { 5 });
        var original = FunctionalVoxelModel.Sample(x * x, g);
        var derivative = original.Derivative(0);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(2 * g.Centre(i)[0], derivative.ValueAtCentre(i), 10);
            Assert.Equal(2.0, derivative.JetAtCentre(i).Gradient(0), 10);
        }
    }

    [Fact]
    public void TrapezoidCauchyIntegralIsExactForLinearIntegrand()
    {
        var x = FunctionalField.Coordinate(1, 0);
        var grid = new FunctionalGrid(new[] { 0.0 }, new[] { 1.0 }, new[] { 5 });
        var integral = FunctionalVoxelModel.Sample(x.Scale(2), grid).Integrate(0, _ => 0);
        for (var i = 0; i < 5; i++)
        {
            var centre = grid.Centre(i)[0];
            Assert.Equal(centre * centre - .25, integral.ValueAtCentre(i), 10);
        }
    }

    [Fact]
    public void ReconstructionIsExactForLinearAnalyticField()
    {
        var x = FunctionalField.Coordinate(3, 0);
        var y = FunctionalField.Coordinate(3, 1);
        var z = FunctionalField.Coordinate(3, 2);
        var field = x.Scale(3) - y.Scale(2) + z + FunctionalField.Constant(3, 1);
        var model = FunctionalVoxelModel.Sample(field, CubicGrid(4, 1));
        var query = new[] { .33, -.76, 1.35 };
        Assert.Equal(field.Evaluate(query).Value, model.Approximate(query), 10);
    }

    [Fact]
    public void MeasureOfSphereConvergesTowardAnalyticVolume()
    {
        var grid = CubicGrid(24, .125);
        var sphere = FunctionalVoxelModel.Sample(FunctionalField.Sphere(0, 0, 0, 1), grid);
        var actual = sphere.PositiveRegionMeasure();
        Assert.InRange(Math.Abs(actual - 4.0 * Math.PI / 3), 0, .12);
    }

    [Fact]
    public void BoxAndCylinderSupportThreeDimensionalInterior()
    {
        var box = FunctionalField.Box(-1, -1, -1, 1, 1, 1);
        var cylinder = FunctionalField.CylinderZ(0, 0, 1, -2, 2);
        Assert.True(box.Evaluate(0, 0, 0).Value > 0);
        Assert.True(box.Evaluate(3, 0, 0).Value < 0);
        Assert.True(cylinder.Evaluate(0, 0, 0).Value > 0);
        Assert.True(cylinder.Evaluate(0, 0, 3).Value < 0);
        Assert.True(cylinder.Evaluate(2, 0, 0).Value < 0);
    }

    [Fact]
    public void NonDifferentiableRIntersectionDoesNotInventGradient()
    {
        var x = FunctionalField.Coordinate(2, 0);
        var y = FunctionalField.Coordinate(2, 1);
        Assert.Throws<ArithmeticException>(() => x.Intersection(y).Evaluate(0, 0));
    }

    [Fact]
    public void MarchingTetrahedraProducesClosedConsistentlyOrientedSphereMesh()
    {
        var grid = CubicGrid(24, .125);
        var sphere = FunctionalVoxelModel.Sample(FunctionalField.Sphere(0, 0, 0, 1), grid);
        var mesh = FunctionalIsoSurface.Extract(sphere);
        Assert.NotNull(mesh);
        var report = DCad.Core.MeshValidator.Validate(mesh!);
        Assert.True(report.IsClosedOrientedManifold,
            $"Degenerate={report.DegenerateTriangles}, boundary={report.BoundaryEdges}, nonmanifold={report.NonManifoldEdges}, winding={report.InconsistentWindingEdges}");
        Assert.InRange(Math.Abs(mesh!.SurfaceArea() - 4 * Math.PI), 0, 1.0);
        Assert.InRange(Math.Abs(mesh.SignedVolume() - 4 * Math.PI / 3), 0, .4);
    }

    [Fact]
    public void IsoSurfaceReturnsNullForFieldWithNoZeroCrossings()
    {
        var model = FunctionalVoxelModel.Sample(FunctionalField.Constant(3, 5), CubicGrid());
        Assert.Null(FunctionalIsoSurface.Extract(model));
    }

    [Fact]
    public void GridsRejectInvalidGeometryAndComposition()
    {
        Assert.Throws<ArgumentException>(() =>
            new FunctionalGrid(new[] { 0.0 }, new[] { -1.0 }, new[] { 5 }));
        var a = FunctionalVoxelModel.Sample(FunctionalField.Constant(1, 1),
            new FunctionalGrid(new[] { 0.0 }, new[] { 1.0 }, new[] { 5 }));
        var b = FunctionalVoxelModel.Sample(FunctionalField.Constant(1, 1),
            new FunctionalGrid(new[] { 0.0 }, new[] { .5 }, new[] { 5 }));
        Assert.Throws<ArgumentException>(() => a.Combine(b, ROperation.Union));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Approximate(new[] { -1.0 }));
    }
}
