using Wallow.Shared.Kernel.Errors;

namespace Wallow.Inquiries.Domain.Errors;

/// <summary>
/// The error catalog the Inquiries module owns. Registered by <c>AddInquiriesModule</c>.
/// </summary>
public static class InquiriesErrors
{
    public static readonly ErrorCatalogEntry InquiryNotFound = new(
        "Inquiry.NotFound", ErrorKind.NotFound, "Inquiry not found");

    public static readonly ErrorCatalogEntry InvalidStatusTransition = new(
        "Inquiries.InvalidStatusTransition", ErrorKind.BusinessRule, "The inquiry cannot move to that status");
}
