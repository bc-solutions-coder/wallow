using Foundry.Inquiries.Domain.Enums;

namespace Foundry.Inquiries.Application.Queries.GetInquiries;

public sealed record GetInquiriesQuery(InquiryStatus? Status = null);
