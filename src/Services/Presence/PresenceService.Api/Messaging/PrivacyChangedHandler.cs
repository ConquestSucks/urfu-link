using System.Text.Json;
using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Messaging;

public sealed class PrivacyChangedHandler(
    IPrivacyProjectionStore store,
    ILogger<PrivacyChangedHandler> logger) : IKafkaMessageHandler
{
    public const string SubscribedEventType = "user.privacy.changed.v1";

    public async Task HandleAsync(string eventType, JsonElement payload, CancellationToken cancellationToken)
    {
        if (!string.Equals(eventType, SubscribedEventType, StringComparison.Ordinal))
        {
            return;
        }

        if (!payload.TryGetProperty("UserId", out var userIdElement)
            || !payload.TryGetProperty("ShowLastVisitTime", out var showLastVisitElement))
        {
            logger.LogWarning("Skipping {EventType}: payload is missing required fields", eventType);
            return;
        }

        var userId = userIdElement.GetGuid();
        var settings = new PrivacySettings(ShowLastVisitTime: showLastVisitElement.GetBoolean());

        await store.SetAsync(userId, settings, cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Updated presence privacy projection for user {UserId}: ShowLastVisit={ShowLastVisit}",
                userId, settings.ShowLastVisitTime);
        }
    }
}
