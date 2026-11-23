using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class OrderRepository(SoraDbContext db) : IOrderRepository
{
    public async Task<CreateOrderResult> CreateAndReserveAsync(Guid customerId, Guid actorId, string fulfillment,
        bool isNightOrder, DateOnly? scheduledDeliveryDate, IReadOnlyList<OrderLineInput> inputs,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive, cancellationToken);
        if (customer is null) return new(CreateOrderResultCode.CustomerNotFound);
        if (await db.SalesOrders.Where(x => x.CustomerId == customerId && x.Status != "CANCELLED")
                .AnyAsync(x => x.TotalAmount > (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0m), cancellationToken))
            return new(CreateOrderResultCode.OutstandingBalance);
        var (code, planned) = await PlanLinesAsync(inputs, cancellationToken);
        if (code is not null || planned is null) return new(code ?? CreateOrderResultCode.InvalidPresentation);
        var lockedLots = await LockLotsAsync(planned.Select(x => x.Product.Id), now, cancellationToken);
        var lots = lockedLots.ByProduct;
        var shortages = GetShortages(planned, lots);
        var pending = shortages.Count > 0;
        if (!pending && HasSaleBelowCost(planned, lots)) return new(CreateOrderResultCode.SaleBelowCost);
        var order = NewOrder(customer, customerId, actorId,
            pending ? "PENDING_CONFIRMATION" : "REGISTERED", fulfillment, now,
            planned.Sum(x => x.Subtotal));
        if (isNightOrder)
        {
            if (fulfillment != "DELIVERY" || scheduledDeliveryDate is null) return new(CreateOrderResultCode.InvalidPresentation);
            order.ScheduleNightDelivery(scheduledDeliveryDate.Value);
        }
        db.SalesOrders.Add(order);

        if (pending)
        {
            var availableRemaining = lots.ToDictionary(x => x.Key,
                x => x.Value.Sum(y => y.Balance.AvailableUnits));
            foreach (var item in planned)
            {
                var available = Math.Min(item.Units, availableRemaining.GetValueOrDefault(item.Product.Id));
                availableRemaining[item.Product.Id] = availableRemaining.GetValueOrDefault(item.Product.Id) - available;
                var line = NewLine(order.Id, item, available, item.Units - available);
                db.OrderLines.Add(line);
            }
            AddHistory(order, actorId, "order.pending_confirmation", new
            {
                reason = "insufficient_stock",
                shortages = shortages.Select(x => new { productId = x.Key, requested = x.Value.Requested, available = x.Value.Available, missing = x.Value.Requested - x.Value.Available }),
                fulfillment
                ,isNightOrder, scheduledDeliveryDate
            }, now);
            AddNotification(order, customer, "ORDER_RECEIVED", OrderReceivedMessage(order, pending: true), now);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new(CreateOrderResultCode.PendingConfirmation, order);
        }

        var createdLines = new List<OrderLine>();
        var quantityByProduct = planned.GroupBy(x => x.Product.Id).ToDictionary(x => x.Key, x => x.Sum(y => y.Units));
        foreach (var item in planned)
        {
            var line = NewLine(order.Id, item, item.Units, 0);
            createdLines.Add(line);
            db.OrderLines.Add(line);
            ReserveLine(line, item, lots, order, actorId, now);
            ApplyDiscount(line, item.Product, quantityByProduct[item.Product.Id]);
        }
        order.SetTotalAmount(createdLines.Sum(x => x.LineTotal));
        ApplyCustomerCredit(order, customer, actorId, now);
        AddHistory(order, actorId, "order.created", new
        {
            fulfillment,
            isNightOrder, scheduledDeliveryDate,
            lines = createdLines.Select(x => new { x.ProductId, x.QuantityUnits, x.UnitSalePriceSnapshot, x.DiscountAmount, x.LineTotal }),
            order.TotalAmount
        }, now);
        AddNotification(order, customer, "ORDER_RECEIVED", OrderReceivedMessage(order, pending: false), now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(CreateOrderResultCode.Created, order);
    }

    public Task<SalesOrder?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OrderGraph().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SalesOrder>> ListAsync(CancellationToken cancellationToken) =>
        await OrderGraph().OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);

    public async Task<PrepareOrderResultCode> MarkPreparedAsync(Guid orderId, Guid actorId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return PrepareOrderResultCode.NotFound;
        if (order.Status != "REGISTERED" || order.Fulfillment != "DELIVERY")
            return PrepareOrderResultCode.InvalidState;
        if (!await CanFulfillAsync(order, cancellationToken)) return PrepareOrderResultCode.PaymentRequired;
        order.SetStatus("PREPARED");
        AddHistory(order, actorId, "order.prepared", new { order.OrderNumber }, now);
        var customer = await db.Customers.SingleAsync(x => x.Id == order.CustomerId, cancellationToken);
        AddNotification(order, customer, "ORDER_PREPARED", $"El pedido {order.OrderNumber} está preparado.", now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return PrepareOrderResultCode.Prepared;
    }

    public async Task<OrderTransitionCode> UpdateAsync(Guid orderId, string fulfillment,
        IReadOnlyList<OrderLineInput> inputs, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return OrderTransitionCode.NotFound;
        if (order.Status is not ("REGISTERED" or "PREPARED") ||
            await db.DispatchOrders.AnyAsync(x => x.OrderId == orderId, cancellationToken))
            return OrderTransitionCode.InvalidState;
        if (order.IsNightOrder && fulfillment != "DELIVERY") return OrderTransitionCode.InvalidState;
        if (await db.OrderPayments.AnyAsync(x => x.OrderId == orderId, cancellationToken)) return OrderTransitionCode.InvalidState;
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == order.CustomerId && x.IsActive, cancellationToken);
        if (customer is null) return OrderTransitionCode.InvalidState;
        var (planCode, planned) = await PlanLinesAsync(inputs, cancellationToken);
        if (planCode is not null || planned is null) return OrderTransitionCode.InvalidState;
        var oldLines = await db.OrderLines.Include(x => x.LotAllocations)
            .Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        if (oldLines.Count == 0 || oldLines.Any(x => x.LotAllocations.Sum(a => a.QuantityReserved) != x.QuantityUnits))
            return OrderTransitionCode.InvalidState;
        var products = planned!.Select(x => x.Product.Id).Concat(oldLines.Select(x => x.ProductId)).Distinct().ToArray();
        var lockedLots = await LockLotsIncludingAllocatedAsync(products,
            oldLines.SelectMany(x => x.LotAllocations).Select(x => x.LotId), now, cancellationToken);
        var lots = lockedLots.ByProduct;
        var before = Snapshot(oldLines);

        foreach (var allocation in oldLines.SelectMany(x => x.LotAllocations))
        {
            var balance = lockedLots.BalancesByLot.GetValueOrDefault(allocation.LotId);
            if (balance is null || !balance.TryChangeReserved(-allocation.QuantityReserved))
                return OrderTransitionCode.InvalidState;
            db.InventoryMovements.Add(new InventoryMovement(allocation.LotId, "RELEASE", 0,
                -allocation.QuantityReserved, actorId, now, $"Liberación por edición de {order.OrderNumber}", order.Id));
        }
        if (GetShortages(planned, lots).Count > 0) return OrderTransitionCode.InvalidState;
        if (HasSaleBelowCost(planned, lots)) return OrderTransitionCode.SaleBelowCost;

        db.OrderLotAllocations.RemoveRange(oldLines.SelectMany(x => x.LotAllocations));
        db.OrderLines.RemoveRange(oldLines);
        var newLines = new List<OrderLine>();
        var quantityByProduct = planned.GroupBy(x => x.Product.Id).ToDictionary(x => x.Key, x => x.Sum(y => y.Units));
        foreach (var item in planned)
        {
            var line = NewLine(order.Id, item, item.Units, 0);
            newLines.Add(line);
            db.OrderLines.Add(line);
            ReserveLine(line, item, lots, order, actorId, now);
            ApplyDiscount(line, item.Product, quantityByProduct[item.Product.Id]);
        }
        order.UpdateFulfillment(fulfillment, fulfillment == "DELIVERY" ? customer.PrimaryAddress : null,
            fulfillment == "DELIVERY" ? customer.Latitude : null,
            fulfillment == "DELIVERY" ? customer.Longitude : null);
        order.SetTotalAmount(newLines.Sum(x => x.LineTotal));
        order.SetStatus("REGISTERED");
        AddHistory(order, actorId, "order.edited", new
        {
            before,
            after = Snapshot(newLines),
            fulfillment,
            total = order.TotalAmount
        }, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OrderTransitionCode.Applied;
    }

    public async Task<OrderAvailabilityResultCode> ConfirmAvailabilityAsync(Guid orderId, bool accepted,
        string note, IReadOnlyList<OrderLineInput> inputs, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return OrderAvailabilityResultCode.NotFound;
        if (order.Status != "PENDING_CONFIRMATION") return OrderAvailabilityResultCode.InvalidState;
        var oldLines = await db.OrderLines.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        var before = Snapshot(oldLines);
        if (!accepted)
        {
            order.SetStatus("CANCELLED");
            AddHistory(order, actorId, "availability.rejected", new { note, requested = before }, now);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return OrderAvailabilityResultCode.Cancelled;
        }
        var (planCode, planned) = await PlanLinesAsync(inputs, cancellationToken);
        if (planCode == CreateOrderResultCode.ProductUnavailable)
            return OrderAvailabilityResultCode.ProductUnavailable;
        if (planCode is not null || planned is null || planned.Count == 0)
            return OrderAvailabilityResultCode.InvalidPresentation;
        var lockedLots = await LockLotsAsync(planned.Select(x => x.Product.Id), now, cancellationToken);
        var lots = lockedLots.ByProduct;
        if (GetShortages(planned, lots).Count > 0) return OrderAvailabilityResultCode.InsufficientStock;
        if (HasSaleBelowCost(planned, lots)) return OrderAvailabilityResultCode.SaleBelowCost;

        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == order.CustomerId && x.IsActive, cancellationToken);
        if (customer is null) return OrderAvailabilityResultCode.InvalidState;
        db.OrderLines.RemoveRange(oldLines);
        var newLines = new List<OrderLine>();
        var quantityByProduct = planned.GroupBy(x => x.Product.Id).ToDictionary(x => x.Key, x => x.Sum(y => y.Units));
        foreach (var item in planned)
        {
            var line = NewLine(order.Id, item, item.Units, 0);
            newLines.Add(line);
            db.OrderLines.Add(line);
            ReserveLine(line, item, lots, order, actorId, now);
            ApplyDiscount(line, item.Product, quantityByProduct[item.Product.Id]);
        }
        order.SetStatus("REGISTERED");
        order.SetTotalAmount(newLines.Sum(x => x.LineTotal));
        ApplyCustomerCredit(order, customer, actorId, now);
        AddHistory(order, actorId, "availability.accepted", new
        {
            note,
            requested = before,
            accepted = Snapshot(newLines),
            total = order.TotalAmount
        }, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OrderAvailabilityResultCode.Confirmed;
    }

    public Task<OrderTransitionCode> CompletePickupAsync(Guid orderId, Guid actorId,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        TransitionStockAsync(orderId, actorId, now, completePickup: true, cancellationToken);

    public Task<OrderTransitionCode> CancelAsync(Guid orderId, CancellationResolutionKind resolution,
        string? reference, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
        TransitionStockAsync(orderId, actorId, now, completePickup: false, cancellationToken, resolution, reference);

    private async Task<OrderTransitionCode> TransitionStockAsync(Guid orderId, Guid actorId,
        DateTimeOffset now, bool completePickup, CancellationToken cancellationToken,
        CancellationResolutionKind resolution = CancellationResolutionKind.NONE, string? reference = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return OrderTransitionCode.NotFound;
        if (completePickup && order.Status is not ("REGISTERED" or "PREPARED")) return OrderTransitionCode.InvalidState;
        var pickupAfterFailedAttempt = completePickup && order.Fulfillment == "DELIVERY" &&
            await db.DispatchOrders.AnyAsync(x => x.OrderId == orderId && x.Result == "NOT_FOUND", cancellationToken) &&
            !await db.DispatchOrders.AnyAsync(x => x.OrderId == orderId && x.Result == null, cancellationToken);
        if (completePickup && order.Fulfillment is not ("PICKUP_NOW" or "PICKUP_LATER") && !pickupAfterFailedAttempt) return OrderTransitionCode.InvalidState;
        if (completePickup && !await CanFulfillAsync(order, cancellationToken)) return OrderTransitionCode.InvalidState;
        if (!completePickup && order.Status is not ("PENDING_CONFIRMATION" or "REGISTERED" or "PREPARED"))
            return OrderTransitionCode.InvalidState;
        var paymentAmounts = completePickup ? new List<(string Kind, decimal Amount)>() :
            (await db.OrderPayments.Where(x => x.OrderId == orderId).Select(x => new { x.Kind, x.Amount })
                .ToListAsync(cancellationToken)).Select(x => (x.Kind, x.Amount)).ToList();
        var paidAmount = paymentAmounts.Sum(x => x.Amount);
        if (!completePickup && paidAmount > 0 && resolution == CancellationResolutionKind.NONE)
            return OrderTransitionCode.PaymentResolutionRequired;
        if (!completePickup && paidAmount == 0) resolution = CancellationResolutionKind.NONE;
        if (!completePickup && resolution == CancellationResolutionKind.REFUND && string.IsNullOrWhiteSpace(reference))
            return OrderTransitionCode.PaymentResolutionRequired;
        if (!completePickup && resolution == CancellationResolutionKind.CREDIT && string.IsNullOrWhiteSpace(reference))
            return OrderTransitionCode.PaymentResolutionRequired;
        var customerForCancel = !completePickup
            ? await db.Customers.SingleAsync(x => x.Id == order.CustomerId, cancellationToken) : null;

        var lines = await db.OrderLines.Include(x => x.LotAllocations)
            .Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        if (lines.Count == 0) return OrderTransitionCode.InvalidState;
        var allocations = lines.SelectMany(x => x.LotAllocations).ToArray();
        if (order.Status != "PENDING_CONFIRMATION" &&
            lines.Any(x => x.LotAllocations.Sum(a => a.QuantityReserved) != x.QuantityUnits))
            return OrderTransitionCode.InvalidState;
        if (order.Status == "PENDING_CONFIRMATION" && allocations.Length != 0)
            return OrderTransitionCode.InvalidState;

        if (allocations.Length > 0)
        {
            var balances = await LockLotsByIdAsync(allocations.Select(x => x.LotId), cancellationToken);
            foreach (var allocation in allocations)
            {
                if (!balances.TryGetValue(allocation.LotId, out var balance) ||
                    !balance.TryChangeReserved(-allocation.QuantityReserved)) return OrderTransitionCode.InvalidState;
                if (completePickup)
                {
                    if (!balance.TryAdjustOnHand(-allocation.QuantityReserved)) return OrderTransitionCode.InvalidState;
                    allocation.SetPicked(allocation.QuantityReserved);
                }
                db.InventoryMovements.Add(new InventoryMovement(allocation.LotId,
                    completePickup ? "PICK" : "RELEASE", completePickup ? -allocation.QuantityReserved : 0,
                    -allocation.QuantityReserved, actorId, now,
                    completePickup ? $"Retiro en empresa para {order.OrderNumber}" : $"Cancelación antes del despacho: {order.OrderNumber}", order.Id));
            }
        }
        order.SetStatus(completePickup ? "DELIVERED" : "CANCELLED");
        var creditRestoredAmount = 0m;
        if (!completePickup)
        {
            var cashPaid = paymentAmounts.Where(x => x.Kind == "CASH").Sum(x => x.Amount);
            var creditPaid = paymentAmounts.Where(x => x.Kind == "CREDIT").Sum(x => x.Amount);
            var resolvedAmount = resolution == CancellationResolutionKind.REFUND ? cashPaid :
                resolution == CancellationResolutionKind.CREDIT ? paidAmount : 0m;
            creditRestoredAmount = resolution == CancellationResolutionKind.CREDIT ? paidAmount :
                resolution == CancellationResolutionKind.REFUND ? creditPaid : 0m;
            if (resolution == CancellationResolutionKind.CREDIT && paidAmount > 0)
                customerForCancel!.GrantCredit(paidAmount);
            else if (resolution == CancellationResolutionKind.REFUND && creditPaid > 0)
                customerForCancel!.GrantCredit(creditPaid);
            db.OrderCancellationResolutions.Add(new OrderCancellationResolution(order.Id,
                resolution.ToString(), resolvedAmount, 0m, reference?.Trim(), actorId, now));
        }
        AddHistory(order, actorId, completePickup ? "order.pickup_completed" : "order.cancelled",
            new { order.OrderNumber, order.TotalAmount, resolution = resolution.ToString(), paidAmount, creditRestoredAmount }, now);
        if (completePickup)
        {
            var customer = await db.Customers.SingleAsync(x => x.Id == order.CustomerId, cancellationToken);
            AddNotification(order, customer, "DELIVERY_COMPLETED", $"El pedido {order.OrderNumber} fue entregado en la empresa.", now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OrderTransitionCode.Applied;
    }

    private IQueryable<SalesOrder> OrderGraph() => db.SalesOrders.AsNoTracking()
        .Include(x => x.Customer).Include(x => x.Payments).ThenInclude(x => x.RecordedBy)
        .Include(x => x.DispatchAssignments)
        .Include(x => x.Lines).ThenInclude(x => x.Product)
        .Include(x => x.Lines).ThenInclude(x => x.Presentation)
        .Include(x => x.Lines).ThenInclude(x => x.LotAllocations).ThenInclude(x => x.Lot)
        .Include(x => x.History).ThenInclude(x => x.Actor);

    public async Task<PaymentOperationCode> RecordPaymentAsync(Guid orderId, decimal amount, string reference,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount || string.IsNullOrWhiteSpace(reference)) return PaymentOperationCode.InvalidAmount;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var order = await LockOrderAsync(orderId, cancellationToken);
        if (order is null) return PaymentOperationCode.NotFound;
        if (order.Status is "CANCELLED" or "PENDING_CONFIRMATION") return PaymentOperationCode.InvalidState;
        reference = reference.Trim();
        if (await db.OrderPayments.AnyAsync(x => x.Reference == reference, cancellationToken)) return PaymentOperationCode.DuplicateReference;
        var paid = await db.OrderPayments.Where(x => x.OrderId == orderId).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var remaining = order.TotalAmount - paid;
        if (remaining <= 0 || amount > remaining) return PaymentOperationCode.Overpayment;
        var customerType = await db.Customers.Where(x => x.Id == order.CustomerId).Select(x => x.CustomerType).SingleAsync(cancellationToken);
        if (customerType != "FAMILY" && amount != remaining) return PaymentOperationCode.PaymentRequired;
        db.OrderPayments.Add(new OrderPayment(orderId, amount, reference, actorId, now, "CASH"));
        AddHistory(order, actorId, "payment.recorded", new { amount, reference, remaining = remaining - amount }, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return PaymentOperationCode.Recorded;
    }

    private async Task<bool> CanFulfillAsync(SalesOrder order, CancellationToken ct)
    {
        var paid = await db.OrderPayments.Where(x => x.OrderId == order.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var type = await db.Customers.Where(x => x.Id == order.CustomerId).Select(x => x.CustomerType).SingleAsync(ct);
        return type == "FAMILY" ? paid > 0m : paid >= order.TotalAmount;
    }

    private void ApplyCustomerCredit(SalesOrder order, Customer customer, Guid actorId, DateTimeOffset now)
    {
        var creditUsed = Math.Min(order.TotalAmount, customer.CreditBalance);
        if (creditUsed <= 0 || !customer.TryConsumeCredit(creditUsed)) return;
        db.OrderPayments.Add(new OrderPayment(order.Id, creditUsed,
            $"CREDIT-{Guid.NewGuid():N}"[..32], actorId, now, "CREDIT"));
        AddHistory(order, actorId, "payment.credit_applied", new
        {
            amount = creditUsed,
            remainingCustomerCredit = customer.CreditBalance
        }, now);
    }

    private void AddNotification(SalesOrder order, Customer customer, string eventType, string message, DateTimeOffset now) =>
        db.NotificationOutbox.Add(new NotificationOutboxEntry(order.Id, customer.Id, eventType, customer.Phone, message, now));

    private static string OrderReceivedMessage(SalesOrder order, bool pending) =>
        $"Recibimos el pedido {order.OrderNumber}" +
        (pending ? ". Está pendiente de confirmar disponibilidad." : $" por Bs {order.TotalAmount:0.00}.") +
        (order.IsNightOrder ? $" Pedido nocturno programado para primera salida del {order.ScheduledDeliveryDate:dd/MM/yyyy}." : string.Empty);

    private Task<SalesOrder?> LockOrderAsync(Guid id, CancellationToken cancellationToken) =>
        db.SalesOrders.FromSqlInterpolated($"SELECT * FROM sales_orders WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<(CreateOrderResultCode? Code, List<PlannedLine>? Lines)> PlanLinesAsync(
        IReadOnlyList<OrderLineInput> inputs, CancellationToken cancellationToken)
    {
        if (inputs.Count == 0) return (CreateOrderResultCode.InvalidPresentation, null);
        var result = new List<PlannedLine>();
        foreach (var input in inputs)
        {
            if (input.ProductId == Guid.Empty || input.Quantity <= 0) return (CreateOrderResultCode.InvalidPresentation, null);
            var product = await db.Products.Include(x => x.Presentations).Include(x => x.DiscountRules)
                .SingleOrDefaultAsync(x => x.Id == input.ProductId && x.IsActive, cancellationToken);
            if (product is null) return (CreateOrderResultCode.ProductUnavailable, null);
            var unitsPerPresentation = 1;
            if (input.PresentationId is Guid presentationId)
            {
                var presentation = product.Presentations.SingleOrDefault(x => x.Id == presentationId && x.IsActive);
                if (presentation is null) return (CreateOrderResultCode.InvalidPresentation, null);
                unitsPerPresentation = presentation.UnitsPerPresentation;
            }
            var rawUnits = input.Quantity * unitsPerPresentation;
            if (rawUnits != decimal.Truncate(rawUnits) || rawUnits > int.MaxValue)
                return (CreateOrderResultCode.InvalidPresentation, null);
            var units = (int)rawUnits;
            var subtotal = decimal.Round(units * product.UnitSalePrice, 2, MidpointRounding.AwayFromZero);
            result.Add(new PlannedLine(product, input.PresentationId, input.Quantity, units, subtotal));
        }
        return (null, result);
    }

    private async Task<LockedLotSet> LockLotsAsync(IEnumerable<Guid> productIds,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        var lotRows = await db.InventoryLots.AsNoTracking().Where(x => ids.Contains(x.ProductId) &&
                (x.ExpiryDate == null || x.ExpiryDate >= currentDate))
            .OrderBy(x => x.ProductId).ThenBy(x => x.ExpiryDate).ThenBy(x => x.Id)
            .Select(x => new LotRow(x.Id, x.ProductId, x.UnitPurchaseCost, true))
            .ToListAsync(cancellationToken);
        return await LockLotRowsAsync(lotRows, lotRows, ids, cancellationToken);
    }

    private async Task<LockedLotSet> LockLotRowsAsync(IReadOnlyList<LotRow> candidateRows,
        IReadOnlyList<LotRow> allRows, Guid[] productIds, CancellationToken cancellationToken)
    {
        var balances = new Dictionary<Guid, InventoryLotBalance>();
        foreach (var lot in allRows.OrderBy(x => x.Id))
        {
            var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                $"SELECT * FROM inventory_lot_balances WHERE lot_id = {lot.Id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (balance is not null) balances[lot.Id] = balance;
        }
        var byProduct = productIds.ToDictionary(x => x, _ => new List<LockedLot>());
        foreach (var lot in candidateRows)
            if (balances.TryGetValue(lot.Id, out var balance))
                byProduct[lot.ProductId].Add(new LockedLot(lot.Id, lot.ProductId, lot.UnitPurchaseCost, balance));
        return new LockedLotSet(byProduct, balances);
    }

    private async Task<LockedLotSet> LockLotsIncludingAllocatedAsync(IEnumerable<Guid> productIds,
        IEnumerable<Guid> allocatedLotIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        var allocationIds = allocatedLotIds.Distinct().ToArray();
        var lotRows = await db.InventoryLots.AsNoTracking().Where(x =>
                (ids.Contains(x.ProductId) && (x.ExpiryDate == null || x.ExpiryDate >= currentDate)) || allocationIds.Contains(x.Id))
            .OrderBy(x => x.ProductId).ThenBy(x => x.ExpiryDate).ThenBy(x => x.Id)
            .Select(x => new LotRow(x.Id, x.ProductId, x.UnitPurchaseCost,
                ids.Contains(x.ProductId) && (x.ExpiryDate == null || x.ExpiryDate >= currentDate)))
            .ToListAsync(cancellationToken);
        return await LockLotRowsAsync(lotRows.Where(x => x.IsEligible).ToArray(), lotRows, ids, cancellationToken);
    }

    private async Task<Dictionary<Guid, InventoryLotBalance>> LockLotsByIdAsync(IEnumerable<Guid> lotIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, InventoryLotBalance>();
        foreach (var lotId in lotIds.Distinct().Order())
        {
            var balance = await db.InventoryLotBalances.FromSqlInterpolated(
                $"SELECT * FROM inventory_lot_balances WHERE lot_id = {lotId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (balance is not null) result[lotId] = balance;
        }
        return result;
    }

    private static Dictionary<Guid, (int Requested, int Available)> GetShortages(
        IReadOnlyList<PlannedLine> lines, Dictionary<Guid, List<LockedLot>> lots)
    {
        var shortages = new Dictionary<Guid, (int Requested, int Available)>();
        foreach (var group in lines.GroupBy(x => x.Product.Id))
        {
            var requested = group.Sum(x => x.Units);
            var available = lots.GetValueOrDefault(group.Key)?.Sum(x => x.Balance.AvailableUnits) ?? 0;
            if (requested > available) shortages[group.Key] = (requested, available);
        }
        return shortages;
    }

    private static bool HasSaleBelowCost(IReadOnlyList<PlannedLine> lines,
        Dictionary<Guid, List<LockedLot>> lots)
    {
        foreach (var group in lines.GroupBy(x => x.Product.Id))
        {
            var remaining = group.Sum(x => x.Units);
            var cost = 0m;
            foreach (var lot in lots.GetValueOrDefault(group.Key) ?? [])
            {
                var take = Math.Min(remaining, lot.Balance.AvailableUnits);
                if (take <= 0) continue;
                cost += take * lot.UnitPurchaseCost;
                remaining -= take;
                if (remaining == 0) break;
            }
            if (remaining > 0) continue;
            var subtotal = group.Sum(x => x.Subtotal);
            if (cost > subtotal) return true;
        }
        return false;
    }

    private static SalesOrder NewOrder(Customer customer, Guid customerId, Guid actorId, string status,
        string fulfillment, DateTimeOffset now, decimal total) => new(
        $"SO-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25], customerId, actorId, status, fulfillment,
        fulfillment == "DELIVERY" ? customer.PrimaryAddress : null,
        fulfillment == "DELIVERY" ? customer.Latitude : null,
        fulfillment == "DELIVERY" ? customer.Longitude : null, total, now);

    private static OrderLine NewLine(Guid orderId, PlannedLine item, int available, int shortage) =>
        new(orderId, item.Product.Id, item.PresentationId, item.EnteredQuantity, item.Units,
            item.Product.UnitSalePrice, item.Subtotal, available, shortage);

    private void ReserveLine(OrderLine line, PlannedLine item, Dictionary<Guid, List<LockedLot>> lots,
        SalesOrder order, Guid actorId, DateTimeOffset now)
    {
        var remaining = item.Units;
        foreach (var lot in lots.GetValueOrDefault(item.Product.Id) ?? [])
        {
            if (remaining == 0) break;
            var quantity = Math.Min(remaining, lot.Balance.AvailableUnits);
            if (quantity <= 0) continue;
            if (!lot.Balance.TryChangeReserved(quantity)) throw new InvalidOperationException("El saldo del lote cambió durante la reserva.");
            var allocation = new OrderLotAllocation(line.Id, lot.LotId, quantity, lot.UnitPurchaseCost);
            line.LotAllocations.Add(allocation);
            db.OrderLotAllocations.Add(allocation);
            db.InventoryMovements.Add(new InventoryMovement(lot.LotId, "RESERVE", 0, quantity,
                actorId, now, $"Reserva FEFO para {order.OrderNumber}", order.Id));
            remaining -= quantity;
        }
        if (remaining != 0) throw new InvalidOperationException("La reserva validada no pudo completarse.");
    }

    private static void ApplyDiscount(OrderLine line, Product product, int totalProductUnits)
    {
        var rule = product.DiscountRules.Where(x => x.MinimumQuantityUnits <= totalProductUnits)
            .OrderByDescending(x => x.MinimumQuantityUnits).FirstOrDefault();
        if (rule is null) return;
        var cost = line.LotAllocations.Sum(x => x.UnitCostSnapshot * x.QuantityReserved);
        var grossMargin = Math.Max(0, line.SubtotalAmount - cost);
        var cap = decimal.Floor(grossMargin * 0.20m * 100m) / 100m;
        var requested = decimal.Round(line.SubtotalAmount * rule.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        line.ApplyDiscount(Math.Min(cap, requested));
    }

    private void AddHistory(SalesOrder order, Guid actorId, string eventType, object details, DateTimeOffset now) =>
        db.OrderHistoryEntries.Add(new OrderHistoryEntry(order.Id, actorId, eventType,
            JsonSerializer.Serialize(details), now));

    private static object Snapshot(IEnumerable<OrderLine> lines) => lines.Select(x => new
    {
        x.ProductId, x.PresentationId, x.EnteredQuantity, x.QuantityUnits,
        x.UnitSalePriceSnapshot, x.SubtotalAmount, x.DiscountAmount, x.LineTotal
    }).ToArray();

    private sealed record LotRow(Guid Id, Guid ProductId, decimal UnitPurchaseCost, bool IsEligible);
    private sealed record LockedLotSet(Dictionary<Guid, List<LockedLot>> ByProduct,
        Dictionary<Guid, InventoryLotBalance> BalancesByLot);
    private sealed record PlannedLine(Product Product, Guid? PresentationId, decimal EnteredQuantity,
        int Units, decimal Subtotal);
    private sealed record LockedLot(Guid LotId, Guid ProductId, decimal UnitPurchaseCost, InventoryLotBalance Balance);
}
