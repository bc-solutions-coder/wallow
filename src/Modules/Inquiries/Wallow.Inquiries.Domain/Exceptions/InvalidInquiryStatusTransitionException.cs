using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Exceptions;

#pragma warning disable CA1032
public sealed class InvalidInquiryStatusTransitionException(string from, string to)
#pragma warning restore CA1032
    : BusinessRuleException("Inquiries.InvalidStatusTransition", $"Cannot transition from {from} to {to}");
