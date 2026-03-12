namespace Urfu.Link.Services.Presence.Domain;

public sealed record ServiceProfile(
    string ServiceName,
    string Datastore,
    string TopicName,
    string EventType);

