using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Services;

public sealed record AvatarBackfillCandidate(Guid UserId, string AvatarUrl);

public interface IAvatarBackfillRepository
{
    Task<IReadOnlyList<AvatarBackfillCandidate>> ListCandidatesAsync(
        int limit,
        CancellationToken cancellationToken);
}

public sealed class AvatarBackfillOptions
{
    public const string SectionName = "AvatarBackfill";

    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 100;
}

public sealed class AvatarBackfillService(
    IAvatarBackfillRepository candidates,
    IUserRepository userRepository,
    IAvatarStorage avatarStorage,
    IAvatarImageProcessor avatarImageProcessor,
    IOptions<AvatarBackfillOptions> options,
    ILogger<AvatarBackfillService> logger)
{
    private readonly AvatarBackfillOptions _options = options.Value;

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogDebug("Avatar backfill is disabled.");
            return;
        }

        var batchSize = Math.Clamp(_options.BatchSize, 1, 1000);
        var batch = await candidates
            .ListCandidatesAsync(batchSize, cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in batch)
        {
            await BackfillCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task BackfillCandidateAsync(
        AvatarBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        var current = await avatarStorage
            .DownloadAsync(candidate.AvatarUrl, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            logger.LogWarning("Skipping avatar backfill for missing object {AvatarUrl}", candidate.AvatarUrl);
            return;
        }

        await using var identifyStream = new MemoryStream(current.Content.ToArray(), writable: false);
        if (await avatarImageProcessor
                .IsNormalizedAsync(identifyStream, current.ContentType, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        await using var sourceStream = new MemoryStream(current.Content.ToArray(), writable: false);
        var processed = await avatarImageProcessor
            .ProcessAsync(sourceStream, current.ContentType, cancellationToken)
            .ConfigureAwait(false);

        await using var processedStream = new MemoryStream(processed.Content.ToArray(), writable: false);
        var newAvatarUrl = await avatarStorage
            .UploadAsync(candidate.UserId, processedStream, processed.ContentType, cancellationToken)
            .ConfigureAwait(false);

        var user = await userRepository.GetByIdAsync(candidate.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !string.Equals(user.Account.AvatarUrl, candidate.AvatarUrl, StringComparison.Ordinal))
        {
            await avatarStorage.DeleteAsync(newAvatarUrl, cancellationToken).ConfigureAwait(false);
            return;
        }

        user.UploadAvatar(newAvatarUrl);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await avatarStorage.DeleteAsync(candidate.AvatarUrl, cancellationToken).ConfigureAwait(false);
    }
}
