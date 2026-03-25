namespace Wallow.Inquiries.Application.Queries.GetInquiryById;

public sealed record GetInquiryByIdQuery(Guid InquiryId, Guid? TenantId = null);
