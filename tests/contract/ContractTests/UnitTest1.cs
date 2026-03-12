using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace ContractTests;

public class UnitTest1
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

    [Fact]
    public void HelmValuesShouldAlignWithPromotedImageTags()
    {
        var prodValues = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "deploy", "helm", "services", "user-service", "values-prod.yaml")));
        var devValues = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "deploy", "helm", "services", "user-service", "values-dev.yaml")));

        Assert.Contains("tag: stable", prodValues, StringComparison.Ordinal);
        Assert.Contains("tag: dev-local", devValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", prodValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", devValues, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalKubernetesOverlayShouldDefineClusterDependencies()
    {
        var overlay = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "platform", "dev", "local-k8s", "kustomization.yaml")));
        var dependencies = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "platform", "dev", "local-k8s", "dependencies.yaml")));

        Assert.Contains("dependencies.yaml", overlay, StringComparison.Ordinal);
        Assert.Contains("kind: Deployment", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: kafka", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: keycloak", dependencies, StringComparison.Ordinal);
    }
}
