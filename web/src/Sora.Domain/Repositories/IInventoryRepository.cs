using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public sealed record InventoryStockLot(
    Guid LotId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string LotCode,
    DateOnly? ExpiryDate,
    int OnHandUnits,
    int ReservedUnits,
    int AvailableUnits,
    decimal UnitPurchaseCost);

public enum StockAdjustmentResult
{
    Applied,
    LotNotFound,
    WouldConsumeReservedStock
}

public interface IInventoryRepository
{
    Task<bool> SupplierIsActiveAsync(Guid supplierId, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetActiveProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
    Task<long> GetNextReceiptNumberAsync(CancellationToken cancellationToken);
    Task ReceiveAsync(PurchaseReceipt receipt, IReadOnlyList<InventoryLot> lots, Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryStockLot>> GetStockAsync(Guid? productId, CancellationToken cancellationToken);
    Task<StockAdjustmentResult> AdjustStockAsync(Guid lotId, int quantityDelta, string movementType,
        Guid actorId, string reason, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);
}
