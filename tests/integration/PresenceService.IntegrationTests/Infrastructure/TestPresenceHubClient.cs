using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Urfu.Link.Services.Presence.Domain.Enums;

namespace PresenceService.IntegrationTests.Infrastructure;

/// <summary>
/// Helper that builds a SignalR <see cref="HubConnection"/> wired to the
/// in-process TestServer. <c>TestServer</c> does not support real WebSocket
/// upgrades, so we stick to LongPolling — protocol-equivalent for this MVP.
/// Real WebSocket is exercised via the gateway smoke step.
/// </summary>
public static class TestPresenceHubClient
{
    public static async Task<HubConnection> ConnectAsync(
        PresenceServiceFactory factory,
        Guid userId,
        Platform platform = Platform.Web,
        string? deviceId = null)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(userId, deviceId);
        var resolvedDeviceId = deviceId ?? Guid.NewGuid().ToString("N");

        var url = new Uri(factory.Server.BaseAddress, $"hubs/presence?platform={platform}&deviceId={resolvedDeviceId}");
        var connection = new HubConnectionBuilder()
            .WithUrl(url, opts =>
            {
                opts.HttpMessageHandlerFactory = _ => new FailingResponseBodyHandler(factory.Server.CreateHandler());
                opts.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    private sealed class FailingResponseBodyHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var body = response.Content is null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"SignalR test request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}
