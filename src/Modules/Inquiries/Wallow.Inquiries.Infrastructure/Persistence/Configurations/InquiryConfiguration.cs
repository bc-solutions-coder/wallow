using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Inquiries.Infrastructure.Persistence.Configurations;

public sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.ToTable("inquiries");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(new StronglyTypedIdConverter<InquiryId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.Phone)
            .HasColumnName("phone")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Company)
            .HasColumnName("company")
            .HasMaxLength(200);

        builder.Property(i => i.SubmitterId)
            .HasColumnName("submitter_id")
            .HasMaxLength(200);

        builder.Property(i => i.ProjectType)
            .HasColumnName("project_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.BudgetRange)
            .HasColumnName("budget_range")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Timeline)
            .HasColumnName("timeline")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.Message)
            .HasColumnName("message")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(i => i.SubmitterIpAddress)
            .HasColumnName("submitter_ip_address")
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(i => i.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(i => i.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Ignore(i => i.DomainEvents);

        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);
        builder.HasIndex(i => i.Email);
    }
}
