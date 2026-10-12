namespace DCad.FunctionalVoxel;

public enum ROperation { Intersection, Union, Difference, Add, Subtract, Multiply, Divide }

/// <summary>
/// Analytic scalar field with the convention F(x) >= 0 for the interior of a solid.
/// Expressions retain derivative information; no polygonization is involved.
/// </summary>
public sealed class FunctionalField
{
    private readonly Func<double[], FunctionalJet> _evaluate;
    public int Dimension { get; }

    private FunctionalField(int dimension, Func<double[], FunctionalJet> evaluate)
    {
        if (dimension < 1 || dimension > 4) throw new ArgumentOutOfRangeException(nameof(dimension));
        Dimension = dimension;
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    public static FunctionalField Analytic(int dimension, Func<double[], FunctionalJet> evaluator) =>
        new(dimension, evaluator);

    public FunctionalJet Evaluate(params double[] coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (coordinates.Length != Dimension || coordinates.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("Coordinates must be finite and match the function dimension.", nameof(coordinates));
        var result = _evaluate(coordinates);
        if (result.Dimension != Dimension) throw new InvalidOperationException("Analytic evaluator returned the wrong dimension.");
        return result;
    }

    public static FunctionalField Constant(int dimension, double value) =>
        new(dimension, _ => FunctionalJet.Constant(value, dimension));

    public static FunctionalField Coordinate(int dimension, int axis)
    {
        if ((uint)axis >= (uint)dimension) throw new ArgumentOutOfRangeException(nameof(axis));
        return new(dimension, p => FunctionalJet.Variable(p, axis));
    }

    private static void Check(FunctionalField a, FunctionalField b)
    {
        if (a.Dimension != b.Dimension) throw new ArgumentException("Different argument dimensions.");
    }

    public static FunctionalField operator +(FunctionalField a, FunctionalField b)
    {
        Check(a, b);
        return new(a.Dimension, p => a._evaluate(p) + b._evaluate(p));
    }
    public static FunctionalField operator -(FunctionalField a, FunctionalField b)
    {
        Check(a, b);
        return new(a.Dimension, p => a._evaluate(p) - b._evaluate(p));
    }
    public static FunctionalField operator -(FunctionalField field) =>
        new(field.Dimension, p => -field._evaluate(p));
    public static FunctionalField operator *(FunctionalField a, FunctionalField b)
    {
        Check(a, b);
        return new(a.Dimension, p => a._evaluate(p) * b._evaluate(p));
    }
    public static FunctionalField operator /(FunctionalField a, FunctionalField b)
    {
        Check(a, b);
        return new(a.Dimension, p => a._evaluate(p) / b._evaluate(p));
    }

    public FunctionalField Scale(double value) =>
        new(Dimension, p => _evaluate(p).Scale(value));
    public FunctionalField Sin() => new(Dimension, p => _evaluate(p).Sin());
    public FunctionalField Cos() => new(Dimension, p => _evaluate(p).Cos());

    /// <summary>Absolute value; its differential is undefined on the zero set.</summary>
    public FunctionalField Abs() => new(Dimension, p =>
    {
        var jet = _evaluate(p);
        if (jet.Value == 0) throw new ArithmeticException("Absolute value is not differentiable at zero.");
        return jet.Scale(Math.Sign(jet.Value));
    });

    /// <summary>
    /// Rvachev alpha-system: R-and=a+b-sqrt(a²+b²-2alpha*a*b);
    /// R-or=a+b+sqrt(a²+b²-2alpha*a*b).
    /// alpha=0 is the classical R0 system. Values are not signed distances.
    /// At a=b=0, first derivatives are mathematically indeterminate and an exception is raised.
    /// </summary>
    public FunctionalField R(FunctionalField other, ROperation op, double alpha = 0)
    {
        ArgumentNullException.ThrowIfNull(other);
        Check(this, other);
        if (!double.IsFinite(alpha) || alpha <= -1 || alpha >= 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Use -1 < alpha < 1 for a non-degenerate alpha system.");
        return new(Dimension, p => RJet(_evaluate(p), other._evaluate(p), op, alpha));
    }

    internal static FunctionalJet RJet(FunctionalJet a, FunctionalJet b, ROperation op, double alpha = 0)
    {
        if (a.Dimension != b.Dimension) throw new ArgumentException("Different argument dimensions.");
        if (op == ROperation.Add) return a + b;
        if (op == ROperation.Subtract) return a - b;
        if (op == ROperation.Multiply) return a * b;
        if (op == ROperation.Divide) return a / b;
        if (op == ROperation.Difference) b = -b;
        var norm2 = a * a + b * b - (a * b).Scale(2 * alpha);
        var root = norm2.Sqrt();
        return op == ROperation.Union ? a + b + root : a + b - root;
    }

    public FunctionalField Intersection(FunctionalField other, double alpha = 0) =>
        R(other, ROperation.Intersection, alpha);
    public FunctionalField Union(FunctionalField other, double alpha = 0) =>
        R(other, ROperation.Union, alpha);
    public FunctionalField Difference(FunctionalField other, double alpha = 0) =>
        R(other, ROperation.Difference, alpha);

    /// <summary>Exact partial derivative with its first derivatives from the original Hessian.
    /// A third derivative is NOT available, so the returned jet intentionally has no Hessian.</summary>
    public FunctionalField Partial(int axis)
    {
        if ((uint)axis >= (uint)Dimension) throw new ArgumentOutOfRangeException(nameof(axis));
        return new(Dimension, p =>
        {
            var source = _evaluate(p);
            var g = new double[Dimension];
            for (var i = 0; i < Dimension; i++) g[i] = source.Hessian(i, axis);
            return FunctionalJet.FromValueGradient(source.Gradient(axis), g);
        });
    }

    /// <summary>
    /// Transform arguments by q = matrix * p + offset, where rows correspond to
    /// source coordinates and columns to target coordinates. Implements exact chain rule.
    /// Rectangular matrices provide dimension lifting/projection, not surface triangulation.
    /// </summary>
    public FunctionalField MapArguments(double[,] matrix, double[] offset)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(offset);
        var targetDim = matrix.GetLength(1);
        if (matrix.GetLength(0) != Dimension || targetDim < 1 || targetDim > 4 || offset.Length != Dimension)
            throw new ArgumentException("Invalid affine argument map.");
        var a = (double[,])matrix.Clone();
        var b = (double[])offset.Clone();
        foreach (var c in a) if (!double.IsFinite(c)) throw new ArgumentException("Non-finite affine map.");
        if (b.Any(c => !double.IsFinite(c))) throw new ArgumentException("Non-finite offset.");
        return new FunctionalField(targetDim, p =>
        {
            var q = new double[Dimension];
            for (var k = 0; k < Dimension; k++)
            {
                q[k] = b[k];
                for (var j = 0; j < targetDim; j++) q[k] += a[k, j] * p[j];
            }
            var jet = _evaluate(q);
            var g = new double[targetDim];
            var h = jet.HasHessian ? new double[targetDim, targetDim] : null;
            for (var i = 0; i < targetDim; i++)
            {
                for (var k = 0; k < Dimension; k++) g[i] += jet.Gradient(k) * a[k, i];
                if (h is not null)
                    for (var j = 0; j < targetDim; j++)
                        for (var k = 0; k < Dimension; k++)
                            for (var l = 0; l < Dimension; l++)
                                h[i, j] += jet.Hessian(k, l) * a[k, i] * a[l, j];
            }
            return FunctionalJet.FromFull(jet.Value, g, h);
        });
    }

    public FunctionalField Lift(int targetDimension, params int[] sourceAxes)
    {
        ArgumentNullException.ThrowIfNull(sourceAxes);
        if (sourceAxes.Length != Dimension || targetDimension < Dimension || targetDimension > 4 ||
            sourceAxes.Distinct().Count() != Dimension || sourceAxes.Any(i => i < 0 || i >= targetDimension))
            throw new ArgumentException("The lifting axes must be distinct target axes.");
        var m = new double[Dimension, targetDimension];
        for (var k = 0; k < Dimension; k++) m[k, sourceAxes[k]] = 1;
        return MapArguments(m, new double[Dimension]);
    }

    public static FunctionalField Sphere(double cx, double cy, double cz, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        var x = Coordinate(3, 0) - Constant(3, cx);
        var y = Coordinate(3, 1) - Constant(3, cy);
        var z = Coordinate(3, 2) - Constant(3, cz);
        return Constant(3, radius * radius) - x * x - y * y - z * z;
    }

    public static FunctionalField Disk(double cx, double cy, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        var x = Coordinate(2, 0) - Constant(2, cx);
        var y = Coordinate(2, 1) - Constant(2, cy);
        return Constant(2, radius * radius) - x * x - y * y;
    }

    public static FunctionalField Box(double xmin, double ymin, double zmin, double xmax, double ymax, double zmax)
    {
        if (!(xmax > xmin && ymax > ymin && zmax > zmin)) throw new ArgumentException("Invalid box bounds.");
        var x = Coordinate(3, 0);
        var y = Coordinate(3, 1);
        var z = Coordinate(3, 2);
        return (x - Constant(3, xmin)).Intersection(Constant(3, xmax) - x)
            .Intersection(y - Constant(3, ymin)).Intersection(Constant(3, ymax) - y)
            .Intersection(z - Constant(3, zmin)).Intersection(Constant(3, zmax) - z);
    }

    public static FunctionalField CylinderZ(double cx, double cy, double radius, double zmin, double zmax)
    {
        if (zmax <= zmin) throw new ArgumentException("Invalid cylinder height.");
        var radial = Disk(cx, cy, radius).Lift(3, 0, 1);
        var z = Coordinate(3, 2);
        return radial.Intersection(z - Constant(3, zmin))
                     .Intersection(Constant(3, zmax) - z);
    }
}
