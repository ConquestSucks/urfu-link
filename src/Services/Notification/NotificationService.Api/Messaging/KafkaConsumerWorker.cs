using Confluent.Kafka;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return KafkaConsumerBackgroundLoop.RunAsync(
            logger,
            configuration,
            "notification",
            "notification-service-dev",
            KafkaTopicNames.NotificationEvents,
            stoppingToken);
    }
}

