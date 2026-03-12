using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Announcements.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(new StronglyTypedIdConverter<AnnouncementId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.HasIndex(a => a.TenantId);

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(a => a.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(a => a.Target)
            .HasColumnName("target")
            .IsRequired();

        builder.Property(a => a.TargetValue)
            .HasColumnName("target_value")
            .HasMaxLength(200);

        builder.Property(a => a.PublishAt)
            .HasColumnName("publish_at");

        builder.Property(a => a.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(a => a.IsPinned)
            .HasColumnName("is_pinned")
            .IsRequired();

        builder.Property(a => a.IsDismissible)
            .HasColumnName("is_dismissible")
            .IsRequired();

        builder.Property(a => a.ActionUrl)
            .HasColumnName("action_url")
            .HasMaxLength(500);

        builder.Property(a => a.ActionLabel)
            .HasColumnName("action_label")
            .HasMaxLength(100);

        builder.Property(a => a.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(a => a.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Ignore(a => a.DomainEvents);

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.Target);
        builder.HasIndex(a => a.PublishAt);
        builder.HasIndex(a => a.ExpiresAt);
    }
}
