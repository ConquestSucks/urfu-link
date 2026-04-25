using System.Diagnostics.CodeAnalysis;

namespace PresenceService.IntegrationTests.Infrastructure;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Required xUnit collection-definition naming convention.")]
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<PresenceServiceFactory>
{
    public const string Name = "PresenceService Integration";
}
