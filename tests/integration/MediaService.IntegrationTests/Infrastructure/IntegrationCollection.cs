using System.Diagnostics.CodeAnalysis;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// All integration tests share a single <see cref="MediaServiceFactory"/> instance
/// (and therefore one PostgreSQL + MinIO container pair) for the whole run. Each
/// test class TRUNCATEs the media schema in its <c>InitializeAsync</c> hook to
/// stay isolated. Serial execution inside the collection also keeps the static
/// <see cref="TestAuthHandler.CurrentPrincipal"/> race-free.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Required xUnit collection-definition naming convention.")]
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<MediaServiceFactory>
{
    public const string Name = "MediaService Integration";
}
