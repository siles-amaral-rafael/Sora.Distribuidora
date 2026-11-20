namespace Sora.Application.Auth;

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAtUtc);
