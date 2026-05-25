using FluentAssertions;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Domain;

public sealed class NotificationPriorityPolicyTests
{
    [Theory]
    [InlineData(NotificationPriority.ChatMessage, NotificationPriority.Mention, true)]
    [InlineData(NotificationPriority.ThreadReply, NotificationPriority.ReplyToMe, true)]
    [InlineData(NotificationPriority.Reaction, NotificationPriority.ChatMessage, true)]
    [InlineData(NotificationPriority.Mention, NotificationPriority.ChatMessage, false)]
    [InlineData(NotificationPriority.ChatMessage, NotificationPriority.ChatMessage, false)]
    public void ShouldUpgrade_ReturnsTrueOnlyForHigherPriority(
        NotificationPriority existing,
        NotificationPriority candidate,
        bool expected)
    {
        NotificationPriorityPolicy.ShouldUpgrade(existing, candidate).Should().Be(expected);
    }
}
