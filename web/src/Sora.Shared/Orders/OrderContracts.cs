using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Orders;

public sealed class CreateOrderRequest
{
    [Required] public Guid CustomerId { get; set; }
    [Required, RegularExpression("DELIVERY|PICKUP_NOW|PICKUP_LATER")] public string Fulfillment { get; set; } = "DELIVERY";
    public bool IsNightOrder { get; set; }
    [Required] public List<CreateOrderLineRequest> Lines { get; set; } = [];
}

public sealed class CreateOrderLineRequest
{
    [Required] public Guid ProductId { get; set; }
    public Guid? PresentationId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999")] public decimal Quantity { get; set; }
}

public sealed class UpdateOrderRequest
{
    [Required, RegularExpression("DELIVERY|PICKUP_NOW|PICKUP_LATER")]
    public string Fulfillment { get; set; } = "DELIVERY";
    [Required, MinLength(1)] public List<CreateOrderLineRequest> Lines { get; set; } = [];
}

public sealed class ConfirmOrderAvailabilityRequest
{
    public bool Accepted { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string ConfirmationNote { get; set; } = string.Empty;
    public List<CreateOrderLineRequest> Lines { get; set; } = [];
}

public sealed class RecordPaymentRequest
{
    [Range(typeof(decimal), "0.01", "999999999")] public decimal Amount { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string Reference { get; set; } = string.Empty;
}

public sealed class CancelOrderRequest
{
    [Required, RegularExpression("NONE|REFUND|CREDIT")] public string Resolution { get; set; } = "NONE";
    [StringLength(100)] public string? Reference { get; set; }
}

public sealed record OrderResponse(Guid Id, string OrderNumber, Guid CustomerId, string Status,
    string Fulfillment, decimal TotalAmount, DateTimeOffset CreatedAtUtc, IReadOnlyList<OrderLineResponse> Lines,
    IReadOnlyList<OrderHistoryResponse> History, decimal PaidAmount, decimal BalanceDue,
    IReadOnlyList<OrderPaymentResponse> Payments, bool CanPickup, bool IsNightOrder,
    DateOnly? ScheduledDeliveryDate);
public sealed record OrderPaymentResponse(decimal Amount, string Reference, DateTimeOffset RecordedAtUtc, string RecordedBy, string Kind);
public sealed record OrderLineResponse(Guid ProductId, string ProductName, string? Presentation,
    decimal Quantity, int QuantityUnits, decimal UnitPrice, decimal LineTotal,
    decimal SubtotalAmount, decimal DiscountAmount, int UnitsAvailableAtCreation, int ShortageUnits,
    IReadOnlyList<OrderLotAllocationResponse> Lots);
public sealed record OrderLotAllocationResponse(string LotCode, DateOnly? ExpiryDate, int QuantityReserved);
public sealed record OrderHistoryResponse(string EventType, DateTimeOffset OccurredAtUtc,
    string ActorName, string Details);
