namespace UserService.Api.Application.Contracts.Requests;

public sealed record UpdateNotificationPreferencesRequest(
    IReadOnlyDictionary<int, ChannelToggleRequest> Categories,
    QuietHoursRequest QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound);

public sealed record ChannelToggleRequest(bool Push, bool Email, bool InApp);

public sealed record QuietHoursRequest(string IanaTimezone, string? Start, string? End, bool Enabled);
