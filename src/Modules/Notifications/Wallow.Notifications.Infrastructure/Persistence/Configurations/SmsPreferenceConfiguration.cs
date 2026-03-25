using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Infrastructure.Persistence.Configurations;

public sealed class SmsPreferenceConfiguration : IEntityTypeConfiguration<SmsPreference>
{
    public void Configure(EntityTypeBuilder<SmsPreference> builder)
    {
        builder.ToTable("sms_preferences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<SmsPreferenceId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.OwnsOne(e => e.PhoneNumber, phone =>
        {
            phone.Property(p => p.Value)
                .HasColumnName("phone_number")
                .HasMaxLength(20)
                .IsRequired();
        });

        builder.Property(e => e.IsOptedIn)
            .HasColumnName("is_opted_in")
            .IsRequired();

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.UserId })
            .IsUnique();
    }
}
