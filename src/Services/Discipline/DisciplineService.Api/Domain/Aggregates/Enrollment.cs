using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Domain.Aggregates;

public sealed class Enrollment
{
    public Guid Id { get; private set; }

    public Guid DisciplineId { get; private set; }

    public Guid UserId { get; private set; }

    public DisciplineRole Role { get; private set; }

    public Guid? SubgroupId { get; private set; }

    public DateTimeOffset EnrolledAtUtc { get; private set; }

    public Guid EnrolledBy { get; private set; }

    private Enrollment()
    {
    }

    internal static Enrollment Create(
        Guid disciplineId,
        Guid userId,
        DisciplineRole role,
        Guid? subgroupId,
        Guid enrolledBy,
        DateTimeOffset enrolledAtUtc)
    {
        return new Enrollment
        {
            Id = Guid.NewGuid(),
            DisciplineId = disciplineId,
            UserId = userId,
            Role = role,
            SubgroupId = subgroupId,
            EnrolledBy = enrolledBy,
            EnrolledAtUtc = enrolledAtUtc,
        };
    }

    internal void SetRole(DisciplineRole newRole, Guid? subgroupId)
    {
        Role = newRole;
        SubgroupId = subgroupId;
    }

    internal void SetSubgroup(Guid subgroupId) => SubgroupId = subgroupId;
}
