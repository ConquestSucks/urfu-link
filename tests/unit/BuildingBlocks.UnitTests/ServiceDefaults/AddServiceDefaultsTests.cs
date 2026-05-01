using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.ServiceDefaults;

namespace Urfu.Link.BuildingBlocks.UnitTests.ServiceDefaults;

public sealed class AddServiceDefaultsTests
{
    /// <summary>
    /// Every SignalR-bearing service (chat, presence, notification) needs its readiness
    /// gated on the Redis backplane connection — without it, broadcasts cannot propagate
    /// across replicas. Registering the check inside <c>AddServiceDefaults</c> removes
    /// the per-service footgun where a freshly added SignalR service silently passes
    /// readiness while broadcasts are dropped.
    /// </summary>
    [Fact]
    public void Auto_registers_signalr_backplane_health_check()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddServiceDefaults(configuration, "test-service");

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registrations.Should().Contain(r =>
            r.Name == "signalr-backplane" && r.Tags.Contains("ready"),
            "AddServiceDefaults must register the SignalR backplane readiness probe so individual services don't have to remember to call AddSignalRBackplaneHealthCheck.");
    }

    /// <summary>
    /// Defensive guard against duplicate registrations if a service still calls
    /// <c>AddSignalRBackplaneHealthCheck</c> manually after the auto-registration —
    /// the readiness endpoint should not surface the same probe twice.
    /// </summary>
    [Fact]
    public void Registers_signalr_backplane_health_check_only_once()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddServiceDefaults(configuration, "test-service");
        services.AddHealthChecks().AddSignalRBackplaneHealthCheck();

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        registrations.Count(r => r.Name == "signalr-backplane")
            .Should().Be(1, "duplicate readiness probes confuse operators inspecting /health/ready.");
    }
}
