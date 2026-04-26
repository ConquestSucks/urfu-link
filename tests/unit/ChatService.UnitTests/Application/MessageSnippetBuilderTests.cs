using FluentAssertions;
using Urfu.Link.Services.Chat.Application.Messages;

namespace Urfu.Link.Services.Chat.UnitTests.Application;

public class MessageSnippetBuilderTests
{
    [Fact]
    public void Build_TermInMiddle_ReturnsSnippetWithEllipsesOnBothSides()
    {
        var body = new string('a', 50) + " match " + new string('b', 50);

        var snippet = MessageSnippetBuilder.Build(body, "match");

        snippet.Should().StartWith("...");
        snippet.Should().EndWith("...");
        snippet.Should().Contain("match");
    }

    [Fact]
    public void Build_TermAtStart_NoLeadingEllipsis()
    {
        var body = "match starts here and goes on and on";

        var snippet = MessageSnippetBuilder.Build(body, "match");

        snippet.Should().NotStartWith("...");
        snippet.Should().Contain("match");
    }

    [Fact]
    public void Build_TermAtEnd_NoTrailingEllipsis()
    {
        var body = "looking for the final word match";

        var snippet = MessageSnippetBuilder.Build(body, "match");

        snippet.Should().NotEndWith("...");
        snippet.Should().Contain("match");
    }

    [Fact]
    public void Build_BodyShorterThanWindow_ReturnsFullBody()
    {
        var body = "short match body";

        var snippet = MessageSnippetBuilder.Build(body, "match");

        snippet.Should().Be("short match body");
    }

    [Fact]
    public void Build_NoMatch_ReturnsNull()
    {
        var body = "completely different body";

        var snippet = MessageSnippetBuilder.Build(body, "missing");

        snippet.Should().BeNull();
    }

    [Fact]
    public void Build_MultiWordQuery_UsesFirstWord()
    {
        var body = "this body contains alpha but not beta";

        var snippet = MessageSnippetBuilder.Build(body, "alpha beta");

        snippet.Should().Contain("alpha");
    }

    [Fact]
    public void Build_QuotedQuery_StripsQuotesAndFindsFirstWord()
    {
        var body = "what about зачёт today";

        var snippet = MessageSnippetBuilder.Build(body, "\"зачёт сдан\"");

        snippet.Should().Contain("зачёт");
    }

    [Fact]
    public void Build_CaseInsensitive_FindsMatch()
    {
        var body = "MATCH is uppercase here";

        var snippet = MessageSnippetBuilder.Build(body, "match");

        snippet.Should().Contain("MATCH");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_EmptyBody_ReturnsNull(string body)
    {
        MessageSnippetBuilder.Build(body, "anything").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!?@#")]
    public void Build_EmptyOrPunctuationQuery_ReturnsNull(string query)
    {
        MessageSnippetBuilder.Build("some body content", query).Should().BeNull();
    }
}
