using FluentAssertions;
using MediaService.Api.Domain;

namespace MediaService.UnitTests.Unit;

public class UploadSessionTests
{
    [Fact]
    public void Open_RejectsZeroOrNegativeTtl()
    {
        var act = () => UploadSession.Open(Guid.NewGuid(), TimeSpan.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Open_SetsExpiryRelativeToNow()
    {
        var session = UploadSession.Open(Guid.NewGuid(), TimeSpan.FromMinutes(15));

        session.IsCompleted.Should().BeFalse();
        session.ExpiresAtUtc.Should().BeAfter(session.CreatedAtUtc);
        (session.ExpiresAtUtc - session.CreatedAtUtc).Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsExpired_TrueAfterExpiry()
    {
        var session = UploadSession.Open(Guid.NewGuid(), TimeSpan.FromMinutes(1));
        session.IsExpired(session.ExpiresAtUtc.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void MarkCompleted_BlocksFurtherCompletion()
    {
        var session = UploadSession.Open(Guid.NewGuid(), TimeSpan.FromMinutes(1));
        session.MarkCompleted();

        session.IsCompleted.Should().BeTrue();
        var act = () => session.MarkCompleted();
        act.Should().Throw<InvalidOperationException>();
    }
}
