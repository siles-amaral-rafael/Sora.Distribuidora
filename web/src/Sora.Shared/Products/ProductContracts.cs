using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Products;

public sealed class SaveProductRequest
{
    [StringLength(60)]
    public string InternalCode { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Barcode { get; set; }

    [Required, StringLength(180, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0.001", "999999999")]
    public decimal? NetContent { get; set; }

    [StringLength(20)]
    public string? ContentUnit { get; set; }

    [Required, StringLength(30, MinimumLength = 1)]
    public string BaseUnit { get; set; } = "unidad";

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal UnitSalePrice { get; set; }
}

public sealed class CreateProductsRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public List<SaveProductRequest> Products { get; set; } = [];
}

public sealed class SaveProductPresentationRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int UnitsPerPresentation { get; set; }
}

public sealed class ReplaceProductDiscountRulesRequest
{
    public List<SaveProductDiscountRuleRequest> Rules { get; set; } = [];
}

public sealed class SaveProductDiscountRuleRequest
{
    [Range(1, int.MaxValue)] public int MinimumQuantityUnits { get; set; }
    [Range(typeof(decimal), "0.01", "100")] public decimal DiscountPercent { get; set; }
}

public sealed record ProductPresentationResponse(
    Guid Id,
    string Name,
    int UnitsPerPresentation,
    bool IsActive);

public sealed record ProductDiscountRuleResponse(int MinimumQuantityUnits, decimal DiscountPercent);

public sealed record ProductResponse(
    Guid Id,
    string InternalCode,
    string? Barcode,
    string Name,
    string? Brand,
    string? Category,
    decimal? NetContent,
    string? ContentUnit,
    string BaseUnit,
    decimal UnitSalePrice,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ProductPresentationResponse> Presentations,
    IReadOnlyList<ProductDiscountRuleResponse> DiscountRules);
