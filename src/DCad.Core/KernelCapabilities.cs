namespace DCad.Core;

public enum GeometryRepresentation
{
    Analytic,
    TriangleMesh,
    VoxelOccupancy,
    SignedDistanceField,
    BoundaryRepresentation
}

[Flags]
public enum ModelingCapability
{
    None = 0,
    PrimitiveSolids = 1 << 0,
    BooleanCsg = 1 << 1,
    AffineTransforms = 1 << 2,
    Sketches = 1 << 3,
    Extrude = 1 << 4,
    Revolve = 1 << 5,
    Sweep = 1 << 6,
    Loft = 1 << 7,
    Fillet = 1 << 8,
    Chamfer = 1 << 9,
    Shell = 1 << 10,
    Offset = 1 << 11,
    Patterns = 1 << 12,
    Morphology = 1 << 13,
    Lattice = 1 << 14,
    ExactStepExchange = 1 << 15,
    FieldSampling = 1 << 16,
}

public sealed record KernelCapabilities(
    string Name,
    GeometryRepresentation NativeRepresentation,
    ModelingCapability Capabilities,
    bool ExactGeometry,
    bool SupportsNegativeCoordinates = true,
    string? Notes = null)
{
    public bool Supports(ModelingCapability capability) => (Capabilities & capability) == capability;
}

public interface ICapabilityModelingKernel : IModelingKernel
{
    KernelCapabilities Description { get; }
}

public static class StandardKernelProfiles
{
    public static KernelCapabilities ManifoldMesh => new(
        "Manifold mesh CSG",
        GeometryRepresentation.TriangleMesh,
        ModelingCapability.PrimitiveSolids |
        ModelingCapability.BooleanCsg |
        ModelingCapability.AffineTransforms,
        ExactGeometry: false,
        Notes: "Robust manifold triangle-solid backend; tessellated rather than exact B-Rep.");

    public static KernelCapabilities VoxelField => new(
        "Voxel/SDF field",
        GeometryRepresentation.SignedDistanceField,
        ModelingCapability.PrimitiveSolids |
        ModelingCapability.BooleanCsg |
        ModelingCapability.AffineTransforms |
        ModelingCapability.Morphology |
        ModelingCapability.Lattice |
        ModelingCapability.Offset |
        ModelingCapability.Shell |
        ModelingCapability.FieldSampling,
        ExactGeometry: false,
        Notes: "Field backend for topology, morphology, scans, TPMS/lattice and CAE coupling.");

    public static KernelCapabilities ExactBrep => new(
        "Exact B-Rep",
        GeometryRepresentation.BoundaryRepresentation,
        ModelingCapability.PrimitiveSolids |
        ModelingCapability.BooleanCsg |
        ModelingCapability.AffineTransforms |
        ModelingCapability.Sketches |
        ModelingCapability.Extrude |
        ModelingCapability.Revolve |
        ModelingCapability.Sweep |
        ModelingCapability.Loft |
        ModelingCapability.Fillet |
        ModelingCapability.Chamfer |
        ModelingCapability.Shell |
        ModelingCapability.Offset |
        ModelingCapability.Patterns |
        ModelingCapability.ExactStepExchange,
        ExactGeometry: true,
        Notes: "Target profile for a future OpenCascade-class adapter.");
}
