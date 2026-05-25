using FluentAssertions;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Domain;

public sealed class NotificationCatalogTests
{
    [Theory]
    [InlineData(NotificationCategory.ChatMessageDirect, "chat.message.direct")]
    [InlineData(NotificationCategory.ChatMessageMention, "chat.mention")]
    [InlineData(NotificationCategory.ChatMessageDiscipline, "chat.message.discipline")]
    [InlineData(NotificationCategory.ChatThreadReply, "chat.thread.reply")]
    [InlineData(NotificationCategory.ChatReplyToMe, "chat.reply_to_me")]
    [InlineData(NotificationCategory.ChatReaction, "chat.reaction.added")]
    [InlineData(NotificationCategory.ChatMessagePinned, "chat.message.pinned")]
    [InlineData(NotificationCategory.ChatParticipantChanged, "chat.participant.changed")]
    [InlineData(NotificationCategory.CallMissed, "call.missed")]
    [InlineData(NotificationCategory.DisciplineEnrollment, "discipline.enrollment")]
    [InlineData(NotificationCategory.DisciplineUnenrollment, "discipline.unenrollment")]
    [InlineData(NotificationCategory.DisciplineUpdated, "discipline.updated")]
    [InlineData(NotificationCategory.DisciplineDeleted, "discipline.deleted")]
    [InlineData(NotificationCategory.DisciplineDeadline, "discipline.deadline")]
    [InlineData(NotificationCategory.MediaAccessGranted, "media.access.granted")]
    [InlineData(NotificationCategory.MediaAccessRevoked, "media.access.revoked")]
    [InlineData(NotificationCategory.MediaUploadProcessed, "media.upload.processed")]
    [InlineData(NotificationCategory.MediaAssetDeleted, "media.asset.deleted")]
    [InlineData(NotificationCategory.SystemMaintenance, "system.maintenance")]
    public void GetByCategory_ReturnsStableFrontendType(NotificationCategory category, string expectedType)
    {
        var descriptor = NotificationCatalog.GetByCategory(category);

        descriptor.Type.Should().Be(expectedType);
        descriptor.Category.Should().Be(category);
        descriptor.Icon.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownType()
    {
        NotificationCatalog.TryGet("unknown.type", out _).Should().BeFalse();
    }
}
