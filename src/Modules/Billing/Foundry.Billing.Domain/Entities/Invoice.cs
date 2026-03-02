using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Exceptions;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.Entities;

/// <summary>
/// Invoice aggregate root. Represents a bill sent to a user.
/// </summary>
/// <remarks>
/// State machine transitions (derived from guard clauses):
/// <code>
///                  ┌──────────────────────────────┐
///                  │                              │
///   [Create]       ▼        Issue()               │  Cancel()
///     ──► Draft ─────────► Issued ────┐           │
///           │                │        │           │
///           │  Cancel()      │        │ MarkAsPaid()
///           ▼                │        │           │
///        Cancelled ◄─────── │        ▼           │
///           ▲                │       Paid         │
///           │    MarkAsOverdue()                  │
///           │                │                    │
///           │                ▼                    │
///           └──────── Overdue ────────────────────┘
///                     Cancel()    MarkAsPaid()
/// </code>
/// </remarks>
public sealed class Invoice : AggregateRoot<InvoiceId>, ITenantScoped, IHasCustomFields
{
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public InvoiceStatus Status { get; private set; }
    public Money TotalAmount { get; private set; } = null!;
    public DateTime? DueDate { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Dictionary<string, object>? CustomFields { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    public void SetCustomFields(Dictionary<string, object>? customFields)
    {
        CustomFields = customFields;
    }

    private Invoice() { } // EF Core

    private Invoice(Guid userId, string invoiceNumber, string currency, DateTime? dueDate)
    {
        Id = InvoiceId.New();
        UserId = userId;
        InvoiceNumber = invoiceNumber;
        Status = InvoiceStatus.Draft;
        TotalAmount = Money.Zero(currency);
        DueDate = dueDate;
    }

    public static Invoice Create(
        Guid userId,
        string invoiceNumber,
        string currency,
        Guid createdByUserId,
        TimeProvider timeProvider,
        DateTime? dueDate = null,
        Dictionary<string, object>? customFields = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new BusinessRuleException("Billing.InvoiceNumberRequired", "Invoice number cannot be empty");
        }

        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException("Billing.UserIdRequired", "User ID is required");
        }

        Invoice invoice = new Invoice(userId, invoiceNumber, currency, dueDate);
        invoice.CustomFields = customFields;
        invoice.SetCreated(timeProvider.GetUtcNow(), createdByUserId);

        invoice.RaiseDomainEvent(new InvoiceCreatedDomainEvent(
            invoice.Id.Value, userId, 0m, currency));

        return invoice;
    }

    public void AddLineItem(string description, Money unitPrice, int quantity, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidInvoiceException("Can only add line items to draft invoices");
        }

        if (quantity <= 0)
        {
            throw new BusinessRuleException("Billing.InvalidQuantity", "Quantity must be greater than zero");
        }

        InvoiceLineItem lineItem = InvoiceLineItem.Create(Id, description, unitPrice, quantity);
        _lineItems.Add(lineItem);
        RecalculateTotal();
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void RemoveLineItem(InvoiceLineItemId lineItemId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidInvoiceException("Can only remove line items from draft invoices");
        }

        InvoiceLineItem? item = _lineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (item is null)
        {
            throw new BusinessRuleException("Billing.LineItemNotFound", "Line item not found on this invoice");
        }

        _lineItems.Remove(item);
        RecalculateTotal();
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Issue(Guid issuedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidInvoiceException("Can only issue draft invoices");
        }

        if (_lineItems.Count == 0)
        {
            throw new InvalidInvoiceException("Cannot issue an invoice with no line items");
        }

        Status = InvoiceStatus.Issued;
        SetUpdated(timeProvider.GetUtcNow(), issuedByUserId);
    }

    public void MarkAsPaid(Guid paymentId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Issued && Status != InvoiceStatus.Overdue)
        {
            throw new InvalidInvoiceException("Can only mark issued or overdue invoices as paid");
        }

        Status = InvoiceStatus.Paid;
        PaidAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

        RaiseDomainEvent(new InvoicePaidDomainEvent(Id.Value, paymentId, PaidAt.Value));
    }

    public void MarkAsOverdue(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Issued)
        {
            throw new InvalidInvoiceException("Can only mark issued invoices as overdue");
        }

        Status = InvoiceStatus.Overdue;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

        RaiseDomainEvent(new InvoiceOverdueDomainEvent(Id.Value, UserId, DueDate ?? timeProvider.GetUtcNow().UtcDateTime));
    }

    public void Cancel(Guid cancelledByUserId, TimeProvider timeProvider)
    {
        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidInvoiceException("Cannot cancel a paid invoice");
        }

        Status = InvoiceStatus.Cancelled;
        SetUpdated(timeProvider.GetUtcNow(), cancelledByUserId);
    }

    private void RecalculateTotal()
    {
        Money total = Money.Zero(TotalAmount.Currency);
        foreach (InvoiceLineItem item in _lineItems)
        {
            total = total + item.LineTotal;
        }
        TotalAmount = total;
    }
}
