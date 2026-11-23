using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Dispatches;

public sealed class CreateDispatchRequest
{
    [Required, StringLength(80, MinimumLength = 1)] public string VehicleIdentifier { get; set; } = string.Empty;
    [Required] public Guid DriverStaffId { get; set; }
    [Required] public List<Guid> OrderIds { get; set; } = [];
}

public sealed class RecordDeliveryResultRequest
{
    [Required, RegularExpression("DELIVERED|NOT_FOUND|REJECTED|CANCELLED")] public string Result { get; set; } = string.Empty;
    [StringLength(160)] public string? RecipientName { get; set; }
    [StringLength(80)] public string? RecipientRelationship { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    [StringLength(400000)] public string? SignatureBase64 { get; set; }
    [StringLength(80)] public string? SignatureMimeType { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal CancellationFeeAmount { get; set; }
}

public sealed class InspectRejectedDeliveryRequest
{
    [Required, StringLength(500, MinimumLength = 3)] public string Notes { get; set; } = string.Empty;
    [Required, RegularExpression("NONE|REFUND|CREDIT")] public string PaymentResolution { get; set; } = "NONE";
    [StringLength(100)] public string? RefundReference { get; set; }
    [Required, MinLength(1)] public List<InspectedLotRequest> Lots { get; set; } = [];
}

public sealed class InspectedLotRequest
{
    [Required] public Guid LotId { get; set; }
    [Range(0, int.MaxValue)] public int GoodUnits { get; set; }
    [Range(0, int.MaxValue)] public int DamagedUnits { get; set; }
}

public sealed record DispatchResponse(Guid Id, string DispatchNumber, string VehicleIdentifier,
    Guid DriverStaffId, DateTimeOffset DepartedAtUtc, IReadOnlyList<DispatchOrderReference> Orders);
public sealed record DispatchOrderReference(Guid DispatchOrderId, Guid OrderId);
public sealed record PendingInspectionResponse(Guid DispatchOrderId, Guid OrderId, string OrderNumber,
    IReadOnlyList<PendingInspectionLotResponse> Lots, string Result, decimal CancellationFeeAmount,
    decimal PaidAmount, decimal BalanceDue);
public sealed record PendingInspectionLotResponse(Guid LotId, string LotCode, int PickedUnits);
public sealed record AssignedDeliveryResponse(Guid DispatchOrderId, Guid OrderId, string OrderNumber,
    string DispatchNumber, string VehicleIdentifier, DateTimeOffset DepartedAtUtc, string CustomerName,
    string DeliveryAddress, decimal? Latitude, decimal? Longitude);
