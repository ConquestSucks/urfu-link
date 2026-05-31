using DisciplineChatE2ETests.Infrastructure;
using DotNet.Testcontainers.Images;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.MongoDb;
using Testcontainers.Redis;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Infrastructure.Grpc;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;
using Urfu.Link.Services.Chat.Realtime;

namespace DisciplineChatE2ETests.Infrastructure;

public sealed class ChatServiceE2EFactory(DisciplineServiceE2EFactory disciplineFactory)
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder()
        .WithImage("mongo:8.0.5")
        .WithImagePullPolicy(PullPolicy.Missing)
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7.4.2-alpine")
        .WithImagePullPolicy(PullPolicy.Missing)
        .Build();

    public FakeOutboxWriter OutboxWriter { get; } = new();

    public IIdempotencyStore IdempotencyStore { get; } = Substitute.For<IIdempotencyStore>();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public string RedisConnectionString => $"{_redis.GetConnectionString()},allowAdmin=true";

    public string DatabaseName { get; } = $"chat_e2e_{Guid.NewGuid():N}";

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
        TestAuthHandler.CurrentPrincipal = null;
    }

    public async Task ResetDataAsync()
    {
        var ctx = Services.GetRequiredService<ChatMongoContext>();
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.ConversationsCollectionName)
            .DeleteManyAsync(FilterDefinition<dynamic>.Empty);
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.MessagesCollectionName)
            .DeleteManyAsync(FilterDefinition<dynamic>.Empty);
        await ctx.Database.GetCollection<dynamic>(ChatMongoContext.ThreadSubscriptionsCollectionName)
            .DeleteManyAsync(FilterDefinition<dynamic>.Empty);

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
                ["GrpcClients:DisciplineService:Address"] = "http://discipline-e2e",
            });
        });

        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<HubLifetimeManager<ChatHub>>();
            services.AddSingleton<HubLifetimeManager<ChatHub>, DefaultHubLifetimeManager<ChatHub>>();

            IdempotencyStore.TryRegisterAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(true));
            services.RemoveAll<IIdempotencyStore>();
            services.AddSingleton(IdempotencyStore);

            services.RemoveAll<IOutboxStore>();
            services.RemoveAll<IOutboxWriter>();
            services.AddSingleton<IOutboxWriter>(OutboxWriter);

            services.RemoveAll<IMediaServiceClient>();
            services.AddSingleton<IMediaServiceClient, FakeMediaServiceClient>();

            services.RemoveAll<IGrpcBearerTokenProvider>();
            services.AddSingleton<IGrpcBearerTokenProvider, NoopGrpcBearerTokenProvider>();

            services.RemoveAll<Urfu.Link.Services.Disciplines.Grpc.InternalApi.InternalApiClient>();
            services.AddSingleton(_ =>
            {
                var channel = GrpcChannel.ForAddress(
                    disciplineFactory.Server.BaseAddress,
                    new GrpcChannelOptions
                    {
                        HttpHandler = disciplineFactory.Server.CreateHandler(),
                    });
                return new Urfu.Link.Services.Disciplines.Grpc.InternalApi.InternalApiClient(channel);
            });

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
            .Where(d => d.ServiceType == typeof(IConfigureOptions<AuthenticationOptions>)
                || d.ServiceType == typeof(IPostConfigureOptions<AuthenticationOptions>)
                || d.ServiceType == typeof(IConfigureOptions<JwtBearerOptions>)
                || d.ServiceType == typeof(IPostConfigureOptions<JwtBearerOptions>)
                || d.ServiceType == typeof(IOptionsChangeTokenSource<JwtBearerOptions>))
            .ToList();
        foreach (var d in authDescriptors)
        {
            services.Remove(d);
        }

        services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { });
        services.AddAuthorization(options =>
            options.AddPolicy(
                AuthenticationExtensions.InternalGrpcPolicy,
                policy => policy
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()));
    }
}
