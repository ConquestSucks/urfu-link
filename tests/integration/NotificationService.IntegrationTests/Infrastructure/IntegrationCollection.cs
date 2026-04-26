namespace NotificationService.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<NotificationServiceFactory>
{
    public const string Name = "Notification integration";
}
