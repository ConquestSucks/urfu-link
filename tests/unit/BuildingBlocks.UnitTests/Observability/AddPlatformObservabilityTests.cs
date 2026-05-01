using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Urfu.Link.BuildingBlocks.Observability;

namespace Urfu.Link.BuildingBlocks.UnitTests.Observability;

public sealed class AddPlatformObservabilityTests
{
    /// <summary>
    /// Cross-service distributed tracing relies on Kafka activities being captured by
    /// the SDK. <see cref="ObservabilityExtensions.AddPlatformObservability"/> must
    /// subscribe to the <c>Confluent.Kafka</c> ActivitySource so producer and consumer
    /// spans link into the trace tree (chat → kafka → notification single trace_id).
    /// Without subscription, <see cref="ActivitySource.StartActivity(string, ActivityKind)"/>
    /// returns <see langword="null"/> because no <see cref="ActivityListener"/> samples it.
    /// </summary>
    [Fact]
    public void Subscribes_to_confluent_kafka_activity_source()
    {
        using var serviceProvider = BuildServiceProviderWithObservability();

        // Force TracerProvider materialization which registers ActivityListener subscriptions.
        _ = serviceProvider.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource("Confluent.Kafka");
        using var activity = source.StartActivity("kafka.publish");

        activity.Should().NotBeNull(
            "AddPlatformObservability must subscribe to 'Confluent.Kafka' ActivitySource so cross-service traces propagate through Kafka.");
    }

    private static ServiceProvider BuildServiceProviderWithObservability()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddPlatformObservability(configuration, "test-service");

        return services.BuildServiceProvider();
    }
}
