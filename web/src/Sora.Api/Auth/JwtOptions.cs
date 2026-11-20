using System.ComponentModel.DataAnnotations;

namespace Sora.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(1)]
    public string Issuer { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string Audience { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 120)]
    public int ExpiresMinutes { get; init; } = 30;
}
