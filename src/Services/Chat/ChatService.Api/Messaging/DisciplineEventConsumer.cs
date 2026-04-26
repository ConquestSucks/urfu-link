using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Chat.Application.Disciplines;

namespace Urfu.Link.Services.Chat.Messaging;

/// <summary>
/// Consumes <c>urfu.discipline.events.v1</c> and projects each event onto the
/// chat conversation backing the discipline. Idempotency is delegated to a
/// Redis dedup set keyed by <see cref="IntegrationEnvelope{T}.MessageId"/>;
/// duplicates within the dedup TTL are silently dropped.
/// </summary>
public sealed class DisciplineEventConsumer(
    ILogger<DisciplineEventConsumer> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis) : BackgroundService
{
    internal const string DedupKeyPrefix = "chat:discipline-events:dedup:";
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
            GroupId = configuration["Discipline:ConsumerGroup"] ?? "chat-service-discipline-v1",
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
        var topic = configuration["Discipline:ConsumerTopic"] ?? KafkaTopicNames.DisciplineEvents;
        consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result is null || result.Message is null)
                    {
                        continue;
                    }

                    await DispatchAsync(result.Message.Value, stoppingToken).ConfigureAwait(false);
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        ex,
                        "[ChatService] Discipline consumer waiting for broker readiness on {Topic}. Retrying.",
                        topic);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                {
                    // Malformed envelopes are logged and skipped — we never want a single poison
                    // message to halt the pipeline. Promoting these to a real DLQ topic
                    // (urfu.discipline.events.v1.dlq) is tracked under the discipline-chat
                    // follow-up issue (see eventing-conventions.md "Retry & DLQ"); until then,
                    // every drop is loud at LogError so operators can detect drift via alerts.
                    logger.LogError(ex, "[ChatService] Failed to dispatch discipline event; dropping (no DLQ yet).");
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
            // Already processed within the dedup window.
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
        var service = scope.ServiceProvider.GetRequiredService<DisciplineConversationService>();

        switch (eventType)
        {
            case "discipline.created.v1":
                {
                    var evt = payloadElement.Deserialize<DisciplineCreatedEvent>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleDisciplineCreatedAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "discipline.user_enrolled.v1":
                {
                    var evt = payloadElement.Deserialize<UserEnrolledEvent>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleUserEnrolledAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "discipline.user_unenrolled.v1":
                {
                    var evt = payloadElement.Deserialize<UserUnenrolledEvent>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleUserUnenrolledAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "discipline.enrollment_role_changed.v1":
                {
                    var evt = payloadElement.Deserialize<EnrollmentRoleChangedEvent>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleEnrollmentRoleChangedAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "discipline.deleted.v1":
                {
                    var evt = payloadElement.Deserialize<DisciplineDeletedEvent>(JsonOptions);
                    if (evt is not null)
                    {
                        await service.HandleDisciplineDeletedAsync(evt, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                }

            case "discipline.updated.v1":
                // No-op for now: the conversation has no display metadata mirrored from the
                // discipline. Hook into a name/cover update flow in a follow-up if needed.
                break;

            default:
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("[ChatService] Ignoring unknown discipline event type {EventType}", eventType);
                }

                break;
        }
    }

    private async Task<bool> TryRegisterAsync(Guid messageId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var key = DedupKeyPrefix + messageId.ToString("N");
        var db = redis.GetDatabase();
        return await db.StringSetAsync(key, "1", DedupTtl, when: When.NotExists).ConfigureAwait(false);
    }
}
