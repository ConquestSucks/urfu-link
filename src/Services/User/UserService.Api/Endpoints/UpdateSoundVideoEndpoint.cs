using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UpdateSoundVideoEndpoint(IUserRepository userRepository)
    : Endpoint<UpdateSoundVideoRequest>
{
    public override void Configure()
    {
        Patch("/me/sound-video");
        Summary(s => s.Summary = "Partially update sound and video device settings");
    }

    public override async Task HandleAsync(UpdateSoundVideoRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            user.UpdateSoundVideo(
                req.PlaybackDeviceId.HasValue ? req.PlaybackDeviceId.Value : null,
                req.RecordingDeviceId.HasValue ? req.RecordingDeviceId.Value : null,
                req.WebcamDeviceId.HasValue ? req.WebcamDeviceId.Value : null);
            userRepository.Add(user);
        }
        else
        {
            user.UpdateSoundVideo(
                req.PlaybackDeviceId.HasValue ? req.PlaybackDeviceId.Value : user.SoundVideo.PlaybackDeviceId,
                req.RecordingDeviceId.HasValue ? req.RecordingDeviceId.Value : user.SoundVideo.RecordingDeviceId,
                req.WebcamDeviceId.HasValue ? req.WebcamDeviceId.Value : user.SoundVideo.WebcamDeviceId);
        }

        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
