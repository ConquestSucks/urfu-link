namespace Urfu.Link.Services.Presence.Domain.ValueObjects;

public sealed record PrivacySettings(bool ShowOnlineStatus, bool ShowLastVisitTime)
{
    public static readonly PrivacySettings Default = new(true, true);
}
