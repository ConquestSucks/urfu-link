using Microsoft.Extensions.Logging;

namespace Urfu.Link.Services.Notification.Channels.PushChannel.Fcm;

/// <summary>
/// Logs the payload to stdout and returns a synthetic provider message id. Used in
/// dev/test environments where no Firebase project / device is available.
/// </summary>
public sealed class FakeFcmClient(ILogger<FakeFcmClient> logger) : IFcmClient
{
    private readonly ILogger<FakeFcmClient> _logger = logger;

    public Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = cancellationToken;

        _logger.LogInformation(
            "[FakeFCM] token={Token} title={Title} body={Body} groupKey={GroupKey}",
            payload.Token,
            payload.Title,
            payload.Body,
            payload.GroupKey);

        return Task.FromResult(PushSendResult.Success($"fake-fcm:{Guid.NewGuid():N}"));
    }
}
