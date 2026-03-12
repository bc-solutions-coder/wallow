using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Notifications.Infrastructure.Persistence.Configurations;

public sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("device_registrations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<DeviceRegistrationId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, value => UserId.Create(value))
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Platform)
            .HasColumnName("platform")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Token)
            .HasColumnName("token")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(e => e.RegisteredAt)
            .HasColumnName("registered_at")
            .IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.Token, e.TenantId })
            .IsUnique();
    }
}
