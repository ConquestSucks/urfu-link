namespace UserService.Api.Domain.Interfaces;

public interface IUserRepository
{
    Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(UserProfile user);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
