using Sora.Shared.Orders;

namespace Sora.Application.Orders;

public interface IOrderService
{
    Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<OrderResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ServiceResult<bool>> MarkPreparedAsync(Guid orderId, Guid actorId, CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> UpdateAsync(Guid orderId, UpdateOrderRequest request, Guid actorId,
        CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> ConfirmAvailabilityAsync(Guid orderId,
        ConfirmOrderAvailabilityRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> CompletePickupAsync(Guid orderId, Guid actorId, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> CancelAsync(Guid orderId, CancelOrderRequest request, Guid actorId, CancellationToken cancellationToken);
    Task<ServiceResult<OrderResponse>> RecordPaymentAsync(Guid orderId, RecordPaymentRequest request, Guid actorId, CancellationToken cancellationToken);
}
