using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Events;

public sealed record InquiryStatusChangedDomainEvent(
    Guid InquiryId,
    string OldStatus,
    string NewStatus) : DomainEvent;
