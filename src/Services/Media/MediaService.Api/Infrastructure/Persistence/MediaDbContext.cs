using MediaService.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediaService.Api.Infrastructure.Persistence;

public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public DbSet<MediaAsset> Assets => Set<MediaAsset>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();
    public DbSet<MediaAccessGrant> Grants => Set<MediaAccessGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("media");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
    }
}
