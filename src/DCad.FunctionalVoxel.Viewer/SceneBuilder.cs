using System.Globalization;
using System.Text;
using DCad.Core;
using DCad.FunctionalVoxel;

namespace DCad.FunctionalVoxel.Viewer;

public enum ShapeKind
{
    Sphere,
    Box,
    Cylinder,
    SphereMinusCylinder,
    TwoSpheresUnion,
    TwoSpheresIntersection
}

public enum FieldView
{
    Original,
    DerivativeX,
    DerivativeY,
    DerivativeZ,
    IntegralX
}

public sealed record SceneSettings(
    ShapeKind Shape = ShapeKind.SphereMinusCylinder,
    FieldView View = FieldView.Original,
    double Radius = 10,
    double SecondaryRadius = 3,
    int Resolution = 32,
    double RAlpha = 0,
    double IntegralInitialValue = -.01);

public sealed record SceneResult(
    SceneSettings Settings,
    FunctionalVoxelModel Model,
    Mesh3d? Mesh,
    MeshValidationReport? MeshAudit,
    EncodedMImages Packed,
    MImageQuantizationReport Quantization,
    double PositiveVoxelVolume);

/// <summary>
/// Thin adapter: all functions and R-operations are performed by the existing
/// functional-voxel kernel, not reimplemented inside the GUI.
/// </summary>
public static class SceneBuilder
{
    public static SceneResult Build(SceneSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(settings.Radius) || settings.Radius < .1 || settings.Radius > 200)
            throw new ArgumentOutOfRangeException(nameof(settings), "Radius must be 0.1..200 mm.");
        if (!double.IsFinite(settings.SecondaryRadius) || settings.SecondaryRadius < .05 ||
            (settings.Shape == ShapeKind.SphereMinusCylinder &&
             settings.SecondaryRadius >= settings.Radius))
            throw new ArgumentOutOfRangeException(nameof(settings),
                "Hole radius must be positive and, for a through-hole, smaller than outer radius.");
        if (settings.Resolution < 12 || settings.Resolution > 80)
            throw new ArgumentOutOfRangeException(nameof(settings),
                "Grid resolution must be between 12 and 80.");
        if (!double.IsFinite(settings.RAlpha) || settings.RAlpha <= -1 || settings.RAlpha >= 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "R-alpha must be in (-1,1).");
        if (!double.IsFinite(settings.IntegralInitialValue))
            throw new ArgumentOutOfRangeException(nameof(settings), "Integral's Cauchy value must be finite.");

        double extent = 1.5 * settings.Radius;
        var n = settings.Resolution;
        var grid = new FunctionalGrid(
            new[] { -extent, -extent, -extent },
            new[] { 2 * extent / n, 2 * extent / n, 2 * extent / n },
            new[] { n, n, n });

        FunctionalVoxelModel Sample(FunctionalField f) =>
            FunctionalVoxelModel.Sample(f, grid);

        var result = settings.Shape switch
        {
            ShapeKind.Sphere => Sample(FunctionalField.Sphere(0, 0, 0, settings.Radius)),
            ShapeKind.Box => Sample(FunctionalField.Box(-settings.Radius * .78,
                -settings.Radius * .78, -settings.Radius * .78,
                settings.Radius * .78, settings.Radius * .78, settings.Radius * .78)),
            ShapeKind.Cylinder => Sample(FunctionalField.CylinderZ(0, 0,
                settings.Radius * .76, -settings.Radius, settings.Radius)),
            ShapeKind.SphereMinusCylinder =>
                Sample(FunctionalField.Sphere(0, 0, 0, settings.Radius))
                    .Combine(
                        Sample(FunctionalField.CylinderZ(.19 * settings.SecondaryRadius, 0,
                            settings.SecondaryRadius, -2 * settings.Radius, 2 * settings.Radius)),
                        ROperation.Difference, settings.RAlpha),
            ShapeKind.TwoSpheresUnion =>
                Sample(FunctionalField.Sphere(-.42 * settings.Radius, 0, 0, .8 * settings.Radius))
                    .Combine(
                        Sample(FunctionalField.Sphere(.42 * settings.Radius, 0, 0, .8 * settings.Radius)),
                        ROperation.Union, settings.RAlpha),
            ShapeKind.TwoSpheresIntersection =>
                Sample(FunctionalField.Sphere(-.42 * settings.Radius, 0, 0, .8 * settings.Radius))
                    .Combine(
                        Sample(FunctionalField.Sphere(.42 * settings.Radius, 0, 0, .8 * settings.Radius)),
                        ROperation.Intersection, settings.RAlpha),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown shape.")
        };

        result = settings.View switch
        {
            FieldView.Original => result,
            FieldView.DerivativeX => result.Derivative(0),
            FieldView.DerivativeY => result.Derivative(1),
            FieldView.DerivativeZ => result.Derivative(2),
            FieldView.IntegralX => result.Integrate(0, _ => settings.IntegralInitialValue),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown field operation.")
        };

        // Iso-surface is only an output for display; no independent raster CSG.
        var mesh = FunctionalIsoSurface.Extract(result);
        var audit = mesh is null ? null : MeshValidator.Validate(mesh);
        var packed = result.EncodeRgb24();
        var quant = packed.CompareWith(result);
        return new SceneResult(settings, result, mesh, audit, packed, quant, result.PositiveRegionMeasure());
    }
}

public static class SceneExports
{
    public static void WriteObj(SceneResult scene, string filename)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Mesh is null) throw new InvalidOperationException("This field has no zero iso-surface.");
        using var writer = new StreamWriter(filename, false, new UTF8Encoding(false));
        writer.WriteLine("# DCad functional-voxel surface, unit: mm");
        foreach (var p in scene.Mesh.Vertices)
            writer.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}"));
        foreach (var face in scene.Mesh.Triangles)
            writer.WriteLine(FormattableString.Invariant($"f {face.A + 1} {face.B + 1} {face.C + 1}"));
    }

    public static void WriteSliceCsv(SceneResult scene, int zIndex, string filename)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var grid = scene.Model.Grid;
        if ((uint)zIndex >= (uint)grid.Count(2))
            throw new ArgumentOutOfRangeException(nameof(zIndex));
        using var writer = new StreamWriter(filename, false, new UTF8Encoding(false));
        writer.WriteLine("x_mm,y_mm,z_mm,f,df_dx,df_dy,df_dz,M0,M1,M2,M3,M4");
        for (var j = 0; j < grid.Count(1); j++)
        for (var i = 0; i < grid.Count(0); i++)
        {
            var id = grid.LinearIndex(i, j, zIndex);
            var p = grid.Centre(id);
            var jet = scene.Model.JetAtCentre(id);
            var row = new List<double>(12)
            {
                p[0], p[1], p[2], jet.Value,
                jet.Gradient(0), jet.Gradient(1), jet.Gradient(2)
            };
            for (var channel = 0; channel < 5; channel++)
                row.Add(scene.Model.Coefficient(channel, id));
            writer.WriteLine(string.Join(",", row.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        }
    }
}
