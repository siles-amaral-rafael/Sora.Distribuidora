using System.Data;
using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class InventoryRepository(SoraDbContext dbContext) : IInventoryRepository
{
    public Task<bool> SupplierIsActiveAsync(Guid supplierId, CancellationToken cancellationToken) =>
        dbContext.Suppliers.AnyAsync(x => x.Id == supplierId && x.IsActive, cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetActiveProductIdsAsync(IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken) =>
        await dbContext.Products.Where(x => productIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id).ToHashSetAsync(cancellationToken);

    public Task<long> GetNextReceiptNumberAsync(CancellationToken cancellationToken) =>
        dbContext.Database.SqlQueryRaw<long>("SELECT nextval('purchase_receipt_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

    public async Task ReceiveAsync(PurchaseReceipt receipt, IReadOnlyList<InventoryLot> lots,
        Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        dbContext.PurchaseReceipts.Add(receipt);
        foreach (var lot in lots)
        {
            dbContext.InventoryLots.Add(lot);
            dbContext.InventoryLotBalances.Add(new InventoryLotBalance(lot.Id, lot.QuantityReceived));
            dbContext.InventoryMovements.Add(new InventoryMovement(
                lot.Id,
                "RECEIPT",
                lot.QuantityReceived,
                0,
                actorId,
                receipt.ReceivedAtUtc,
                $"Recepción {receipt.InvoiceNumber}"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryStockLot>> GetStockAsync(Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryLots.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Balance)
            .AsQueryable();
        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        return await query.OrderBy(x => x.Product.Name)
            .ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.Id)
            .Select(x => new InventoryStockLot(
                x.Id,
                x.ProductId,
                x.Product.InternalCode,
                x.Product.Name,
                x.LotCode,
                x.ExpiryDate,
                x.Balance.OnHandUnits,
                x.Balance.ReservedUnits,
                x.Balance.OnHandUnits - x.Balance.ReservedUnits,
                x.UnitPurchaseCost))
            .ToListAsync(cancellationToken);
    }

    public async Task<StockAdjustmentResult> AdjustStockAsync(Guid lotId, int quantityDelta,
        string movementType, Guid actorId, string reason, DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        var balance = await dbContext.InventoryLotBalances
            .FromSqlInterpolated($"SELECT * FROM inventory_lot_balances WHERE lot_id = {lotId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (balance is null)
        {
            return StockAdjustmentResult.LotNotFound;
        }
        if (!balance.TryAdjustOnHand(quantityDelta))
        {
            return StockAdjustmentResult.WouldConsumeReservedStock;
        }

        dbContext.InventoryMovements.Add(new InventoryMovement(
            lotId,
            movementType,
            quantityDelta,
            0,
            actorId,
            occurredAtUtc,
            reason));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StockAdjustmentResult.Applied;
    }
}
