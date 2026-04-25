using System.Text.Json;
using Confluent.Kafka;

namespace Urfu.Link.Services.Presence.Messaging;

/// <summary>
/// Subscribes to <c>Kafka:ConsumerTopic</c> (defaults to <c>urfu.user.events.v1</c>),
/// parses <c>IntegrationEnvelope</c>, and dispatches the inner payload + event type
/// to scoped <see cref="IKafkaMessageHandler"/> instances. Currently the only
/// handler is <see cref="PrivacyChangedHandler"/>; new ones can be added without
/// touching this worker.
/// </summary>
public sealed class KafkaConsumerWorker(
    ILogger<KafkaConsumerWorker> logger,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const string DefaultGroupId = "presence-privacy-projection-v1";
    private const string DefaultTopic = "urfu.user.events.v1";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => RunAsync(stoppingToken), stoppingToken);
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = configuration["Kafka:ConsumerGroup"] ?? DefaultGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        if (Enum.TryParse<SecurityProtocol>(configuration["Kafka:SecurityProtocol"], ignoreCase: true, out var securityProtocol))
        {
            consumerConfig.SecurityProtocol = securityProtocol;
        }
        if (Enum.TryParse<SaslMechanism>(configuration["Kafka:SaslMechanism"], ignoreCase: true, out var saslMechanism))
        {
            consumerConfig.SaslMechanism = saslMechanism;
        }
        var saslUsername = configuration["Kafka:SaslUsername"];
        if (!string.IsNullOrWhiteSpace(saslUsername)) consumerConfig.SaslUsername = saslUsername;
        var saslPassword = configuration["Kafka:SaslPassword"];
        if (!string.IsNullOrWhiteSpace(saslPassword)) consumerConfig.SaslPassword = saslPassword;

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        var topic = configuration["Kafka:ConsumerTopic"] ?? DefaultTopic;
        consumer.Subscribe(topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    if (result?.Message is null) continue;
                    await DispatchAsync(result.Message.Value, stoppingToken).ConfigureAwait(false);
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex,
                        "[Kafka:presence] consumer is waiting for broker/topic readiness on {Topic}",
                        topic);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
                {
                    logger.LogError(ex, "[Kafka:presence] failed to dispatch message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[Kafka:presence] consumer stopped");
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task DispatchAsync(string raw, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("Payload", out var payload)) return;
        if (!payload.TryGetProperty("EventType", out var eventTypeElement)) return;

        var eventType = eventTypeElement.GetString();
        if (string.IsNullOrEmpty(eventType)) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IKafkaMessageHandler>();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(eventType, payload, cancellationToken).ConfigureAwait(false);
        }
    }
}
