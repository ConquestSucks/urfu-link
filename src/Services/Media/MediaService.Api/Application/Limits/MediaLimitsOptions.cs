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
    public KindLimit Voice { get; set; } = new(10L * 1024 * 1024);   // 10 MB
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

public sealed record KindLimit(long MaxSizeBytes);
