using Wallow.Inquiries.Domain.Errors;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Exceptions;

public sealed class InvalidInquiryStatusTransitionException(string from, string to)
    : BusinessRuleException(InquiriesErrors.InvalidStatusTransition, $"Cannot transition from {from} to {to}");
