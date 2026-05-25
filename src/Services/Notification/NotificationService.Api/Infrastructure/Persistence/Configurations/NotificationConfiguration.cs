using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationAggregate>
{
    private static readonly JsonSerializerOptions DataJsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<NotificationAggregate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("notifications");

        // Composite primary key required by Postgres declarative range partitioning on created_at_utc.
        builder.HasKey(n => new { n.Id, n.CreatedAtUtc });

        builder.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(n => n.RecipientUserId).HasColumnName("recipient_user_id");

        builder.Property(n => n.Category)
            .HasColumnName("category")
            .HasConversion<short>();

        builder.Property(n => n.Severity)
            .HasColumnName("severity")
            .HasConversion<short>();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasMaxLength(NotificationAggregate.NotificationTypeMaxLength)
            .HasDefaultValue("system.update")
            .IsRequired();

        builder.OwnsOne(n => n.Content, content =>
        {
            content.Property(c => c.Title).HasColumnName("title").HasMaxLength(NotificationContent.TitleMaxLength).IsRequired();
            content.Property(c => c.Body).HasColumnName("body").HasMaxLength(NotificationContent.BodyMaxLength).IsRequired();
            content.Property(c => c.ImageUrl).HasColumnName("image_url").HasMaxLength(NotificationContent.UrlMaxLength);
            content.Property(c => c.DeepLink).HasColumnName("deep_link").HasMaxLength(NotificationContent.UrlMaxLength);
        });

        builder.Property(n => n.Data)
            .HasColumnName("data")
            .HasColumnType("jsonb")
            .HasConversion(
                value => Serialize(value),
                json => Deserialize(json));

        builder.Property(n => n.Actor)
            .HasColumnName("actor")
            .HasColumnType("jsonb")
            .HasConversion(
                value => SerializeNullable(value),
                json => DeserializeNullable<NotificationActor>(json));

        builder.Property(n => n.Entity)
            .HasColumnName("entity")
            .HasColumnType("jsonb")
            .HasConversion(
                value => SerializeNullable(value),
                json => DeserializeNullable<NotificationEntity>(json));

        var actionComparer = new ValueComparer<IReadOnlyList<NotificationAction>>(
            (left, right) => SequenceEqual(left, right),
            value => ComputeHash(value),
            value => value.ToArray());

        builder.Property(n => n.Actions)
            .HasColumnName("actions")
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, DataJsonOptions),
                json => DeserializeActions(json))
            .Metadata.SetValueComparer(actionComparer);

        builder.Property(n => n.GroupKey)
            .HasColumnName("group_key")
            .HasMaxLength(GroupKey.MaxLength)
            .HasConversion(
                value => value.HasValue ? value.Value.Value : null,
                stored => stored == null ? (GroupKey?)null : new GroupKey(stored));

        builder.Property(n => n.SourceEventId).HasColumnName("source_event_id");
        builder.Property(n => n.SourceEventType)
            .HasColumnName("source_event_type")
            .HasMaxLength(NotificationAggregate.SourceEventTypeMaxLength)
            .IsRequired();

        builder.Property(n => n.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(n => n.ReadAtUtc).HasColumnName("read_at_utc");
        builder.Property(n => n.SeenAtUtc).HasColumnName("seen_at_utc");
        builder.Property(n => n.SavedAtUtc).HasColumnName("saved_at_utc");
        builder.Property(n => n.DoneAtUtc).HasColumnName("done_at_utc");
        builder.Property(n => n.ArchivedAtUtc).HasColumnName("archived_at_utc");
        builder.Property(n => n.SnoozedUntilUtc).HasColumnName("snoozed_until_utc");
        builder.Property(n => n.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(n => n.OccurrenceCount)
            .HasColumnName("occurrence_count")
            .HasDefaultValue(1);
        builder.Property(n => n.LastOccurrenceAtUtc).HasColumnName("last_occurrence_at_utc");

        builder.Ignore(n => n.DomainEvents);
        builder.Ignore(n => n.Deliveries);
    }

    private static string Serialize(NotificationData data)
        => JsonSerializer.Serialize(data.Values, DataJsonOptions);

    private static string? SerializeNullable<T>(T? value)
        where T : class
        => value is null ? null : JsonSerializer.Serialize(value, DataJsonOptions);

    private static T? DeserializeNullable<T>(string? json)
        where T : class
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<T>(json, DataJsonOptions);

    private static NotificationData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return NotificationData.Empty;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, DataJsonOptions);
        return NotificationData.From(dict);
    }

    private static NotificationAction[] DeserializeActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<NotificationAction[]>(json, DataJsonOptions) ?? [];
    }

    private static bool SequenceEqual(
        IReadOnlyList<NotificationAction>? left,
        IReadOnlyList<NotificationAction>? right)
        => left is null
            ? right is null
            : right is not null && left.SequenceEqual(right);

    private static int ComputeHash(IReadOnlyList<NotificationAction> value)
    {
        var hash = new HashCode();
        foreach (var action in value)
        {
            hash.Add(action);
        }

        return hash.ToHashCode();
    }
}
