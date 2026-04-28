using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class ThreadSubscriptionTests
{
    private static readonly Guid Root = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid User = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset SubscribedAt = new(2026, 04, 25, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Subscribe_StoresRootUserAndReason()
    {
        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Manual, SubscribedAt);

        sub.RootMessageId.Should().Be(Root);
        sub.UserId.Should().Be(User);
        sub.Reason.Should().Be(ThreadSubscriptionReason.Manual);
        sub.SubscribedAtUtc.Should().Be(SubscribedAt);
    }

    [Fact]
    public void Subscribe_DefaultsLastActivityToSubscribedAt_WhenNotProvided()
    {
        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Manual, SubscribedAt);

        sub.LastActivityAtUtc.Should().Be(SubscribedAt);
    }

    [Fact]
    public void Subscribe_UsesProvidedLastActivity_WhenGiven()
    {
        var lastActivity = SubscribedAt.AddMinutes(-5);

        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Manual, SubscribedAt, lastActivity);

        sub.LastActivityAtUtc.Should().Be(lastActivity);
    }

    [Fact]
    public void Subscribe_RejectsEmptyRootMessageId()
    {
        var act = () => ThreadSubscription.Subscribe(Guid.Empty, User, ThreadSubscriptionReason.Manual, SubscribedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*rootMessageId*");
    }

    [Fact]
    public void Subscribe_RejectsEmptyUserId()
    {
        var act = () => ThreadSubscription.Subscribe(Root, Guid.Empty, ThreadSubscriptionReason.Manual, SubscribedAt);

        act.Should().Throw<ArgumentException>().WithMessage("*userId*");
    }

    [Theory]
    [InlineData(ThreadSubscriptionReason.Manual, ThreadSubscriptionReason.Mentioned, true)]
    [InlineData(ThreadSubscriptionReason.Manual, ThreadSubscriptionReason.Replied, true)]
    [InlineData(ThreadSubscriptionReason.Mentioned, ThreadSubscriptionReason.Replied, true)]
    public void EscalateReason_ToHigherPriority_SucceedsAndUpdates(
        ThreadSubscriptionReason initial, ThreadSubscriptionReason next, bool expectedChanged)
    {
        var sub = ThreadSubscription.Subscribe(Root, User, initial, SubscribedAt);

        var changed = sub.EscalateReason(next);

        changed.Should().Be(expectedChanged);
        sub.Reason.Should().Be(next);
    }

    [Theory]
    [InlineData(ThreadSubscriptionReason.Replied, ThreadSubscriptionReason.Mentioned)]
    [InlineData(ThreadSubscriptionReason.Replied, ThreadSubscriptionReason.Manual)]
    [InlineData(ThreadSubscriptionReason.Mentioned, ThreadSubscriptionReason.Manual)]
    public void EscalateReason_ToLowerOrEqualPriority_DoesNotDowngrade(
        ThreadSubscriptionReason initial, ThreadSubscriptionReason attempt)
    {
        var sub = ThreadSubscription.Subscribe(Root, User, initial, SubscribedAt);

        var changed = sub.EscalateReason(attempt);

        changed.Should().BeFalse();
        sub.Reason.Should().Be(initial);
    }

    [Fact]
    public void EscalateReason_SameLevel_NoChange()
    {
        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Mentioned, SubscribedAt);

        var changed = sub.EscalateReason(ThreadSubscriptionReason.Mentioned);

        changed.Should().BeFalse();
    }

    [Fact]
    public void TouchActivity_WithNewerTimestamp_Updates()
    {
        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Manual, SubscribedAt);
        var newer = SubscribedAt.AddMinutes(5);

        sub.TouchActivity(newer);

        sub.LastActivityAtUtc.Should().Be(newer);
    }

    [Fact]
    public void TouchActivity_WithOlderTimestamp_KeepsExisting()
    {
        var sub = ThreadSubscription.Subscribe(Root, User, ThreadSubscriptionReason.Manual, SubscribedAt);
        var older = SubscribedAt.AddMinutes(-5);

        sub.TouchActivity(older);

        sub.LastActivityAtUtc.Should().Be(SubscribedAt);
    }

    [Fact]
    public void Hydrate_RestoresAllFields()
    {
        var lastActivity = SubscribedAt.AddMinutes(30);

        var sub = ThreadSubscription.Hydrate(Root, User, ThreadSubscriptionReason.Replied, SubscribedAt, lastActivity);

        sub.RootMessageId.Should().Be(Root);
        sub.UserId.Should().Be(User);
        sub.Reason.Should().Be(ThreadSubscriptionReason.Replied);
        sub.SubscribedAtUtc.Should().Be(SubscribedAt);
        sub.LastActivityAtUtc.Should().Be(lastActivity);
    }
}
