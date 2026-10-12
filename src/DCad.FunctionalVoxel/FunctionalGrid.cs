namespace DCad.FunctionalVoxel;

/// <summary>A regular argument-space lattice. All samples lie at cell centres.</summary>
public sealed class FunctionalGrid
{
    private readonly double[] _minimum;
    private readonly double[] _step;
    private readonly int[] _counts;
    private readonly int[] _strides;
    public int Dimension => _counts.Length;
    public int CellCount { get; }
    public double CellMeasure { get; }
    public double Minimum(int axis) => _minimum[axis];
    public double Step(int axis) => _step[axis];
    public int Count(int axis) => _counts[axis];
    public int Stride(int axis) => _strides[axis];

    public FunctionalGrid(double[] minimum, double[] step, int[] counts)
    {
        ArgumentNullException.ThrowIfNull(minimum);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Length < 1 || counts.Length > 4 || minimum.Length != counts.Length || step.Length != counts.Length)
            throw new ArgumentException("A functional grid requires matching arrays for 1..4 arguments.");
        _minimum = (double[])minimum.Clone();
        _step = (double[])step.Clone();
        _counts = (int[])counts.Clone();
        _strides = new int[Dimension];
        var product = 1;
        var measure = 1.0;
        for (var i = Dimension - 1; i >= 0; i--)
        {
            if (_counts[i] < 1 || !double.IsFinite(_minimum[i]) ||
                !double.IsFinite(_step[i]) || _step[i] <= 0)
                throw new ArgumentException("Invalid grid geometry.");
            _strides[i] = product;
            product = checked(product * _counts[i]);
            measure *= _step[i];
        }
        if (!double.IsFinite(measure)) throw new ArgumentException("Grid measure is non-finite.");
        CellCount = product;
        CellMeasure = measure;
    }

    public int LinearIndex(params int[] indices)
    {
        if (indices.Length != Dimension) throw new ArgumentException("Grid index dimension mismatch.");
        var linear = 0;
        for (var i = 0; i < Dimension; i++)
        {
            if ((uint)indices[i] >= (uint)_counts[i]) throw new ArgumentOutOfRangeException(nameof(indices));
            linear += indices[i] * _strides[i];
        }
        return linear;
    }

    public int Index(int linear, int axis)
    {
        if ((uint)linear >= (uint)CellCount || (uint)axis >= (uint)Dimension)
            throw new ArgumentOutOfRangeException(nameof(linear));
        return (linear / _strides[axis]) % _counts[axis];
    }

    public double[] Centre(int linear)
    {
        if ((uint)linear >= (uint)CellCount) throw new ArgumentOutOfRangeException(nameof(linear));
        var result = new double[Dimension];
        for (var i = 0; i < Dimension; i++)
            result[i] = _minimum[i] + (Index(linear, i) + 0.5) * _step[i];
        return result;
    }

    public bool Contains(ReadOnlySpan<double> point)
    {
        if (point.Length != Dimension) return false;
        for (var i = 0; i < Dimension; i++)
            if (!double.IsFinite(point[i]) || point[i] < _minimum[i] ||
                point[i] > _minimum[i] + _step[i] * _counts[i]) return false;
        return true;
    }

    public bool SameGeometry(FunctionalGrid other)
    {
        if (other.Dimension != Dimension) return false;
        for (var i = 0; i < Dimension; i++)
            if (Count(i) != other.Count(i) || Step(i) != other.Step(i) || Minimum(i) != other.Minimum(i))
                return false;
        return true;
    }
}

/// <summary>
/// Local implicit tangent hyperplane to u=F(x): a[0]*x0+...+a[n-1]*x[n-1]+a[n]*u+a[n+1]=0.
/// Therefore a spatial function of n arguments takes n+2 separate M-images.
/// Normalization is with respect to (x0,...,x[n-1],u), not the translation coefficient.
/// </summary>
public sealed class LocalFunctionalPlane
{
    private readonly double[] _coefficients;
    public int Dimension => _coefficients.Length - 2;
    public int ChannelCount => _coefficients.Length;
    public double Coefficient(int channel) => _coefficients[channel];

    public LocalFunctionalPlane(double[] coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (coefficients.Length < 3 || coefficients.Length > 6 || coefficients.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("Local plane expects 3..6 finite coefficients.");
        if (coefficients[^2] == 0) throw new ArgumentException("Coefficient at the function value must not be zero.");
        _coefficients = (double[])coefficients.Clone();
    }

    public static LocalFunctionalPlane FromJet(ReadOnlySpan<double> point, FunctionalJet jet)
    {
        if (point.Length != jet.Dimension) throw new ArgumentException("Point/jet dimension mismatch.");
        // The published M-image colour mapping requires every homogeneous
        // coefficient, INCLUDING the free term, to lie within [-1, 1].
        var components = new double[jet.Dimension + 2];
        var offset = -jet.Value;
        for (var i = 0; i < jet.Dimension; i++)
        {
            if (!double.IsFinite(point[i])) throw new ArgumentException("Non-finite point.");
            components[i] = -jet.Gradient(i);
            offset += jet.Gradient(i) * point[i];
        }
        components[jet.Dimension] = 1.0;
        components[^1] = offset;
        if (!double.IsFinite(offset)) throw new ArithmeticException("Non-finite local-plane offset.");
        var scale = components.Max(Math.Abs);
        var sum = 0.0;
        for (var i = 0; i < components.Length; i++)
        {
            var scaled = components[i] / scale;
            sum += scaled * scaled;
        }
        var root = Math.Sqrt(sum);
        for (var i = 0; i < components.Length; i++) components[i] = (components[i] / scale) / root;
        return new LocalFunctionalPlane(components);
    }

    public double ValueAt(ReadOnlySpan<double> point)
    {
        if (point.Length != Dimension) throw new ArgumentException("Point/plane dimension mismatch.");
        var result = _coefficients[^1];
        for (var i = 0; i < Dimension; i++) result += _coefficients[i] * point[i];
        return -result / _coefficients[Dimension];
    }

    public double Gradient(int axis)
    {
        if ((uint)axis >= (uint)Dimension) throw new ArgumentOutOfRangeException(nameof(axis));
        return -_coefficients[axis] / _coefficients[Dimension];
    }

    public FunctionalJet ReconstructJet(ReadOnlySpan<double> point)
    {
        var g = new double[Dimension];
        for (var i = 0; i < Dimension; i++) g[i] = Gradient(i);
        return FunctionalJet.FromValueGradient(ValueAt(point), g);
    }

    public double[] ToArray() => (double[])_coefficients.Clone();
}

/// <summary>
/// RGB24 M-images: fixed canonical sign-normalized channels or explicit optional
/// min/max calibration. 16,777,216 grades do not guarantee 1e-5 geometric accuracy.
/// </summary>
public sealed class EncodedMImages
{
    public const uint MaxRgb = 16777215;
    private readonly uint[][] _layers;
    private readonly double[] _minima;
    private readonly double[] _maxima;
    public FunctionalGrid Grid { get; }
    public int ChannelCount => _layers.Length;
    public int VoxelCount => Grid.CellCount;

    private EncodedMImages(FunctionalGrid grid, uint[][] layers, double[] minima, double[] maxima)
    {
        Grid = grid;
        _layers = layers;
        _minima = minima;
        _maxima = maxima;
    }

    /// <summary>
    /// Published sign-normalized M-image encoding:
    /// M=round((1+n)/2 * (2^24-1)), n=2*M/(2^24-1)-1.
    /// The integer limit is P-1, not P=2^24, to preserve both endpoints.
    /// The channel calibrations are fixed at [-1,1] for every image.
    /// </summary>
    internal static EncodedMImages EncodeCanonical(FunctionalGrid grid, double[][] channels)
    {
        var layers = new uint[channels.Length][];
        var minima = Enumerable.Repeat(-1.0, channels.Length).ToArray();
        var maxima = Enumerable.Repeat(1.0, channels.Length).ToArray();
        for (var c = 0; c < channels.Length; c++)
        {
            layers[c] = new uint[channels[c].Length];
            for (var i = 0; i < channels[c].Length; i++)
            {
                var n = channels[c][i];
                if (!double.IsFinite(n) || n < -1.0 - 1e-12 || n > 1.0 + 1e-12)
                    throw new ArithmeticException("M-image coefficient is outside [-1,1]. Normalize its full homogeneous vector.");
                var x = (Math.Clamp(n, -1, 1) + 1.0) * (0.5 * MaxRgb);
                layers[c][i] = (uint)Math.Round(x, MidpointRounding.AwayFromZero);
            }
        }
        return new EncodedMImages(grid, layers, minima, maxima);
    }

    /// <summary>
    /// Optional range-adaptive packing for engineering experiments.
    /// It is NOT the canonical M-image colour mapping of Tolok et al.
    /// </summary>
    internal static EncodedMImages EncodeCalibrated(FunctionalGrid grid, double[][] channels)
    {
        var layers = new uint[channels.Length][];
        var minima = new double[channels.Length];
        var maxima = new double[channels.Length];
        for (var c = 0; c < channels.Length; c++)
        {
            var source = channels[c];
            var lo = source.Min();
            var hi = source.Max();
            minima[c] = lo;
            maxima[c] = hi;
            layers[c] = new uint[source.Length];
            if (hi == lo) continue;
            for (var i = 0; i < source.Length; i++)
            {
                var normalized = (source[i] - lo) / (hi - lo);
                if (!double.IsFinite(normalized)) throw new ArithmeticException("M-image quantization overflow.");
                layers[c][i] = (uint)Math.Clamp(Math.Round(normalized * MaxRgb), 0, MaxRgb);
            }
        }
        return new EncodedMImages(grid, layers, minima, maxima);
    }

    public uint Rgb24(int channel, int linearIndex) => _layers[channel][linearIndex];
    public double Minimum(int channel) => _minima[channel];
    public double Maximum(int channel) => _maxima[channel];
    public double Decode(int channel, int linearIndex) =>
        _minima[channel] + (_maxima[channel] - _minima[channel]) * _layers[channel][linearIndex] / MaxRgb;

    public FunctionalVoxelModel ToModel()
    {
        var channels = new double[ChannelCount][];
        for (var c = 0; c < ChannelCount; c++)
        {
            channels[c] = new double[VoxelCount];
            for (var i = 0; i < VoxelCount; i++) channels[c][i] = Decode(c, i);
        }
        return FunctionalVoxelModel.FromChannels(Grid, channels);
    }

    /// <summary>
    /// Report actual reconstruction error, not merely 24-bit component error.
    /// Division by the (sometimes small) coefficient of u amplifies RGB noise.
    /// </summary>
    public MImageQuantizationReport CompareWith(FunctionalVoxelModel original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!Grid.SameGeometry(original.Grid))
            throw new ArgumentException("Different lattices for quantization comparison.");
        var restored = ToModel();
        var worstValue = 0.0;
        var worstRelative = 0.0;
        var worstGradient = 0.0;
        var worstId = 0;
        for (var voxel = 0; voxel < VoxelCount; voxel++)
        {
            var initial = original.JetAtCentre(voxel);
            var decoded = restored.JetAtCentre(voxel);
            var error = Math.Abs(initial.Value - decoded.Value);
            if (error > worstValue)
            {
                worstValue = error;
                worstId = voxel;
            }
            worstRelative = Math.Max(worstRelative, error / Math.Max(1.0, Math.Abs(initial.Value)));
            for (var axis = 0; axis < Grid.Dimension; axis++)
                worstGradient = Math.Max(worstGradient,
                    Math.Abs(initial.Gradient(axis) - decoded.Gradient(axis)));
        }
        return new MImageQuantizationReport(worstValue, worstRelative, worstGradient, worstId);
    }
}

/// <summary>Quantization errors measured in the original model's coordinate/field units.</summary>
public sealed record MImageQuantizationReport(
    double MaxValueAbsoluteError,
    double MaxValueRelativeError,
    double MaxGradientAbsoluteError,
    int WorstVoxelId);
