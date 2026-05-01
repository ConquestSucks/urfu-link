using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Applies user preferences (per-channel toggles, quiet hours, DND) on top of the candidate
/// channel set produced by <see cref="SeverityRouter"/>.
/// </summary>
public static class PreferenceFilter
{
    public static IReadOnlyList<DeliveryChannel> Filter(
        IReadOnlyList<DeliveryChannel> candidates,
        NotificationCategory category,
        NotificationSeverity severity,
        UserPreferences preferences,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(preferences);

        var quietActive = preferences.QuietHours.IsActive(utcNow);
        var bypass = severity == NotificationSeverity.Urgent;
        var dndActive = preferences.DndEnabled && !bypass;
        var toggle = preferences.GetToggle(category);

        var result = new List<DeliveryChannel>(candidates.Count);
        foreach (var channel in candidates)
        {
            switch (channel)
            {
                case DeliveryChannel.InApp:
                    if (toggle.InApp)
                    {
                        result.Add(DeliveryChannel.InApp);
                    }

                    break;
                case DeliveryChannel.Push:
                    if (toggle.Push && !dndActive && (!quietActive || bypass))
                    {
                        result.Add(DeliveryChannel.Push);
                    }

                    break;
                case DeliveryChannel.Email:
                    if (toggle.Email && severity >= NotificationSeverity.High && (!quietActive || bypass))
                    {
                        result.Add(DeliveryChannel.Email);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(candidates), channel, "Unknown channel.");
            }
        }

        return result;
    }
}
