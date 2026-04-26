namespace DisciplineService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<DisciplineServiceFactory>
{
    public const string Name = "DisciplineService Integration";
}
