namespace DCad.FunctionalVoxel;

/// <summary>
/// Value, gradient and (when available) Hessian of an analytic scalar function.
/// All derivatives are with respect to the spatial arguments, not an image pixel index.
/// No finite differences are silently substituted for analytic differentiation.
/// </summary>
public sealed class FunctionalJet
{
    private readonly double[] _gradient;
    private readonly double[,]? _hessian;
    public double Value { get; }
    public int Dimension => _gradient.Length;
    public bool HasHessian => _hessian is not null;
    public double Gradient(int axis) => _gradient[axis];
    public double Hessian(int row, int column) => _hessian is null
        ? throw new InvalidOperationException("Second derivatives are unavailable for this jet.")
        : _hessian[row, column];

    private FunctionalJet(double value, double[] gradient, double[,]? hessian)
    {
        if (!double.IsFinite(value) || gradient.Any(x => !double.IsFinite(x)))
            throw new ArithmeticException("A functional jet contains a non-finite value or gradient.");
        if (hessian is not null)
            foreach (var v in hessian)
                if (!double.IsFinite(v)) throw new ArithmeticException("A functional Hessian is non-finite.");
        Value = value;
        _gradient = gradient;
        _hessian = hessian;
    }

    public static FunctionalJet Constant(double value, int dimension)
    {
        if (dimension < 1 || dimension > 4) throw new ArgumentOutOfRangeException(nameof(dimension));
        return new FunctionalJet(value, new double[dimension], new double[dimension, dimension]);
    }

    public static FunctionalJet Variable(ReadOnlySpan<double> coordinates, int axis)
    {
        if (coordinates.Length < 1 || coordinates.Length > 4)
            throw new ArgumentOutOfRangeException(nameof(coordinates));
        if ((uint)axis >= (uint)coordinates.Length) throw new ArgumentOutOfRangeException(nameof(axis));
        var gradient = new double[coordinates.Length];
        gradient[axis] = 1;
        return new FunctionalJet(coordinates[axis], gradient, new double[coordinates.Length, coordinates.Length]);
    }

    /// <summary>
    /// A first-order jet, e.g. reconstructed from M-images. It does not invent a Hessian.
    /// </summary>
    public static FunctionalJet FromValueGradient(double value, ReadOnlySpan<double> gradient)
    {
        if (gradient.Length < 1 || gradient.Length > 4) throw new ArgumentOutOfRangeException(nameof(gradient));
        return new FunctionalJet(value, gradient.ToArray(), null);
    }

    internal static FunctionalJet FromFull(double value, double[] gradient, double[,]? hessian) =>
        new(value, gradient, hessian);

    private static void Check(FunctionalJet a, FunctionalJet b)
    {
        if (a.Dimension != b.Dimension) throw new ArgumentException("Jet dimensionality mismatch.");
    }

    public static FunctionalJet operator +(FunctionalJet a, FunctionalJet b)
    {
        Check(a, b);
        var n = a.Dimension;
        var g = new double[n];
        var h = a.HasHessian && b.HasHessian ? new double[n, n] : null;
        for (var i = 0; i < n; i++)
        {
            g[i] = a._gradient[i] + b._gradient[i];
            if (h is not null)
                for (var j = 0; j < n; j++) h[i, j] = a._hessian![i, j] + b._hessian![i, j];
        }
        return new FunctionalJet(a.Value + b.Value, g, h);
    }

    public static FunctionalJet operator -(FunctionalJet a) => a.Scale(-1);
    public static FunctionalJet operator -(FunctionalJet a, FunctionalJet b) => a + (-b);

    public FunctionalJet Scale(double factor)
    {
        var n = Dimension;
        var g = new double[n];
        var h = HasHessian ? new double[n, n] : null;
        for (var i = 0; i < n; i++)
        {
            g[i] = factor * _gradient[i];
            if (h is not null)
                for (var j = 0; j < n; j++) h[i, j] = factor * _hessian![i, j];
        }
        return new FunctionalJet(factor * Value, g, h);
    }

    public static FunctionalJet operator *(FunctionalJet a, FunctionalJet b)
    {
        Check(a, b);
        var n = a.Dimension;
        var g = new double[n];
        var h = a.HasHessian && b.HasHessian ? new double[n, n] : null;
        for (var i = 0; i < n; i++)
        {
            g[i] = a._gradient[i] * b.Value + a.Value * b._gradient[i];
            if (h is not null)
                for (var j = 0; j < n; j++)
                    h[i, j] = a._hessian![i, j] * b.Value + a.Value * b._hessian![i, j]
                        + a._gradient[i] * b._gradient[j] + a._gradient[j] * b._gradient[i];
        }
        return new FunctionalJet(a.Value * b.Value, g, h);
    }

    /// <summary>Apply y=g(f), with g' and g'' supplied at f.Value.</summary>
    public FunctionalJet Unary(double value, double first, double second)
    {
        var n = Dimension;
        var g = new double[n];
        var h = HasHessian ? new double[n, n] : null;
        for (var i = 0; i < n; i++)
        {
            g[i] = first * _gradient[i];
            if (h is not null)
                for (var j = 0; j < n; j++)
                    h[i, j] = second * _gradient[i] * _gradient[j] + first * _hessian![i, j];
        }
        return new FunctionalJet(value, g, h);
    }

    public FunctionalJet Reciprocal()
    {
        if (Value == 0) throw new DivideByZeroException("Singularity in the reciprocal field.");
        return Unary(1 / Value, -1 / (Value * Value), 2 / (Value * Value * Value));
    }

    public static FunctionalJet operator /(FunctionalJet a, FunctionalJet b) => a * b.Reciprocal();

    public FunctionalJet Sqrt()
    {
        // At zero the derivative of sqrt(a²+b²) is generally not defined.
        // Do not silently manufacture a false local tangent plane.
        if (Value <= 0) throw new ArithmeticException("Square-root derivative is undefined at a non-positive argument.");
        var root = Math.Sqrt(Value);
        return Unary(root, .5 / root, -.25 / (Value * root));
    }

    public FunctionalJet Sin() => Unary(Math.Sin(Value), Math.Cos(Value), -Math.Sin(Value));
    public FunctionalJet Cos() => Unary(Math.Cos(Value), -Math.Sin(Value), -Math.Cos(Value));
}
