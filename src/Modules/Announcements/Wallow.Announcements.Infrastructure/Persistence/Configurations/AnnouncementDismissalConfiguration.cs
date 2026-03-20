using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Announcements.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementDismissalConfiguration : IEntityTypeConfiguration<AnnouncementDismissal>
{
    public void Configure(EntityTypeBuilder<AnnouncementDismissal> builder)
    {
        builder.ToTable("announcement_dismissals");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(new StronglyTypedIdConverter<AnnouncementDismissalId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.AnnouncementId)
            .HasConversion(new StronglyTypedIdConverter<AnnouncementId>())
            .HasColumnName("announcement_id")
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasConversion(new StronglyTypedIdConverter<UserId>())
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(d => d.DismissedAt)
            .HasColumnName("dismissed_at")
            .IsRequired();

        builder.HasIndex(d => d.AnnouncementId);
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.AnnouncementId, d.UserId }).IsUnique();
    }
}
