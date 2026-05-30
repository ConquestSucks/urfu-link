using FastEndpoints;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class MuteConversationNotificationsEndpoint(IUserRepository userRepository)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/me/notifications/muted-conversations/{conversationId}");
        Summary(s => s.Summary = "Disable notifications for one conversation");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var conversationId = Route<string>("conversationId")!;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            AddError("conversationId", "Conversation id is required.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            userRepository.Add(user);
        }

        user.MuteConversationNotifications(conversationId);
        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}

public sealed class UnmuteConversationNotificationsEndpoint(IUserRepository userRepository)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/me/notifications/muted-conversations/{conversationId}");
        Summary(s => s.Summary = "Enable notifications for one conversation");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var conversationId = Route<string>("conversationId")!;
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            AddError("conversationId", "Conversation id is required.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            userRepository.Add(user);
        }

        user.UnmuteConversationNotifications(conversationId);
        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
