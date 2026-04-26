namespace Urfu.Link.BuildingBlocks.Contracts.Integration.User;

public sealed record NotificationPreferencesPayload(
    IReadOnlyDictionary<int, ChannelTogglePayload> Categories,
    QuietHoursPayload QuietHours,
    bool DndEnabled,
    string Locale);

public sealed record ChannelTogglePayload(bool Push, bool Email, bool InApp);

public sealed record QuietHoursPayload(string IanaTimezone, string? Start, string? End, bool Enabled);
