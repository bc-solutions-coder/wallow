using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Showcases.Infrastructure.Persistence.Configurations;

internal sealed class ShowcaseConfiguration : IEntityTypeConfiguration<Showcase>
{
    public void Configure(EntityTypeBuilder<Showcase> builder)
    {
        builder.ToTable("showcases");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => ShowcaseId.Create(value))
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description");

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .IsRequired();

        builder.Property(s => s.DemoUrl)
            .HasColumnName("demo_url");

        builder.Property(s => s.GitHubUrl)
            .HasColumnName("github_url");

        builder.Property(s => s.VideoUrl)
            .HasColumnName("video_url");

        builder.Property<List<string>>("_tags")
            .HasColumnName("tags")
            .HasColumnType("text[]")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(s => s.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(s => s.IsPublished)
            .HasColumnName("is_published")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Ignore(s => s.CreatedBy);
        builder.Ignore(s => s.UpdatedBy);
    }
}
