using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => MembershipId.Create(value))
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => OrganizationId.Create(value))
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.IsOwner).HasColumnName("is_owner").IsRequired();
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at");
        builder.Property(e => e.JoinedAt).HasColumnName("joined_at");
        builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.RoleIds);
        builder.Ignore(e => e.IsActive);

        // The uniqueness guarantee the design doc states as a composite primary key. The surrogate
        // key exists because Entity<TId> carries a single Id; this index is what makes a second
        // membership for the same pair impossible.
        builder.HasIndex(e => new { e.UserId, e.OrganizationId }).IsUnique();
        builder.HasIndex(e => e.OrganizationId);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.OwnsMany<MembershipRole>("_roles", role =>
        {
            role.ToTable("membership_roles");
            role.WithOwner().HasForeignKey(r => r.MembershipId);
            role.HasKey(r => new { r.MembershipId, r.RoleId });

            role.Property(r => r.MembershipId)
                .HasConversion(
                    id => id.Value,
                    value => MembershipId.Create(value))
                .HasColumnName("membership_id");

            role.Property(r => r.RoleId).HasColumnName("role_id");

            // Referential integrity against the global role catalog: without it, deleting a role
            // leaves dangling GUIDs the resolver silently drops.
            role.HasOne<WallowRole>()
                .WithMany()
                .HasForeignKey(r => r.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            role.HasIndex(r => r.RoleId);
        });
    }
}
