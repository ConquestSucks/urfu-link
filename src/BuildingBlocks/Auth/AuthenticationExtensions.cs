using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

        var tokenHeader = configuration["Auth:TokenHeader"];
        var jwksUrl = configuration["Auth:JwksUrl"];
        var authority = configuration["Auth:Authority"] ?? "http://localhost:8080/realms/urfu-link";
        var audience = configuration["Auth:Audience"] ?? "urfu-link-api";
        var metadataAddress = configuration["Auth:MetadataAddress"];
        var validIssuer = configuration["Auth:ValidIssuer"];
        var nameClaim = configuration["Auth:NameClaim"] ?? "preferred_username";
        var roleClaim = configuration["Auth:RoleClaim"] ?? "roles";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrEmpty(tokenHeader))
                {
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var jwt = context.Request.Headers[tokenHeader].FirstOrDefault();
                            if (!string.IsNullOrEmpty(jwt))
                                context.Token = jwt;
                            return Task.CompletedTask;
                        }
                    };
                }

                if (!string.IsNullOrEmpty(jwksUrl))
                {
                    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        jwksUrl,
                        new JwksRetriever(),
                        new HttpDocumentRetriever { RequireHttps = false });
                }
                else
                {
                    options.Authority = authority;
                    if (!string.IsNullOrEmpty(metadataAddress))
                        options.MetadataAddress = metadataAddress;
                    options.RequireHttpsMetadata = Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
                        && authorityUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                }

                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuer = true,
                    ValidIssuer = validIssuer ?? authority,
                    NameClaimType = nameClaim,
                    RoleClaimType = roleClaim,
                };
            });

        services.AddAuthorization();

        return services;
    }

    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var json = await retriever.GetDocumentAsync(address, cancel);
            var config = new OpenIdConnectConfiguration();
            var jwks = new JsonWebKeySet(json);
            foreach (var key in jwks.GetSigningKeys())
                config.SigningKeys.Add(key);
            return config;
        }
    }
}
