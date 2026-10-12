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
        var norm2 = 1.0;
        for (var i = 0; i < jet.Dimension; i++) norm2 += jet.Gradient(i) * jet.Gradient(i);
        var norm = Math.Sqrt(norm2);
        if (!double.IsFinite(norm)) throw new ArithmeticException("Invalid local normal.");
        var components = new double[jet.Dimension + 2];
        var offset = -jet.Value;
        for (var i = 0; i < jet.Dimension; i++)
        {
            if (!double.IsFinite(point[i])) throw new ArgumentException("Non-finite point.");
            components[i] = -jet.Gradient(i) / norm;
            offset += jet.Gradient(i) * point[i];
        }
        components[jet.Dimension] = 1.0 / norm;
        components[^1] = offset / norm;
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
/// RGB24-compatible packed M-images. Each channel uses its OWN affine min/max calibration;
/// 16,777,216 grades imply quantization, not guaranteed global 1e-5 absolute accuracy.
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

    internal static EncodedMImages Encode(FunctionalGrid grid, double[][] channels)
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
}
