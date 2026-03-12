namespace Urfu.Link.Services.Call.Domain;

public sealed record ServiceProfile(
    string ServiceName,
    string Datastore,
    string TopicName,
    string EventType);

