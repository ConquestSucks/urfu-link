using Confluent.Kafka;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace Urfu.Link.Services.Chat.Messaging;

public sealed class KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return KafkaConsumerBackgroundLoop.RunAsync(
            logger,
            configuration,
            "chat",
            "chat-service-dev",
            KafkaTopicNames.ChatEvents,
            stoppingToken);
    }
}

