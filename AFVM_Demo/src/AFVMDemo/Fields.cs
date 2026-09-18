namespace AFVMDemo;

public interface IField3d
{
    string Name { get; }
    double Value(DVec3 p);
    DVec3 Gradient(DVec3 p);
}

public sealed class SphereField(DVec3 center, double radius) : IField3d
{
    public string Name => "Sphere";
    public double Value(DVec3 p) => (p - center).Length - radius;
    public DVec3 Gradient(DVec3 p) => (p - center).Normalized();
}

public sealed class InfiniteCylinderZField(DVec3 center, double radius) : IField3d
{
    public string Name => "CylinderZ";
    public double Value(DVec3 p)
    {
        double x = p.X - center.X, y = p.Y - center.Y;
        return Math.Sqrt(x * x + y * y) - radius;
    }
    public DVec3 Gradient(DVec3 p)
    {
        double x = p.X - center.X, y = p.Y - center.Y;
        double len = Math.Sqrt(x * x + y * y);
        return len > 1e-14 ? new(x / len, y / len, 0) : DVec3.UnitX;
    }
}

public enum ROperation { Union, Intersection, Difference }

public static class RFunctions
{
    // Convention: field <= 0 is inside. Smooth epsilon only removes the singular derivative at f=g=0.
    public const double Epsilon = 1e-12;

    public static (double value, double df, double dg) Evaluate(double f, double g, ROperation op)
    {
        double s = Math.Sqrt(f * f + g * g + Epsilon * Epsilon);
        return op switch
        {
            ROperation.Union => (f + g - s, 1.0 - f / s, 1.0 - g / s),
            ROperation.Intersection => (f + g + s, 1.0 + f / s, 1.0 + g / s),
            ROperation.Difference => (f - g + s, 1.0 + f / s, -1.0 + g / s),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };
    }
}

public sealed class RBinaryField(IField3d a, IField3d b, ROperation operation, string? name = null) : IField3d
{
    public string Name { get; } = name ?? $"R-{operation}({a.Name},{b.Name})";
    public double Value(DVec3 p) => RFunctions.Evaluate(a.Value(p), b.Value(p), operation).value;
    public DVec3 Gradient(DVec3 p)
    {
        double av = a.Value(p), bv = b.Value(p);
        var r = RFunctions.Evaluate(av, bv, operation);
        return a.Gradient(p) * r.df + b.Gradient(p) * r.dg;
    }
}

public sealed class AffineField(double c, DVec3 gradient) : IField3d
{
    public string Name => "Affine self-test";
    public double Value(DVec3 p) => c + DVec3.Dot(gradient, p);
    public DVec3 Gradient(DVec3 p) => gradient;
}
