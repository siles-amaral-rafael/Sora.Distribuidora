using Sora.Domain.Repositories;
using Sora.Shared.Orders;

namespace Sora.Application.Orders;

public sealed class OrderService(IOrderRepository repository, TimeProvider clock) : IOrderService
{
    public async Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || request.CustomerId == Guid.Empty || request.Lines.Count == 0 ||
            request.Lines.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0) ||
            request.Fulfillment is not ("DELIVERY" or "PICKUP_NOW" or "PICKUP_LATER") ||
            request.IsNightOrder && request.Fulfillment != "DELIVERY")
            return ServiceResult<OrderResponse>.Failure("invalid_order");

        var lines = request.Lines.Select(x => new OrderLineInput(x.ProductId, x.PresentationId, x.Quantity)).ToArray();
        DateOnly? scheduledDeliveryDate = request.IsNightOrder
            ? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(-4)).DateTime).AddDays(1)
            : null;
        var result = await repository.CreateAndReserveAsync(request.CustomerId, actorId, request.Fulfillment,
            request.IsNightOrder, scheduledDeliveryDate,
            lines, clock.GetUtcNow(), cancellationToken);
        if (result.Order is null)
            return ServiceResult<OrderResponse>.Failure(result.Code switch
            {
                CreateOrderResultCode.CustomerNotFound => "customer_not_found",
                CreateOrderResultCode.ProductUnavailable => "product_not_found_or_inactive",
                CreateOrderResultCode.InvalidPresentation => "invalid_presentation_or_quantity",
                CreateOrderResultCode.InsufficientStock => "insufficient_stock",
                CreateOrderResultCode.SaleBelowCost => "sale_below_lot_cost",
                CreateOrderResultCode.OutstandingBalance => "customer_outstanding_balance",
                _ => "order_creation_failed"
            });

        var saved = await repository.GetAsync(result.Order.Id, cancellationToken);
        return saved is null ? ServiceResult<OrderResponse>.Failure("order_creation_failed")
            : ServiceResult<OrderResponse>.Success(Map(saved));
    }

    public async Task<OrderResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await repository.GetAsync(id, cancellationToken)) is { } order ? Map(order) : null;

    public async Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<ServiceResult<bool>> MarkPreparedAsync(Guid orderId, Guid actorId, CancellationToken cancellationToken)
    {
        if (orderId == Guid.Empty) return ServiceResult<bool>.Failure("invalid_order");
        return await repository.MarkPreparedAsync(orderId, actorId, clock.GetUtcNow(), cancellationToken) switch
        {
            PrepareOrderResultCode.Prepared => ServiceResult<bool>.Success(true),
            PrepareOrderResultCode.NotFound => ServiceResult<bool>.Failure("order_not_found"),
            PrepareOrderResultCode.PaymentRequired => ServiceResult<bool>.Failure("payment_required_for_fulfillment"),
            _ => ServiceResult<bool>.Failure("order_must_be_delivery_and_registered")
        };
    }

    public async Task<ServiceResult<OrderResponse>> UpdateAsync(Guid orderId, UpdateOrderRequest request,
        Guid actorId, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0) ||
            request.Fulfillment is not ("DELIVERY" or "PICKUP_NOW" or "PICKUP_LATER"))
            return ServiceResult<OrderResponse>.Failure("invalid_order");
        var inputs = request.Lines.Select(x => new OrderLineInput(x.ProductId, x.PresentationId, x.Quantity)).ToArray();
        var code = await repository.UpdateAsync(orderId, request.Fulfillment, inputs, actorId, clock.GetUtcNow(), cancellationToken);
        if (code != OrderTransitionCode.Applied) return ServiceResult<OrderResponse>.Failure(TransitionError(code));
        var order = await repository.GetAsync(orderId, cancellationToken);
        return order is null ? ServiceResult<OrderResponse>.Failure("order_not_found") : ServiceResult<OrderResponse>.Success(Map(order));
    }

    public async Task<ServiceResult<OrderResponse>> ConfirmAvailabilityAsync(Guid orderId,
        ConfirmOrderAvailabilityRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConfirmationNote)) return ServiceResult<OrderResponse>.Failure("confirmation_note_required");
        var inputs = request.Lines.Select(x => new OrderLineInput(x.ProductId, x.PresentationId, x.Quantity)).ToArray();
        if (request.Accepted && (inputs.Length == 0 || inputs.Any(x => x.Quantity <= 0)))
            return ServiceResult<OrderResponse>.Failure("invalid_order");
        var code = await repository.ConfirmAvailabilityAsync(orderId, request.Accepted,
            request.ConfirmationNote.Trim(), inputs, actorId, clock.GetUtcNow(), cancellationToken);
        if (code is OrderAvailabilityResultCode.NotFound or OrderAvailabilityResultCode.InvalidState or
            OrderAvailabilityResultCode.InsufficientStock or OrderAvailabilityResultCode.ProductUnavailable or
            OrderAvailabilityResultCode.InvalidPresentation or OrderAvailabilityResultCode.SaleBelowCost)
            return ServiceResult<OrderResponse>.Failure(code switch
            {
                OrderAvailabilityResultCode.NotFound => "order_not_found",
                OrderAvailabilityResultCode.InvalidState => "order_transition_not_allowed",
                OrderAvailabilityResultCode.InsufficientStock => "insufficient_stock",
                OrderAvailabilityResultCode.SaleBelowCost => "sale_below_lot_cost",
                OrderAvailabilityResultCode.ProductUnavailable => "product_not_found_or_inactive",
                _ => "invalid_presentation_or_quantity"
            });
        var order = await repository.GetAsync(orderId, cancellationToken);
        return order is null ? ServiceResult<OrderResponse>.Failure("order_not_found") : ServiceResult<OrderResponse>.Success(Map(order));
    }

    public async Task<ServiceResult<bool>> CompletePickupAsync(Guid orderId, Guid actorId, CancellationToken cancellationToken) =>
        await MapTransition(await repository.CompletePickupAsync(orderId, actorId, clock.GetUtcNow(), cancellationToken));

    public async Task<ServiceResult<bool>> CancelAsync(Guid orderId, CancelOrderRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CancellationResolutionKind>(request.Resolution, true, out var resolution))
            return ServiceResult<bool>.Failure("invalid_cancellation_resolution");
        if (resolution is CancellationResolutionKind.REFUND or CancellationResolutionKind.CREDIT &&
            string.IsNullOrWhiteSpace(request.Reference))
            return ServiceResult<bool>.Failure("cancellation_reference_required");
        return await MapTransition(await repository.CancelAsync(orderId, resolution, request.Reference,
            actorId, clock.GetUtcNow(), cancellationToken));
    }

    public async Task<ServiceResult<OrderResponse>> RecordPaymentAsync(Guid orderId, RecordPaymentRequest request,
        Guid actorId, CancellationToken cancellationToken)
    {
        var code = await repository.RecordPaymentAsync(orderId, request.Amount, request.Reference, actorId,
            clock.GetUtcNow(), cancellationToken);
        if (code != PaymentOperationCode.Recorded)
            return ServiceResult<OrderResponse>.Failure(code switch
            {
                PaymentOperationCode.NotFound => "order_not_found",
                PaymentOperationCode.DuplicateReference => "payment_reference_already_used",
                PaymentOperationCode.Overpayment => "payment_exceeds_balance",
                PaymentOperationCode.PaymentRequired => "customer_type_requires_full_payment",
                PaymentOperationCode.InvalidAmount => "invalid_payment",
                _ => "payment_not_allowed"
            });
        var order = await repository.GetAsync(orderId, cancellationToken);
        return order is null ? ServiceResult<OrderResponse>.Failure("order_not_found") : ServiceResult<OrderResponse>.Success(Map(order));
    }

    private static Task<ServiceResult<bool>> MapTransition(OrderTransitionCode code) => Task.FromResult(code switch
    {
        OrderTransitionCode.Applied => ServiceResult<bool>.Success(true),
        OrderTransitionCode.NotFound => ServiceResult<bool>.Failure("order_not_found"),
        OrderTransitionCode.PaymentResolutionRequired => ServiceResult<bool>.Failure("payment_resolution_required"),
        _ => ServiceResult<bool>.Failure("order_transition_not_allowed")
    });

    private static string TransitionError(OrderTransitionCode code) => code switch
    {
        OrderTransitionCode.NotFound => "order_not_found",
        OrderTransitionCode.SaleBelowCost => "sale_below_lot_cost",
        _ => "order_transition_not_allowed"
    };

    private static OrderResponse Map(Sora.Domain.Entities.SalesOrder order) => new(
        order.Id, order.OrderNumber, order.CustomerId, order.Status, order.Fulfillment,
        order.TotalAmount, order.CreatedAtUtc,
        order.Lines.Select(line => new OrderLineResponse(line.ProductId, line.Product.Name,
            line.Presentation?.Name, line.EnteredQuantity, line.QuantityUnits,
            line.UnitSalePriceSnapshot, line.LineTotal, line.SubtotalAmount, line.DiscountAmount,
            line.UnitsAvailableAtCreation, line.ShortageUnits,
            line.LotAllocations.Select(a => new OrderLotAllocationResponse(a.Lot.LotCode,
                a.Lot.ExpiryDate, a.QuantityReserved)).ToList())).ToList(),
        order.History.OrderBy(x => x.OccurredAtUtc).Select(x => new OrderHistoryResponse(
            x.EventType, x.OccurredAtUtc, x.Actor.FullName, x.Details)).ToList(),
        order.Payments.Sum(x => x.Amount), Math.Max(0, order.TotalAmount - order.Payments.Sum(x => x.Amount)),
        order.Payments.OrderBy(x => x.RecordedAtUtc).Select(x => new OrderPaymentResponse(x.Amount, x.Reference, x.RecordedAtUtc, x.RecordedBy.FullName, x.Kind)).ToList(),
        order.Fulfillment is "PICKUP_NOW" or "PICKUP_LATER" ||
        order.DispatchAssignments.Any(x => x.Result == "NOT_FOUND") &&
        order.DispatchAssignments.All(x => x.Result is not null), order.IsNightOrder,
        order.ScheduledDeliveryDate);
}
