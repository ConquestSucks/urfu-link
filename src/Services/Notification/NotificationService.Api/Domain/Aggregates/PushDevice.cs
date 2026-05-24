using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Aggregates;

public sealed class PushDevice
{
    public const int TokenMaxLength = 500;
    public const int FingerprintMaxLength = 200;
    public const int PlatformMaxLength = 32;
    public const int AppVersionMaxLength = 32;
    public const int LocaleMaxLength = 16;
    public const int ReasonMaxLength = 200;

    public const string DefaultLocale = "ru-RU";

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public PushProvider Provider { get; private set; }

    public string Token { get; private set; } = null!;

    public string DeviceFingerprint { get; private set; } = null!;

    public string Platform { get; private set; } = null!;

    public string? AppVersion { get; private set; }

    public string Locale { get; private set; } = DefaultLocale;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public string? DeactivationReason { get; private set; }

    public DateTimeOffset? DeactivatedAtUtc { get; private set; }

    private PushDevice()
    {
    }

    public static PushDevice Register(
        Guid userId,
        PushProvider provider,
        string token,
        string deviceFingerprint,
        string platform,
        string? appVersion,
        string? locale,
        DateTimeOffset registeredAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        var trimmedToken = token.Trim();
        var trimmedFingerprint = deviceFingerprint.Trim();
        var trimmedPlatform = platform.Trim();
        var trimmedAppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim();
        var resolvedLocale = string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale.Trim();

        EnsureMaxLength(trimmedToken, TokenMaxLength, nameof(token));
        EnsureMaxLength(trimmedFingerprint, FingerprintMaxLength, nameof(deviceFingerprint));
        EnsureMaxLength(trimmedPlatform, PlatformMaxLength, nameof(platform));
        if (trimmedAppVersion is not null)
        {
            EnsureMaxLength(trimmedAppVersion, AppVersionMaxLength, nameof(appVersion));
        }

        EnsureMaxLength(resolvedLocale, LocaleMaxLength, nameof(locale));

        return new PushDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider,
            Token = trimmedToken,
            DeviceFingerprint = trimmedFingerprint,
            Platform = trimmedPlatform,
            AppVersion = trimmedAppVersion,
            Locale = resolvedLocale,
            CreatedAtUtc = registeredAtUtc,
            LastSeenAtUtc = registeredAtUtc,
            IsActive = true,
        };
    }

    public void Touch(DateTimeOffset atUtc)
    {
        if (atUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Touch timestamp precedes device registration.", nameof(atUtc));
        }

        LastSeenAtUtc = atUtc;
    }

    public void Deactivate(DateTimeOffset atUtc, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!IsActive)
        {
            return;
        }

        var trimmed = reason.Trim();
        EnsureMaxLength(trimmed, ReasonMaxLength, nameof(reason));

        IsActive = false;
        DeactivationReason = trimmed;
        DeactivatedAtUtc = atUtc;
    }

    public void Reactivate(string newToken, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newToken);
        var trimmed = newToken.Trim();
        EnsureMaxLength(trimmed, TokenMaxLength, nameof(newToken));

        Token = trimmed;
        IsActive = true;
        DeactivationReason = null;
        DeactivatedAtUtc = null;
        LastSeenAtUtc = atUtc;
    }

    public void RotateToken(string newToken, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newToken);
        var trimmed = newToken.Trim();
        EnsureMaxLength(trimmed, TokenMaxLength, nameof(newToken));
        Token = trimmed;
        LastSeenAtUtc = atUtc;
    }

    private static void EnsureMaxLength(string value, int max, string parameterName)
    {
        if (value.Length > max)
        {
            throw new ArgumentException($"Value exceeds {max} characters.", parameterName);
        }
    }
}
