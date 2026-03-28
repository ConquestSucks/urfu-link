namespace UserService.Api.Domain.ValueObjects;

public sealed record AccountSettings(string? AvatarUrl, string? AboutMe)
{
    public static readonly AccountSettings Default = new(null, null);
}
