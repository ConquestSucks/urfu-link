using System.Globalization;
using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Domain.ValueObjects;
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

    private UserProfileResponse MapToResponse(UserProfile user)
    {
        var prefs = user.Notifications;

        var categories = prefs.Categories.ToDictionary(
            kv => kv.Key,
            kv => new ChannelToggleResponse(kv.Value.Push, kv.Value.Email, kv.Value.InApp));

        var quietHours = new QuietHoursResponse(
            prefs.QuietHours.IanaTimezone,
            prefs.QuietHours.Start?.ToString("HH:mm", CultureInfo.InvariantCulture),
            prefs.QuietHours.End?.ToString("HH:mm", CultureInfo.InvariantCulture),
            prefs.QuietHours.Enabled);

        var legacyDirect = prefs.GetToggle(NotificationCategoryCode.ChatMessageDirect);
        var legacyDiscipline = prefs.GetToggle(NotificationCategoryCode.ChatMessageDiscipline);
        var legacyMentions = prefs.GetToggle(NotificationCategoryCode.ChatMessageMention);

        return new UserProfileResponse(
            UserId: user.Id,
            Identity: new IdentityResponse(
                Name: HttpContext.User.GetDisplayName(),
                Email: HttpContext.User.GetEmail(),
                Username: HttpContext.User.GetUsername()),
            Account: new AccountResponse(user.Account.AvatarUrl, user.Account.AboutMe),
            Privacy: new PrivacyResponse(user.Privacy.ShowOnlineStatus, user.Privacy.ShowLastVisitTime),
            Notifications: new NotificationsResponse(
                NewMessages: legacyDirect.Push || legacyDirect.InApp,
                NotificationSound: prefs.Sound,
                DisciplineChatMessages: legacyDiscipline.Push || legacyDiscipline.InApp,
                Mentions: legacyMentions.Push || legacyMentions.InApp,
                MutedConversationIds: prefs.MutedConversationIds),
            Preferences: new NotificationPreferencesResponse(
                categories,
                quietHours,
                prefs.DndEnabled,
                prefs.Locale,
                prefs.Sound,
                prefs.MutedConversationIds),
            SoundVideo: new SoundVideoResponse(
                user.SoundVideo.PlaybackDeviceId,
                user.SoundVideo.RecordingDeviceId,
                user.SoundVideo.WebcamDeviceId));
    }
}
