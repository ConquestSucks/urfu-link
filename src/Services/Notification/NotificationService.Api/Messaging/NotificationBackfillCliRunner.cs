using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Infrastructure;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Messaging;

public static class NotificationBackfillCliRunner
{
    private static readonly DateTimeOffset DefaultFrom = DateTimeOffset.Parse(
        "2026-05-25T15:24:21Z",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static readonly IReadOnlyDictionary<string, string> TopicAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat"] = KafkaTopicNames.ChatEvents,
            ["discipline"] = KafkaTopicNames.DisciplineEvents,
            ["media"] = KafkaTopicNames.MediaEvents,
            ["call"] = KafkaTopicNames.CallEvents,
            ["user"] = KafkaTopicNames.UserEvents,
            ["system"] = KafkaTopicNames.SystemEvents,
        };

    public static async Task<bool> TryRunAsync(
        string[] args,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!args.Any(a =>
                string.Equals(a, "--backfill-notifications", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "backfill-notifications", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            var options = NotificationBackfillOptions.Parse(args);
            await using var provider = BuildProvider(configuration);
            var runner = ActivatorUtilities.CreateInstance<NotificationBackfillRunner>(provider, configuration);
            var report = await runner.RunAsync(options, cancellationToken).ConfigureAwait(false);
            await report.WriteAsync(Console.Out).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // CLI mode must convert any failure into a non-zero process exit for jobs.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await Console.Error.WriteLineAsync(
                $"Notification backfill failed: {ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddNotificationModule(configuration);
        services.RemoveAll<INotificationBroadcaster>();
        services.AddSingleton<INotificationBroadcaster, NoopBackfillNotificationBroadcaster>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed record NotificationBackfillOptions(
        DateTimeOffset From,
        DateTimeOffset To,
        string[] Topics,
        int Limit,
        bool DryRun)
    {
        public static NotificationBackfillOptions Parse(string[] args)
        {
            var from = ParseDate(GetValue(args, "--from"), DefaultFrom);
            var to = ParseDate(GetValue(args, "--to"), DateTimeOffset.UtcNow);
            var topics = ParseTopics(GetValue(args, "--topics"));
            var limit = ParseLimit(GetValue(args, "--limit"));
            var dryRun = !args.Any(a => string.Equals(a, "--execute", StringComparison.OrdinalIgnoreCase));
            if (to < from)
            {
                throw new ArgumentException("--to must be greater than or equal to --from.");
            }

            return new NotificationBackfillOptions(from, to, topics, limit, dryRun);
        }

        private static string? GetValue(string[] args, string name)
        {
            var prefix = $"{name}=";
            return args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
        }

        private static DateTimeOffset ParseDate(string? value, DateTimeOffset fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Invalid date value '{value}'.");
        }

        private static string[] ParseTopics(string? value)
        {
            var requested = string.IsNullOrWhiteSpace(value)
                ? ["chat", "discipline", "media", "call", "user"]
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return requested
                .Select(topic =>
                    TopicAliases.TryGetValue(topic, out var mapped)
                        ? mapped
                        : topic)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static int ParseLimit(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 10_000;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            throw new ArgumentException($"Invalid limit value '{value}'.");
        }
    }

    private sealed class NotificationBackfillRunner(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        public async Task<NotificationBackfillReport> RunAsync(
            NotificationBackfillOptions options,
            CancellationToken cancellationToken)
        {
            var report = new NotificationBackfillReport(options.From, options.To, options.DryRun);
            var config = BuildConsumerConfig(configuration);
            using var consumer = new ConsumerBuilder<string, string>(config).Build();

            foreach (var topic in options.Topics)
            {
                await ConsumeTopicAsync(consumer, topic, options, report, cancellationToken).ConfigureAwait(false);
                if (report.ProcessedMessages >= options.Limit)
                {
                    break;
                }
            }

            return report;
        }

        private async Task ConsumeTopicAsync(
            IConsumer<string, string> consumer,
            string topic,
            NotificationBackfillOptions options,
            NotificationBackfillReport report,
            CancellationToken cancellationToken)
        {
            using var admin = new AdminClientBuilder(BuildAdminConfig(configuration)).Build();
            var metadata = admin.GetMetadata(topic, TimeSpan.FromSeconds(10));
            var topicMetadata = metadata.Topics.SingleOrDefault(t => string.Equals(t.Topic, topic, StringComparison.Ordinal));
            if (topicMetadata is null || topicMetadata.Error.IsError)
            {
                report.Add(topic, "topic-unavailable", 0);
                return;
            }

            var partitions = topicMetadata.Partitions
                .Select(p => new TopicPartition(topic, new Partition(p.PartitionId)))
                .ToArray();
            if (partitions.Length == 0)
            {
                return;
            }

            var offsets = consumer.OffsetsForTimes(
                partitions.Select(p => new TopicPartitionTimestamp(p, new Timestamp(options.From.UtcDateTime))).ToList(),
                TimeSpan.FromSeconds(10));
            var assignments = offsets
                .Where(o => o.Offset != Offset.Unset)
                .ToList();
            if (assignments.Count == 0)
            {
                return;
            }

            var highWatermarks = assignments.ToDictionary(
                o => o.TopicPartition,
                o => consumer.QueryWatermarkOffsets(o.TopicPartition, TimeSpan.FromSeconds(10)).High);
            var active = assignments
                .Where(o => o.Offset < highWatermarks[o.TopicPartition])
                .Select(o => o.TopicPartition)
                .ToHashSet();
            if (active.Count == 0)
            {
                return;
            }

            consumer.Assign(assignments);
            while (active.Count > 0 && report.ProcessedMessages < options.Limit && !cancellationToken.IsCancellationRequested)
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(2));
                if (result is null)
                {
                    break;
                }

                if (result.IsPartitionEOF)
                {
                    active.Remove(result.TopicPartition);
                    continue;
                }

                if (result.Message?.Value is null)
                {
                    continue;
                }

                if (result.Message.Timestamp.UtcDateTime > options.To.UtcDateTime)
                {
                    active.Remove(result.TopicPartition);
                    continue;
                }

                await DispatchMessageAsync(result.Message.Value, topic, options.DryRun, report, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Offset + 1 >= highWatermarks[result.TopicPartition])
                {
                    active.Remove(result.TopicPartition);
                }
            }

            consumer.Unassign();
        }

        private async Task DispatchMessageAsync(
            string raw,
            string topic,
            bool dryRun,
            NotificationBackfillReport report,
            CancellationToken cancellationToken)
        {
            try
            {
                var envelope = KafkaEnvelopeReader.Read(raw);
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationKafkaEventDispatcher>();
                var result = await dispatcher.DispatchAsync(
                    envelope.EventType,
                    envelope.Payload,
                    scope.ServiceProvider,
                    dryRun,
                    cancellationToken).ConfigureAwait(false);
                report.Add(topic, result.Status, result.Affected, result.EventType);
            }
            catch (JsonException)
            {
                report.Add(topic, "malformed", 0);
            }
        }

        private static ConsumerConfig BuildConsumerConfig(IConfiguration configuration)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                GroupId = $"notification-service-backfill-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnablePartitionEof = true,
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

            return consumerConfig;
        }

        private static AdminClientConfig BuildAdminConfig(IConfiguration configuration)
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            };

            if (Enum.TryParse<SecurityProtocol>(configuration["Kafka:SecurityProtocol"], ignoreCase: true, out var securityProtocol))
            {
                adminConfig.SecurityProtocol = securityProtocol;
            }

            if (Enum.TryParse<SaslMechanism>(configuration["Kafka:SaslMechanism"], ignoreCase: true, out var saslMechanism))
            {
                adminConfig.SaslMechanism = saslMechanism;
            }

            var saslUsername = configuration["Kafka:SaslUsername"];
            if (!string.IsNullOrWhiteSpace(saslUsername))
            {
                adminConfig.SaslUsername = saslUsername;
            }

            var saslPassword = configuration["Kafka:SaslPassword"];
            if (!string.IsNullOrWhiteSpace(saslPassword))
            {
                adminConfig.SaslPassword = saslPassword;
            }

            return adminConfig;
        }
    }

    private sealed class NotificationBackfillReport(DateTimeOffset from, DateTimeOffset to, bool dryRun)
    {
        private readonly Dictionary<string, BackfillCounter> _counters = new(StringComparer.Ordinal);

        public int ProcessedMessages { get; private set; }

        public void Add(string topic, string status, int affected, string? eventType = null)
        {
            var key = $"{topic}|{eventType ?? "*"}|{status}";
            if (!_counters.TryGetValue(key, out var counter))
            {
                counter = new BackfillCounter(topic, eventType ?? "*", status);
                _counters[key] = counter;
            }

            counter.Messages++;
            counter.Affected += affected;
            ProcessedMessages++;
        }

        public async Task WriteAsync(TextWriter writer)
        {
            await writer.WriteLineAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Notification backfill {(dryRun ? "dry-run" : "execute")} from {from:O} to {to:O}.")).ConfigureAwait(false);
            foreach (var counter in _counters.Values.OrderBy(c => c.Topic, StringComparer.Ordinal).ThenBy(c => c.EventType, StringComparer.Ordinal))
            {
                await writer.WriteLineAsync(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{counter.Topic} {counter.EventType} {counter.Status}: messages={counter.Messages}, affected={counter.Affected}"))
                    .ConfigureAwait(false);
            }
        }
    }

    private sealed class BackfillCounter(string topic, string eventType, string status)
    {
        public string Topic { get; } = topic;

        public string EventType { get; } = eventType;

        public string Status { get; } = status;

        public int Messages { get; set; }

        public int Affected { get; set; }
    }

    private sealed class NoopBackfillNotificationBroadcaster : INotificationBroadcaster
    {
        public Task NotifyReceivedAsync(NotificationDto notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyUpsertedAsync(NotificationDto notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyReadAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyStateChangedAsync(Guid recipientUserId, NotificationStateChangedDto change, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyRemovedAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyBadgeUpdatedAsync(Guid recipientUserId, BadgeSnapshotDto snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyBatchReadAsync(Guid recipientUserId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyBackfillRequiredAsync(Guid recipientUserId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
