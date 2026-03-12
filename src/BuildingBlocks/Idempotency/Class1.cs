using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Urfu.Link.BuildingBlocks.Idempotency;

public static class IdempotencyExtensions
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
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
            var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>().Value;
            return ConnectionMultiplexer.Connect(options.RedisConfiguration);
        });
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }
}

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public string RedisConfiguration { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "urfu:idempotency";

    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(24);
}

public interface IIdempotencyStore
{
    ValueTask<bool> TryRegisterAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class RedisIdempotencyStore(
    IConnectionMultiplexer multiplexer,
    IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    public ValueTask<bool> TryRegisterAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new ValueTask<bool>(RegisterAsync(key, cancellationToken));
    }

    private async Task<bool> RegisterAsync(string key, CancellationToken cancellationToken)
    {
        var database = multiplexer.GetDatabase();
        var optionsValue = options.Value;

        return await database
            .StringSetAsync(
                $"{optionsValue.KeyPrefix}:{key}",
                "1",
                optionsValue.Retention,
                When.NotExists)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class IdempotencyEndpointFilter(IIdempotencyStore store) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var values))
        {
            return Results.BadRequest(new { error = "missing_idempotency_key" });
        }

        var key = values.ToString();
        var isNew = await store.TryRegisterAsync(key, context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (!isNew)
        {
            return Results.Conflict(new { error = "duplicate_request" });
        }

        return await next(context).ConfigureAwait(false);
    }
}
