using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed record UpdateInquiryStatusCommand(Guid InquiryId, InquiryStatus NewStatus);
