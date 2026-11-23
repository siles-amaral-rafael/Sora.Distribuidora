namespace Sora.Application;

public sealed record ServiceResult<T>(bool Succeeded, T? Value, string? ErrorCode)
{
    public static ServiceResult<T> Success(T value) => new(true, value, null);
    public static ServiceResult<T> Failure(string errorCode) => new(false, default, errorCode);
}
