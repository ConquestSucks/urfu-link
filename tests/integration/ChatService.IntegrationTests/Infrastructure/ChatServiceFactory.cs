using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Application.Presence;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;
using Urfu.Link.Services.Chat.Realtime;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Boots ChatService.Api against a real MongoDB container. Background workers are stripped
/// (Kafka consumer, outbox publisher) so tests don't need a Kafka broker; outbox writer is
/// replaced with a capturing fake. Mongo indexes are created against the container directly,
/// reproducing what <see cref="MongoIndexInitializer"/> does on real startup.
/// </summary>
public sealed class ChatServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:8.0.5")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.4.2-alpine")
        .Build();

    public FakeOutboxWriter OutboxWriter { get; } = new();

    public FakeMediaServiceClient MediaServiceClient { get; } = new();

    public FakeDisciplineRoleResolver DisciplineRoleResolver { get; } = new();

    public FakeChatBroadcaster ChatBroadcaster { get; } = new();

    public FakeDisciplineServiceClient DisciplineServiceClient { get; } = new();

    public FakePresenceServiceClient PresenceServiceClient { get; } = new();

    public IIdempotencyStore IdempotencyStore { get; } = Substitute.For<IIdempotencyStore>();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public string RedisConnectionString => $"{_redis.GetConnectionString()},allowAdmin=true";

    public string DatabaseName { get; } = $"chat_test_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _redis.StartAsync());
        await EnsureIndexesAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _redis.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    public void ResetCapturedState()
    {
        OutboxWriter.Clear();
        MediaServiceClient.Reset();
        DisciplineRoleResolver.Reset();
        ChatBroadcaster.Reset();
        DisciplineServiceClient.Reset();
        PresenceServiceClient.Reset();
        TestAuthHandler.CurrentPrincipal = null;
    }

    public async Task ResetDataAsync()
    {
        var ctx = Services.GetRequiredService<ChatMongoContext>();
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.ConversationsCollectionName)
            .DeleteManyAsync(MongoDB.Driver.FilterDefinition<dynamic>.Empty);
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.MessagesCollectionName)
            .DeleteManyAsync(MongoDB.Driver.FilterDefinition<dynamic>.Empty);
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.ThreadSubscriptionsCollectionName)
            .DeleteManyAsync(MongoDB.Driver.FilterDefinition<dynamic>.Empty);

        var multiplexer = Services.GetRequiredService<IConnectionMultiplexer>();
        var endpoints = multiplexer.GetEndPoints();
        if (endpoints.Length > 0)
        {
            var server = multiplexer.GetServer(endpoints[0]);
            await server.FlushDatabaseAsync();
        }

        ResetCapturedState();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:9999/realms/test",
                ["Observability:Otlp:Endpoint"] = "http://localhost:9999",
                ["ConnectionStrings:Primary"] = MongoConnectionString,
                ["ChatMongo:DatabaseName"] = DatabaseName,
                ["Infrastructure:Redis:Configuration"] = RedisConnectionString,
                ["Kafka:BootstrapServers"] = "localhost:9999",
                ["Outbox:RedisConfiguration"] = RedisConnectionString,
                ["Idempotency:RedisConfiguration"] = RedisConnectionString,
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Drop all background workers (Kafka consumer, outbox publisher, MongoIndexInitializer
            // — indexes are created up-front in InitializeAsync).
            services.RemoveAll<IHostedService>();

            IdempotencyStore.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(true));
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton(IdempotencyStore);

            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            services.RemoveAll<IMediaServiceClient>();
            services.AddSingleton<IMediaServiceClient>(MediaServiceClient);

            services.RemoveAll<IDisciplineRoleResolver>();
            services.AddSingleton<IDisciplineRoleResolver>(DisciplineRoleResolver);

            services.RemoveAll<IDisciplineServiceClient>();
            services.AddSingleton<IDisciplineServiceClient>(DisciplineServiceClient);
            // Drop the gRPC channel + InternalApiClient registration so it doesn't try to dial
            // the production discipline-service address during tests.
            services.RemoveAll<Urfu.Link.Services.Disciplines.Grpc.InternalApi.InternalApiClient>();

            services.RemoveAll<IPresenceServiceClient>();
            services.AddSingleton<IPresenceServiceClient>(PresenceServiceClient);

            ReplaceAuthWithTestScheme(services);
        });
    }

    private async Task EnsureIndexesAsync()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ChatMongoOptions
        {
            ConnectionString = MongoConnectionString,
            DatabaseName = DatabaseName,
        });
        using var ctx = new ChatMongoContext(options);
        var initializer = new MongoIndexInitializer(ctx);
        await initializer.StartAsync(CancellationToken.None);
    }

    private static void ReplaceAuthWithTestScheme(IServiceCollection services)
    {
        var authDescriptors = services
            .Where(d => d.ServiceType.FullName?.Contains("AuthenticationScheme", StringComparison.Ordinal) == true
                || d.ServiceType.FullName?.Contains("JwtBearer", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in authDescriptors)
        {
            services.Remove(d);
        }

        services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
    }
}
