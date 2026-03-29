using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Inquiries.Infrastructure.Persistence.Configurations;

public sealed class InquiryCommentConfiguration : IEntityTypeConfiguration<InquiryComment>
{
    public void Configure(EntityTypeBuilder<InquiryComment> builder)
    {
        builder.ToTable("inquiry_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(new StronglyTypedIdConverter<InquiryCommentId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.InquiryId)
            .HasConversion(new StronglyTypedIdConverter<InquiryId>())
            .HasColumnName("inquiry_id")
            .IsRequired();

        builder.Property(c => c.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.AuthorId)
            .HasColumnName("author_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.AuthorName)
            .HasColumnName("author_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(c => c.IsInternal)
            .HasColumnName("is_internal")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(c => c.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Ignore(c => c.DomainEvents);

        builder.HasOne<Inquiry>()
            .WithMany()
            .HasForeignKey(c => c.InquiryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.InquiryId);
        builder.HasIndex(c => c.TenantId);
    }
}
