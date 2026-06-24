namespace Inventar.Utils;

public static class PoMjeriAllocationPlanner
{
    public static PoMjeriAllocationPlannerResult Evaluate(
        IEnumerable<PoMjeriAllocationPlannerCandidate> candidates,
        int requestedQuantity,
        int maxOrderQuantity)
    {
        requestedQuantity = Math.Min(Math.Max(requestedQuantity, 1), maxOrderQuantity);

        var orderedCandidates = candidates
            .Where(candidate => candidate.MaxAvailableQuantity > 0)
            .OrderBy(candidate => candidate.Width * candidate.RemainingLength)
            .ThenBy(candidate => candidate.Width)
            .ThenBy(candidate => candidate.RemainingLength)
            .ThenBy(candidate => candidate.ProductId)
            .ToList();

        if (orderedCandidates.Count == 0)
        {
            return PoMjeriAllocationPlannerResult.Invalid(
                "Trenutno nema dostupnih rola za zadate dimenzije.");
        }

        var maxAvailableQuantity = Math.Min(
            orderedCandidates.Sum(candidate => candidate.MaxAvailableQuantity),
            maxOrderQuantity);

        if (maxAvailableQuantity <= 0)
        {
            return PoMjeriAllocationPlannerResult.Invalid(
                "Trenutno nema dovoljno preostale duzine za zadati komad.");
        }

        var prefixCandidates = new List<PoMjeriAllocationPlannerCandidate>();
        var prefixCapacity = 0;
        List<PoMjeriAllocationPlannerPlan>? rankedPlans = null;

        foreach (var candidate in orderedCandidates)
        {
            prefixCandidates.Add(candidate);
            prefixCapacity += candidate.MaxAvailableQuantity;

            if (prefixCapacity < requestedQuantity)
            {
                continue;
            }

            rankedPlans = EnumeratePlans(prefixCandidates, requestedQuantity)
                .OrderBy(plan => plan.ScoreUsedLeftover)
                .ThenBy(plan => plan.UsedCandidateCount)
                .ThenBy(plan => plan.ScoreLargestUsedLeftover)
                .ThenBy(plan => plan.SourceProductIds)
                .ToList();

            if (rankedPlans.Count > 0)
            {
                break;
            }
        }

        if (rankedPlans == null || rankedPlans.Count == 0)
        {
            return PoMjeriAllocationPlannerResult.Invalid(
                maxAvailableQuantity > 0
                    ? $"Moguce je naruciti najvise {maxAvailableQuantity} komada za zadate dimenzije."
                    : "Trenutno nema dovoljno preostale duzine za zadati komad.",
                maxAvailableQuantity);
        }

        return PoMjeriAllocationPlannerResult.Success(
            rankedPlans[0],
            rankedPlans,
            maxAvailableQuantity);
    }

    private static IEnumerable<PoMjeriAllocationPlannerPlan> EnumeratePlans(
        IReadOnlyList<PoMjeriAllocationPlannerCandidate> candidates,
        int requestedQuantity)
    {
        var workingAllocations = new List<PoMjeriAllocationPlannerSlice>();

        foreach (var plan in Enumerate(candidates, 0, requestedQuantity, workingAllocations))
        {
            yield return plan;
        }
    }

    private static IEnumerable<PoMjeriAllocationPlannerPlan> Enumerate(
        IReadOnlyList<PoMjeriAllocationPlannerCandidate> candidates,
        int index,
        int remainingQuantity,
        List<PoMjeriAllocationPlannerSlice> workingAllocations)
    {
        if (remainingQuantity == 0)
        {
            yield return BuildPlan(workingAllocations);
            yield break;
        }

        if (index >= candidates.Count)
        {
            yield break;
        }

        var candidate = candidates[index];
        var maxFromCandidate = Math.Min(candidate.MaxAvailableQuantity, remainingQuantity);

        for (var quantity = maxFromCandidate; quantity >= 0; quantity--)
        {
            if (quantity > 0)
            {
                workingAllocations.Add(new PoMjeriAllocationPlannerSlice(
                    candidate.ProductId,
                    quantity,
                    candidate.ConsumedLengthPerUnit,
                    candidate.RemainingLength - (quantity * candidate.ConsumedLengthPerUnit)));
            }

            foreach (var plan in Enumerate(candidates, index + 1, remainingQuantity - quantity, workingAllocations))
            {
                yield return plan;
            }

            if (quantity > 0)
            {
                workingAllocations.RemoveAt(workingAllocations.Count - 1);
            }
        }
    }

    private static PoMjeriAllocationPlannerPlan BuildPlan(IReadOnlyCollection<PoMjeriAllocationPlannerSlice> slices)
    {
        var orderedSlices = slices
            .OrderBy(slice => slice.ProductId)
            .ToList();

        return new PoMjeriAllocationPlannerPlan(
            orderedSlices,
            orderedSlices.Sum(slice => slice.RemainingLengthAfter),
            orderedSlices.Count,
            orderedSlices.Count == 0 ? 0 : orderedSlices.Max(slice => slice.RemainingLengthAfter));
    }
}

public sealed record PoMjeriAllocationPlannerCandidate(
    int ProductId,
    int Width,
    int RemainingLength,
    int ConsumedLengthPerUnit,
    int MaxAvailableQuantity);

public sealed record PoMjeriAllocationPlannerSlice(
    int ProductId,
    int Quantity,
    int ConsumedLengthPerUnit,
    int RemainingLengthAfter);

public sealed record PoMjeriAllocationPlannerPlan(
    IReadOnlyList<PoMjeriAllocationPlannerSlice> Slices,
    int ScoreUsedLeftover,
    int UsedCandidateCount,
    int ScoreLargestUsedLeftover)
{
    public string SourceProductIds => string.Join(",", Slices.Select(slice => slice.ProductId));
}

public sealed class PoMjeriAllocationPlannerResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public int MaxAvailableQuantity { get; init; }
    public PoMjeriAllocationPlannerPlan? BestPlan { get; init; }
    public IReadOnlyList<PoMjeriAllocationPlannerPlan> RankedPlans { get; init; } = Array.Empty<PoMjeriAllocationPlannerPlan>();

    public static PoMjeriAllocationPlannerResult Success(
        PoMjeriAllocationPlannerPlan bestPlan,
        IReadOnlyList<PoMjeriAllocationPlannerPlan> rankedPlans,
        int maxAvailableQuantity)
    {
        return new PoMjeriAllocationPlannerResult
        {
            IsValid = true,
            BestPlan = bestPlan,
            RankedPlans = rankedPlans,
            MaxAvailableQuantity = maxAvailableQuantity,
            Message = maxAvailableQuantity == 1
                ? "Dostupan je 1 komad za zadate dimenzije."
                : $"Moguce je naruciti najvise {maxAvailableQuantity} komada za zadate dimenzije."
        };
    }

    public static PoMjeriAllocationPlannerResult Invalid(string message, int maxAvailableQuantity = 0)
    {
        return new PoMjeriAllocationPlannerResult
        {
            IsValid = false,
            Message = message,
            MaxAvailableQuantity = maxAvailableQuantity
        };
    }
}
