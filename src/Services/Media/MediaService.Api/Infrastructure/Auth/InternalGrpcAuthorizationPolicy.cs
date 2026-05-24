namespace MediaService.Api.Infrastructure.Auth;

public static class InternalGrpcAuthorizationPolicy
{
    public const string PolicyName = "MediaInternalApi";

    public const string MediaInternalRole = "service:media-internal";

    public static readonly string[] AllowedRoles =
    [
        MediaInternalRole,
    ];
}
