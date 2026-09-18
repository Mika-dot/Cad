using OpenTK.Mathematics;

namespace AFVMDemo;

public sealed record ResearchModel(
    string Name,
    string ShortName,
    RenderMesh Surface,
    VisualizationGeometry Visual,
    Vector3 BaseColor,
    ErrorMetrics Metrics,
    string Details);
