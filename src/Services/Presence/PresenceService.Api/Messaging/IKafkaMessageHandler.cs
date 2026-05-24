using System.Text.Json;

namespace Urfu.Link.Services.Presence.Messaging;

public interface IKafkaMessageHandler
{
    /// <summary>
    /// Handles a single integration event payload. Implementations are expected
    /// to filter by <paramref name="eventType"/> and ignore unknown values.
    /// </summary>
    Task HandleAsync(string eventType, JsonElement payload, CancellationToken cancellationToken);
}
