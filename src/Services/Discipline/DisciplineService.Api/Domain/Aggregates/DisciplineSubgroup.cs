namespace DisciplineService.Api.Domain.Aggregates;

public sealed class DisciplineSubgroup
{
    public Guid Id { get; private set; }

    public Guid DisciplineId { get; private set; }

    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc.HasValue;

    private DisciplineSubgroup()
    {
    }

    internal static DisciplineSubgroup Create(
        Guid disciplineId,
        string name,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DisciplineSubgroup
        {
            Id = Guid.NewGuid(),
            DisciplineId = disciplineId,
            Name = name.Trim(),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    internal void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    internal void Archive()
    {
        if (ArchivedAtUtc.HasValue)
        {
            return;
        }

        ArchivedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = ArchivedAtUtc.Value;
    }
}
