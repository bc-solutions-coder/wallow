using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class ActiveSessionConfiguration : IEntityTypeConfiguration<ActiveSession>
{
    public void Configure(EntityTypeBuilder<ActiveSession> builder)
    {
        builder.ToTable("active_sessions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => ActiveSessionId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.SessionToken)
            .HasColumnName("session_token")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.LastActivityAt)
            .HasColumnName("last_activity_at")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(e => e.IsRevoked)
            .HasColumnName("is_revoked")
            .HasDefaultValue(false);

        builder.HasIndex(e => e.SessionToken).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.IsRevoked, e.ExpiresAt });
    }
}
