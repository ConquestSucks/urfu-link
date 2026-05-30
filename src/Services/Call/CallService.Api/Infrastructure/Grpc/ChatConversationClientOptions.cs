namespace Urfu.Link.Services.Call.Infrastructure.Grpc;

public sealed class ChatConversationClientOptions
{
    public const string SectionName = "GrpcClients:ChatService";

    public string Address { get; set; } = string.Empty;
}
