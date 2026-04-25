namespace Urfu.Link.Services.Presence.Application.Contracts.Requests;

public sealed class BatchPresenceRequest
{
    public const int MaxUserIds = 100;

    public Guid[] UserIds { get; set; } = [];
}
