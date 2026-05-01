namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

/// <summary>
/// Configuration for the gRPC client that fans out typing signals to PresenceService.
/// Empty address disables the client and falls back to the no-op stub registered in DI —
/// appropriate for tests and on-prem profiles without presence.
/// </summary>
public sealed class PresenceServiceClientOptions
{
    public const string SectionName = "GrpcClients:PresenceService";

    public string Address { get; set; } = string.Empty;
}
