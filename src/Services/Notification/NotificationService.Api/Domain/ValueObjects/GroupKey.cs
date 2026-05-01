namespace Urfu.Link.Services.Notification.Domain.ValueObjects;

public readonly record struct GroupKey(string Value)
{
    public const int MaxLength = 200;

    public static GroupKey ForDirectChat(Guid conversationId) => new($"chat:direct:{conversationId:N}");

    public static GroupKey ForChatMention(Guid conversationId) => new($"chat:mention:{conversationId:N}");

    public static GroupKey ForDisciplineChat(Guid conversationId) => new($"chat:disc:{conversationId:N}");

    public static GroupKey ForDisciplineAnnouncement(Guid disciplineId) => new($"disc:ann:{disciplineId:N}");

    public static GroupKey ForDisciplineMaterial(Guid disciplineId) => new($"disc:mat:{disciplineId:N}");

    public static GroupKey ForDisciplineDeadline(Guid disciplineId, Guid assignmentId)
        => new($"disc:dl:{disciplineId:N}:{assignmentId:N}");

    public static GroupKey ForCall(Guid callId) => new($"call:{callId:N}");

    public static GroupKey ForSystem(string updateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updateId);
        return new($"system:{updateId.Trim()}");
    }
}
