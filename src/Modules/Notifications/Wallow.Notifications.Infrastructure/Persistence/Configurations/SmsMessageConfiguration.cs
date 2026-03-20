using Wallow.Notifications.Domain.Channels.Sms.Entities;
using Wallow.Notifications.Domain.Channels.Sms.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Notifications.Infrastructure.Persistence.Configurations;

public sealed class SmsMessageConfiguration : IEntityTypeConfiguration<SmsMessage>
{
    public void Configure(EntityTypeBuilder<SmsMessage> builder)
    {
        builder.ToTable("sms_messages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<SmsMessageId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.OwnsOne(e => e.To, to =>
        {
            to.Property(p => p.Value)
                .HasColumnName("to_phone_number")
                .HasMaxLength(20)
                .IsRequired();
        });

        builder.OwnsOne(e => e.From, from =>
        {
            from.Property(p => p.Value)
                .HasColumnName("from_phone_number")
                .HasMaxLength(20);
        });

        builder.Property(e => e.Body)
            .HasColumnName("body")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.SentAt)
            .HasColumnName("sent_at");

        builder.Property(e => e.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(1000);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
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
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);

        builder.Ignore(e => e.DomainEvents);
    }
}
