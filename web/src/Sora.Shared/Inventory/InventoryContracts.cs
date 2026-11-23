using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Inventory;

public sealed class CreatePurchaseReceiptRequest
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTimeOffset? ReceivedAtUtc { get; set; }

    [Required, MinLength(1)]
    public List<PurchaseReceiptLineRequest> Lines { get; set; } = [];
}

public sealed class PurchaseReceiptLineRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Required, StringLength(80, MinimumLength = 1)]
    public string LotCode { get; set; } = string.Empty;

    public DateOnly? ExpiryDate { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal UnitPurchaseCost { get; set; }
}

public sealed record PurchaseReceiptResponse(
    Guid Id,
    Guid SupplierId,
    string InvoiceNumber,
    DateTimeOffset ReceivedAtUtc,
    int LotCount,
    int TotalUnits);

public sealed record InventoryStockLotResponse(
    Guid LotId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string LotCode,
    DateOnly? ExpiryDate,
    int OnHandUnits,
    int ReservedUnits,
    int AvailableUnits,
    decimal UnitPurchaseCost);

public sealed class AdjustStockRequest
{
    [Required]
    public Guid LotId { get; set; }

    [Range(typeof(int), "-2147483647", "2147483647")]
    public int QuantityDelta { get; set; }

    [Required, RegularExpression("ADJUSTMENT|LOSS|DAMAGE|EXPIRY")]
    public string MovementType { get; set; } = "ADJUSTMENT";

    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}
