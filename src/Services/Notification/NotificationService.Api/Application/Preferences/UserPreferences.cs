using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Preferences;

public sealed record UserPreferences(
    IReadOnlyDictionary<NotificationCategory, ChannelToggle> Categories,
    QuietHours QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound)
{
    public ChannelToggle GetToggle(NotificationCategory category)
        => Categories.TryGetValue(category, out var toggle) ? toggle : ChannelToggle.AllOn;

    public static UserPreferences Default { get; } = new(
        BuildDefault(),
        QuietHours.Disabled("Asia/Yekaterinburg"),
        DndEnabled: false,
        Locale: "ru-RU",
        Sound: true);

    private static Dictionary<NotificationCategory, ChannelToggle> BuildDefault()
    {
        var dict = new Dictionary<NotificationCategory, ChannelToggle>();
        foreach (NotificationCategory cat in Enum.GetValues<NotificationCategory>())
        {
            dict[cat] = ChannelToggle.AllOn;
        }

        return dict;
    }
}

public sealed record ChannelToggle(bool Push, bool Email, bool InApp)
{
    public static ChannelToggle AllOn { get; } = new(true, true, true);

    public static ChannelToggle InAppOnly { get; } = new(false, false, true);

    public static ChannelToggle AllOff { get; } = new(false, false, false);
}
