using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Customers;

public sealed class SaveCustomerRequest
{
    [Required, RegularExpression("STORE|FAMILY|INSTITUTION")]
    public string CustomerType { get; set; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(40, MinimumLength = 5)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(500, MinimumLength = 4)]
    public string PrimaryAddress { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal? Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal? Longitude { get; set; }
}

public sealed record CustomerResponse(
    Guid Id,
    string CustomerType,
    string FullName,
    string Phone,
    string PrimaryAddress,
    decimal? Latitude,
    decimal? Longitude,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
