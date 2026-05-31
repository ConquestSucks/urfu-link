using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using UserService.Api.Domain;
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
            "https://storage.ghjc.ru/user-avatars/avatars/user/avatar-v2-256.webp");
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

        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default, default!, default);
        await userRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await storage.DidNotReceive().DeleteAsync(candidate.AvatarUrl, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsyncShouldProcessOldAvatarUrlsUntilNoCandidatesRemain()
    {
        var candidates = Substitute.For<IAvatarBackfillRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var storage = Substitute.For<IAvatarStorage>();
        var processor = Substitute.For<IAvatarImageProcessor>();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var oldUrl = "https://storage.ghjc.ru/user-avatars/avatars/user/avatar.png";
        var newUrl = "https://storage.ghjc.ru/user-avatars/avatars/user/avatar-v2-256.webp";
        var user = UserProfile.CreateDefault(userId);
        user.UploadAvatar(oldUrl);
        IReadOnlyList<AvatarBackfillCandidate> firstBatch = [new AvatarBackfillCandidate(userId, oldUrl)];
        IReadOnlyList<AvatarBackfillCandidate> emptyBatch = [];

        candidates
            .ListCandidatesAsync(100, Arg.Any<CancellationToken>())
            .Returns(
                firstBatch,
                emptyBatch);
        storage
            .DownloadAsync(oldUrl, Arg.Any<CancellationToken>())
            .Returns(new AvatarObject(new byte[] { 1, 2, 3 }, "image/png"));
        var processed = new ProcessedAvatar([
            new ProcessedAvatarVariant(64, new byte[] { 1 }, "image/webp"),
            new ProcessedAvatarVariant(128, new byte[] { 2 }, "image/webp"),
            new ProcessedAvatarVariant(256, new byte[] { 3 }, "image/webp"),
        ]);
        processor
            .ProcessAsync(Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>())
            .Returns(processed);
        storage
            .UploadAsync(userId, processed, Arg.Any<CancellationToken>())
            .Returns(newUrl);
        userRepository
            .GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var service = new AvatarBackfillService(
            candidates,
            userRepository,
            storage,
            processor,
            Options.Create(new AvatarBackfillOptions { Enabled = true }),
            NullLogger<AvatarBackfillService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(newUrl, user.Account.AvatarUrl);
        await userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await storage.Received(1).DeleteAsync(oldUrl, Arg.Any<CancellationToken>());
        await candidates.Received(2).ListCandidatesAsync(100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsyncShouldReprocessLegacyCanonicalAvatarUrls()
    {
        var candidates = Substitute.For<IAvatarBackfillRepository>();
        var userRepository = Substitute.For<IUserRepository>();
        var storage = Substitute.For<IAvatarStorage>();
        var processor = Substitute.For<IAvatarImageProcessor>();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var legacyUrl = "https://storage.ghjc.ru/user-avatars/avatars/user/avatar-256.webp";
        var newUrl = "https://storage.ghjc.ru/user-avatars/avatars/user/avatar-v2-256.webp";
        var user = UserProfile.CreateDefault(userId);
        user.UploadAvatar(legacyUrl);
        IReadOnlyList<AvatarBackfillCandidate> firstBatch = [new AvatarBackfillCandidate(userId, legacyUrl)];
        IReadOnlyList<AvatarBackfillCandidate> emptyBatch = [];

        candidates
            .ListCandidatesAsync(100, Arg.Any<CancellationToken>())
            .Returns(
                firstBatch,
                emptyBatch);
        storage
            .DownloadAsync(legacyUrl, Arg.Any<CancellationToken>())
            .Returns(new AvatarObject(new byte[] { 1, 2, 3 }, "image/webp"));
        processor
            .IsNormalizedAsync(
                Arg.Any<Stream>(),
                "image/webp",
                Arg.Any<CancellationToken>())
            .Returns(true);
        var processed = new ProcessedAvatar([
            new ProcessedAvatarVariant(64, new byte[] { 1 }, "image/webp"),
            new ProcessedAvatarVariant(128, new byte[] { 2 }, "image/webp"),
            new ProcessedAvatarVariant(256, new byte[] { 3 }, "image/webp"),
        ]);
        processor
            .ProcessAsync(Arg.Any<Stream>(), "image/webp", Arg.Any<CancellationToken>())
            .Returns(processed);
        storage
            .UploadAsync(userId, processed, Arg.Any<CancellationToken>())
            .Returns(newUrl);
        userRepository
            .GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        storage.ClearReceivedCalls();

        var service = new AvatarBackfillService(
            candidates,
            userRepository,
            storage,
            processor,
            Options.Create(new AvatarBackfillOptions { Enabled = true }),
            NullLogger<AvatarBackfillService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);

        Assert.Equal(newUrl, user.Account.AvatarUrl);
        await storage.Received(1).UploadAsync(userId, processed, Arg.Any<CancellationToken>());
        await storage.Received(1).DeleteAsync(legacyUrl, Arg.Any<CancellationToken>());
        await userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
