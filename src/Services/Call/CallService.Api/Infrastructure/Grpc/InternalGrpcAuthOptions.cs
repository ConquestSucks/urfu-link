namespace Urfu.Link.Services.Call.Infrastructure.Grpc;

public sealed class InternalGrpcAuthOptions
{
    public const string SectionName = "GrpcClients:InternalAuth";

    public string TokenEndpoint { get; set; } = string.Empty;

    public string ClientId { get; set; } = "chat-service-internal";

    public string ClientSecret { get; set; } = string.Empty;

    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromSeconds(30);

    internal bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(TokenEndpoint)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}
