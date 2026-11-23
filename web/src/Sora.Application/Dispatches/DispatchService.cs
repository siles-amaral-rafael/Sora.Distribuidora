using Sora.Domain.Repositories;
using Sora.Shared.Dispatches;

namespace Sora.Application.Dispatches;

public sealed class DispatchService(IDispatchRepository repository, TimeProvider clock) : IDispatchService
{
    public async Task<IReadOnlyList<AssignedDeliveryResponse>> ListAssignedOpenDeliveriesAsync(Guid? driverId,
        CancellationToken cancellationToken) =>
        (await repository.ListAssignedOpenDeliveriesAsync(driverId, cancellationToken)).Select(x =>
            new AssignedDeliveryResponse(x.Id, x.OrderId, x.Order.OrderNumber, x.Dispatch.DispatchNumber,
                x.Dispatch.VehicleIdentifier, x.Dispatch.DepartedAtUtc, x.Order.Customer.FullName,
                x.Order.DeliveryAddressSnapshot ?? x.Order.Customer.PrimaryAddress,
                x.Order.DeliveryLatitude, x.Order.DeliveryLongitude)).ToList();

    public async Task<IReadOnlyList<PendingInspectionResponse>> ListPendingInspectionsAsync(CancellationToken cancellationToken) =>
        (await repository.ListPendingInspectionsAsync(cancellationToken)).Select(x => new PendingInspectionResponse(
            x.Id, x.OrderId, x.Order.OrderNumber,
            x.Order.Lines.SelectMany(l => l.LotAllocations).GroupBy(a => a.LotId).Select(g =>
                new PendingInspectionLotResponse(g.Key, g.First().Lot.LotCode, g.Sum(a => a.QuantityPicked))).ToList(),
            x.Result!, x.CancellationFeeAmount, x.Order.Payments.Sum(p => p.Amount),
            Math.Max(0m, x.Order.TotalAmount - x.Order.Payments.Sum(p => p.Amount)))).ToList();

    public async Task<ServiceResult<bool>> InspectRejectedAsync(Guid dispatchOrderId,
        InspectRejectedDeliveryRequest request, Guid inspectorId, CancellationToken cancellationToken)
    {
        if (dispatchOrderId == Guid.Empty || inspectorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Notes) ||
            request.Lots.Count == 0 || request.Lots.Any(x => x.LotId == Guid.Empty || x.GoodUnits < 0 || x.DamagedUnits < 0))
            return ServiceResult<bool>.Failure("invalid_inspection");
        if (request.PaymentResolution is not ("NONE" or "REFUND" or "CREDIT") ||
            request.PaymentResolution is "REFUND" or "CREDIT" && string.IsNullOrWhiteSpace(request.RefundReference))
            return ServiceResult<bool>.Failure("invalid_payment_resolution");
        var lots = request.Lots.Select(x => (x.LotId, x.GoodUnits, x.DamagedUnits)).ToArray();
        var result = await repository.InspectRejectedAsync(dispatchOrderId, lots, request.Notes.Trim(),
            request.PaymentResolution, request.RefundReference,
            inspectorId, clock.GetUtcNow(), cancellationToken);
        return result switch
        {
            DispatchOperationCode.Applied => ServiceResult<bool>.Success(true),
            DispatchOperationCode.NotFound => ServiceResult<bool>.Failure("dispatch_order_not_found"),
            DispatchOperationCode.InvalidInspection => ServiceResult<bool>.Failure("invalid_inspection_quantities"),
            _ => ServiceResult<bool>.Failure("inspection_not_allowed")
        };
    }

    public async Task<ServiceResult<DispatchResponse>> CreateAsync(CreateDispatchRequest request, Guid leadId,
        CancellationToken cancellationToken)
    {
        var ids = request.OrderIds.Distinct().ToArray();
        if (leadId == Guid.Empty || request.DriverStaffId == Guid.Empty || string.IsNullOrWhiteSpace(request.VehicleIdentifier) ||
            ids.Length == 0 || ids.Length != request.OrderIds.Count)
            return ServiceResult<DispatchResponse>.Failure("invalid_dispatch");
        var result = await repository.CreateAsync(request.VehicleIdentifier, leadId, request.DriverStaffId,
            ids, clock.GetUtcNow(), cancellationToken);
        if (result.Code != DispatchOperationCode.Applied || result.Dispatch is null)
            return ServiceResult<DispatchResponse>.Failure(result.Code switch
            {
                DispatchOperationCode.InvalidDriver => "driver_not_found_or_not_authorized",
                DispatchOperationCode.NotFound => "order_not_found",
                DispatchOperationCode.InvalidOrderState => "order_not_ready_or_stock_changed",
                _ => "dispatch_failed"
            });
        var dispatch = result.Dispatch;
        return ServiceResult<DispatchResponse>.Success(new(dispatch.Id, dispatch.DispatchNumber,
            dispatch.VehicleIdentifier, dispatch.DriverStaffId, dispatch.DepartedAtUtc,
            dispatch.Orders.Select(x => new DispatchOrderReference(x.Id, x.OrderId)).ToList()));
    }

    public async Task<ServiceResult<bool>> RecordResultAsync(Guid dispatchOrderId,
        RecordDeliveryResultRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        byte[]? signature = null;
        if (request.SignatureBase64 is not null)
        {
            try { signature = Convert.FromBase64String(request.SignatureBase64); }
            catch (FormatException) { return ServiceResult<bool>.Failure("invalid_signature"); }
            if (signature.Length is 0 or > 300_000 || request.SignatureMimeType is not ("image/png" or "image/jpeg"))
                return ServiceResult<bool>.Failure("invalid_signature");
        }
        if (request.Result is not ("DELIVERED" or "NOT_FOUND" or "REJECTED" or "CANCELLED") ||
            (request.Result is "DELIVERED" or "REJECTED" or "CANCELLED") && signature is null ||
            request.Result == "DELIVERED" && string.IsNullOrWhiteSpace(request.RecipientName) ||
            request.Result is "NOT_FOUND" or "REJECTED" or "CANCELLED" && string.IsNullOrWhiteSpace(request.Reason) ||
            request.Result != "CANCELLED" && request.CancellationFeeAmount != 0 ||
            request.CancellationFeeAmount < 0)
            return ServiceResult<bool>.Failure("delivery_result_requires_recipient_or_signature_and_reason");

        var result = await repository.RecordResultAsync(dispatchOrderId, request.Result,
            request.RecipientName, request.RecipientRelationship, request.Reason, signature,
            request.SignatureMimeType, request.CancellationFeeAmount, actorId, clock.GetUtcNow(), cancellationToken);
        return result.Code switch
        {
            DispatchOperationCode.Applied => ServiceResult<bool>.Success(true),
            DispatchOperationCode.NotFound => ServiceResult<bool>.Failure("dispatch_order_not_found"),
            DispatchOperationCode.AlreadyRecorded => ServiceResult<bool>.Failure("delivery_result_already_recorded"),
            DispatchOperationCode.InvalidDriver => ServiceResult<bool>.Failure("delivery_result_actor_not_assigned"),
            _ => ServiceResult<bool>.Failure("invalid_delivery_result")
        };
    }
}
