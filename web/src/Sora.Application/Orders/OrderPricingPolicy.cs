namespace Sora.Application.Orders;

public sealed record AvailableLot(Guid LotId, DateOnly? ExpiryDate, int AvailableUnits, decimal UnitCost);
public sealed record PlannedLotAllocation(Guid LotId, int Units);
public sealed record LotAllocationPlan(IReadOnlyList<PlannedLotAllocation> Allocations, int MissingUnits);

/// <summary>Pure policies shared by order processing and unit tests.</summary>
public static class OrderPricingPolicy
{
    public static LotAllocationPlan PlanFefo(int requestedUnits, IEnumerable<AvailableLot> lots)
    {
        if (requestedUnits < 0) throw new ArgumentOutOfRangeException(nameof(requestedUnits));
        var remaining = requestedUnits;
        var allocations = new List<PlannedLotAllocation>();
        foreach (var lot in lots.OrderBy(x => x.ExpiryDate ?? DateOnly.MaxValue).ThenBy(x => x.LotId))
        {
            var take = Math.Min(remaining, Math.Max(0, lot.AvailableUnits));
            if (take > 0) allocations.Add(new PlannedLotAllocation(lot.LotId, take));
            remaining -= take;
            if (remaining == 0) break;
        }
        return new LotAllocationPlan(allocations, remaining);
    }

    public static decimal CalculateDiscount(decimal subtotal, decimal cost, decimal discountPercent)
    {
        if (subtotal < 0 || cost < 0 || discountPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(subtotal));
        var grossMargin = Math.Max(0m, subtotal - cost);
        var marginCap = decimal.Floor(grossMargin * 0.20m * 100m) / 100m;
        var requested = decimal.Round(subtotal * discountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return Math.Min(marginCap, requested);
    }
}
