namespace DCad.FunctionalVoxel;

/// <summary>
/// Deterministic local search over voxel-centre neighbours, guided by M-image
/// gradients and constrained by the sign of an independent functional field.
/// It provides an engineering baseline; it does not claim to reproduce the
/// published Tolok/Grigoryev raster traversal algorithm step-for-step.
/// </summary>
public static class FunctionalVoxelLocalSearch
{
    public sealed record Result(
        IReadOnlyList<int> VisitedVoxelIds,
        int BestVoxelId,
        double BestValue,
        bool AtCoordinateLocalMinimum,
        string TerminationReason);

    public static Result Minimize(
        FunctionalVoxelModel objective,
        int startVoxelId,
        FunctionalVoxelModel? admissible = null,
        int maxSteps = 10000,
        double relativeImprovement = 1e-12)
    {
        ArgumentNullException.ThrowIfNull(objective);
        if ((uint)startVoxelId >= (uint)objective.VoxelCount)
            throw new ArgumentOutOfRangeException(nameof(startVoxelId));
        if (maxSteps < 0) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        if (!double.IsFinite(relativeImprovement) || relativeImprovement < 0)
            throw new ArgumentOutOfRangeException(nameof(relativeImprovement));
        var grid = objective.Grid;
        if (admissible is not null && !grid.SameGeometry(admissible.Grid))
            throw new ArgumentException("Admissibility field uses a different lattice.");
        if (admissible is not null && admissible.ValueAtCentre(startVoxelId) < 0)
            throw new ArgumentException("Initial point does not satisfy the constraints.");

        var visited = new List<int> { startVoxelId };
        var current = startVoxelId;
        var value = objective.ValueAtCentre(current);

        for (var iteration = 0; iteration < maxSteps; iteration++)
        {
            var next = current;
            var nextValue = value;
            var jet = objective.JetAtCentre(current);

            // Examine the axes of largest predicted first-order decrease first,
            // then check both directions to avoid assuming locally linear geometry.
            var orderedAxes = Enumerable.Range(0, grid.Dimension)
                .OrderByDescending(axis => Math.Abs(jet.Gradient(axis) * grid.Step(axis)))
                .ThenBy(axis => axis);

            foreach (var axis in orderedAxes)
            {
                var preferred = jet.Gradient(axis) >= 0 ? -1 : 1;
                foreach (var direction in new[] { preferred, -preferred })
                {
                    var coordinate = grid.Index(current, axis) + direction;
                    if (coordinate < 0 || coordinate >= grid.Count(axis)) continue;
                    var candidate = current + direction * grid.Stride(axis);
                    if (admissible is not null && admissible.ValueAtCentre(candidate) < 0)
                        continue;
                    var candidateValue = objective.ValueAtCentre(candidate);
                    var threshold = relativeImprovement * Math.Max(1, Math.Abs(nextValue));
                    if (candidateValue < nextValue - threshold)
                    {
                        nextValue = candidateValue;
                        next = candidate;
                    }
                }
            }

            if (next == current)
                return new Result(visited.ToArray(), current, value, true,
                    "No improving axis-neighbour in the admissible voxel lattice.");

            current = next;
            value = nextValue;
            visited.Add(current);
        }
        return new Result(visited.ToArray(), current, value, false, "Step budget exhausted.");
    }
}
