namespace UserService.Api.Domain.ValueObjects;

public sealed record ChannelToggle(bool Push, bool Email, bool InApp)
{
    public static ChannelToggle AllOn { get; } = new(true, true, true);

    public static ChannelToggle InAppOnly { get; } = new(false, false, true);

    public static ChannelToggle AllOff { get; } = new(false, false, false);
}
