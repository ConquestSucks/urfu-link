using System.Diagnostics.CodeAnalysis;

namespace ChatService.IntegrationTests.Infrastructure;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Required xUnit collection-definition naming convention.")]
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ChatServiceFactory>
{
    public const string Name = "ChatService Integration";
}
