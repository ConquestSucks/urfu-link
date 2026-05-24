namespace Urfu.Link.Services.Presence.Infrastructure.Auth;

public static class InternalGrpcAuthorizationPolicy
{
    public const string PolicyName = "PresenceInternalApi";

    public const string PresenceInternalRole = "service:presence-internal";

    public static readonly string[] AllowedRoles =
    [
        PresenceInternalRole,
    ];
}
