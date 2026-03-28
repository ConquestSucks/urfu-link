using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace UserService.Api.Messaging;

public sealed class KafkaConsumerWorker(ILogger<KafkaConsumerWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return KafkaConsumerBackgroundLoop.RunAsync(
            logger,
            configuration,
            "user",
            "user-service-dev",
            KafkaTopicNames.UserEvents,
            stoppingToken);
    }
}

