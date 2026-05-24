using System.Collections.Concurrent;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace DisciplineService.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory <see cref="IIdempotencyStore"/> for the test stack. The production
/// implementation lives on Redis; we don't want a Redis container in this test
/// suite, but we do need real key-collision semantics so tests that deliberately
/// reuse an Idempotency-Key see a 409 instead of a falsely-clean 201.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

    public ValueTask<bool> TryRegisterAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<bool>(_seen.TryAdd(key, 0));
    }

    public void Clear() => _seen.Clear();
}
