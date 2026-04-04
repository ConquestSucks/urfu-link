using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Urfu.Link.BuildingBlocks.SessionRevocation;

public static class SessionRevocationExtensions
{
    public static IServiceCollection AddSessionRevocation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SessionRevocationOptions>()
            .Bind(configuration.GetSection(SessionRevocationOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.RedisConfiguration))
                {
                    options.RedisConfiguration =
                        configuration["Infrastructure:Redis:Configuration"]
                        ?? configuration["ConnectionStrings:Redis"]
                        ?? configuration["ConnectionStrings:Primary"]
                        ?? "localhost:6379";
                }
            });

        services.TryAddSingleton<IConnectionMultiplexer>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SessionRevocationOptions>>().Value;
            var configOptions = ConfigurationOptions.Parse(options.RedisConfiguration);
            configOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.AddSingleton<ISessionRevocationStore, RedisSessionRevocationStore>();

        return services;
    }
}

public sealed class SessionRevocationOptions
{
    public const string SectionName = "SessionRevocation";

    public string RedisConfiguration { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "urfu:session";

    public TimeSpan Ttl { get; set; } = TimeSpan.FromSeconds(300);
}

public interface ISessionRevocationStore
{
    Task RevokeAsync(string userId, string callerSessionId, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
}

public sealed class RedisSessionRevocationStore(
    IConnectionMultiplexer multiplexer,
    IOptions<SessionRevocationOptions> options) : ISessionRevocationStore
{
    public async Task RevokeAsync(string userId, string callerSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerSessionId);

        var db = multiplexer.GetDatabase();
        var opts = options.Value;
        var revokedKey = $"{opts.KeyPrefix}:revoked:{userId}";
        var allowedKey = $"{opts.KeyPrefix}:allowed:{userId}";

        var batch = db.CreateBatch();
        var t1 = batch.StringSetAsync(revokedKey, "1", opts.Ttl);
        var t2 = batch.SetAddAsync(allowedKey, callerSessionId);
        var t3 = batch.KeyExpireAsync(allowedKey, opts.Ttl);
        batch.Execute();

        await Task.WhenAll(t1, t2, t3).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsRevokedAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var db = multiplexer.GetDatabase();
        var opts = options.Value;
        var revokedKey = $"{opts.KeyPrefix}:revoked:{userId}";

        var isRevoked = await db.KeyExistsAsync(revokedKey).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!isRevoked)
            return false;

        var allowedKey = $"{opts.KeyPrefix}:allowed:{userId}";
        var isAllowed = await db.SetContainsAsync(allowedKey, sessionId).WaitAsync(cancellationToken).ConfigureAwait(false);

        return !isAllowed;
    }
}
