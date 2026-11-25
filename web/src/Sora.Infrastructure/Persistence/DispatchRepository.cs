using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class DispatchRepository(SoraDbContext db) : IDispatchRepository
{
    public async Task<IReadOnlyList<DispatchOrder>> ListAssignedOpenDeliveriesAsync(Guid? driverId,
        CancellationToken cancellationToken)
    {
        var query = db.DispatchOrders.AsNoTracking().Where(x => x.Result == null)
            .Include(x => x.Dispatch).Include(x => x.Order).ThenInclude(x => x.Customer).AsQueryable();
        if (driverId.HasValue) query = query.Where(x => x.Dispatch.DriverStaffId == driverId.Value);
        return await query.OrderBy(x => x.Dispatch.DepartedAtUtc).ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DispatchOrder>> ListPendingInspectionsAsync(CancellationToken cancellationToken) =>
        await db.DispatchOrders.AsNoTracking().Where(x => (x.Result == "REJECTED" || x.Result == "CANCELLED") && x.InspectedAtUtc == null)
            .Include(x => x.Order).ThenInclude(x => x.Payments)
            .Include(x => x.Order).ThenInclude(x => x.Lines).ThenInclude(x => x.LotAllocations).ThenInclude(x => x.Lot)
            .OrderBy(x => x.AttemptedAtUtc).ToListAsync(cancellationToken);

    public async Task<DispatchOperationCode> InspectRejectedAsync(Guid dispatchOrderId,
        IReadOnlyList<(Guid LotId, int GoodUnits, int DamagedUnits)> lots, string notes,
        string paymentResolution, string? refundReference, Guid inspectorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var orderId = await db.DispatchOrders.AsNoTracking().Where(x => x.Id == dispatchOrderId)
            .Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(cancellationToken);
        if (orderId is null) return DispatchOperationCode.NotFound;
        var order = await db.SalesOrders.FromSqlInterpolated(
                $"SELECT * FROM sales_orders WHERE id = {orderId.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) return DispatchOperationCode.NotFound;
        var assignment = await db.DispatchOrders.FromSqlInterpolated(
                $"SELECT * FROM dispatch_orders WHERE id = {dispatchOrderId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null) return DispatchOperationCode.NotFound;
        if (assignment.Result is not ("REJECTED" or "CANCELLED") || assignment.InspectedAtUtc is not null)
            return DispatchOperationCode.InvalidOrderState;
        var paymentRows = await db.OrderPayments.Where(x => x.OrderId == order.Id)
            .Select(x => new { x.Kind, x.Amount }).ToListAsync(cancellationToken);
        var paidAmount = paymentRows.Sum(x => x.Amount);
        if (paidAmount > 0 && (paymentResolution is not ("REFUND" or "CREDIT") || string.IsNullOrWhiteSpace(refundReference)))
            return DispatchOperationCode.InvalidInspection;
        await db.Entry(order).Collection(x => x.Lines).Query().Include(x => x.LotAllocations)
            .ThenInclude(x => x.Lot).LoadAsync(cancellationToken);

        var allocations = order.Lines.SelectMany(x => x.LotAllocations).GroupBy(x => x.LotId)
            .Select(x => new { LotId = x.Key, QuantityPicked = x.Sum(a => a.QuantityPicked) }).ToArray();
        if (lots.Count != allocations.Length || lots.Select(x => x.LotId).Distinct().Count() != lots.Count)
            return DispatchOperationCode.InvalidInspection;
        foreach (var allocation in allocations)
        {
            var inspected = lots.SingleOrDefault(x => x.LotId == allocation.LotId);
            if (inspected.LotId == Guid.Empty || inspected.GoodUnits < 0 || inspected.DamagedUnits < 0 ||
                inspected.GoodUnits + inspected.DamagedUnits != allocation.QuantityPicked)
                return DispatchOperationCode.InvalidInspection;
        }

        foreach (var allocation in allocations)
        {
            var inspected = lots.Single(x => x.LotId == allocation.LotId);
            var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                    $"SELECT * FROM inventory_lot_balances WHERE lot_id = {allocation.LotId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (balance is null || !balance.TryChangeReserved(-allocation.QuantityPicked))
                return DispatchOperationCode.InvalidOrderState;
            if (inspected.GoodUnits > 0)
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "RELEASE", 0,
                    -inspected.GoodUnits, inspectorId, now,
                    $"Inspección de rechazo: {notes}; unidades aptas", order.Id, assignment.Id));
            if (inspected.DamagedUnits > 0)
            {
                if (!balance.TryAdjustOnHand(-inspected.DamagedUnits))
                    return DispatchOperationCode.InvalidOrderState;
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "DAMAGE",
                    -inspected.DamagedUnits, -inspected.DamagedUnits, inspectorId, now,
                    $"Inspección de rechazo: {notes}; unidades dañadas", order.Id, assignment.Id));
            }
        }
        assignment.CompleteInspection(inspectorId, now, notes.Trim());
        var customer = await db.Customers.SingleAsync(x => x.Id == order.CustomerId, cancellationToken);
        var cancellationFee = assignment.Result == "CANCELLED" ? assignment.CancellationFeeAmount : 0m;
        var cashPaid = paymentRows.Where(x => x.Kind == "CASH").Sum(x => x.Amount);
        var creditPaid = paymentRows.Where(x => x.Kind == "CREDIT").Sum(x => x.Amount);
        var resolutionKind = paidAmount == 0 ? "NONE" : paymentResolution;
        var resolutionAmount = resolutionKind == "REFUND" ? Math.Max(0m, cashPaid - cancellationFee) :
            resolutionKind == "CREDIT" ? Math.Max(0m, paidAmount - cancellationFee) : 0m;
        var creditRestoredAmount = resolutionKind == "CREDIT" ? resolutionAmount :
            resolutionKind == "REFUND" ? Math.Max(0m, creditPaid - Math.Max(0m, cancellationFee - cashPaid)) : 0m;
        if (resolutionKind == "CREDIT" && resolutionAmount > 0) customer.GrantCredit(resolutionAmount);
        else if (resolutionKind == "REFUND")
        {
            if (creditRestoredAmount > 0) customer.GrantCredit(creditRestoredAmount);
        }
        db.OrderCancellationResolutions.Add(new OrderCancellationResolution(order.Id, resolutionKind,
            resolutionAmount, cancellationFee, refundReference?.Trim(), inspectorId, now));
        order.SetStatus("CANCELLED");
        AddOrderHistory(order.Id, inspectorId, "rejection.inspected", new
        {
            dispatchOrderId,
            notes,
            result = assignment.Result,
            cancellationFee,
            paymentResolution = resolutionKind,
            resolvedAmount = resolutionAmount,
            creditRestoredAmount,
            refundReference,
            lots = lots.Select(x => new { x.LotId, x.GoodUnits, x.DamagedUnits })
        }, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return DispatchOperationCode.Applied;
    }

    public async Task<DispatchOperationResult> CreateAsync(string vehicle, Guid leadId, Guid driverId,
        IReadOnlyList<Guid> orderIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var driverExists = await db.StaffAccountRoles.AnyAsync(x => x.StaffAccountId == driverId &&
            x.RoleCode == "DRIVER" && x.StaffAccount.IsActive, cancellationToken);
        if (!driverExists) return new(DispatchOperationCode.InvalidDriver);

        var sortedOrderIds = orderIds.Order().ToArray();
        var orders = await db.SalesOrders.FromSqlInterpolated(
                $"SELECT * FROM sales_orders WHERE id = ANY ({sortedOrderIds}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (orders.Count > 0)
        {
            var ids = orders.Select(x => x.Id).ToArray();
            await db.OrderLines.Include(x => x.LotAllocations).Where(x => ids.Contains(x.OrderId))
                .LoadAsync(cancellationToken);
        }
        if (orders.Count != orderIds.Count) return new(DispatchOperationCode.NotFound);
        var todayInBolivia = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(-4)).DateTime);
        if (orders.Any(x => x.IsNightOrder && x.ScheduledDeliveryDate > todayInBolivia))
            return new(DispatchOperationCode.InvalidOrderState);
        orders = orders.OrderByDescending(x => x.IsNightOrder)
            .ThenBy(x => x.ScheduledDeliveryDate ?? DateOnly.MaxValue).ThenBy(x => x.Id).ToList();
        foreach (var order in orders)
        {
            var attemptCount = await db.DispatchOrders.CountAsync(x => x.OrderId == order.Id, cancellationToken);
            var eligible = order.Fulfillment == "DELIVERY" && order.Status == "PREPARED" && attemptCount == 0 ||
                order.Fulfillment == "DELIVERY" && order.Status == "PREPARED" && attemptCount == 1 &&
                await db.DispatchOrders.AnyAsync(x => x.OrderId == order.Id && x.AttemptNumber == 1 && x.Result == "NOT_FOUND", cancellationToken);
            if (!eligible || order.Lines.Count == 0 || order.Lines.Any(l => l.LotAllocations.Sum(a => a.QuantityReserved) != l.QuantityUnits))
                return new(DispatchOperationCode.InvalidOrderState);
            var customerType = await db.Customers.Where(x => x.Id == order.CustomerId).Select(x => x.CustomerType).SingleAsync(cancellationToken);
            var paid = await db.OrderPayments.Where(x => x.OrderId == order.Id).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            if (customerType == "FAMILY" ? paid <= 0m : paid < order.TotalAmount)
                return new(DispatchOperationCode.InvalidOrderState);
        }

        var dispatch = new Dispatch($"DSP-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25],
            vehicle.Trim(), leadId, driverId, now, now);
        db.Dispatches.Add(dispatch);
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var attempt = (short)(await db.DispatchOrders.CountAsync(x => x.OrderId == order.Id, cancellationToken) + 1);
            var assignment = new DispatchOrder(dispatch.Id, order.Id, attempt, i + 1);
            db.DispatchOrders.Add(assignment);
            dispatch.Orders.Add(assignment);
            foreach (var allocation in order.Lines.SelectMany(x => x.LotAllocations))
            {
                var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                    $"SELECT * FROM inventory_lot_balances WHERE lot_id = {allocation.LotId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (balance is null || !balance.TryChangeReserved(-allocation.QuantityReserved) ||
                    !balance.TryAdjustOnHand(-allocation.QuantityReserved))
                    return new(DispatchOperationCode.InvalidOrderState);
                allocation.SetPicked(allocation.QuantityReserved);
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "DISPATCH",
                    -allocation.QuantityReserved, -allocation.QuantityReserved, leadId, now,
                    $"Salida de ruta {dispatch.DispatchNumber}", order.Id, assignment.Id));
            }
            order.SetStatus("DISPATCHED");
            AddOrderHistory(order.Id, leadId, "order.dispatched", new
            {
                dispatch.DispatchNumber,
                driverId,
                vehicle,
                assignment.Id,
                assignment.AttemptNumber,
                assignment.SequenceNumber
            }, now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(DispatchOperationCode.Applied, dispatch);
    }

    public async Task<DispatchOperationResult> RecordResultAsync(Guid dispatchOrderId, string result,
        string? recipientName, string? relationship, string? reason, byte[]? signature,
        string? mimeType, decimal cancellationFeeAmount, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var orderId = await db.DispatchOrders.AsNoTracking().Where(x => x.Id == dispatchOrderId)
            .Select(x => (Guid?)x.OrderId).SingleOrDefaultAsync(cancellationToken);
        if (orderId is null) return new(DispatchOperationCode.NotFound);
        var order = await db.SalesOrders.FromSqlInterpolated(
                $"SELECT * FROM sales_orders WHERE id = {orderId.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) return new(DispatchOperationCode.NotFound);
        var assignment = await db.DispatchOrders.FromSqlInterpolated(
                $"SELECT * FROM dispatch_orders WHERE id = {dispatchOrderId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (assignment is null) return new(DispatchOperationCode.NotFound);
        if (assignment.Result is not null) return new(DispatchOperationCode.AlreadyRecorded);
        var assignmentOwners = await db.Dispatches.Where(x => x.Id == assignment.DispatchId)
            .Select(x => new { x.DriverStaffId, x.DispatchLeadId }).SingleAsync(cancellationToken);
        if (actorId != assignmentOwners.DriverStaffId && actorId != assignmentOwners.DispatchLeadId)
            return new(DispatchOperationCode.InvalidDriver);
        await db.Entry(order).Collection(x => x.Lines).Query()
            .Include(x => x.LotAllocations).LoadAsync(cancellationToken);
        if (result == "DELIVERED" && string.IsNullOrWhiteSpace(recipientName))
            return new(DispatchOperationCode.InvalidSignature);
        if (result is "REJECTED" or "CANCELLED" && (string.IsNullOrWhiteSpace(reason) || signature is null))
            return new(DispatchOperationCode.InvalidSignature);

        assignment.RecordResult(result, recipientName?.Trim(), relationship?.Trim(), reason?.Trim(),
            signature, mimeType, now, actorId, cancellationFeeAmount);
        if (result == "DELIVERED")
        {
            order.SetStatus("DELIVERED");
            AddOrderHistory(order.Id, actorId, "delivery.completed", new
            {
                assignment.Id,
                recipientName,
                relationship,
                signatureMimeType = mimeType
            }, now);
        }
        else if (result == "NOT_FOUND")
        {
            foreach (var allocation in order.Lines.SelectMany(x => x.LotAllocations))
            {
                var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                    $"SELECT * FROM inventory_lot_balances WHERE lot_id = {allocation.LotId} FOR UPDATE")
                    .SingleAsync(cancellationToken);
                if (!balance.TryAdjustOnHand(allocation.QuantityPicked) ||
                    !balance.TryChangeReserved(allocation.QuantityPicked))
                    return new(DispatchOperationCode.InvalidOrderState);
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "RETURN",
                    allocation.QuantityPicked, allocation.QuantityPicked, actorId, now,
                    "Cliente no encontrado; unidades apartadas para un segundo intento", assignment.OrderId, assignment.Id));
            }
            order.SetStatus("PREPARED");
            AddOrderHistory(order.Id, actorId, "delivery.not_found", new
            {
                assignment.Id,
                reason,
                reattemptAllowed = assignment.AttemptNumber == 1
            }, now);
        }
        else // Rechazo o cancelación posterior a la salida: regresa como reservado hasta inspección.
        {
            foreach (var allocation in order.Lines.SelectMany(x => x.LotAllocations))
            {
                var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                    $"SELECT * FROM inventory_lot_balances WHERE lot_id = {allocation.LotId} FOR UPDATE")
                    .SingleAsync(cancellationToken);
                if (!balance.TryAdjustOnHand(allocation.QuantityPicked) ||
                    !balance.TryChangeReserved(allocation.QuantityPicked))
                    return new(DispatchOperationCode.InvalidOrderState);
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "RETURN",
                    allocation.QuantityPicked, allocation.QuantityPicked, actorId, now,
                    $"{(result == "CANCELLED" ? "Cancelación" : "Rechazo")} pendiente de inspección: {reason}", assignment.OrderId, assignment.Id));
            }
            order.SetStatus("DELIVERY_FAILED");
            AddOrderHistory(order.Id, actorId, result == "CANCELLED" ? "delivery.cancelled_after_departure" : "delivery.rejected", new
            {
                assignment.Id,
                reason,
                cancellationFeeAmount,
                signatureMimeType = mimeType,
                inspectionRequired = true
            }, now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(DispatchOperationCode.Applied);
    }

    private void AddOrderHistory(Guid orderId, Guid actorId, string eventType, object details, DateTimeOffset now) =>
        db.OrderHistoryEntries.Add(new OrderHistoryEntry(orderId, actorId, eventType,
            JsonSerializer.Serialize(details), now));
}
