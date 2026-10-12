namespace DCad.FunctionalVoxel;

/// <summary>
/// A functional-voxel model: n+2 double-precision M-image channels over an
/// n-dimensional argument lattice, independent of the source analytic function.
/// The sign convention is F >= 0 inside. This is not a binary occupancy grid.
/// </summary>
public sealed class FunctionalVoxelModel
{
    private readonly double[][] _channels;
    public FunctionalGrid Grid { get; }
    public int ChannelCount => Grid.Dimension + 2;
    public int VoxelCount => Grid.CellCount;
    public int Dimension => Grid.Dimension;

    private FunctionalVoxelModel(FunctionalGrid grid, double[][] channels)
    {
        Grid = grid;
        _channels = channels;
    }

    public static FunctionalVoxelModel Sample(FunctionalField field, FunctionalGrid grid)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(grid);
        if (field.Dimension != grid.Dimension) throw new ArgumentException("Field/grid dimension mismatch.");
        var channels = Allocate(grid);
        for (var linear = 0; linear < grid.CellCount; linear++)
        {
            var point = grid.Centre(linear);
            var jet = field.Evaluate(point);
            var plane = LocalFunctionalPlane.FromJet(point, jet);
            for (var c = 0; c < channels.Length; c++) channels[c][linear] = plane.Coefficient(c);
        }
        return new FunctionalVoxelModel(grid, channels);
    }

    internal static FunctionalVoxelModel FromChannels(FunctionalGrid grid, double[][] channels)
    {
        if (channels.Length != grid.Dimension + 2 ||
            channels.Any(layer => layer.Length != grid.CellCount || layer.Any(x => !double.IsFinite(x))))
            throw new ArgumentException("M-image channel shape or content is invalid.");
        var copy = channels.Select(layer => (double[])layer.Clone()).ToArray();
        for (var i = 0; i < grid.CellCount; i++)
            if (copy[grid.Dimension][i] == 0)
                throw new ArgumentException("A reconstructed M-image has a zero function-value coefficient.");
        return new FunctionalVoxelModel(grid, copy);
    }

    private static double[][] Allocate(FunctionalGrid grid)
    {
        var result = new double[grid.Dimension + 2][];
        for (var i = 0; i < result.Length; i++) result[i] = new double[grid.CellCount];
        return result;
    }

    public double Coefficient(int channel, int voxel) => _channels[channel][voxel];

    public LocalFunctionalPlane Plane(int voxel)
    {
        if ((uint)voxel >= (uint)VoxelCount) throw new ArgumentOutOfRangeException(nameof(voxel));
        var coefficients = new double[ChannelCount];
        for (var c = 0; c < coefficients.Length; c++) coefficients[c] = _channels[c][voxel];
        return new LocalFunctionalPlane(coefficients);
    }

    public double ValueAtCentre(int voxel) => Plane(voxel).ValueAt(Grid.Centre(voxel));
    public FunctionalJet JetAtCentre(int voxel) => Plane(voxel).ReconstructJet(Grid.Centre(voxel));

    /// <summary>
    /// Continuous reconstruction by multilinear blending of neighbouring local
    /// planes. At sampled centres the original value and gradient are retained.
    /// This is an interpolation scheme, not an exact analytic evaluator.
    /// </summary>
    public double Approximate(ReadOnlySpan<double> position)
    {
        if (!Grid.Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var lo = new int[Dimension];
        var hi = new int[Dimension];
        var t = new double[Dimension];
        for (var axis = 0; axis < Dimension; axis++)
        {
            var fractional = (position[axis] - Grid.Minimum(axis)) / Grid.Step(axis) - .5;
            fractional = Math.Clamp(fractional, 0, Grid.Count(axis) - 1);
            lo[axis] = (int)Math.Floor(fractional);
            hi[axis] = Math.Min(lo[axis] + 1, Grid.Count(axis) - 1);
            t[axis] = fractional - lo[axis];
        }
        double accumulated = 0;
        for (var corner = 0; corner < (1 << Dimension); corner++)
        {
            var weight = 1.0;
            var linear = 0;
            for (var axis = 0; axis < Dimension; axis++)
            {
                var upper = ((corner >> axis) & 1) != 0;
                weight *= upper ? t[axis] : 1 - t[axis];
                linear += (upper ? hi[axis] : lo[axis]) * Grid.Stride(axis);
            }
            if (weight != 0) accumulated += weight * Plane(linear).ValueAt(position);
        }
        return accumulated;
    }

    /// <summary>Fixed sign-normalized RGB24 mapping used in published M-images.</summary>
    public EncodedMImages EncodeRgb24() => EncodedMImages.EncodeCanonical(Grid, _channels);

    /// <summary>Optional non-canonical image-dependent range calibration.</summary>
    public EncodedMImages EncodeRgb24Calibrated() => EncodedMImages.EncodeCalibrated(Grid, _channels);

    /// <summary>
    /// Perform R algebra solely on the stored local hyperplanes. This does not
    /// consult the analytic source. Correct first-order chain rule is retained.
    /// </summary>
    public FunctionalVoxelModel Combine(FunctionalVoxelModel other, ROperation operation, double alpha = 0)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Grid.SameGeometry(other.Grid)) throw new ArgumentException("Incompatible argument lattices.");
        if (!double.IsFinite(alpha) || alpha <= -1 || alpha >= 1) throw new ArgumentOutOfRangeException(nameof(alpha));
        var layers = Allocate(Grid);
        for (var i = 0; i < VoxelCount; i++)
        {
            var at = Grid.Centre(i);
            var a = Plane(i).ReconstructJet(at);
            var b = other.Plane(i).ReconstructJet(at);
            var combined = FunctionalField.RJet(a, b, operation, alpha);
            var plane = LocalFunctionalPlane.FromJet(at, combined);
            for (var c = 0; c < ChannelCount; c++) layers[c][i] = plane.Coefficient(c);
        }
        return new FunctionalVoxelModel(Grid, layers);
    }

    /// <summary>
    /// Pointwise absolute value computed solely from M-images. Like the analytical
    /// absolute-value field it is nondifferentiable at a zero-valued voxel centre.
    /// </summary>
    public FunctionalVoxelModel Abs()
    {
        var layers = Allocate(Grid);
        for (var i = 0; i < VoxelCount; i++)
        {
            var at = Grid.Centre(i);
            var source = Plane(i).ReconstructJet(at);
            if (source.Value == 0)
                throw new ArithmeticException("Absolute-value jet is undefined at a zero voxel.");
            var plane = LocalFunctionalPlane.FromJet(at, source.Scale(Math.Sign(source.Value)));
            for (var c = 0; c < ChannelCount; c++) layers[c][i] = plane.Coefficient(c);
        }
        return new FunctionalVoxelModel(Grid, layers);
    }

    /// <summary>
    /// Value of an analytic partial derivative is available directly from M-images.
    /// Its gradients (second derivatives of the source) are approximated by finite
    /// differences of the sampled partial values. This is not an exact Hessian.
    /// </summary>
    public FunctionalVoxelModel Derivative(int axis)
    {
        if ((uint)axis >= (uint)Dimension) throw new ArgumentOutOfRangeException(nameof(axis));
        var values = new double[VoxelCount];
        for (var i = 0; i < VoxelCount; i++) values[i] = Plane(i).Gradient(axis);
        return FromSampledValues(Grid, values);
    }

    /// <summary>
    /// Trapezoidal Cauchy integration along a chosen argument. The initial value
    /// is prescribed at the FIRST CELL CENTRE of each transverse line, not at the
    /// outer box boundary. Gradients are reconstructed by finite differences.
    /// </summary>
    public FunctionalVoxelModel Integrate(int axis, Func<double[], double> initialAtFirstCentre)
    {
        if ((uint)axis >= (uint)Dimension) throw new ArgumentOutOfRangeException(nameof(axis));
        ArgumentNullException.ThrowIfNull(initialAtFirstCentre);
        var values = new double[VoxelCount];
        var stride = Grid.Stride(axis);
        for (var i = 0; i < VoxelCount; i++)
        {
            if (Grid.Index(i, axis) == 0)
            {
                values[i] = initialAtFirstCentre(Grid.Centre(i));
            }
            else
            {
                var preceding = i - stride;
                values[i] = values[preceding] +
                    .5 * Grid.Step(axis) * (ValueAtCentre(preceding) + ValueAtCentre(i));
            }
            if (!double.IsFinite(values[i])) throw new ArithmeticException("Integral diverged.");
        }
        return FromSampledValues(Grid, values);
    }

    public static FunctionalVoxelModel FromSampledValues(FunctionalGrid grid, double[] samples)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length != grid.CellCount || samples.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("Scalar sample lattice mismatch.");
        var channels = Allocate(grid);
        for (var i = 0; i < grid.CellCount; i++)
        {
            var grad = new double[grid.Dimension];
            for (var axis = 0; axis < grid.Dimension; axis++)
            {
                var along = grid.Index(i, axis);
                var stride = grid.Stride(axis);
                if (grid.Count(axis) == 1) grad[axis] = 0;
                else if (along == 0) grad[axis] = (samples[i + stride] - samples[i]) / grid.Step(axis);
                else if (along == grid.Count(axis) - 1) grad[axis] = (samples[i] - samples[i - stride]) / grid.Step(axis);
                else grad[axis] = (samples[i + stride] - samples[i - stride]) / (2 * grid.Step(axis));
            }
            var plane = LocalFunctionalPlane.FromJet(grid.Centre(i), FunctionalJet.FromValueGradient(samples[i], grad));
            for (var c = 0; c < channels.Length; c++) channels[c][i] = plane.Coefficient(c);
        }
        return new FunctionalVoxelModel(grid, channels);
    }

    /// <summary>Voxel-count measure of the nonnegative region (area/volume/hypervolume).
    /// Boundary cells are not fractionally integrated.</summary>
    public double PositiveRegionMeasure()
    {
        long positive = 0;
        for (var i = 0; i < VoxelCount; i++)
            if (ValueAtCentre(i) >= 0) positive++;
        return positive * Grid.CellMeasure;
    }
}
