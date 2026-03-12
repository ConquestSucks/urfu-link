namespace Urfu.Link.Services.Chat.Domain;

public sealed record ServiceProfile(
    string ServiceName,
    string Datastore,
    string TopicName,
    string EventType);

