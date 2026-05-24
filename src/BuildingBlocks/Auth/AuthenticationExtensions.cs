using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Urfu.Link.BuildingBlocks.Auth;

public static class AuthenticationExtensions
{
    public const string InternalJwtBearerScheme = "InternalJwtBearer";
    public const string InternalGrpcPolicy = "InternalGrpc";

    public static IServiceCollection AddPlatformJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var defaultJwt = PlatformJwtOptions.From(configuration.GetSection("Auth"));
        var internalJwt = PlatformJwtOptions.From(configuration.GetSection("Auth:Internal"));
        var hasInternalJwt = internalJwt.HasExplicitIssuerSource;

        var authentication = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => ConfigureJwtBearer(options, defaultJwt, useTokenHeader: true));

        if (hasInternalJwt)
        {
            authentication.AddJwtBearer(
                InternalJwtBearerScheme,
                options => ConfigureJwtBearer(options, internalJwt, useTokenHeader: false));
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(InternalGrpcPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(
                    hasInternalJwt
                        ? InternalJwtBearerScheme
                        : JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions options,
        PlatformJwtOptions jwt,
        bool useTokenHeader)
    {
        if (useTokenHeader && !string.IsNullOrEmpty(jwt.TokenHeader))
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Headers[jwt.TokenHeader].FirstOrDefault();
                    if (!string.IsNullOrEmpty(token))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                },
            };
        }

        if (!string.IsNullOrEmpty(jwt.JwksUrl))
        {
            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwt.JwksUrl,
                new JwksRetriever(),
                new HttpDocumentRetriever { RequireHttps = false });
        }
        else
        {
            options.Authority = jwt.Authority;
            if (!string.IsNullOrEmpty(jwt.MetadataAddress))
            {
                options.MetadataAddress = jwt.MetadataAddress;
            }

            options.RequireHttpsMetadata = Uri.TryCreate(jwt.Authority, UriKind.Absolute, out var authorityUri)
                && authorityUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        options.Audience = jwt.Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuer = true,
            ValidIssuer = jwt.ValidIssuer ?? jwt.Authority,
            NameClaimType = jwt.NameClaim,
            RoleClaimType = jwt.RoleClaim,
        };
    }

    private sealed record PlatformJwtOptions(
        string? TokenHeader,
        string? JwksUrl,
        string Authority,
        string Audience,
        string? MetadataAddress,
        string? ValidIssuer,
        string NameClaim,
        string RoleClaim,
        bool HasExplicitIssuerSource)
    {
        public static PlatformJwtOptions From(IConfiguration section)
        {
            ArgumentNullException.ThrowIfNull(section);

            var hasExplicitIssuerSource =
                !string.IsNullOrWhiteSpace(section["JwksUrl"])
                || !string.IsNullOrWhiteSpace(section["Authority"])
                || !string.IsNullOrWhiteSpace(section["MetadataAddress"]);

            return new PlatformJwtOptions(
                section["TokenHeader"],
                section["JwksUrl"],
                section["Authority"] ?? "http://localhost:8080/realms/urfu-link",
                section["Audience"] ?? "urfu-link-api",
                section["MetadataAddress"],
                section["ValidIssuer"],
                section["NameClaim"] ?? "preferred_username",
                section["RoleClaim"] ?? "roles",
                hasExplicitIssuerSource);
        }
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
