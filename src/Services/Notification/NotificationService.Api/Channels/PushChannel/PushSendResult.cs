namespace Urfu.Link.Services.Notification.Channels.PushChannel;

public enum PushSendOutcome
{
    Success = 0,
    TokenInvalid = 1,
    RetryLater = 2,
    PermanentFailure = 3,
}

public sealed record PushSendResult(PushSendOutcome Outcome, string? ProviderMessageId, string? Error)
{
    public static PushSendResult Success(string providerMessageId)
        => new(PushSendOutcome.Success, providerMessageId, null);

    public static PushSendResult TokenInvalid(string error)
        => new(PushSendOutcome.TokenInvalid, null, error);

    public static PushSendResult RetryLater(string error)
        => new(PushSendOutcome.RetryLater, null, error);

    public static PushSendResult Failed(string error)
        => new(PushSendOutcome.PermanentFailure, null, error);
}

public sealed record PushPayload(
    string Token,
    string Title,
    string Body,
    string? ImageUrl,
    string? DeepLink,
    string? GroupKey,
    IReadOnlyDictionary<string, string> Data);
