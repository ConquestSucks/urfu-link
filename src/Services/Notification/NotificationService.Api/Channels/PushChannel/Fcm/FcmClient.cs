using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FcmMessage = FirebaseAdmin.Messaging.Message;
using FcmNotification = FirebaseAdmin.Messaging.Notification;

namespace Urfu.Link.Services.Notification.Channels.PushChannel.Fcm;

/// <summary>
/// Firebase Cloud Messaging adapter built on top of <c>FirebaseAdmin</c>. Handles the
/// usual error mapping: unregistered/invalid-argument tokens are signaled as
/// <see cref="PushSendOutcome.TokenInvalid"/>; quota and unavailable failures are
/// retriable.
/// </summary>
public sealed class FcmClient : IFcmClient, IDisposable
{
    private readonly Lazy<FirebaseMessaging> _messaging;
    private readonly ILogger<FcmClient> _logger;
    private FirebaseApp? _app;

    public FcmClient(IOptions<FcmOptions> options, ILogger<FcmClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;
        _logger = logger;

        _messaging = new Lazy<FirebaseMessaging>(() =>
        {
            if (string.IsNullOrWhiteSpace(settings.ServiceAccountPath) || !File.Exists(settings.ServiceAccountPath))
            {
                throw new InvalidOperationException(
                    $"FCM service-account JSON not found at '{settings.ServiceAccountPath}'.");
            }

            var credential = GoogleCredential.FromFile(settings.ServiceAccountPath);
            _app = FirebaseApp.Create(
                new AppOptions { Credential = credential, ProjectId = settings.ProjectId },
                $"notification-service-{Guid.NewGuid():N}");
            return FirebaseMessaging.GetMessaging(_app);
        });
    }

    public async Task<PushSendResult> SendAsync(PushPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var message = new FcmMessage
        {
            Token = payload.Token,
            Notification = new FcmNotification
            {
                Title = payload.Title,
                Body = payload.Body,
                ImageUrl = payload.ImageUrl,
            },
            Data = new Dictionary<string, string>(payload.Data, StringComparer.Ordinal),
            Android = new AndroidConfig
            {
                CollapseKey = payload.GroupKey,
                Priority = Priority.High,
            },
            Apns = new ApnsConfig
            {
                Aps = new Aps { ThreadId = payload.GroupKey, MutableContent = true },
            },
        };

        try
        {
            var providerMessageId = await _messaging.Value.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return PushSendResult.Success(providerMessageId);
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch)
        {
            _logger.LogInformation("FCM token rejected for {Token}: {Reason}", Truncate(payload.Token), ex.MessagingErrorCode);
            return PushSendResult.TokenInvalid((ex.MessagingErrorCode ?? MessagingErrorCode.Unregistered).ToString());
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
        {
            _logger.LogWarning(ex, "FCM rejected payload as invalid for {Token}", Truncate(payload.Token));
            return PushSendResult.TokenInvalid("InvalidArgument");
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode is MessagingErrorCode.QuotaExceeded or MessagingErrorCode.Unavailable or MessagingErrorCode.Internal)
        {
            return PushSendResult.RetryLater((ex.MessagingErrorCode ?? MessagingErrorCode.Unavailable).ToString());
        }
        catch (FirebaseMessagingException ex)
        {
            return PushSendResult.Failed($"{ex.MessagingErrorCode}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _app?.Delete();
    }

    private static string Truncate(string token) => token.Length <= 8 ? token : token[..8] + "…";
}
