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
        DisciplineChatKind? disciplineChatKind,
        Guid? disciplineSubgroupId,
        string? disciplineTitle,
        string? disciplineSubgroupName,
        DateTimeOffset? archivedAtUtc,
        string? title,
        Guid? coverAssetId,
        GroupSubtype? groupSubtype,
        bool isAnnouncementOnly)
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
        DisciplineChatKind = disciplineChatKind;
        DisciplineSubgroupId = disciplineSubgroupId;
        DisciplineTitle = disciplineTitle;
        DisciplineSubgroupName = disciplineSubgroupName;
        ArchivedAtUtc = archivedAtUtc;
        Title = title;
        CoverAssetId = coverAssetId;
        GroupSubtype = groupSubtype;
        IsAnnouncementOnly = isAnnouncementOnly;
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

    public DisciplineChatKind? DisciplineChatKind { get; }

    public Guid? DisciplineSubgroupId { get; }

    public string? DisciplineTitle { get; private set; }

    public string? DisciplineSubgroupName { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc.HasValue;

    public IReadOnlyDictionary<Guid, ParticipantRole> ParticipantRoles => _participantRoles;

    /// <summary>
    /// Display title for group conversations. Mirrors the source aggregate's title — for
    /// discipline-bound groups it is kept in sync with the latest <c>discipline.updated.v1</c>
    /// event. Null for direct conversations.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    /// Optional cover image for group conversations. Mirrors the source aggregate's cover —
    /// for discipline-bound groups it tracks <c>discipline.updated.v1</c>. Null for direct.
    /// </summary>
    public Guid? CoverAssetId { get; private set; }

    /// <summary>
    /// Sub-classification of a <see cref="ConversationType.Group"/> conversation. Always
    /// <see cref="Enums.GroupSubtype.Discipline"/> for discipline-sourced groups; null for direct.
    /// </summary>
    public GroupSubtype? GroupSubtype { get; }

    /// <summary>
    /// When true, only Teachers (or admins) may post in the conversation. Default false. Toggle
    /// is intended to be controlled by Teachers/admins through a future endpoint; the domain
    /// already enforces it at <see cref="IsParticipant"/> sites in <c>SendMessageService</c>.
    /// </summary>
    public bool IsAnnouncementOnly { get; private set; }

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
            disciplineChatKind: null,
            disciplineSubgroupId: null,
            disciplineTitle: null,
            disciplineSubgroupName: null,
            archivedAtUtc: null,
            title: null,
            coverAssetId: null,
            groupSubtype: null,
            isAnnouncementOnly: false);
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
        DateTimeOffset nowUtc,
        string? title = null,
        Guid? coverAssetId = null)
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
            disciplineChatKind: Enums.DisciplineChatKind.General,
            disciplineSubgroupId: null,
            disciplineTitle: title,
            disciplineSubgroupName: null,
            archivedAtUtc: null,
            title: title,
            coverAssetId: coverAssetId,
            groupSubtype: Enums.GroupSubtype.Discipline,
            isAnnouncementOnly: false);
    }

    public static Conversation OpenDisciplineSubgroup(
        Guid disciplineId,
        Guid subgroupId,
        string disciplineTitle,
        string subgroupName,
        IEnumerable<Guid> teacherUserIds,
        IEnumerable<Guid> studentUserIds,
        DateTimeOffset nowUtc,
        Guid? coverAssetId = null)
    {
        if (disciplineId == Guid.Empty)
        {
            throw new ArgumentException("Discipline id is required.", nameof(disciplineId));
        }

        if (subgroupId == Guid.Empty)
        {
            throw new ArgumentException("Subgroup id is required.", nameof(subgroupId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subgroupName);

        var participants = new List<Guid>();
        var roles = new Dictionary<Guid, ParticipantRole>();
        foreach (var teacherId in teacherUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            participants.Add(teacherId);
            roles[teacherId] = ParticipantRole.Teacher;
        }

        foreach (var studentId in studentUserIds.Where(id => id != Guid.Empty).Distinct())
        {
            if (!roles.ContainsKey(studentId))
            {
                participants.Add(studentId);
            }

            roles[studentId] = ParticipantRole.Student;
        }

        var normalizedSubgroupName = subgroupName.Trim();
        return new Conversation(
            ComputeDisciplineSubgroupId(disciplineId, subgroupId),
            ConversationType.Group,
            participants,
            nowUtc,
            nowUtc,
            lastMessagePreview: null,
            pinnedMessageIds: null,
            participantRoles: roles,
            disciplineId: disciplineId,
            disciplineChatKind: Enums.DisciplineChatKind.Subgroup,
            disciplineSubgroupId: subgroupId,
            disciplineTitle: disciplineTitle,
            disciplineSubgroupName: normalizedSubgroupName,
            archivedAtUtc: null,
            title: normalizedSubgroupName,
            coverAssetId: coverAssetId,
            groupSubtype: Enums.GroupSubtype.Discipline,
            isAnnouncementOnly: false);
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
        DisciplineChatKind? disciplineChatKind = null,
        Guid? disciplineSubgroupId = null,
        string? disciplineTitle = null,
        string? disciplineSubgroupName = null,
        DateTimeOffset? archivedAtUtc = null,
        string? title = null,
        Guid? coverAssetId = null,
        GroupSubtype? groupSubtype = null,
        bool isAnnouncementOnly = false)
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
            disciplineChatKind,
            disciplineSubgroupId,
            disciplineTitle,
            disciplineSubgroupName,
            archivedAtUtc,
            title,
            coverAssetId,
            groupSubtype,
            isAnnouncementOnly);

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

    /// <summary>
    /// Refreshes the display metadata from the source aggregate (called from
    /// <c>discipline.updated.v1</c> handlers). Always overwrites — partial updates are not
    /// supported because the source publishes the full snapshot.
    /// </summary>
    public void UpdateMetadata(string? title, Guid? coverAssetId)
    {
        Title = title;
        if (DisciplineChatKind == Enums.DisciplineChatKind.General)
        {
            DisciplineTitle = title;
        }

        CoverAssetId = coverAssetId;
    }

    public void UpdateDisciplineMetadata(string? disciplineTitle, Guid? coverAssetId)
    {
        DisciplineTitle = disciplineTitle;
        if (DisciplineChatKind == Enums.DisciplineChatKind.General)
        {
            Title = disciplineTitle;
        }

        CoverAssetId = coverAssetId;
    }

    public void UpdateSubgroupMetadata(string subgroupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subgroupName);
        var normalized = subgroupName.Trim();
        DisciplineSubgroupName = normalized;
        if (DisciplineChatKind == Enums.DisciplineChatKind.Subgroup)
        {
            Title = normalized;
        }
    }

    /// <summary>
    /// Toggles the announcement-only flag. Authorisation of the caller is the responsibility
    /// of the application layer.
    /// </summary>
    public void SetAnnouncementOnly(bool value) => IsAnnouncementOnly = value;

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

    public static string ComputeDisciplineId(Guid disciplineId)
        => $"discipline:{disciplineId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string ComputeDisciplineSubgroupId(Guid disciplineId, Guid subgroupId)
        => $"discipline:{disciplineId.ToString("N", CultureInfo.InvariantCulture)}:subgroup:{subgroupId.ToString("N", CultureInfo.InvariantCulture)}";
}
