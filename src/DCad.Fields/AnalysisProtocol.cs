using System.Text.Json;
using System.Text.Json.Serialization;

namespace DCad.Fields;

public sealed record AnalysisRegion(
    string Name,
    string Kind,
    string Selector,
    double[]? Direction = null,
    double? Magnitude = null,
    double? Value = null);

public sealed record AnalysisLoadCase(
    string Name,
    double Weight,
    IReadOnlyList<AnalysisRegion> Loads);

public sealed record AnalysisMaterial(
    string Name,
    double YoungModulus,
    double PoissonRatio,
    double Density,
    double YieldStrength,
    string Units = "N/mm2");

public sealed record OptimizationSettings(
    bool Enabled = false,
    string Method = "simp-oc",
    double TargetVolumeRatio = 0.30,
    int MaxIterations = 80,
    double FilterRadiusMm = 3.0,
    double MinimumFeatureMm = 0.0,
    string LoadAggregation = "weighted_sum",
    bool RobustThreeField = false,
    double RobustDelta = 0.10,
    string? BuildDirection = null);

public sealed record AnalysisRequest(
    string Schema,
    string DocumentFingerprint,
    string GeometryPayload,
    double VoxelSizeMm,
    AnalysisMaterial Material,
    IReadOnlyList<AnalysisRegion> BoundaryConditions,
    IReadOnlyList<AnalysisLoadCase> LoadCases,
    OptimizationSettings Optimization,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public const string CurrentSchema = "dcad.analysis.request/1";

    public static AnalysisRequest Create(
        string fingerprint,
        string geometryPayload,
        double voxelSizeMm,
        AnalysisMaterial material,
        IEnumerable<AnalysisRegion>? boundaryConditions = null,
        IEnumerable<AnalysisLoadCase>? loadCases = null,
        OptimizationSettings? optimization = null,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            CurrentSchema,
            fingerprint,
            geometryPayload,
            voxelSizeMm,
            material,
            (boundaryConditions ?? []).ToArray(),
            (loadCases ?? []).ToArray(),
            optimization ?? new OptimizationSettings(),
            metadata);

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, JsonOptions(indented));

    public static AnalysisRequest FromJson(string json)
    {
        var value = JsonSerializer.Deserialize<AnalysisRequest>(json, JsonOptions(false))
            ?? throw new InvalidDataException("Analysis request JSON is empty.");
        if (!string.Equals(value.Schema, CurrentSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported analysis request schema '{value.Schema}'. Expected '{CurrentSchema}'.");
        if (!(value.VoxelSizeMm > 0.0) || !double.IsFinite(value.VoxelSizeMm))
            throw new InvalidDataException("voxelSizeMm must be finite and positive.");
        if (value.LoadCases.Count == 0)
            throw new InvalidDataException("At least one load case is required.");
        return value;
    }

    private static JsonSerializerOptions JsonOptions(bool indented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = indented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record AnalysisResultSummary(
    string Schema,
    string RequestFingerprint,
    bool Success,
    string Reason,
    double Compliance,
    double MaxDisplacementMm,
    double MaxVonMisesMpa,
    double VolumeRatio,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, double>? Metrics = null)
{
    public const string CurrentSchema = "dcad.analysis.result/1";

    public string ToJson(bool indented = true)
        => JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
}
