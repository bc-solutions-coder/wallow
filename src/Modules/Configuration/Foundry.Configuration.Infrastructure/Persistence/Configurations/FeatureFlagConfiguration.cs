using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Configuration.Infrastructure.Persistence.Configurations;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => FeatureFlagId.Create(value))
            .IsRequired();

        builder.Property(f => f.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(f => f.Key)
            .IsUnique();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(f => f.FlagType)
            .HasColumnName("flag_type")
            .IsRequired();

        builder.Property(f => f.DefaultEnabled)
            .HasColumnName("default_enabled")
            .IsRequired();

        builder.Property(f => f.RolloutPercentage)
            .HasColumnName("rollout_percentage");

        builder.OwnsMany(f => f.Variants, variant =>
        {
            variant.ToTable("feature_flag_variants");
            variant.Property<Guid>("Id").ValueGeneratedOnAdd();
            variant.HasKey("Id");
            variant.WithOwner().HasForeignKey("FeatureFlagId");
            variant.Property(v => v.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            variant.Property(v => v.Weight).HasColumnName("weight").IsRequired();
        });

        builder.Property(f => f.DefaultVariant)
            .HasColumnName("default_variant")
            .HasMaxLength(100);

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Ignore(f => f.CreatedBy);
        builder.Ignore(f => f.UpdatedBy);

        builder.HasMany(f => f.Overrides)
            .WithOne(o => o.Flag)
            .HasForeignKey(o => o.FlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
