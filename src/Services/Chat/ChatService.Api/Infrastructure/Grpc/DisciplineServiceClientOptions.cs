namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

public sealed class DisciplineServiceClientOptions
{
    public const string SectionName = "GrpcClients:DisciplineService";

    public string Address { get; set; } = string.Empty;
}
