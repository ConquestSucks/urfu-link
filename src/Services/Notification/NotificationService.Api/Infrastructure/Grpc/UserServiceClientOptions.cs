namespace Urfu.Link.Services.Notification.Infrastructure.Grpc;

public sealed class UserServiceClientOptions
{
    public const string SectionName = "UserService";

    public string GrpcEndpoint { get; set; } = "http://user-service:8081";

    public TimeSpan PreferencesCacheTtl { get; set; } = TimeSpan.FromMinutes(2);
}
