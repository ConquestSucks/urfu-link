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
    private readonly Dictionary<Guid, ParticipantRole> _participantRoles;

    private Conversation(
        string id,
        ConversationType type,
        IEnumerable<Guid> participants,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastMessageAtUtc,
        MessagePreview? lastMessagePreview,
        IEnumerable<Guid>? pinnedMessageIds,
        IReadOnlyDictionary<Guid, ParticipantRole>? participantRoles,
        Guid? disciplineId,
        DateTimeOffset? archivedAtUtc)
    {
        Id = id;
        Type = type;
        _participants = participants.ToList();
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = lastMessageAtUtc;
        LastMessagePreview = lastMessagePreview;
        _pinnedMessageIds = pinnedMessageIds?.ToList() ?? [];
        _participantRoles = participantRoles is null
            ? new Dictionary<Guid, ParticipantRole>()
            : new Dictionary<Guid, ParticipantRole>(participantRoles);
        DisciplineId = disciplineId;
        ArchivedAtUtc = archivedAtUtc;
    }

    public string Id { get; }

    public ConversationType Type { get; }

    public IReadOnlyList<Guid> Participants => _participants;

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset LastMessageAtUtc { get; private set; }

    public MessagePreview? LastMessagePreview { get; private set; }

    public IReadOnlyList<Guid> PinnedMessageIds => _pinnedMessageIds;

    /// <summary>
    /// Optional discipline binding — non-null only for conversations sourced from a Discipline.
    /// Used by the consumer to look up an existing discipline conversation idempotently.
    /// </summary>
    public Guid? DisciplineId { get; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc.HasValue;

    public IReadOnlyDictionary<Guid, ParticipantRole> ParticipantRoles => _participantRoles;

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
            pinnedMessageIds: null,
            participantRoles: null,
            disciplineId: null,
            archivedAtUtc: null);
    }

    /// <summary>
    /// Opens a group conversation that belongs to a discipline. The owner teacher is
    /// added as the first participant with <see cref="ParticipantRole.Teacher"/>.
    /// Id is deterministic so concurrent <c>DisciplineCreated</c> deliveries collapse
    /// onto the same document via the unique-key insert.
    /// </summary>
    public static Conversation OpenDiscipline(
        Guid disciplineId,
        Guid ownerTeacherId,
        DateTimeOffset nowUtc)
    {
        if (disciplineId == Guid.Empty)
        {
            throw new ArgumentException("Discipline id is required.", nameof(disciplineId));
        }

        if (ownerTeacherId == Guid.Empty)
        {
            throw new ArgumentException("Owner teacher id is required.", nameof(ownerTeacherId));
        }

        return new Conversation(
            ComputeDisciplineId(disciplineId),
            ConversationType.Group,
            [ownerTeacherId],
            nowUtc,
            nowUtc,
            lastMessagePreview: null,
            pinnedMessageIds: null,
            participantRoles: new Dictionary<Guid, ParticipantRole>
            {
                [ownerTeacherId] = ParticipantRole.Teacher,
            },
            disciplineId: disciplineId,
            archivedAtUtc: null);
    }

    public static Conversation Hydrate(
        string id,
        ConversationType type,
        IEnumerable<Guid> participants,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastMessageAtUtc,
        MessagePreview? lastMessagePreview,
        IEnumerable<Guid>? pinnedMessageIds = null,
        IReadOnlyDictionary<Guid, ParticipantRole>? participantRoles = null,
        Guid? disciplineId = null,
        DateTimeOffset? archivedAtUtc = null)
        => new(
            id,
            type,
            participants,
            createdAtUtc,
            lastMessageAtUtc,
            lastMessagePreview,
            pinnedMessageIds,
            participantRoles,
            disciplineId,
            archivedAtUtc);

    public bool IsParticipant(Guid userId) => _participants.Contains(userId);

    /// <summary>
    /// Returns the role of <paramref name="userId"/> inside this conversation. For direct
    /// chats and group rows persisted before role tracking existed the result is
    /// <see cref="ParticipantRole.Member"/>.
    /// </summary>
    public ParticipantRole RoleOf(Guid userId)
        => _participantRoles.TryGetValue(userId, out var role) ? role : ParticipantRole.Member;

    public bool IsTeacher(Guid userId) => RoleOf(userId) == ParticipantRole.Teacher;

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

    /// <summary>
    /// Adds <paramref name="userId"/> with the supplied <paramref name="role"/> if not already
    /// present. Returns <c>false</c> when the user is already a participant (idempotent).
    /// </summary>
    public bool AddParticipant(Guid userId, ParticipantRole role)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (_participants.Contains(userId))
        {
            return false;
        }

        _participants.Add(userId);
        _participantRoles[userId] = role;
        return true;
    }

    public bool RemoveParticipant(Guid userId)
    {
        if (!_participants.Remove(userId))
        {
            return false;
        }

        _participantRoles.Remove(userId);
        return true;
    }

    public bool ChangeParticipantRole(Guid userId, ParticipantRole newRole)
    {
        if (!_participants.Contains(userId))
        {
            return false;
        }

        _participantRoles[userId] = newRole;
        return true;
    }

    public void Archive(DateTimeOffset nowUtc)
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAtUtc = nowUtc;
    }

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

    private static string ComputeDisciplineId(Guid disciplineId)
        => $"discipline:{disciplineId.ToString("N", CultureInfo.InvariantCulture)}";
}
