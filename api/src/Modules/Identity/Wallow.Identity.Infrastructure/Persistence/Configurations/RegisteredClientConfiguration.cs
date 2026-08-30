using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class RegisteredClientConfiguration : IEntityTypeConfiguration<RegisteredClient>
{
    public void Configure(EntityTypeBuilder<RegisteredClient> builder)
    {
        builder.ToTable("registered_clients");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => RegisteredClientId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(e => e.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(e => e.LastRotatedByUserId)
            .HasColumnName("last_rotated_by_user_id");

        builder.Property(e => e.LastRotatedAt)
            .HasColumnName("last_rotated_at");

        builder.HasIndex(e => e.ClientId).IsUnique();
        builder.HasIndex(e => e.OrganizationId);
    }
}
