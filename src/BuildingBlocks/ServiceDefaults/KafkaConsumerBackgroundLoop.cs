using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Urfu.Link.BuildingBlocks.ServiceDefaults;

public static class KafkaConsumerBackgroundLoop
{
    public static Task RunAsync(
        ILogger logger,
        IConfiguration configuration,
        string serviceName,
        string defaultGroupId,
        string defaultTopic,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultGroupId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTopic);

        return Task.Run(async () =>
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = configuration["Kafka:ConsumerGroup"] ?? defaultGroupId,
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
            if (!string.IsNullOrWhiteSpace(saslUsername))
            {
                consumerConfig.SaslUsername = saslUsername;
            }

            var saslPassword = configuration["Kafka:SaslPassword"];
            if (!string.IsNullOrWhiteSpace(saslPassword))
            {
                consumerConfig.SaslPassword = saslPassword;
            }

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            var topic = configuration["Kafka:ConsumerTopic"] ?? defaultTopic;
            consumer.Subscribe(topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(stoppingToken);
                        if (logger.IsEnabled(LogLevel.Information))
                        {
                            logger.LogInformation(
                                "[Kafka:{Service}] Received message {Key} from {Topic}",
                                serviceName,
                                result.Message.Key,
                                result.Topic);
                        }
                    }
                    catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning(
                            ex,
                            "[Kafka:{Service}] Consumer is waiting for broker/topic readiness on {Topic}. Retrying.",
                            serviceName,
                            topic);

                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("[Kafka:{Service}] consumer stopped.", serviceName);
                }
            }
            finally
            {
                consumer.Close();
            }
        }, stoppingToken);
    }
}
