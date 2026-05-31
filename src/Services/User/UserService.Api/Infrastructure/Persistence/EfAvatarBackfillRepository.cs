using Microsoft.EntityFrameworkCore;
using UserService.Api.Services;

namespace UserService.Api.Infrastructure.Persistence;

public sealed class EfAvatarBackfillRepository(UserDbContext dbContext) : IAvatarBackfillRepository
{
    public async Task<IReadOnlyList<AvatarBackfillCandidate>> ListCandidatesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 1000);

        return await dbContext.UserProfiles
            .AsNoTracking()
            .Where(user => user.Account.AvatarUrl != null)
            .OrderBy(user => user.Id)
            .Take(normalizedLimit)
            .Select(user => new AvatarBackfillCandidate(user.Id, user.Account.AvatarUrl!))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
