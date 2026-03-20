using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Entities;

/// <summary>
/// Represents a single line item on an invoice.
/// </summary>
public sealed class InvoiceLineItem : Entity<InvoiceLineItemId>
{
    public InvoiceId InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money LineTotal { get; private set; } = null!;

    // ReSharper disable once UnusedMember.Local
    private InvoiceLineItem() { } // EF Core

    private InvoiceLineItem(InvoiceId invoiceId, string description, Money unitPrice, int quantity)
    {
        Id = InvoiceLineItemId.New();
        InvoiceId = invoiceId;
        Description = description;
        UnitPrice = unitPrice;
        Quantity = quantity;
        LineTotal = Money.Create(unitPrice.Amount * quantity, unitPrice.Currency);
    }

    internal static InvoiceLineItem Create(InvoiceId invoiceId, string description, Money unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BusinessRuleException("Billing.LineItemDescriptionRequired", "Line item description cannot be empty");
        }

        return new InvoiceLineItem(invoiceId, description, unitPrice, quantity);
    }
}
