namespace UserService.Api.Application.Contracts.Requests;

public sealed record UpdateSoundVideoRequest(
    Optional<string> PlaybackDeviceId = default,
    Optional<string> RecordingDeviceId = default,
    Optional<string> WebcamDeviceId = default);
