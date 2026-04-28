using FluentAssertions;
using NSubstitute;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Application.Mentions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

/// <summary>
/// Unit coverage for <see cref="MentionResolver"/>. Direct chats must reject the
/// discipline-only <c>@teachers</c> / <c>@students</c> tokens (silent drop, no gRPC),
/// while discipline group conversations must call out to DisciplineService and merge
/// the role-filtered roster with explicit mentions, deduplicated and capped.
/// </summary>
public sealed class MentionResolverTests
{
    private static readonly Guid TeacherA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TeacherB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StudentA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StudentB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid DisciplineId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IDisciplineServiceClient _discipline = Substitute.For<IDisciplineServiceClient>();

    [Fact]
    public async Task Direct_chat_silently_drops_teachers_and_students_tokens_without_grpc_call()
    {
        var direct = Conversation.OpenDirect(TeacherA, StudentA, DateTimeOffset.UtcNow);
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("hi @teachers and @students", direct, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEmpty(
            "direct chats have no role hierarchy; the special tokens have no expansion target.");

        await _discipline.DidNotReceive().ListMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discipline_chat_expands_teachers_token_via_grpc()
    {
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA, TeacherB, StudentA]);
        _discipline.ListMembersAsync(DisciplineId, Arg.Any<CancellationToken>()).Returns(new List<DisciplineMember>
        {
            new(TeacherA, ParticipantRole.Teacher),
            new(TeacherB, ParticipantRole.Teacher),
            new(StudentA, ParticipantRole.Student),
        });
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("@teachers please review", conv, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEquivalentTo([TeacherA, TeacherB]);
    }

    [Fact]
    public async Task Discipline_chat_expands_students_token_via_grpc()
    {
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA, StudentA, StudentB]);
        _discipline.ListMembersAsync(DisciplineId, Arg.Any<CancellationToken>()).Returns(new List<DisciplineMember>
        {
            new(TeacherA, ParticipantRole.Teacher),
            new(StudentA, ParticipantRole.Student),
            new(StudentB, ParticipantRole.Student),
        });
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("@students homework!", conv, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEquivalentTo([StudentA, StudentB]);
    }

    [Fact]
    public async Task Discipline_chat_dedupes_explicit_and_role_based_mentions()
    {
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA, TeacherB, StudentA]);
        _discipline.ListMembersAsync(DisciplineId, Arg.Any<CancellationToken>()).Returns(new List<DisciplineMember>
        {
            new(TeacherA, ParticipantRole.Teacher),
            new(TeacherB, ParticipantRole.Teacher),
        });
        var resolver = new MentionResolver(_discipline);

        var body = $"@{TeacherA:D} and @teachers";
        var mentions = await resolver.ResolveAsync(body, conv, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEquivalentTo([TeacherA, TeacherB],
            "TeacherA appears as both an explicit mention and as a member of @teachers; the result must dedupe.");
    }

    [Fact]
    public async Task Filters_role_members_who_are_not_actually_in_the_conversation()
    {
        // The discipline roster (returned by gRPC) and the chat roster can drift between an
        // enrollment Kafka event and its projection. Mentioning a teacher who hasn't been
        // synced into the chat yet must not leak that user id back to the broadcast pipeline.
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA]);
        _discipline.ListMembersAsync(DisciplineId, Arg.Any<CancellationToken>()).Returns(new List<DisciplineMember>
        {
            new(TeacherA, ParticipantRole.Teacher),
            new(TeacherB, ParticipantRole.Teacher), // not yet in chat participants
        });
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("@teachers", conv, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEquivalentTo([TeacherA]);
    }

    [Fact]
    public async Task Skips_grpc_call_when_body_has_no_special_tokens()
    {
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA, StudentA]);
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("plain message", conv, maxMentions: 50, CancellationToken.None);

        mentions.Should().BeEmpty();
        await _discipline.DidNotReceive().ListMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caps_at_max_mentions()
    {
        var conv = OpenDisciplineConversation(TeacherA, [TeacherA, TeacherB, StudentA, StudentB]);
        _discipline.ListMembersAsync(DisciplineId, Arg.Any<CancellationToken>()).Returns(new List<DisciplineMember>
        {
            new(TeacherA, ParticipantRole.Teacher),
            new(TeacherB, ParticipantRole.Teacher),
            new(StudentA, ParticipantRole.Student),
            new(StudentB, ParticipantRole.Student),
        });
        var resolver = new MentionResolver(_discipline);

        var mentions = await resolver.ResolveAsync("@teachers @students", conv, maxMentions: 2, CancellationToken.None);

        mentions.Should().HaveCount(2, "the cap protects against amplification when @everyone-style tokens cover huge rosters.");
    }

    private static Conversation OpenDisciplineConversation(Guid ownerTeacher, IReadOnlyList<Guid> participants)
    {
        var conv = Conversation.OpenDiscipline(
            DisciplineId,
            ownerTeacher,
            DateTimeOffset.UtcNow,
            title: "Test discipline");
        foreach (var participant in participants)
        {
            if (participant != ownerTeacher)
            {
                conv.AddParticipant(participant, ParticipantRole.Student);
            }
        }
        return conv;
    }
}
