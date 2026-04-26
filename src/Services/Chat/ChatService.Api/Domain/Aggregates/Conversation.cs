using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Aggregates;

public sealed class Conversation
{
    private readonly List<Guid> _participants;
    private readonly List<Guid> _pinnedMessageIds;

    private Conversation(
        string id,
        ConversationType type,
        IEnumerable<Guid> participants,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastMessageAtUtc,
        MessagePreview? lastMessagePreview,
        IEnumerable<Guid>? pinnedMessageIds)
    {
        Id = id;
        Type = type;
        _participants = participants.ToList();
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = lastMessageAtUtc;
        LastMessagePreview = lastMessagePreview;
        _pinnedMessageIds = pinnedMessageIds?.ToList() ?? [];
    }

    public string Id { get; }

    public ConversationType Type { get; }

    public IReadOnlyList<Guid> Participants => _participants;

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset LastMessageAtUtc { get; private set; }

    public MessagePreview? LastMessagePreview { get; private set; }

    public IReadOnlyList<Guid> PinnedMessageIds => _pinnedMessageIds;

    public static Conversation OpenDirect(Guid userA, Guid userB, DateTimeOffset nowUtc)
    {
        if (userA == userB)
        {
            throw new ArgumentException("Cannot open a direct conversation as a self-chat.", nameof(userB));
        }

        var (lo, hi) = userA.CompareTo(userB) < 0 ? (userA, userB) : (userB, userA);
        var id = ComputeDirectId(lo, hi);

        return new Conversation(
            id,
            ConversationType.Direct,
            new[] { lo, hi },
            nowUtc,
            nowUtc,
            lastMessagePreview: null,
            pinnedMessageIds: null);
    }

    public static Conversation Hydrate(
        string id,
        ConversationType type,
        IEnumerable<Guid> participants,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastMessageAtUtc,
        MessagePreview? lastMessagePreview,
        IEnumerable<Guid>? pinnedMessageIds = null)
        => new(id, type, participants, createdAtUtc, lastMessageAtUtc, lastMessagePreview, pinnedMessageIds);

    public bool IsParticipant(Guid userId) => _participants.Contains(userId);

    public void RegisterMessage(MessagePreview preview, DateTimeOffset sentAtUtc)
    {
        ArgumentNullException.ThrowIfNull(preview);
        LastMessagePreview = preview;
        LastMessageAtUtc = sentAtUtc;
    }

    public bool IsPinned(Guid messageId) => _pinnedMessageIds.Contains(messageId);

    /// <summary>
    /// Pins the message inside the in-memory aggregate. Returns <c>false</c> if the message is
    /// already pinned (idempotent) or if the pinned-list is at <paramref name="maxPinned"/>.
    /// Callers should distinguish the two by inspecting <see cref="IsPinned"/> before invoking.
    /// </summary>
    public bool PinMessage(Guid messageId, int maxPinned)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPinned);

        if (IsPinned(messageId))
        {
            return false;
        }

        if (_pinnedMessageIds.Count >= maxPinned)
        {
            return false;
        }

        _pinnedMessageIds.Add(messageId);
        return true;
    }

    public bool UnpinMessage(Guid messageId) => _pinnedMessageIds.Remove(messageId);

    // SHA1 is used here as a non-cryptographic deterministic hash to derive a stable
    // identifier for a sorted user pair. Collision resistance for the (Guid, Guid) input
    // space is sufficient and the ID is not a secret.
#pragma warning disable CA5350
    private static string ComputeDirectId(Guid lo, Guid hi)
    {
        var key = $"{lo.ToString("N", CultureInfo.InvariantCulture)}:{hi.ToString("N", CultureInfo.InvariantCulture)}";
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(key));
#pragma warning disable CA1308
        return Convert.ToHexString(hash).ToLowerInvariant();
#pragma warning restore CA1308
    }
#pragma warning restore CA5350
}
