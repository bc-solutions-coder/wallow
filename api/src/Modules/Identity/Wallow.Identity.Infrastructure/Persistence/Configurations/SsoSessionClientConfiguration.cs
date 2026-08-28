using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class SsoSessionClientConfiguration : IEntityTypeConfiguration<SsoSessionClient>
{
    public void Configure(EntityTypeBuilder<SsoSessionClient> builder)
    {
        builder.ToTable("sso_session_clients");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => SsoSessionClientId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.Sid)
            .HasColumnName("sid")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(e => new { e.Sid, e.ClientId }).IsUnique();
        builder.HasIndex(e => e.CreatedAt);
    }
}
