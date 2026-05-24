using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace Urfu.Link.Services.Notification.Messaging;

/// <summary>
/// Base hosted service that consumes a single Kafka topic, deduplicates by envelope
/// MessageId via the shared <see cref="IIdempotencyStore"/> from
/// <c>BuildingBlocks.Idempotency</c> (Redis-backed <c>SET NX</c> with platform-wide TTL),
/// and dispatches each event to <see cref="HandleEventAsync"/>. Each derived consumer
/// owns a distinct Kafka group id so that lag and offsets are independent across event
/// domains. Each consumer also owns a distinct <see cref="DedupKeyPrefix"/> so the same
/// envelope (e.g. UserDeletedEvent) consumed by multiple topics does not de-duplicate
/// across consumers.
/// </summary>
public abstract class KafkaConsumerBase(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger logger) : BackgroundService
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger _logger = logger;

    protected abstract string Topic { get; }

    protected abstract string GroupId { get; }

    protected abstract string DedupKeyPrefix { get; }

    protected abstract Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken);

    protected sealed override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => RunAsync(stoppingToken), stoppingToken);
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = BuildConsumerConfig();
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Kafka consume error on {Topic}; retrying", Topic);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                if (result?.Message?.Value is null)
                {
                    continue;
                }

                try
                {
                    await ProcessAsync(result.Message.Value, stoppingToken).ConfigureAwait(false);
                    consumer.Commit(result);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Skipping malformed envelope on {Topic} at offset {Offset}", Topic, result.Offset);
                    consumer.Commit(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(string raw, CancellationToken cancellationToken)
    {
        var envelope = JsonNode.Parse(raw)
            ?? throw new JsonException("Envelope is null");

        var messageId = ReadStringOrThrow(envelope, "messageId");
        var payloadNode = envelope["payload"]
            ?? throw new JsonException("Envelope payload missing");
        var eventType = ReadStringOrThrow(payloadNode, "eventType");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var idempotency = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var dedupKey = $"{DedupKeyPrefix}:{messageId}";
        if (!await idempotency.TryRegisterAsync(dedupKey, cancellationToken).ConfigureAwait(false))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Duplicate envelope {MessageId} on {Topic} — skipped", messageId, Topic);
            }

            return;
        }

        try
        {
            await HandleEventAsync(eventType, payloadNode, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Handler for {EventType} on {Topic} failed", eventType, Topic);
            throw;
        }
    }

    private ConsumerConfig BuildConsumerConfig()
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        if (Enum.TryParse<SecurityProtocol>(_configuration["Kafka:SecurityProtocol"], ignoreCase: true, out var securityProtocol))
        {
            consumerConfig.SecurityProtocol = securityProtocol;
        }

        if (Enum.TryParse<SaslMechanism>(_configuration["Kafka:SaslMechanism"], ignoreCase: true, out var saslMechanism))
        {
            consumerConfig.SaslMechanism = saslMechanism;
        }

        var saslUsername = _configuration["Kafka:SaslUsername"];
        if (!string.IsNullOrWhiteSpace(saslUsername))
        {
            consumerConfig.SaslUsername = saslUsername;
        }

        var saslPassword = _configuration["Kafka:SaslPassword"];
        if (!string.IsNullOrWhiteSpace(saslPassword))
        {
            consumerConfig.SaslPassword = saslPassword;
        }

        return consumerConfig;
    }

    private static string ReadStringOrThrow(JsonNode node, string property)
    {
        var value = node[property]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException(
                string.Create(CultureInfo.InvariantCulture, $"Missing or empty '{property}' on Kafka envelope."));
        }

        return value;
    }
}
