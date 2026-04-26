using FluentAssertions;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace NotificationService.UnitTests.Domain;

public sealed class GroupKeyTests
{
    [Fact]
    public void ForDirectChat_FormatsConversationId()
    {
        var convId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        GroupKey.ForDirectChat(convId).Value.Should().Be("chat:direct:11111111111111111111111111111111");
    }

    [Fact]
    public void ForChatMention_FormatsConversationId()
    {
        var convId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        GroupKey.ForChatMention(convId).Value.Should().Be("chat:mention:22222222222222222222222222222222");
    }

    [Fact]
    public void ForDisciplineChat_FormatsConversationId()
    {
        var convId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        GroupKey.ForDisciplineChat(convId).Value.Should().Be("chat:disc:33333333333333333333333333333333");
    }

    [Fact]
    public void ForDisciplineAnnouncement_FormatsDisciplineId()
    {
        var discId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        GroupKey.ForDisciplineAnnouncement(discId).Value.Should().Be("disc:ann:44444444444444444444444444444444");
    }

    [Fact]
    public void ForCall_FormatsCallId()
    {
        var callId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        GroupKey.ForCall(callId).Value.Should().Be("call:55555555555555555555555555555555");
    }

    [Fact]
    public void ForSystem_FormatsUpdateId()
    {
        var updateId = "release-2026-04";
        GroupKey.ForSystem(updateId).Value.Should().Be("system:release-2026-04");
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var convId = Guid.NewGuid();
        GroupKey.ForDirectChat(convId).Should().Be(GroupKey.ForDirectChat(convId));
        GroupKey.ForDirectChat(convId).Should().NotBe(GroupKey.ForChatMention(convId));
    }

    [Fact]
    public void ForSystem_RejectsBlankUpdateId()
    {
        var act = () => GroupKey.ForSystem("   ");
        act.Should().Throw<ArgumentException>();
    }
}
