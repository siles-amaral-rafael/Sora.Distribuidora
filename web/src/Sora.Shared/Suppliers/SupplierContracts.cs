using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Suppliers;

public sealed class SaveSupplierRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(40, MinimumLength = 5)]
    public string Phone { get; set; } = string.Empty;
}

public sealed class ReplaceSupplierProductsRequest
{
    public List<Guid> ProductIds { get; set; } = [];
}

public sealed record SupplierProductResponse(Guid ProductId, string InternalCode, string ProductName);
public sealed record SupplierResponse(Guid Id, string Name, string Phone, bool IsActive,
    IReadOnlyList<SupplierProductResponse> Products);
