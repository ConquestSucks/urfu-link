using System.Reflection;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Auth;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.BuildingBlocks.Observability;

namespace Urfu.Link.BuildingBlocks.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static IServiceCollection AddServiceDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddProblemDetails();
        services.AddOpenApi();
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });
        var validatorsAssembly = Assembly.GetEntryAssembly() ?? typeof(ServiceDefaultsExtensions).Assembly;
        services.AddValidatorsFromAssembly(validatorsAssembly);
        services.AddHealthChecks();
        services.AddIdempotency(configuration);
        services.AddPlatformJwtAuthentication(configuration);
        services.AddPlatformObservability(configuration, serviceName);

        return services;
    }

    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();

        // Liveness must not depend on external resources — kubelet liveness failures restart the pod.
        // Readiness aggregates checks tagged "ready" (e.g. SignalR Redis backplane); a degraded
        // dependency takes the pod out of rotation but does not trigger a restart.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });
        app.MapOpenApi();

        return app;
    }
}
