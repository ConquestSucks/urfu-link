using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Limits;

/// <summary>
/// Per-asset-kind upload limits. Bound from configuration section "MediaLimits"
/// so they can be tuned per environment without recompiling.
/// </summary>
public sealed class MediaLimitsOptions
{
    public const string SectionName = "MediaLimits";

    public KindLimit Image { get; set; } = new(20L * 1024 * 1024);   // 20 MB
    public KindLimit Video { get; set; } = new(100L * 1024 * 1024);  // 100 MB
    // Voice: 10 MB AND ≤5 minutes per EPIC #206. The size cap is enforced at
    // /upload/init from the request payload; the duration cap is enforced via
    // the client-declared `DurationSeconds` field which the recorder UI fills
    // from the actual recording length. A stricter server-side ffmpeg probe
    // can be layered on as a Worker post-CompleteUpload (TODO: tracked separately).
    public KindLimit Voice { get; set; } = new(10L * 1024 * 1024, MaxDurationSeconds: 300);
    public KindLimit Audio { get; set; } = new(50L * 1024 * 1024);   // 50 MB
    public KindLimit Document { get; set; } = new(100L * 1024 * 1024); // 100 MB

    public KindLimit For(AssetKind kind) => kind switch
    {
        AssetKind.Image => Image,
        AssetKind.Video => Video,
        AssetKind.Voice => Voice,
        AssetKind.Audio => Audio,
        AssetKind.Document => Document,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>
/// Per-asset-kind upload constraints. <see cref="MaxSizeBytes"/> is the universal cap;
/// <see cref="MaxDurationSeconds"/> applies only to voice (and potentially video in the
/// future). <c>null</c> means duration is not constrained for the kind.
/// </summary>
public sealed record KindLimit(long MaxSizeBytes, int? MaxDurationSeconds = null);
