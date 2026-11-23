using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public enum DispatchOperationCode { Applied, NotFound, InvalidOrderState, InvalidDriver, InvalidSignature, AlreadyRecorded, InvalidInspection }
public sealed record DispatchOperationResult(DispatchOperationCode Code, Dispatch? Dispatch = null);

public interface IDispatchRepository
{
    Task<IReadOnlyList<DispatchOrder>> ListPendingInspectionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DispatchOrder>> ListAssignedOpenDeliveriesAsync(Guid? driverId, CancellationToken cancellationToken);
    Task<DispatchOperationCode> InspectRejectedAsync(Guid dispatchOrderId, IReadOnlyList<(Guid LotId, int GoodUnits, int DamagedUnits)> lots,
        string notes, string paymentResolution, string? refundReference, Guid inspectorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DispatchOperationResult> CreateAsync(string vehicle, Guid leadId, Guid driverId,
        IReadOnlyList<Guid> orderIds, DateTimeOffset now, CancellationToken cancellationToken);
    Task<DispatchOperationResult> RecordResultAsync(Guid dispatchOrderId, string result,
        string? recipientName, string? relationship, string? reason, byte[]? signature,
        string? mimeType, decimal cancellationFeeAmount, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}
