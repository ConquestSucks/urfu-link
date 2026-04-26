using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Api.Domain;
using UserService.Api.Domain.ValueObjects;

namespace UserService.Api.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_profiles");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.OwnsOne(u => u.Account, account =>
        {
            account.Property(a => a.AvatarUrl).HasColumnName("avatar_url");
            account.Property(a => a.AboutMe).HasColumnName("about_me").HasMaxLength(500);
        });

        builder.OwnsOne(u => u.Privacy, privacy =>
        {
            privacy.Property(p => p.ShowOnlineStatus).HasColumnName("show_online_status");
            privacy.Property(p => p.ShowLastVisitTime).HasColumnName("show_last_visit_time");
        });

        builder.Property(u => u.Notifications)
            .HasColumnName("notification_settings")
            .HasColumnType("jsonb")
            .HasConversion(
                value => Serialize(value),
                json => Deserialize(json));

        builder.OwnsOne(u => u.SoundVideo, soundVideo =>
        {
            soundVideo.Property(s => s.PlaybackDeviceId).HasColumnName("playback_device_id");
            soundVideo.Property(s => s.RecordingDeviceId).HasColumnName("recording_device_id");
            soundVideo.Property(s => s.WebcamDeviceId).HasColumnName("webcam_device_id");
        });

        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        builder.Navigation(u => u.Account).IsRequired();
        builder.Navigation(u => u.Privacy).IsRequired();
        builder.Navigation(u => u.SoundVideo).IsRequired();
    }

    private static string Serialize(NotificationSettings settings)
    {
        var dto = new NotificationSettingsDto(
            settings.Categories.ToDictionary(
                kv => kv.Key.ToString(CultureInfo.InvariantCulture),
                kv => new ChannelToggleDto(kv.Value.Push, kv.Value.Email, kv.Value.InApp)),
            new QuietHoursDto(
                settings.QuietHours.IanaTimezone,
                settings.QuietHours.Start?.ToString("HH:mm", CultureInfo.InvariantCulture),
                settings.QuietHours.End?.ToString("HH:mm", CultureInfo.InvariantCulture),
                settings.QuietHours.Enabled),
            settings.DndEnabled,
            settings.Locale,
            settings.Sound);

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static NotificationSettings Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return NotificationSettings.Default;
        }

        var dto = JsonSerializer.Deserialize<NotificationSettingsDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize notification settings.");

        var categories = dto.Categories.ToDictionary(
            kv => int.Parse(kv.Key, CultureInfo.InvariantCulture),
            kv => new ChannelToggle(kv.Value.Push, kv.Value.Email, kv.Value.InApp));

        var quietHours = dto.QuietHours.Enabled
            && TimeOnly.TryParse(dto.QuietHours.Start, CultureInfo.InvariantCulture, out var start)
            && TimeOnly.TryParse(dto.QuietHours.End, CultureInfo.InvariantCulture, out var end)
            ? QuietHours.Create(dto.QuietHours.IanaTimezone, start, end)
            : QuietHours.Disabled(dto.QuietHours.IanaTimezone);

        return new NotificationSettings(
            categories,
            quietHours,
            dto.DndEnabled,
            dto.Locale,
            dto.Sound);
    }

    private sealed record NotificationSettingsDto(
        IReadOnlyDictionary<string, ChannelToggleDto> Categories,
        QuietHoursDto QuietHours,
        bool DndEnabled,
        string Locale,
        bool Sound);

    private sealed record ChannelToggleDto(bool Push, bool Email, bool InApp);

    private sealed record QuietHoursDto(string IanaTimezone, string? Start, string? End, bool Enabled);
}
