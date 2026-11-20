namespace Sora.Shared.Auth;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
