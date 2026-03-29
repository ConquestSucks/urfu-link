using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Api.Domain;

namespace UserService.Api.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
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
            privacy.Property(p => p.ShowOnlineStatus).HasColumnName("show_online_status").HasDefaultValue(true);
            privacy.Property(p => p.ShowLastVisitTime).HasColumnName("show_last_visit_time").HasDefaultValue(true);
        });

        builder.OwnsOne(u => u.Notifications, notifications =>
        {
            notifications.Property(n => n.NewMessages).HasColumnName("notify_new_messages").HasDefaultValue(true);
            notifications.Property(n => n.NotificationSound).HasColumnName("notify_sound").HasDefaultValue(true);
            notifications.Property(n => n.DisciplineChatMessages).HasColumnName("notify_discipline_chats").HasDefaultValue(true);
            notifications.Property(n => n.Mentions).HasColumnName("notify_mentions").HasDefaultValue(true);
        });

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
        builder.Navigation(u => u.Notifications).IsRequired();
        builder.Navigation(u => u.SoundVideo).IsRequired();
    }
}
