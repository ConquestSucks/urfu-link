using System.Globalization;
using FluentAssertions;
using NSubstitute;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class GetUserActiveThreadsQueryTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture);
    private const string ConvId = "conv-1";

    private readonly IThreadSubscriptionRepository _subscriptions = Substitute.For<IThreadSubscriptionRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();

    private GetUserActiveThreadsQuery Build() => new(_subscriptions, _messages);

    private static Message MakeRoot(Guid id, int replyCount = 0)
    {
        return Message.Hydrate(
            id, ConvId, Other, "root", Array.Empty<Attachment>(), $"c-{id}",
            MessageState.Sent, Now, null, null,
            threadReplyCount: replyCount);
    }

    [Fact]
    public async Task Execute_NoSubscriptions_ReturnsEmptyPage()
    {
        _subscriptions.ListUserActiveAsync(User, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ThreadSubscription>());

        var page = await Build().ExecuteAsync(User, null, null, default);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ProjectsSubscriptions_WithRoots()
    {
        var rootA = MakeRoot(Guid.NewGuid(), replyCount: 3);
        var rootB = MakeRoot(Guid.NewGuid(), replyCount: 7);
        var subA = ThreadSubscription.Subscribe(rootA.Id, User, ThreadSubscriptionReason.Replied, Now, Now.AddMinutes(10));
        var subB = ThreadSubscription.Subscribe(rootB.Id, User, ThreadSubscriptionReason.Mentioned, Now, Now.AddMinutes(5));

        _subscriptions.ListUserActiveAsync(User, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { subA, subB });
        _messages.GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { rootA, rootB });

        var page = await Build().ExecuteAsync(User, null, null, default);

        page.Items.Should().HaveCount(2);
        page.Items[0].RootMessageId.Should().Be(rootA.Id);
        page.Items[0].ReplyCount.Should().Be(3);
        page.Items[0].Reason.Should().Be(ThreadSubscriptionReason.Replied);
        page.Items[1].Reason.Should().Be(ThreadSubscriptionReason.Mentioned);
    }

    [Fact]
    public async Task Execute_FiltersOutDeletedRoots()
    {
        var deletedRoot = Message.Hydrate(
            Guid.NewGuid(), ConvId, Other, "", Array.Empty<Attachment>(), "c-deleted",
            MessageState.Deleted, Now, null, null,
            deletedAtUtc: Now, deletedBy: Other, deleteMode: DeleteMode.ForEveryone);
        var sub = ThreadSubscription.Subscribe(deletedRoot.Id, User, ThreadSubscriptionReason.Replied, Now, Now);

        _subscriptions.ListUserActiveAsync(User, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { sub });
        _messages.GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { deletedRoot });

        var page = await Build().ExecuteAsync(User, null, null, default);

        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_EncodesNextCursor_WhenMore()
    {
        var roots = new List<Message>();
        var subs = new List<ThreadSubscription>();
        for (int i = 0; i < 31; i++)
        {
            var root = MakeRoot(Guid.NewGuid(), replyCount: 1);
            roots.Add(root);
            subs.Add(ThreadSubscription.Subscribe(
                root.Id, User, ThreadSubscriptionReason.Replied, Now, Now.AddSeconds(i)));
        }

        _subscriptions.ListUserActiveAsync(User, null, 31, Arg.Any<CancellationToken>())
            .Returns(subs);
        _messages.GetManyAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(roots);

        var page = await Build().ExecuteAsync(User, null, 30, default);

        page.Items.Should().HaveCount(30);
        page.NextCursor.Should().NotBeNull();
    }
}
