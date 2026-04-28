using System.Collections.Concurrent;
using Urfu.Link.Services.Chat.Application.Presence;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Test stand-in for <see cref="IPresenceServiceClient"/>. Records every SetTyping invocation
/// so tests can assert that ChatHub.StartTyping/StopTyping fanned out, and that
/// SendMessageService also fired StopTyping after a successful send.
/// </summary>
public sealed record FakeSetTypingRecord(string ConversationId, Guid UserId, bool IsTyping);

public sealed class FakePresenceServiceClient : IPresenceServiceClient
{
    private readonly ConcurrentBag<FakeSetTypingRecord> _records = new();

    public IReadOnlyCollection<FakeSetTypingRecord> Records => _records;

    public void Reset()
    {
        while (_records.TryTake(out _))
        {
            // drain
        }
    }

    public Task SetTypingAsync(string conversationId, Guid userId, bool isTyping, CancellationToken cancellationToken)
    {
        _records.Add(new FakeSetTypingRecord(conversationId, userId, isTyping));
        return Task.CompletedTask;
    }
}
