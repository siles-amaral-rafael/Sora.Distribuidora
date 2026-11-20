using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Auth;

public sealed class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;
}
