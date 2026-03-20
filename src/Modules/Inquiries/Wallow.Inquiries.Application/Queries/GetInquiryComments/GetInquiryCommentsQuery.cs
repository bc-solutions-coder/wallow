using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Queries.GetInquiryComments;

public sealed record GetInquiryCommentsQuery(InquiryId InquiryId, bool IncludeInternal);
