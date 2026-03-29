namespace UserService.Api.Domain.ValueObjects;

public sealed record SoundVideoSettings(
    string? PlaybackDeviceId,
    string? RecordingDeviceId,
    string? WebcamDeviceId)
{
    public static readonly SoundVideoSettings Default = new(null, null, null);
}
