using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Application.Queries.GetInquiryComments;

public sealed record GetInquiryCommentsQuery(InquiryId InquiryId, bool IncludeInternal);
