using System.Diagnostics.CodeAnalysis;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// All integration tests share the static <see cref="TestAuthHandler.CurrentPrincipal"/>,
/// so they must run serially within a single xUnit collection. Without this, parallel
/// execution races on the shared principal and produces flaky 401/500 responses.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Required xUnit collection-definition naming convention.")]
[CollectionDefinition(Name)]
public sealed class IntegrationCollection
{
    public const string Name = "MediaService Integration";
}
