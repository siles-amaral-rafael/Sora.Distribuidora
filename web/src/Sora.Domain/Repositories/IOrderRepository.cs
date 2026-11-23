using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public sealed record OrderLineInput(Guid ProductId, Guid? PresentationId, decimal Quantity);
public enum CreateOrderResultCode { Created, PendingConfirmation, CustomerNotFound, ProductUnavailable, InvalidPresentation, InsufficientStock, SaleBelowCost, OutstandingBalance }
public sealed record CreateOrderResult(CreateOrderResultCode Code, SalesOrder? Order = null);
public enum PrepareOrderResultCode { Prepared, NotFound, InvalidState, PaymentRequired }
public enum OrderTransitionCode { Applied, NotFound, InvalidState, SaleBelowCost, PaymentResolutionRequired }
public enum CancellationResolutionKind { NONE, REFUND, CREDIT }
public enum OrderAvailabilityResultCode { Confirmed, Cancelled, NotFound, InvalidState, InsufficientStock, ProductUnavailable, InvalidPresentation, SaleBelowCost }
public enum PaymentOperationCode { Recorded, NotFound, InvalidState, InvalidAmount, PaymentRequired, Overpayment, DuplicateReference }

public interface IOrderRepository
{
    Task<CreateOrderResult> CreateAndReserveAsync(Guid customerId, Guid actorId, string fulfillment,
        bool isNightOrder, DateOnly? scheduledDeliveryDate, IReadOnlyList<OrderLineInput> lines,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<SalesOrder?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesOrder>> ListAsync(CancellationToken cancellationToken);
    Task<PrepareOrderResultCode> MarkPreparedAsync(Guid orderId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OrderTransitionCode> UpdateAsync(Guid orderId, string fulfillment, IReadOnlyList<OrderLineInput> lines,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OrderAvailabilityResultCode> ConfirmAvailabilityAsync(Guid orderId, bool accepted, string note,
        IReadOnlyList<OrderLineInput> lines, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OrderTransitionCode> CompletePickupAsync(Guid orderId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OrderTransitionCode> CancelAsync(Guid orderId, CancellationResolutionKind resolution, string? reference, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<PaymentOperationCode> RecordPaymentAsync(Guid orderId, decimal amount, string reference, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}
