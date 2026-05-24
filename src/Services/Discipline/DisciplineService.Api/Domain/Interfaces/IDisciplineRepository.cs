using DisciplineService.Api.Domain.Aggregates;

namespace DisciplineService.Api.Domain.Interfaces;

public interface IDisciplineRepository
{
    Task<Discipline?> GetByIdAsync(Guid disciplineId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeDisciplineId, CancellationToken cancellationToken);

    void Add(Discipline discipline);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
