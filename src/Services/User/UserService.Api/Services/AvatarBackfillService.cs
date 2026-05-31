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
    public int IntervalMinutes { get; set; } = 360;
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
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await candidates
                .ListCandidatesAsync(batchSize, cancellationToken)
                .ConfigureAwait(false);

            if (batch.Count == 0)
            {
                return;
            }

            var madeProgress = false;
            foreach (var candidate in batch)
            {
                try
                {
                    madeProgress |= await BackfillCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
#pragma warning disable CA1031 // One invalid legacy avatar must not stop backfilling other users.
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to backfill avatar {AvatarUrl} for user {UserId}",
                        candidate.AvatarUrl,
                        candidate.UserId);
                }
#pragma warning restore CA1031
            }

            if (!madeProgress)
            {
                return;
            }
        }
    }

    private async Task<bool> BackfillCandidateAsync(
        AvatarBackfillCandidate candidate,
        CancellationToken cancellationToken)
    {
        var current = await avatarStorage
            .DownloadAsync(candidate.AvatarUrl, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            logger.LogWarning("Skipping avatar backfill for missing object {AvatarUrl}", candidate.AvatarUrl);
            return false;
        }

        await using var identifyStream = new MemoryStream(current.Content.ToArray(), writable: false);
        if (HasCanonicalVariantUrl(candidate.AvatarUrl) &&
            await avatarImageProcessor
                .IsNormalizedAsync(identifyStream, current.ContentType, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        await using var sourceStream = new MemoryStream(current.Content.ToArray(), writable: false);
        var processed = await avatarImageProcessor
            .ProcessAsync(sourceStream, current.ContentType, cancellationToken)
            .ConfigureAwait(false);

        var newAvatarUrl = await avatarStorage
            .UploadAsync(candidate.UserId, processed, cancellationToken)
            .ConfigureAwait(false);

        var user = await userRepository.GetByIdAsync(candidate.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !string.Equals(user.Account.AvatarUrl, candidate.AvatarUrl, StringComparison.Ordinal))
        {
            await avatarStorage.DeleteAsync(newAvatarUrl, cancellationToken).ConfigureAwait(false);
            return false;
        }

        user.UploadAvatar(newAvatarUrl);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await avatarStorage.DeleteAsync(candidate.AvatarUrl, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool HasCanonicalVariantUrl(string avatarUrl)
    {
        var path = Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : avatarUrl;

        return path.EndsWith("-256.webp", StringComparison.OrdinalIgnoreCase);
    }
}
