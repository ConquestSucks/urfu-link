using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace ContractTests;

public sealed class KafkaContractTests
{
    [Fact]
    public void TopicNamesShouldFollowUrfuPrefix()
    {
        var topics = new[]
        {
            KafkaTopicNames.UserEvents,
            KafkaTopicNames.MediaEvents,
            KafkaTopicNames.ChatEvents,
            KafkaTopicNames.PresenceEvents,
            KafkaTopicNames.NotificationEvents,
            KafkaTopicNames.CallEvents,
        };

        Assert.All(topics, topic => Assert.StartsWith("urfu.", topic, StringComparison.Ordinal));
        Assert.All(topics, topic => Assert.EndsWith(".v1", topic, StringComparison.Ordinal));
    }
}
