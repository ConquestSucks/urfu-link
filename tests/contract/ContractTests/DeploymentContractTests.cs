namespace ContractTests;

public sealed class DeploymentContractTests
{
    [Fact]
    public void HelmValuesShouldAlignWithPromotedImageTags()
    {
        var prodValues = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "deploy", "helm", "services", "user-service", "values-prod.yaml")));
        var devValues = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "deploy", "helm", "services", "user-service", "values-dev.yaml")));

        Assert.Matches(@"tag:\s+sha-[0-9a-f]{7}", prodValues);
        Assert.Contains("tag: dev-local", devValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", prodValues, StringComparison.Ordinal);
        Assert.Contains("secrets:", devValues, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalKubernetesOverlayShouldDefineClusterDependencies()
    {
        var overlay = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "platform", "dev", "local-k8s", "kustomization.yaml")));
        var dependencies = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "platform", "dev", "local-k8s", "dependencies.yaml")));

        Assert.Contains("dependencies.yaml", overlay, StringComparison.Ordinal);
        Assert.Contains("kind: Deployment", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: kafka", dependencies, StringComparison.Ordinal);
        Assert.Contains("name: keycloak", dependencies, StringComparison.Ordinal);
    }
}
