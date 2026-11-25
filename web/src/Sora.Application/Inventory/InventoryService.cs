using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Inventory;
using System.Globalization;

namespace Sora.Application.Inventory;

public sealed class InventoryService(IInventoryRepository inventory, TimeProvider timeProvider) : IInventoryService
{
    public async Task<ServiceResult<PurchaseReceiptResponse>> ReceiveAsync(CreatePurchaseReceiptRequest request,
        Guid actorId, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || request.SupplierId == Guid.Empty)
        {
            return ServiceResult<PurchaseReceiptResponse>.Failure("invalid_reference");
        }
        if (!await inventory.SupplierIsActiveAsync(request.SupplierId, cancellationToken))
        {
            return ServiceResult<PurchaseReceiptResponse>.Failure("supplier_not_found");
        }

        var duplicateLotLine = request.Lines
            .GroupBy(x => (x.ProductId, LotCode: x.LotCode.Trim().ToUpperInvariant(), x.ExpiryDate))
            .Any(x => x.Count() > 1);
        if (duplicateLotLine)
        {
            return ServiceResult<PurchaseReceiptResponse>.Failure("duplicate_lot_line");
        }

        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var activeProducts = await inventory.GetActiveProductIdsAsync(productIds, cancellationToken);
        if (activeProducts.Count != productIds.Length)
        {
            return ServiceResult<PurchaseReceiptResponse>.Failure("product_not_found_or_inactive");
        }

        var todayUtc = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (request.Lines.Any(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value < todayUtc))
        {
            return ServiceResult<PurchaseReceiptResponse>.Failure("received_lot_is_expired");
        }

        var receivedAtUtc = request.ReceivedAtUtc?.ToUniversalTime() ?? timeProvider.GetUtcNow();
        var nextReceiptNumber = await inventory.GetNextReceiptNumberAsync(cancellationToken);
        var invoiceNumber = $"REC-{nextReceiptNumber.ToString("D6", CultureInfo.InvariantCulture)}";
        var receipt = new PurchaseReceipt(request.SupplierId, invoiceNumber, receivedAtUtc, actorId);
        var lots = request.Lines.Select(line => new InventoryLot(
            receipt.Id,
            line.ProductId,
            line.LotCode.Trim(),
            line.ExpiryDate,
            line.Quantity,
            line.UnitPurchaseCost)).ToArray();

        await inventory.ReceiveAsync(receipt, lots, actorId, cancellationToken);
        return ServiceResult<PurchaseReceiptResponse>.Success(new PurchaseReceiptResponse(
            receipt.Id,
            receipt.SupplierId,
            receipt.InvoiceNumber,
            receipt.ReceivedAtUtc,
            lots.Length,
            lots.Sum(x => x.QuantityReceived)));
    }

    public async Task<IReadOnlyList<InventoryStockLotResponse>> GetStockAsync(Guid? productId,
        CancellationToken cancellationToken) =>
        (await inventory.GetStockAsync(productId, cancellationToken))
        .Select(x => new InventoryStockLotResponse(x.LotId, x.ProductId, x.ProductCode, x.ProductName,
            x.LotCode, x.ExpiryDate, x.OnHandUnits, x.ReservedUnits, x.AvailableUnits, x.UnitPurchaseCost))
        .ToList();

    public async Task<ServiceResult<bool>> AdjustStockAsync(AdjustStockRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        if (request.LotId == Guid.Empty || actorId == Guid.Empty || request.QuantityDelta == 0)
        {
            return ServiceResult<bool>.Failure("invalid_adjustment");
        }

        var movementType = request.MovementType.Trim().ToUpperInvariant();
        if (movementType is not ("ADJUSTMENT" or "LOSS" or "DAMAGE" or "EXPIRY"))
        {
            return ServiceResult<bool>.Failure("invalid_movement_type");
        }

        var result = await inventory.AdjustStockAsync(request.LotId, request.QuantityDelta,
            movementType, actorId, request.Reason.Trim(), timeProvider.GetUtcNow(), cancellationToken);
        return result switch
        {
            StockAdjustmentResult.Applied => ServiceResult<bool>.Success(true),
            StockAdjustmentResult.LotNotFound => ServiceResult<bool>.Failure("lot_not_found"),
            StockAdjustmentResult.WouldConsumeReservedStock => ServiceResult<bool>.Failure("adjustment_would_consume_reserved_stock"),
            _ => ServiceResult<bool>.Failure("adjustment_failed")
        };
    }
}
