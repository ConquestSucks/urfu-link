using FluentAssertions;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;

namespace MediaService.UnitTests.Unit;

public class MediaAccessGrantTests
{
    [Fact]
    public void Direct_RequiresNullSourceId()
    {
        var grant = MediaAccessGrant.Create(
            assetId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            source: GrantSource.Direct,
            sourceId: null,
            grantedByUserId: Guid.NewGuid());

        grant.Source.Should().Be(GrantSource.Direct);
        grant.SourceId.Should().BeNull();
    }

    [Fact]
    public void Direct_WithSourceId_Throws()
    {
        var act = () => MediaAccessGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), GrantSource.Direct, "conv-1", Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Conversation_RequiresSourceId()
    {
        var act = () => MediaAccessGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), GrantSource.Conversation, sourceId: null, Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Discipline_WithSourceId_Succeeds()
    {
        var grant = MediaAccessGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), GrantSource.Discipline, "discipline-42", Guid.NewGuid());

        grant.Source.Should().Be(GrantSource.Discipline);
        grant.SourceId.Should().Be("discipline-42");
    }
}
