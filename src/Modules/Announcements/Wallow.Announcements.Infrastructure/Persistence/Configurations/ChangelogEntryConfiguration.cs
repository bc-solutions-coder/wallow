using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Announcements.Infrastructure.Persistence.Configurations;

public sealed class ChangelogEntryConfiguration : IEntityTypeConfiguration<ChangelogEntry>
{
    public void Configure(EntityTypeBuilder<ChangelogEntry> builder)
    {
        builder.ToTable("changelog_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<ChangelogEntryId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(e => e.ReleasedAt)
            .HasColumnName("released_at")
            .IsRequired();

        builder.Property(e => e.IsPublished)
            .HasColumnName("is_published")
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

        builder.Ignore(e => e.DomainEvents);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.Version).IsUnique();
        builder.HasIndex(e => e.IsPublished);
        builder.HasIndex(e => e.ReleasedAt);
    }
}
