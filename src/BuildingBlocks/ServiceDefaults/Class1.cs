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

        app.MapHealthChecks("/health/live", new HealthCheckOptions());
        app.MapHealthChecks("/health/ready", new HealthCheckOptions());
        app.MapOpenApi();

        return app;
    }
}
