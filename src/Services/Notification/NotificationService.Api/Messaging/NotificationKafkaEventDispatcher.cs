using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Handlers.Call;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Discipline;
using Urfu.Link.Services.Notification.Application.Handlers.Media;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed record NotificationKafkaDispatchResult(string EventType, string Status, int Affected);

public sealed class NotificationKafkaEventDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NotificationKafkaDispatchResult> DispatchAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(scope);

        return eventType switch
        {
            "chat.message.sent.v1" => await RouteAsync(
                payload.Deserialize<ChatMessageSentEvent>(JsonOptions),
                scope.GetRequiredService<ChatMessageSentHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.mention.created.v1" => await RouteAsync(
                payload.Deserialize<ChatMentionCreatedEvent>(JsonOptions),
                scope.GetRequiredService<ChatMentionCreatedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.thread.reply_posted.v1" => await RouteAsync(
                payload.Deserialize<ChatThreadReplyPostedEvent>(JsonOptions),
                scope.GetRequiredService<ChatThreadReplyPostedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.reaction.added.v1" => await RouteAsync(
                payload.Deserialize<ChatReactionAddedEvent>(JsonOptions),
                scope.GetRequiredService<ChatReactionAddedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.message.pinned.v1" => await RouteAsync(
                payload.Deserialize<ChatMessagePinnedEvent>(JsonOptions),
                scope.GetRequiredService<ChatMessagePinnedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.participant_joined.v1" => await RouteAsync(
                payload.Deserialize<ChatParticipantJoinedEvent>(JsonOptions),
                scope.GetRequiredService<AdminChatInviteHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.participant_role_changed.v1" => await RouteAsync(
                payload.Deserialize<ChatParticipantRoleChangedEvent>(JsonOptions),
                scope.GetRequiredService<ChatParticipantRoleChangedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "chat.reaction.removed.v1" => await ArchiveReactionAsync(payload, scope, dryRun, cancellationToken)
                .ConfigureAwait(false),
            "chat.message.unpinned.v1" => await ArchivePinAsync(payload, scope, dryRun, cancellationToken)
                .ConfigureAwait(false),
            "chat.participant_left.v1" => await ArchiveParticipantAsync(payload, scope, dryRun, cancellationToken)
                .ConfigureAwait(false),
            "chat.message.deleted.v1" => await ArchiveMessageAsync(payload, scope, dryRun, cancellationToken)
                .ConfigureAwait(false),
            "chat.message.edited.v1" or "chat.conversation_archived.v1" or "chat.thread.subscription_changed.v1" =>
                new NotificationKafkaDispatchResult(eventType, "ignored", 0),
            "call.incoming.v1" => await RouteAsync(
                payload.Deserialize<CallIncomingEvent>(JsonOptions),
                scope.GetRequiredService<CallIncomingHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "call.missed.v1" => await RouteAsync(
                payload.Deserialize<CallMissedEvent>(JsonOptions),
                scope.GetRequiredService<CallMissedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "call.ended.v1" => await ArchiveCallAsync(payload, scope, dryRun, cancellationToken)
                .ConfigureAwait(false),
            "discipline.user_enrolled.v1" => await RouteAsync(
                payload.Deserialize<UserEnrolledEvent>(JsonOptions),
                scope.GetRequiredService<UserEnrolledHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.user_unenrolled.v1" => await RouteAsync(
                payload.Deserialize<UserUnenrolledEvent>(JsonOptions),
                scope.GetRequiredService<UserUnenrolledHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.enrollment_role_changed.v1" => await RouteAsync(
                payload.Deserialize<EnrollmentRoleChangedEvent>(JsonOptions),
                scope.GetRequiredService<EnrollmentRoleChangedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.announcement.v1" => await RouteAsync(
                payload.Deserialize<DisciplineAnnouncementEvent>(JsonOptions),
                scope.GetRequiredService<DisciplineAnnouncementHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.material.published.v1" => await RouteAsync(
                payload.Deserialize<DisciplineMaterialPublishedEvent>(JsonOptions),
                scope.GetRequiredService<DisciplineMaterialHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.deadline.approaching.v1" => await RouteAsync(
                payload.Deserialize<DisciplineDeadlineApproachingEvent>(JsonOptions),
                scope.GetRequiredService<DisciplineDeadlineHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "discipline.created.v1" or "discipline.updated.v1" or "discipline.deleted.v1" =>
                new NotificationKafkaDispatchResult(eventType, "ignored", 0),
            "media.access_granted.v1" => await RouteAsync(
                payload.Deserialize<MediaAccessGrantedEvent>(JsonOptions),
                scope.GetRequiredService<MediaAccessGrantedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "media.access_revoked.v1" => await RouteAsync(
                payload.Deserialize<MediaAccessRevokedEvent>(JsonOptions),
                scope.GetRequiredService<MediaAccessRevokedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "media.asset_uploaded.v1" => await RouteAsync(
                payload.Deserialize<MediaAssetUploadedEvent>(JsonOptions),
                scope.GetRequiredService<MediaAssetUploadedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "media.asset_deleted.v1" => await RouteAsync(
                payload.Deserialize<MediaAssetDeletedEvent>(JsonOptions),
                scope.GetRequiredService<MediaAssetDeletedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "media.asset_hard_deleted.v1" => new NotificationKafkaDispatchResult(eventType, "ignored", 0),
            "user.role_changed.v1" => await RouteAsync(
                payload.Deserialize<UserRoleChangedEvent>(JsonOptions),
                scope.GetRequiredService<AdminRoleChangedHandler>(),
                scope,
                dryRun,
                cancellationToken).ConfigureAwait(false),
            "user.notification_settings_changed.v1" => InvalidatePreferences(payload, scope),
            _ => new NotificationKafkaDispatchResult(eventType, "unsupported", 0),
        };
    }

    private static async Task<NotificationKafkaDispatchResult> RouteAsync<TEvent>(
        TEvent? integrationEvent,
        INotificationHandler<TEvent> handler,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
        where TEvent : Urfu.Link.BuildingBlocks.Contracts.Integration.IIntegrationEvent
    {
        if (integrationEvent is null)
        {
            throw new JsonException($"{typeof(TEvent).Name} payload null");
        }

        if (dryRun)
        {
            var intents = await handler.PrepareAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
            return new NotificationKafkaDispatchResult(integrationEvent.EventType, "dry-run", intents.Count);
        }

        var router = scope.GetRequiredService<NotificationRouter>();
        var outcome = await router.RouteAsync(integrationEvent, handler, cancellationToken).ConfigureAwait(false);
        return new NotificationKafkaDispatchResult(
            integrationEvent.EventType,
            "routed",
            outcome.Created + outcome.Updated + outcome.Skipped);
    }

    private static NotificationKafkaDispatchResult InvalidatePreferences(JsonNode payload, IServiceProvider scope)
    {
        var evt = payload.Deserialize<UserNotificationSettingsChangedEvent>(JsonOptions)
            ?? throw new JsonException("UserNotificationSettingsChangedEvent payload null");
        scope.GetRequiredService<IUserPreferencesClient>().Invalidate(evt.UserId);
        return new NotificationKafkaDispatchResult(evt.EventType, "cache-invalidated", 1);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchiveReactionAsync(
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var evt = payload.Deserialize<ChatReactionRemovedEvent>(JsonOptions)
            ?? throw new JsonException("ChatReactionRemovedEvent payload null");
        return await ArchiveAsync(
            evt.EventType,
            NotificationSourceActions.ChatReaction(evt.ConversationId, evt.MessageId, evt.UserId, evt.Emoji),
            evt.OccurredAtUtc,
            scope,
            dryRun,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchivePinAsync(
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var evt = payload.Deserialize<ChatMessageUnpinnedEvent>(JsonOptions)
            ?? throw new JsonException("ChatMessageUnpinnedEvent payload null");
        return await ArchiveAsync(
            evt.EventType,
            NotificationSourceActions.ChatPin(evt.ConversationId, evt.MessageId),
            evt.OccurredAtUtc,
            scope,
            dryRun,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchiveParticipantAsync(
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var evt = payload.Deserialize<ChatParticipantLeftEvent>(JsonOptions)
            ?? throw new JsonException("ChatParticipantLeftEvent payload null");
        return await ArchiveAsync(
            evt.EventType,
            NotificationSourceActions.ChatParticipant(evt.ConversationId, evt.UserId),
            evt.OccurredAtUtc,
            scope,
            dryRun,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchiveMessageAsync(
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var evt = payload.Deserialize<ChatMessageDeletedEvent>(JsonOptions)
            ?? throw new JsonException("ChatMessageDeletedEvent payload null");
        if (dryRun)
        {
            return new NotificationKafkaDispatchResult(evt.EventType, "dry-run-cleanup", 3);
        }

        var lifecycle = scope.GetRequiredService<NotificationLifecycleService>();
        var affected = 0;
        affected += await lifecycle.ArchiveBySourceActionAsync(
            NotificationSourceActions.ChatMessage(evt.ConversationId, evt.MessageId),
            evt.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
        affected += await lifecycle.ArchiveBySourceActionAsync(
            NotificationSourceActions.ChatThreadReply(evt.ConversationId, evt.MessageId),
            evt.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
        affected += await lifecycle.ArchiveBySourceActionAsync(
            NotificationSourceActions.ChatPin(evt.ConversationId, evt.MessageId),
            evt.OccurredAtUtc,
            cancellationToken).ConfigureAwait(false);
        return new NotificationKafkaDispatchResult(evt.EventType, "cleanup", affected);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchiveCallAsync(
        JsonNode payload,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var evt = payload.Deserialize<CallEndedEvent>(JsonOptions)
            ?? throw new JsonException("CallEndedEvent payload null");
        return await ArchiveAsync(
            evt.EventType,
            NotificationSourceActions.CallIncoming(evt.CallId),
            evt.OccurredAtUtc,
            scope,
            dryRun,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NotificationKafkaDispatchResult> ArchiveAsync(
        string eventType,
        string sourceActionId,
        DateTimeOffset occurredAtUtc,
        IServiceProvider scope,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            return new NotificationKafkaDispatchResult(eventType, "dry-run-cleanup", 1);
        }

        var affected = await scope.GetRequiredService<NotificationLifecycleService>()
            .ArchiveBySourceActionAsync(sourceActionId, occurredAtUtc, cancellationToken)
            .ConfigureAwait(false);
        return new NotificationKafkaDispatchResult(eventType, "cleanup", affected);
    }
}
