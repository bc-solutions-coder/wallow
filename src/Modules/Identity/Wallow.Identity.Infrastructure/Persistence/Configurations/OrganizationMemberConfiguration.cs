using Wallow.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");

        builder.HasKey("organization_id", nameof(OrganizationMember.UserId));

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.Role)
            .HasColumnName("role")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex("organization_id", nameof(OrganizationMember.UserId));
        builder.HasIndex(e => e.UserId);
    }
}
