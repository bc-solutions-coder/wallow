using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.Email.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Infrastructure.Persistence.Configurations;

public sealed class EmailPreferenceConfiguration : IEntityTypeConfiguration<EmailPreference>
{
    public void Configure(EntityTypeBuilder<EmailPreference> builder)
    {
        builder.ToTable("email_preferences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<EmailPreferenceId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.NotificationType)
            .HasColumnName("notification_type")
            .HasConversion<string>()
            .HasMaxLength(50)
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
        builder.HasIndex(e => new { e.TenantId, e.UserId, e.NotificationType })
            .IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
