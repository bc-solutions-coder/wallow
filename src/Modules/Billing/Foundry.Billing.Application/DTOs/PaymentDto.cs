using JetBrains.Annotations;

namespace Foundry.Billing.Application.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? TransactionReference,
    string? FailureReason,
    DateTime? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Dictionary<string, object>? CustomFields);
