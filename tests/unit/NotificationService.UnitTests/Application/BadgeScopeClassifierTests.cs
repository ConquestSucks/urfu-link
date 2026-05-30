using FluentAssertions;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Application;

public sealed class BadgeScopeClassifierTests
{
    [Fact]
    public void ClassifyUnreadScopes_DoesNotCountDirectNotificationsAsDisciplines()
    {
        var counts = BadgeScopeClassifier.ClassifyUnreadScopes(
            [
                new BadgeScopeInput(NotificationCategory.ChatMessageDirect, "direct-1"),
            ],
            disciplineConversationIds: new HashSet<string>(StringComparer.Ordinal));

        counts["chats"].Should().Be(1);
        counts["disciplines"].Should().Be(0);
    }

    [Fact]
    public void ClassifyUnreadScopes_UsesDisciplineConversationProjectionForChatEvents()
    {
        var counts = BadgeScopeClassifier.ClassifyUnreadScopes(
            [
                new BadgeScopeInput(NotificationCategory.ChatMessageMention, "discipline-1"),
                new BadgeScopeInput(NotificationCategory.CallIncoming, "discipline-1"),
                new BadgeScopeInput(NotificationCategory.ChatMessagePinned, "direct-1"),
            ],
            disciplineConversationIds: new HashSet<string>(["discipline-1"], StringComparer.Ordinal));

        counts["chats"].Should().Be(1);
        counts["disciplines"].Should().Be(2);
    }
}
