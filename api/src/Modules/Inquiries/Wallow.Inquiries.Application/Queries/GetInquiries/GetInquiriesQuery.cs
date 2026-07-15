using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Application.Queries.GetInquiries;

public sealed record GetInquiriesQuery(InquiryStatus? Status = null);
