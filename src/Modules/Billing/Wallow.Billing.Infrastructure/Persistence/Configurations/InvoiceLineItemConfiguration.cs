using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Billing.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("invoice_line_items");

        builder.HasKey(li => li.Id);
        builder.Property(li => li.Id)
            .HasConversion(new StronglyTypedIdConverter<InvoiceLineItemId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(li => li.InvoiceId)
            .HasConversion(new StronglyTypedIdConverter<InvoiceId>())
            .HasColumnName("invoice_id")
            .IsRequired();

        builder.Property(li => li.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.OwnsOne(li => li.UnitPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("unit_price")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(li => li.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.OwnsOne(li => li.LineTotal, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("line_total")
                .HasPrecision(18, 2)
                .IsRequired();
            money.Property(m => m.Currency)
                .HasColumnName("line_total_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.HasIndex(li => li.InvoiceId);
    }
}
