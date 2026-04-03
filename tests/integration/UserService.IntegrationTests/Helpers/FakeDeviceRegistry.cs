using UserService.Api.Domain.Interfaces;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeDeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<string, string> _devices = new(StringComparer.Ordinal);

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
        return Task.CompletedTask;
    }

    public Task RemoveAllAsync(IEnumerable<string> keycloakSessionIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in keycloakSessionIds)
            _devices.Remove(id);
        return Task.CompletedTask;
    }
}
