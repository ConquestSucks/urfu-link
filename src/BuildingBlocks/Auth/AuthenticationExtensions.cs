using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Urfu.Link.BuildingBlocks.Auth;

public static class AuthenticationExtensions
{
    public const string EdgeJwtBearerScheme = "EdgeJwtBearer";
    public const string MobileJwtBearerScheme = "MobileJwtBearer";
    public const string InternalJwtBearerScheme = "InternalJwtBearer";
    public const string InternalGrpcPolicy = "InternalGrpc";

    private const string AccessTokenQueryKey = "access_token";
    private const string BearerPrefix = "Bearer ";
    private static readonly PathString HubsPathPrefix = new("/hubs");

    public static IServiceCollection AddPlatformJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var defaultJwt = PlatformJwtOptions.From(configuration.GetSection("Auth"));
        var mobileJwt = PlatformJwtOptions.From(configuration.GetSection("Auth:Mobile"));
        var internalJwt = PlatformJwtOptions.From(configuration.GetSection("Auth:Internal"));
        var hasMobileJwt = mobileJwt.HasExplicitIssuerSource;
        var hasInternalJwt = internalJwt.HasExplicitIssuerSource;

        var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        if (hasMobileJwt)
        {
            authentication
                .AddPolicyScheme(
                    JwtBearerDefaults.AuthenticationScheme,
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.ForwardDefaultSelector = context => SelectExternalJwtScheme(context, defaultJwt);
                    })
                .AddJwtBearer(
                    EdgeJwtBearerScheme,
                    options => ConfigureJwtBearer(
                        options,
                        defaultJwt,
                        useTokenHeader: true,
                        useHubAccessTokenQuery: false))
                .AddJwtBearer(
                    MobileJwtBearerScheme,
                    options => ConfigureJwtBearer(
                        options,
                        mobileJwt,
                        useTokenHeader: false,
                        useHubAccessTokenQuery: true));
        }
        else
        {
            authentication.AddJwtBearer(
                options => ConfigureJwtBearer(
                    options,
                    defaultJwt,
                    useTokenHeader: true,
                    useHubAccessTokenQuery: false));
        }

        if (hasInternalJwt)
        {
            authentication.AddJwtBearer(
                InternalJwtBearerScheme,
                options => ConfigureJwtBearer(
                    options,
                    internalJwt,
                    useTokenHeader: false,
                    useHubAccessTokenQuery: false));
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
        bool useTokenHeader,
        bool useHubAccessTokenQuery)
    {
        if ((useTokenHeader && !string.IsNullOrEmpty(jwt.TokenHeader)) || useHubAccessTokenQuery)
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (useTokenHeader && !string.IsNullOrEmpty(jwt.TokenHeader))
                    {
                        var headerToken = context.Request.Headers[jwt.TokenHeader].FirstOrDefault();
                        if (!string.IsNullOrEmpty(headerToken))
                        {
                            context.Token = headerToken;
                            return Task.CompletedTask;
                        }
                    }

                    if (useHubAccessTokenQuery && TryGetHubAccessToken(context.HttpContext, out var queryToken))
                    {
                        context.Token = queryToken;
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

    private static string SelectExternalJwtScheme(HttpContext context, PlatformJwtOptions edgeJwt)
    {
        if (!string.IsNullOrEmpty(edgeJwt.TokenHeader)
            && context.Request.Headers.TryGetValue(edgeJwt.TokenHeader, out var edgeToken)
            && !string.IsNullOrEmpty(edgeToken.FirstOrDefault()))
        {
            return EdgeJwtBearerScheme;
        }

        if (HasBearerAuthorizationHeader(context) || TryGetHubAccessToken(context, out _))
        {
            return MobileJwtBearerScheme;
        }

        return EdgeJwtBearerScheme;
    }

    private static bool HasBearerAuthorizationHeader(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var header))
        {
            return false;
        }

        return header.ToString().StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetHubAccessToken(HttpContext context, out string accessToken)
    {
        accessToken = string.Empty;

        if (!context.Request.Path.StartsWithSegments(HubsPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        accessToken = context.Request.Query[AccessTokenQueryKey].FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(accessToken);
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
            var json = await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false);
            var config = new OpenIdConnectConfiguration();
            var jwks = new JsonWebKeySet(json);
            foreach (var key in jwks.GetSigningKeys())
            {
                config.SigningKeys.Add(key);
            }

            return config;
        }
    }
}
