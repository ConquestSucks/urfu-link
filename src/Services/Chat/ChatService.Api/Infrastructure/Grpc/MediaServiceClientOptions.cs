namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

public sealed class MediaServiceClientOptions
{
    public const string SectionName = "GrpcClients:MediaService";

    public string Address { get; set; } = string.Empty;
}
