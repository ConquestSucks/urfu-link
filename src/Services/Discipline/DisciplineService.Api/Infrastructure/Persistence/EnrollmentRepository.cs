using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Infrastructure.Persistence;

public sealed class EnrollmentRepository(DisciplineDbContext dbContext) : IEnrollmentRepository
{
    public async Task<IReadOnlyList<Discipline>> ListDisciplinesAsync(
        DisciplineFilter filter,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = dbContext.Disciplines
            .AsNoTracking()
            .Include(d => d.Enrollments)
            .AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(d => d.ArchivedAtUtc == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Semester))
        {
            var semester = filter.Semester.Trim();
            query = query.Where(d => d.Semester == semester);
        }

        if (filter.UserId.HasValue)
        {
            var userId = filter.UserId.Value;
            query = query.Where(d => d.Enrollments.Any(e => e.UserId == userId));
        }

        return await query
            .OrderBy(d => d.Semester)
            .ThenBy(d => d.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DisciplineMembership>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Join(
                dbContext.Disciplines.AsNoTracking().Where(d => d.ArchivedAtUtc == null),
                e => e.DisciplineId,
                d => d.Id,
                (e, d) => new DisciplineMembership(
                    d.Id,
                    d.Code,
                    d.Title,
                    d.Semester,
                    d.OwnerTeacherId,
                    d.CoverAssetId,
                    e.Role))
            .OrderBy(m => m.Semester)
            .ThenBy(m => m.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> IsMemberAsync(Guid disciplineId, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.DisciplineId == disciplineId && e.UserId == userId, cancellationToken);
    }
}
