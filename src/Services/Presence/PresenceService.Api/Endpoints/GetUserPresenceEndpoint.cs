using FastEndpoints;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Application.Contracts.Responses;
using Urfu.Link.Services.Presence.Application.Privacy;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace Urfu.Link.Services.Presence.Endpoints;

public sealed class GetUserPresenceRequest
{
    public Guid UserId { get; set; }
}

public sealed class GetUserPresenceEndpoint(
    IPresenceSessionStore sessions,
    ILastSeenRepository lastSeen,
    IPrivacyProjectionStore privacy,
    PresenceAggregator aggregator)
    : Endpoint<GetUserPresenceRequest, PresenceInfoResponse>
{
    public override void Configure()
    {
        Get("users/{userId}");
        Group<PresenceGroup>();
        Summary(s => s.Summary = "Return aggregated presence with privacy filter applied.");
    }

    public override async Task HandleAsync(GetUserPresenceRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var userSessions = await sessions.GetSessionsAsync(req.UserId, ct).ConfigureAwait(false);
        var ls = await lastSeen.GetAsync(req.UserId, ct).ConfigureAwait(false);
        var aggregated = aggregator.Aggregate(req.UserId, userSessions, ls?.LastSeenAt);
        var settings = await privacy.GetAsync(req.UserId, ct).ConfigureAwait(false);
        var publicView = PrivacyFilter.Apply(aggregated, settings);

        await Send.OkAsync(PresenceInfoResponse.From(publicView), ct).ConfigureAwait(false);
    }
}
