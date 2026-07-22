using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;

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
            .HasConversion(new EnumToStringConverter<OrgMemberRole>())
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex("organization_id", nameof(OrganizationMember.UserId));
        builder.HasIndex(e => e.UserId);
    }
}
