using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace ChatService.UnitTests.Domain;

public sealed class ConversationDisciplineTests
{
    [Fact]
    public void OpenDiscipline_GeneratesDeterministicIdAndAddsOwnerAsTeacher()
    {
        var disciplineId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var teacherId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var conv = Conversation.OpenDiscipline(disciplineId, teacherId, now);

        conv.Id.Should().Be("discipline:11111111222233334444555555555555");
        conv.Type.Should().Be(ConversationType.Group);
        conv.DisciplineId.Should().Be(disciplineId);
        conv.IsArchived.Should().BeFalse();
        conv.Participants.Should().ContainSingle().Which.Should().Be(teacherId);
        conv.RoleOf(teacherId).Should().Be(ParticipantRole.Teacher);
        conv.IsTeacher(teacherId).Should().BeTrue();
    }

    [Fact]
    public void OpenDiscipline_DeterministicAcrossInvocations()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = Conversation.OpenDiscipline(disciplineId, teacherId, now);
        var second = Conversation.OpenDiscipline(disciplineId, teacherId, now);

        first.Id.Should().Be(second.Id);
    }

    [Fact]
    public void AddParticipant_NewUser_PersistsRole()
    {
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var studentId = Guid.NewGuid();

        var added = conv.AddParticipant(studentId, ParticipantRole.Student);

        added.Should().BeTrue();
        conv.IsParticipant(studentId).Should().BeTrue();
        conv.RoleOf(studentId).Should().Be(ParticipantRole.Student);
    }

    [Fact]
    public void AddParticipant_AlreadyPresent_ReturnsFalse()
    {
        var teacherId = Guid.NewGuid();
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), teacherId, DateTimeOffset.UtcNow);

        var added = conv.AddParticipant(teacherId, ParticipantRole.Teacher);

        added.Should().BeFalse();
        conv.Participants.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveParticipant_DropsRole()
    {
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var studentId = Guid.NewGuid();
        conv.AddParticipant(studentId, ParticipantRole.Student);

        var removed = conv.RemoveParticipant(studentId);

        removed.Should().BeTrue();
        conv.IsParticipant(studentId).Should().BeFalse();
        conv.RoleOf(studentId).Should().Be(ParticipantRole.Member);
    }

    [Fact]
    public void ChangeParticipantRole_PromotesStudentToTeacher()
    {
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var studentId = Guid.NewGuid();
        conv.AddParticipant(studentId, ParticipantRole.Student);

        var changed = conv.ChangeParticipantRole(studentId, ParticipantRole.Teacher);

        changed.Should().BeTrue();
        conv.IsTeacher(studentId).Should().BeTrue();
    }

    [Fact]
    public void ChangeParticipantRole_NonMember_ReturnsFalse()
    {
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var changed = conv.ChangeParticipantRole(Guid.NewGuid(), ParticipantRole.Teacher);

        changed.Should().BeFalse();
    }

    [Fact]
    public void Archive_SetsArchivedAtAndIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var conv = Conversation.OpenDiscipline(Guid.NewGuid(), Guid.NewGuid(), now);

        conv.Archive(now);
        var firstArchive = conv.ArchivedAtUtc;
        conv.Archive(now.AddMinutes(5));

        conv.IsArchived.Should().BeTrue();
        conv.ArchivedAtUtc.Should().Be(firstArchive);
    }

    [Fact]
    public void Direct_RoleOf_FallsBackToMember()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var conv = Conversation.OpenDirect(userA, userB, DateTimeOffset.UtcNow);

        conv.RoleOf(userA).Should().Be(ParticipantRole.Member);
        conv.RoleOf(userB).Should().Be(ParticipantRole.Member);
        conv.IsTeacher(userA).Should().BeFalse();
    }

    [Fact]
    public void Hydrate_ReadsArchivedAtAndDisciplineId()
    {
        var disciplineId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var archived = DateTimeOffset.UtcNow;

        var conv = Conversation.Hydrate(
            id: $"discipline:{disciplineId:N}",
            type: ConversationType.Group,
            participants: [teacherId],
            createdAtUtc: archived.AddDays(-1),
            lastMessageAtUtc: archived.AddDays(-1),
            lastMessagePreview: null,
            pinnedMessageIds: null,
            participantRoles: new Dictionary<Guid, ParticipantRole>
            {
                [teacherId] = ParticipantRole.Teacher,
            },
            disciplineId: disciplineId,
            archivedAtUtc: archived);

        conv.DisciplineId.Should().Be(disciplineId);
        conv.IsArchived.Should().BeTrue();
        conv.RoleOf(teacherId).Should().Be(ParticipantRole.Teacher);
    }
}
