namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

// gRPC адрес UserService (см. ChatService.Application.Users.IUserServiceClient).
// Конфигурируется через GrpcClients:UserService:Address.
public sealed class UserServiceClientOptions
{
    public const string SectionName = "GrpcClients:UserService";

    public string Address { get; set; } = string.Empty;
}
