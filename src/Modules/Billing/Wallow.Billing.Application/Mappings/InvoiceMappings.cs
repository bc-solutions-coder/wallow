using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Domain.Entities;

namespace Wallow.Billing.Application.Mappings;

public static class InvoiceMappings
{
    public static InvoiceDto ToDto(this Invoice invoice)
    {
        return new InvoiceDto(
            Id: invoice.Id.Value,
            UserId: invoice.UserId,
            InvoiceNumber: invoice.InvoiceNumber,
            Status: invoice.Status.ToString(),
            TotalAmount: invoice.TotalAmount.Amount,
            Currency: invoice.TotalAmount.Currency,
            DueDate: invoice.DueDate,
            PaidAt: invoice.PaidAt,
            CreatedAt: invoice.CreatedAt,
            UpdatedAt: invoice.UpdatedAt,
            LineItems: invoice.LineItems.Select(li => li.ToDto()).ToList(),
            CustomFields: invoice.CustomFields);
    }

    public static InvoiceLineItemDto ToDto(this InvoiceLineItem lineItem)
    {
        return new InvoiceLineItemDto(
            Id: lineItem.Id.Value,
            Description: lineItem.Description,
            UnitPrice: lineItem.UnitPrice.Amount,
            Currency: lineItem.UnitPrice.Currency,
            Quantity: lineItem.Quantity,
            LineTotal: lineItem.LineTotal.Amount);
    }
}
