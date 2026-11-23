using Sora.Shared.Inventory;

namespace Sora.Application.Inventory;

public interface IInventoryService
{
    Task<ServiceResult<PurchaseReceiptResponse>> ReceiveAsync(CreatePurchaseReceiptRequest request,
        Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryStockLotResponse>> GetStockAsync(Guid? productId, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> AdjustStockAsync(AdjustStockRequest request, Guid actorId,
        CancellationToken cancellationToken);
}
