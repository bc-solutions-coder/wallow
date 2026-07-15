using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Notifications.Domain.Preferences.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Infrastructure.Persistence.Configurations;

public sealed class ChannelPreferenceConfiguration : IEntityTypeConfiguration<ChannelPreference>
{
    public void Configure(EntityTypeBuilder<ChannelPreference> builder)
    {
        builder.ToTable("channel_preferences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<ChannelPreferenceId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.ChannelType)
            .HasColumnName("channel_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.NotificationType)
            .HasColumnName("notification_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.UserId });
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.ChannelType, e.NotificationType })
            .IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
