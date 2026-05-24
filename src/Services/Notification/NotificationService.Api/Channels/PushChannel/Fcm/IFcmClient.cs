namespace Urfu.Link.Services.Notification.Channels.PushChannel.Fcm;

public interface IFcmClient
{
    Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken);
}

public sealed class FcmOptions
{
    public const string SectionName = "Notification:Push:Fcm";

    public string ServiceAccountPath { get; set; } = string.Empty;

    public string? ProjectId { get; set; }
}
