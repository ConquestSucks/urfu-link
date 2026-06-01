namespace UserService.Api.Domain.ValueObjects;

public sealed record SoundVideoSettings(
    string? PlaybackDeviceId,
    string? RecordingDeviceId,
    string? WebcamDeviceId,
    string? WebcamFacingMode)
{
    public static readonly SoundVideoSettings Default = new(null, null, null, null);
}
