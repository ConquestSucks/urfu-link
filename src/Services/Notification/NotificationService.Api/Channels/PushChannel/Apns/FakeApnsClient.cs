using Microsoft.Extensions.Logging;

namespace Urfu.Link.Services.Notification.Channels.PushChannel.Apns;

public sealed class FakeApnsClient(ILogger<FakeApnsClient> logger) : IApnsClient
{
    private readonly ILogger<FakeApnsClient> _logger = logger;

    public Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = cancellationToken;

        _logger.LogInformation(
            "[FakeAPNs] token={Token} title={Title} body={Body} groupKey={GroupKey}",
            payload.Token,
            payload.Title,
            payload.Body,
            payload.GroupKey);

        return Task.FromResult(PushSendResult.Success($"fake-apns:{Guid.NewGuid():N}"));
    }
}
