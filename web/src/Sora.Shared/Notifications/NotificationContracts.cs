namespace Sora.Shared.Notifications;

public sealed record NotificationOutboxResponse(Guid Id, Guid OrderId, string OrderNumber,
    string CustomerName, string RecipientPhone, string EventType, string Message, string State,
    string Mode, int Attempts, int RetryCount, string? LastError, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ProcessedAtUtc, DateTimeOffset? LastRetryAtUtc);
