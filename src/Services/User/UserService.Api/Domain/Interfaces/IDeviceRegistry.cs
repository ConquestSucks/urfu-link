namespace UserService.Api.Domain.Interfaces;

public interface IDeviceRegistry
{
    Task SaveAsync(string keycloakSessionId, string userAgent, CancellationToken cancellationToken = default);
    Task<string?> GetDeviceNameAsync(string keycloakSessionId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string keycloakSessionId, CancellationToken cancellationToken = default);
    Task RemoveAllAsync(IEnumerable<string> keycloakSessionIds, CancellationToken cancellationToken = default);

    Task SavePomeriumMappingAsync(string pomeriumSid, string keycloakSessionId, CancellationToken cancellationToken = default);
    Task<string?> GetKeycloakSessionIdAsync(string pomeriumSid, CancellationToken cancellationToken = default);
}
