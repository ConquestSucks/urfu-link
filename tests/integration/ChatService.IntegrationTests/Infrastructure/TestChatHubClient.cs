using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Builds a <see cref="HubConnection"/> wired to the in-process TestServer. The TestServer does
/// not support real WebSocket upgrades, so we use LongPolling — protocol-equivalent for these
/// tests. Real WebSocket transport is exercised end-to-end via the gateway smoke step.
/// </summary>
public static class TestChatHubClient
{
    public static async Task<HubConnection> ConnectAsync(ChatServiceFactory factory, Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.Authenticated(userId);

        var url = new Uri(factory.Server.BaseAddress, "hubs/chat");
        var connection = new HubConnectionBuilder()
            .WithUrl(url, opts =>
            {
                opts.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
