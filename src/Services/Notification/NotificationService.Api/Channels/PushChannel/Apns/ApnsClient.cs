using dotAPNS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Urfu.Link.Services.Notification.Channels.PushChannel.Apns;

/// <summary>
/// Apple Push Notification adapter built on top of <c>dotAPNS</c>. JWT (.p8) auth is
/// configured once at construction and reused for the lifetime of the singleton.
/// </summary>
public sealed class ApnsClient : IApnsClient
{
    private readonly Lazy<dotAPNS.IApnsClient> _client;
    private readonly ILogger<ApnsClient> _logger;

    public ApnsClient(IOptions<ApnsOptions> options, IHttpClientFactory httpClientFactory, ILogger<ApnsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        var settings = options.Value;
        _logger = logger;

        _client = new Lazy<dotAPNS.IApnsClient>(() =>
        {
            var jwtOptions = new ApnsJwtOptions
            {
                BundleId = settings.BundleId,
                KeyId = settings.KeyId,
                TeamId = settings.TeamId,
                CertFilePath = settings.P8KeyPath,
            };

            return dotAPNS.ApnsClient.CreateUsingJwt(httpClientFactory.CreateClient("apns"), jwtOptions);
        });
    }

    public async Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _ = cancellationToken;

        var push = new ApplePush(ApplePushType.Alert)
            .AddAlert(payload.Title, payload.Body)
            .AddToken(payload.Token);

        if (!string.IsNullOrWhiteSpace(payload.GroupKey))
        {
            push.AddCustomProperty("thread-id", payload.GroupKey);
        }

        if (!string.IsNullOrWhiteSpace(payload.DeepLink))
        {
            push.AddCustomProperty("deepLink", payload.DeepLink);
        }

        foreach (var (key, value) in payload.Data)
        {
            push.AddCustomProperty(key, value);
        }

        try
        {
            var response = await _client.Value.SendAsync(push).ConfigureAwait(false);
            if (response.IsSuccessful)
            {
                return PushSendResult.Success(Guid.NewGuid().ToString("N"));
            }

            return MapError(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "APNs HTTP request failed");
            return PushSendResult.RetryLater(ex.Message);
        }
    }

    private static PushSendResult MapError(ApnsResponse response) => response.Reason switch
    {
        ApnsResponseReason.BadDeviceToken or ApnsResponseReason.Unregistered or ApnsResponseReason.DeviceTokenNotForTopic
            => PushSendResult.TokenInvalid(response.Reason.ToString()),
        ApnsResponseReason.TooManyRequests or ApnsResponseReason.IdleTimeout or ApnsResponseReason.ServiceUnavailable
            => PushSendResult.RetryLater(response.Reason.ToString()),
        _ => PushSendResult.Failed(response.Reason.ToString()),
    };
}
