namespace Urfu.Link.Services.Notification.Channels.PushChannel.Apns;

public interface IApnsClient
{
    Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken);
}

public sealed class ApnsOptions
{
    public const string SectionName = "Notification:Push:Apns";

    public string P8KeyPath { get; set; } = string.Empty;

    public string KeyId { get; set; } = string.Empty;

    public string TeamId { get; set; } = string.Empty;

    public string BundleId { get; set; } = string.Empty;

    public bool IsProduction { get; set; }
}
