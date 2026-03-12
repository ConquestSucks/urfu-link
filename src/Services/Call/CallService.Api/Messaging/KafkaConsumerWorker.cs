using Confluent.Kafka;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace Urfu.Link.Services.Call.Messaging;

public sealed class KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return KafkaConsumerBackgroundLoop.RunAsync(
            logger,
            configuration,
            "call",
            "call-service-dev",
            KafkaTopicNames.CallEvents,
            stoppingToken);
    }
}

