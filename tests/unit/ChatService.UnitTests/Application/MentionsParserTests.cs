using FluentAssertions;
using Urfu.Link.Services.Chat.Application.Mentions;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class MentionsParserTests
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Carol = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid[] Participants = [Alice, Bob, Carol];

    [Fact]
    public void Parse_NullOrEmptyBody_ReturnsEmpty()
    {
        MentionsParser.Parse(null, Participants, maxMentions: 10).Should().BeEmpty();
        MentionsParser.Parse(string.Empty, Participants, maxMentions: 10).Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoMentions_ReturnsEmpty()
    {
        MentionsParser.Parse("plain hello", Participants, 10).Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleParticipantMention_IsExtracted()
    {
        var body = $"hi @{Alice:D}!";

        MentionsParser.Parse(body, Participants, 10).Should().ContainSingle().Which.Should().Be(Alice);
    }

    [Fact]
    public void Parse_MentionOfNonParticipant_IsDropped()
    {
        var body = $"hi @{Stranger:D}";

        MentionsParser.Parse(body, Participants, 10).Should().BeEmpty();
    }

    [Fact]
    public void Parse_DuplicateMention_IsDeduped()
    {
        var body = $"hello @{Bob:D}, again @{Bob:D}";

        MentionsParser.Parse(body, Participants, 10).Should().ContainSingle().Which.Should().Be(Bob);
    }

    [Fact]
    public void Parse_Everyone_ExpandsToAllParticipants()
    {
        MentionsParser.Parse("@everyone please review", Participants, 10)
            .Should().BeEquivalentTo(Participants);
    }

    [Fact]
    public void Parse_TeachersStudentsTokens_AreEmptyInStub()
    {
        MentionsParser.Parse("@teachers note", Participants, 10).Should().BeEmpty();
        MentionsParser.Parse("@students homework", Participants, 10).Should().BeEmpty();
    }

    [Fact]
    public void Parse_MixedEveryoneAndExplicit_IsDeduped()
    {
        var body = $"@everyone — and especially @{Alice:D}";

        var result = MentionsParser.Parse(body, Participants, 10);

        result.Should().HaveCount(Participants.Length);
        result.Should().BeEquivalentTo(Participants);
    }

    [Fact]
    public void Parse_AboveMaxMentions_IsTruncated()
    {
        var body = string.Join(' ', Participants.Select(p => $"@{p:D}"));

        var result = MentionsParser.Parse(body, Participants, maxMentions: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_MaxMentionsZero_Throws()
    {
        var act = () => MentionsParser.Parse("hi", Participants, maxMentions: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Parse_GuidMatchIsCaseInsensitive()
    {
        var body = $"hi @{Alice.ToString("D").ToUpperInvariant()}";

        MentionsParser.Parse(body, Participants, 10).Should().ContainSingle().Which.Should().Be(Alice);
    }

    [Fact]
    public void Parse_PreservesOrderOfFirstAppearance()
    {
        var body = $"@{Carol:D} @{Alice:D} @{Bob:D}";

        var result = MentionsParser.Parse(body, Participants, 10);

        result.Should().Equal(Carol, Alice, Bob);
    }

    [Fact]
    public void Parse_TextLookingLikeEmail_IsNotMentioned()
    {
        var body = "ping admin@example.com if needed";

        MentionsParser.Parse(body, Participants, 10).Should().BeEmpty();
    }
}
