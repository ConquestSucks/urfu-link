using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class GetMyProfileEndpoint(IUserRepository userRepository)
    : EndpointWithoutRequest<UserProfileResponse>
{
    public override void Configure()
    {
        Get("/me");
        Summary(s => s.Summary = "Get current user profile and settings");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();

        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            userRepository.Add(user);
            await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await HttpContext.Response.SendAsync(MapToResponse(user), cancellation: ct).ConfigureAwait(false);
    }

    private static UserProfileResponse MapToResponse(UserProfile user)
    {
        return new UserProfileResponse(
            UserId: user.Id,
            Account: new AccountResponse(user.Account.AvatarUrl, user.Account.AboutMe),
            Privacy: new PrivacyResponse(user.Privacy.ShowOnlineStatus, user.Privacy.ShowLastVisitTime),
            Notifications: new NotificationsResponse(
                user.Notifications.NewMessages,
                user.Notifications.NotificationSound,
                user.Notifications.DisciplineChatMessages,
                user.Notifications.Mentions),
            SoundVideo: new SoundVideoResponse(
                user.SoundVideo.PlaybackDeviceId,
                user.SoundVideo.RecordingDeviceId,
                user.SoundVideo.WebcamDeviceId));
    }
}
