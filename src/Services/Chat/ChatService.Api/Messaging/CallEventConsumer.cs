using System.Text.Json;
using Confluent.Kafka;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Chat.Application.Calls;

namespace Urfu.Link.Services.Chat.Messaging;

public sealed class CallEventConsumer(
    ILogger<CallEventConsumer> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis) : BackgroundService
{
    internal const string DedupKeyPrefix = "chat:call-events:dedup:";
    internal static readonly TimeSpan DedupTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = configuration["Call:ConsumerGroup"] ?? "chat-service-call-v1",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        if (Enum.TryParse<SecurityProtocol>(configuration["Kafka:SecurityProtocol"], ignoreCase: true, out var sec))
        {
            consumerConfig.SecurityProtocol = sec;
        }

        if (Enum.TryParse<SaslMechanism>(configuration["Kafka:SaslMechanism"], ignoreCase: true, out var sasl))
        {
            consumerConfig.SaslMechanism = sasl;
        }

        var saslUser = configuration["Kafka:SaslUsername"];
        if (!string.IsNullOrWhiteSpace(saslUser))
        {
            consumerConfig.SaslUsername = saslUser;
        }

        var saslPwd = configuration["Kafka:SaslPassword"];
        if (!string.IsNullOrWhiteSpace(saslPwd))
        {
            consumerConfig.SaslPassword = saslPwd;
        }

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var topic = configuration["Call:ConsumerTopic"] ?? KafkaTopicNames.CallEvents;
        consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message is null)
                    {
                        continue;
                    }

                    await DispatchAsync(result.Message.Value, stoppingToken).ConfigureAwait(false);
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "[ChatService] Call consumer waiting for broker readiness on {Topic}. Retrying.", topic);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                {
                    logger.LogError(ex, "[ChatService] Failed to dispatch call event; dropping.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }

    internal async Task DispatchAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var messageId = root.TryGetProperty("messageId", out var midProp) && midProp.TryGetGuid(out var mid)
            ? mid
            : Guid.Empty;
        if (messageId != Guid.Empty && !await TryRegisterAsync(messageId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!root.TryGetProperty("payload", out var payloadElement))
        {
            return;
        }

        var eventType = payloadElement.TryGetProperty("eventType", out var etProp)
            ? etProp.GetString()
            : null;
        if (string.IsNullOrEmpty(eventType))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CallSystemMessageService>();

        switch (eventType)
        {
            case "call.incoming.v2":
                {
                    var evt = payloadElement.Deserialize<CallIncomingV2Event>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleIncomingAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "call.missed.v2":
                {
                    var evt = payloadElement.Deserialize<CallMissedV2Event>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleMissedAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "call.ended.v2":
                {
                    var evt = payloadElement.Deserialize<CallEndedV2Event>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleEndedAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            default:
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("[ChatService] Ignoring unknown call event type {EventType}.", eventType);
                }

                break;
        }
    }

    private async Task<bool> TryRegisterAsync(Guid messageId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var key = DedupKeyPrefix + messageId.ToString("N");
        return await redis.GetDatabase()
            .StringSetAsync(key, "1", DedupTtl, when: When.NotExists)
            .ConfigureAwait(false);
    }
}
