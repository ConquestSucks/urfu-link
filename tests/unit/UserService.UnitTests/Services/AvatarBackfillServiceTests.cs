using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Services;

namespace UserService.UnitTests.Services;

public sealed class AvatarBackfillServiceTests
{
    [Fact]
    public async Task RunOnceAsyncShouldSkipAlreadyNormalizedAvatars()
    {
        var candidates = Substitute.For<IAvatarBackfillRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var storage = Substitute.For<IAvatarStorage>();
        var processor = Substitute.For<IAvatarImageProcessor>();

        var candidate = new AvatarBackfillCandidate(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "https://storage.ghjc.ru/user-avatars/avatars/user/avatar.webp");
        candidates
            .ListCandidatesAsync(100, Arg.Any<CancellationToken>())
            .Returns([candidate]);
        storage
            .DownloadAsync(candidate.AvatarUrl, Arg.Any<CancellationToken>())
            .Returns(new AvatarObject(new byte[] { 1, 2, 3 }, "image/webp"));
        processor
            .IsNormalizedAsync(
                Arg.Any<Stream>(),
                "image/webp",
                Arg.Any<CancellationToken>())
            .Returns(true);

        var service = new AvatarBackfillService(
            candidates,
            userRepository,
            storage,
            processor,
            Options.Create(new AvatarBackfillOptions { Enabled = true }),
            NullLogger<AvatarBackfillService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);

        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default, Stream.Null, string.Empty, default);
        await userRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await storage.DidNotReceive().DeleteAsync(candidate.AvatarUrl, Arg.Any<CancellationToken>());
    }
}
