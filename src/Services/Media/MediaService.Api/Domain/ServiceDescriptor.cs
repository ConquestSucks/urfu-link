namespace Urfu.Link.Services.Media.Domain;

public sealed record ServiceProfile(
    string ServiceName,
    string Datastore,
    string TopicName,
    string EventType);

