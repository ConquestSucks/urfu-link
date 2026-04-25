using FastEndpoints;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Application.Contracts.Requests;
using Urfu.Link.Services.Presence.Application.Contracts.Responses;
using Urfu.Link.Services.Presence.Application.Privacy;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace Urfu.Link.Services.Presence.Endpoints;

public sealed class BatchPresenceResponse
{
    public PresenceInfoResponse[] Items { get; set; } = [];
}

public sealed class BatchPresenceEndpoint(
    IPresenceSessionStore sessions,
    ILastSeenRepository lastSeen,
    IPrivacyProjectionStore privacy,
    PresenceAggregator aggregator)
    : Endpoint<BatchPresenceRequest, BatchPresenceResponse>
{
    public override void Configure()
    {
        Post("users/batch");
        Group<PresenceGroup>();
        Summary(s => s.Summary = "Return aggregated presence for many users (privacy applied).");
    }

    public override async Task HandleAsync(BatchPresenceRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var distinct = req.UserIds.Distinct().ToArray();
        var items = new List<PresenceInfoResponse>(distinct.Length);
        foreach (var userId in distinct)
        {
            var userSessions = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
            var ls = await lastSeen.GetAsync(userId, ct).ConfigureAwait(false);
            var aggregated = aggregator.Aggregate(userId, userSessions, ls?.LastSeenAt);
            var settings = await privacy.GetAsync(userId, ct).ConfigureAwait(false);
            var publicView = PrivacyFilter.Apply(aggregated, settings);
            items.Add(PresenceInfoResponse.From(publicView));
        }

        await Send.OkAsync(new BatchPresenceResponse { Items = items.ToArray() }, ct).ConfigureAwait(false);
    }
}
