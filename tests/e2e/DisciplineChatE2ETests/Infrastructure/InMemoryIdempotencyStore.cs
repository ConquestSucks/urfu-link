using System.Collections.Concurrent;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace DisciplineChatE2ETests.Infrastructure;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

    public ValueTask<bool> TryRegisterAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_seen.TryAdd(key, 0));
    }

    public void Clear() => _seen.Clear();
}
