using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Urfu.Link.BuildingBlocks.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddPlatformJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration["Auth:Authority"] ?? "http://localhost:8080/realms/urfu-link";
        var audience = configuration["Auth:Audience"] ?? "urfu-link-api";
        var metadataAddress = configuration["Auth:MetadataAddress"];
        var validIssuer = configuration["Auth:ValidIssuer"];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                if (!string.IsNullOrEmpty(metadataAddress))
                    options.MetadataAddress = metadataAddress;
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
                    && authorityUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuer = true,
                    ValidIssuer = validIssuer ?? authority,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                };
            });

        services.AddAuthorization();

        return services;
    }
}
