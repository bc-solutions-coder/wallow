#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Inquiries.Domain.Exceptions;

public sealed class InvalidInquiryStatusTransitionException(string from, string to)
    : BusinessRuleException("Inquiries.InvalidStatusTransition", $"Cannot transition from {from} to {to}");
