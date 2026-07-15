using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Events;

public sealed record InquirySubmittedDomainEvent(
    Guid InquiryId,
    string Name,
    string Email,
    string Phone,
    string? Company,
    string? SubmitterId,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message) : DomainEvent;
