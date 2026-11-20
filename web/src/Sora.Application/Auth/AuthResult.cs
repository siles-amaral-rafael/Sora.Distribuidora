namespace Sora.Application.Auth;

public sealed record AuthResult(bool Succeeded, string? ErrorCode, IssuedToken? Token)
{
    public static AuthResult Success(IssuedToken token) => new(true, null, token);
    public static AuthResult Failure(string errorCode) => new(false, errorCode, null);
}
