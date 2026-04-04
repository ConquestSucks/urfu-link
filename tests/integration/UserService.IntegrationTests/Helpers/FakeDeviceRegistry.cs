using UserService.Api.Domain.Interfaces;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeDeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<string, string> _devices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mappings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reverseMappings = new(StringComparer.Ordinal);

    public Task SaveAsync(string keycloakSessionId, string userAgent, CancellationToken cancellationToken = default)
    {
        _devices[keycloakSessionId] = userAgent;
        return Task.CompletedTask;
    }

    public Task<string?> GetDeviceNameAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        _devices.TryGetValue(keycloakSessionId, out var name);
        return Task.FromResult(name);
    }

    public Task RemoveAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        _devices.Remove(keycloakSessionId);
        if (_reverseMappings.Remove(keycloakSessionId, out var pomeriumSid))
            _mappings.Remove(pomeriumSid);
        return Task.CompletedTask;
    }

    public Task RemoveAllAsync(IEnumerable<string> keycloakSessionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keycloakSessionIds);
        foreach (var id in keycloakSessionIds)
        {
            _devices.Remove(id);
            if (_reverseMappings.Remove(id, out var pomeriumSid))
                _mappings.Remove(pomeriumSid);
        }
        return Task.CompletedTask;
    }

    public Task SavePomeriumMappingAsync(string pomeriumSid, string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        _mappings[pomeriumSid] = keycloakSessionId;
        _reverseMappings[keycloakSessionId] = pomeriumSid;
        return Task.CompletedTask;
    }

    public Task<string?> GetKeycloakSessionIdAsync(string pomeriumSid, CancellationToken cancellationToken = default)
    {
        _mappings.TryGetValue(pomeriumSid, out var id);
        return Task.FromResult(id);
    }

    public Task<string?> GetPomeriumSidByKeycloakSessionAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        _reverseMappings.TryGetValue(keycloakSessionId, out var sid);
        return Task.FromResult(sid);
    }
}
