using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.BuildingBlocks.Outbox;

public static class OutboxExtensions
{
    public static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
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
            var options = serviceProvider.GetRequiredService<IOptions<OutboxOptions>>().Value;
            return ConnectionMultiplexer.Connect(options.RedisConfiguration);
        });
        services.AddSingleton<IOutboxStore, RedisOutboxStore>();
        services.AddSingleton<IOutboxWriter>(serviceProvider => serviceProvider.GetRequiredService<IOutboxStore>());
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }

    public static IServiceCollection AddKafkaPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IKafkaPublisher>(_ =>
        {
            var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
            }).Build();

            return new KafkaPublisher(producer);
        });

        return services;
    }
}

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public string RedisConfiguration { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "urfu:outbox";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}

public interface IOutboxWriter
{
    ValueTask EnqueueAsync<TEvent>(string topic, IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}

public interface IOutboxStore : IOutboxWriter
{
    Task RecoverAsync(CancellationToken cancellationToken = default);

    Task<OutboxMessage?> DequeueAsync(CancellationToken cancellationToken = default);

    Task CompleteAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessage(
    string Id,
    string Topic,
    string Key,
    string Payload,
    DateTimeOffset EnqueuedAtUtc);

public sealed class RedisOutboxStore(
    IConnectionMultiplexer multiplexer,
    IOptions<OutboxOptions> options) : IOutboxStore
{
    public ValueTask EnqueueAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(envelope);

        return new ValueTask(EnqueueInternalAsync(topic, envelope, cancellationToken));
    }

    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        var database = multiplexer.GetDatabase();
        while (true)
        {
            var entryId = await database.ListLeftPopAsync(GetProcessingKey()).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (entryId.IsNullOrEmpty)
            {
                break;
            }

            await database.ListRightPushAsync(GetPendingKey(), entryId).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<OutboxMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var database = multiplexer.GetDatabase();
        var entryId = await database.ListLeftPopAsync(GetPendingKey()).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (entryId.IsNullOrEmpty)
        {
            return null;
        }

        await database.ListRightPushAsync(GetProcessingKey(), entryId).WaitAsync(cancellationToken).ConfigureAwait(false);
        var payload = await database.HashGetAsync(GetMessagesKey(), entryId).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
        {
            await database.ListRemoveAsync(GetProcessingKey(), entryId, 1).WaitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        return JsonSerializer.Deserialize<OutboxMessage>(payload.ToString())!;
    }

    public async Task CompleteAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var database = multiplexer.GetDatabase();
        await database.ListRemoveAsync(GetProcessingKey(), message.Id, 1).WaitAsync(cancellationToken).ConfigureAwait(false);
        await database.HashDeleteAsync(GetMessagesKey(), message.Id).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnqueueInternalAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var entry = new OutboxMessage(
            envelope.MessageId.ToString("N"),
            topic,
            envelope.MessageId.ToString("N"),
            JsonSerializer.Serialize(envelope),
            DateTimeOffset.UtcNow);

        var serializedEntry = JsonSerializer.Serialize(entry);
        var database = multiplexer.GetDatabase();

        await database.HashSetAsync(GetMessagesKey(), entry.Id, serializedEntry).WaitAsync(cancellationToken).ConfigureAwait(false);
        await database.ListRightPushAsync(GetPendingKey(), entry.Id).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private string GetPendingKey() => $"{options.Value.KeyPrefix}:pending";

    private string GetProcessingKey() => $"{options.Value.KeyPrefix}:processing";

    private string GetMessagesKey() => $"{options.Value.KeyPrefix}:messages";
}

public sealed class OutboxPublisherWorker(
    IOutboxStore outboxStore,
    IKafkaPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await outboxStore.RecoverAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextMessage = await outboxStore.DequeueAsync(stoppingToken).ConfigureAwait(false);
            if (nextMessage is null)
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await publisher
                    .PublishSerializedAsync(nextMessage.Topic, nextMessage.Key, nextMessage.Payload, stoppingToken)
                    .ConfigureAwait(false);

                await outboxStore.CompleteAsync(nextMessage, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is KafkaException
                or ProduceException<string, string>
                or RedisException
                or TimeoutException
                or InvalidOperationException
                or JsonException)
            {
                logger.LogError(exception, "Outbox publish failed for message {MessageId}", nextMessage.Id);
                await Task.Delay(options.Value.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}

public interface IKafkaPublisher
{
    Task PublishAsync<TEvent>(string topic, IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    Task PublishSerializedAsync(string topic, string key, string payload, CancellationToken cancellationToken = default);
}

public sealed class KafkaPublisher(IProducer<string, string> producer) : IKafkaPublisher
{
    public async Task PublishAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(envelope);

        var message = JsonSerializer.Serialize(envelope);
        await PublishSerializedAsync(
            topic,
            envelope.MessageId.ToString("N"),
            message,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishSerializedAsync(
        string topic,
        string key,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        await producer.ProduceAsync(
            topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload,
            },
            cancellationToken).ConfigureAwait(false);
    }
}
