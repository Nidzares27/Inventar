using Inventar.Storefront.Models;

namespace Inventar.Storefront.Services;

public static class StorefrontPoMjeriPlanner
{
    public static string BuildGroupKey(StorefrontProduct product)
    {
        return product.PoMjeri
            ? $"{Normalize(product.Name)}::{Normalize(product.ProductNumber)}::{Normalize(product.Model)}"
            : $"{Normalize(product.Name)}::{Normalize(product.Model)}";
    }

    public static PoMjeriPlanResult Evaluate(
        IEnumerable<StorefrontProduct> variants,
        PoMjeriInventorySnapshot snapshot,
        string? selectedColor,
        int customWidth,
        int customLength,
        int requestedQuantity)
    {
        requestedQuantity = Math.Max(requestedQuantity, 1);

        if (customWidth <= 0 || customLength <= 0)
        {
            return PoMjeriPlanResult.Invalid("Unesite željenu širinu i dužinu.");
        }

        var candidates = variants
            .Where(product =>
                product.PoMjeri &&
                product.Width.HasValue &&
                product.Length.HasValue &&
                (string.IsNullOrWhiteSpace(selectedColor) ||
                 string.Equals(product.Color, selectedColor, StringComparison.OrdinalIgnoreCase)))
            .Select(product => BuildCandidate(product, snapshot, customWidth, customLength))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.Width * candidate.RemainingLength)
            .ThenBy(candidate => candidate.Width)
            .ThenBy(candidate => candidate.RemainingLength)
            .ThenBy(candidate => candidate.ProductId)
            .ToList();

        if (candidates.Count == 0)
        {
            return PoMjeriPlanResult.Invalid("Trenutno nema dostupne role za zadate dimenzije.");
        }

        var maxAvailableQuantity = candidates.Sum(candidate => candidate.MaxAvailableQuantity);

        if (maxAvailableQuantity <= 0)
        {
            return PoMjeriPlanResult.Invalid("Trenutno nema dovoljno preostale dužine za zadati komad.");
        }

        var prefixCandidates = new List<PoMjeriCandidate>();
        var prefixCapacity = 0;
        List<PoMjeriAllocationPlan>? rankedPlans = null;

        foreach (var candidate in candidates)
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
            return PoMjeriPlanResult.Invalid(
                maxAvailableQuantity > 0
                    ? $"Moguće je naručiti najviše {maxAvailableQuantity} komada za zadate dimenzije."
                    : "Trenutno nema dovoljno preostale dužine za zadati komad.",
                maxAvailableQuantity);
        }

        return PoMjeriPlanResult.Success(rankedPlans[0], rankedPlans, maxAvailableQuantity);
    }

    public static int CalculateConsumedLengthPerUnit(int remainingWidth, int customWidth, int customLength)
    {
        if (remainingWidth <= 0 || customWidth <= 0 || customLength <= 0)
        {
            return 0;
        }

        return customLength;
    }

    public static int CalculateMaxAvailableQuantity(int remainingWidth, int remainingLength, int customWidth, int customLength)
    {
        if (remainingWidth <= 0 || remainingLength <= 0)
        {
            return 0;
        }

        var consumedLengthPerUnit = CalculateConsumedLengthPerUnit(remainingWidth, customWidth, customLength);
        return consumedLengthPerUnit <= 0
            ? 0
            : Math.Max(remainingLength / consumedLengthPerUnit, 0);
    }

    private static PoMjeriCandidate? BuildCandidate(
        StorefrontProduct product,
        PoMjeriInventorySnapshot snapshot,
        int customWidth,
        int customLength)
    {
        var remainingWidth = product.Width ?? 0;
        var remainingLength = snapshot.GetAvailableRemainingLength(product.Id);

        if (customWidth != remainingWidth || customLength > remainingLength)
        {
            return null;
        }

        var consumedLengthPerUnit = CalculateConsumedLengthPerUnit(remainingWidth, customWidth, customLength);
        var maxAvailableQuantity = CalculateMaxAvailableQuantity(remainingWidth, remainingLength, customWidth, customLength);

        return maxAvailableQuantity < 1
            ? null
            : new PoMjeriCandidate(
                product.Id,
                remainingWidth,
                remainingLength,
                consumedLengthPerUnit,
                maxAvailableQuantity);
    }

    private static IEnumerable<PoMjeriAllocationPlan> EnumeratePlans(
        IReadOnlyList<PoMjeriCandidate> candidates,
        int requestedQuantity)
    {
        var workingAllocations = new List<PoMjeriAllocationSlice>();

        foreach (var plan in Enumerate(candidates, 0, requestedQuantity, workingAllocations))
        {
            yield return plan;
        }
    }

    private static IEnumerable<PoMjeriAllocationPlan> Enumerate(
        IReadOnlyList<PoMjeriCandidate> candidates,
        int index,
        int remainingQuantity,
        List<PoMjeriAllocationSlice> workingAllocations)
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
                workingAllocations.Add(new PoMjeriAllocationSlice(
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

    private static PoMjeriAllocationPlan BuildPlan(IReadOnlyCollection<PoMjeriAllocationSlice> slices)
    {
        var orderedSlices = slices
            .OrderBy(slice => slice.ProductId)
            .ToList();

        return new PoMjeriAllocationPlan(
            orderedSlices,
            orderedSlices.Sum(slice => slice.RemainingLengthAfter),
            orderedSlices.Count,
            orderedSlices.Count == 0 ? 0 : orderedSlices.Max(slice => slice.RemainingLengthAfter));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }
}

public sealed record PoMjeriCandidate(
    int ProductId,
    int Width,
    int RemainingLength,
    int ConsumedLengthPerUnit,
    int MaxAvailableQuantity);

public sealed record PoMjeriAllocationSlice(
    int ProductId,
    int Quantity,
    int ConsumedLengthPerUnit,
    int RemainingLengthAfter);

public sealed record PoMjeriAllocationPlan(
    IReadOnlyList<PoMjeriAllocationSlice> Slices,
    int ScoreUsedLeftover,
    int UsedCandidateCount,
    int ScoreLargestUsedLeftover)
{
    public string SourceProductIds => string.Join(",", Slices.Select(slice => slice.ProductId));
}

public sealed class PoMjeriPlanResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public int MaxAvailableQuantity { get; init; }
    public PoMjeriAllocationPlan? BestPlan { get; init; }
    public IReadOnlyList<PoMjeriAllocationPlan> RankedPlans { get; init; } = Array.Empty<PoMjeriAllocationPlan>();

    public static PoMjeriPlanResult Success(
        PoMjeriAllocationPlan bestPlan,
        IReadOnlyList<PoMjeriAllocationPlan> rankedPlans,
        int maxAvailableQuantity)
    {
        return new PoMjeriPlanResult
        {
            IsValid = true,
            BestPlan = bestPlan,
            RankedPlans = rankedPlans,
            MaxAvailableQuantity = maxAvailableQuantity,
            Message = maxAvailableQuantity == 1
                ? "Dostupan je 1 komad za zadate dimenzije."
                : $"Moguće je naručiti najviše {maxAvailableQuantity} komada za zadate dimenzije."
        };
    }

    public static PoMjeriPlanResult Invalid(string message, int maxAvailableQuantity = 0)
    {
        return new PoMjeriPlanResult
        {
            IsValid = false,
            Message = message,
            MaxAvailableQuantity = maxAvailableQuantity
        };
    }
}
