using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;
}
