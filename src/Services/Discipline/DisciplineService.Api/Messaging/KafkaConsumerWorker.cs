using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace DisciplineService.Api.Messaging;

public sealed class KafkaConsumerWorker(
    ILogger<KafkaConsumerWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return KafkaConsumerBackgroundLoop.RunAsync(
            logger,
            configuration,
            "discipline",
            "discipline-service-dev",
            KafkaTopicNames.DisciplineEvents,
            stoppingToken);
    }
}
