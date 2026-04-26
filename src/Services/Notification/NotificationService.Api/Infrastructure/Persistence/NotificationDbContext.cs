using Microsoft.EntityFrameworkCore;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<NotificationAggregate> Notifications => Set<NotificationAggregate>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
