using Sora.Shared.Dispatches;

namespace Sora.Application.Dispatches;

public interface IDispatchService
{
    Task<ServiceResult<DispatchResponse>> CreateAsync(CreateDispatchRequest request, Guid leadId, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> RecordResultAsync(Guid dispatchOrderId, RecordDeliveryResultRequest request,
        Guid actorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingInspectionResponse>> ListPendingInspectionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AssignedDeliveryResponse>> ListAssignedOpenDeliveriesAsync(Guid? driverId, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> InspectRejectedAsync(Guid dispatchOrderId, InspectRejectedDeliveryRequest request,
        Guid inspectorId, CancellationToken cancellationToken);
}
