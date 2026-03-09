using JetBrains.Annotations;

namespace Foundry.Billing.Application.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvoiceDto(
    Guid Id,
    Guid UserId,
    string InvoiceNumber,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime? DueDate,
    DateTime? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<InvoiceLineItemDto> LineItems,
    Dictionary<string, object>? CustomFields);
