namespace UserService.Api.Domain.ValueObjects;

public sealed record PrivacySettings(bool ShowLastVisitTime)
{
    public static readonly PrivacySettings Default = new(true);
}
