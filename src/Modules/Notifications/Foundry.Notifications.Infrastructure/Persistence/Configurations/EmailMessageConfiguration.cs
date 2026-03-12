using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Channels.Email.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Notifications.Infrastructure.Persistence.Configurations;

public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("email_messages");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<EmailMessageId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.OwnsOne(e => e.To, to =>
        {
            to.Property(ea => ea.Value)
                .HasColumnName("to_address")
                .HasMaxLength(256)
                .IsRequired();
        });

        builder.OwnsOne(e => e.From, from =>
        {
            from.Property(ea => ea.Value)
                .HasColumnName("from_address")
                .HasMaxLength(256);
        });

        builder.OwnsOne(e => e.Content, content =>
        {
            content.Property(c => c.Subject)
                .HasColumnName("subject")
                .HasMaxLength(500)
                .IsRequired();

            content.Property(c => c.Body)
                .HasColumnName("body")
                .IsRequired();
        });

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
