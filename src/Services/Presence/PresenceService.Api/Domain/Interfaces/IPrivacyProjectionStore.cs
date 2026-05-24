using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Domain.Interfaces;

public interface IPrivacyProjectionStore
{
    /// <summary>Returns <see cref="PrivacySettings.Default"/> if no projection exists yet.</summary>
    Task<PrivacySettings> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task SetAsync(Guid userId, PrivacySettings settings, CancellationToken cancellationToken);
}
