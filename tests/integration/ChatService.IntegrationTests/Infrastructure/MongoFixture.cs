using Microsoft.Extensions.Options;
using Testcontainers.MongoDb;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots a single MongoDB container for the test class and exposes a fresh
/// <see cref="ChatMongoContext"/> bound to a unique database per test.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:8.0.5")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public ChatMongoContext CreateContext(string? databaseName = null)
    {
        var options = Options.Create(new ChatMongoOptions
        {
            ConnectionString = ConnectionString,
            DatabaseName = databaseName ?? $"chat_test_{Guid.NewGuid():N}",
        });
        return new ChatMongoContext(options);
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
