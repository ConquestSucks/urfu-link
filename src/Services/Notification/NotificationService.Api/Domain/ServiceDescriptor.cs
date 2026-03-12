namespace Urfu.Link.Services.Notification.Domain;

public sealed record ServiceProfile(
    string ServiceName,
    string Datastore,
    string TopicName,
    string EventType);

