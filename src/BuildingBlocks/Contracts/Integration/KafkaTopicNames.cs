namespace Urfu.Link.BuildingBlocks.Contracts.Integration;

public static class KafkaTopicNames
{
    public const string UserEvents = "urfu.user.events.v1";
    public const string MediaEvents = "urfu.media.events.v1";
    public const string ChatEvents = "urfu.chat.events.v1";
    public const string PresenceEvents = "urfu.presence.events.v1";
    public const string NotificationEvents = "urfu.notification.events.v1";
    public const string CallEvents = "urfu.call.events.v1";

    public const string DlqSuffix = ".dlq";
}
