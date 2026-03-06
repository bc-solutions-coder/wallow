using Foundry.Inquiries.Domain.Enums;

namespace Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed record UpdateInquiryStatusCommand(Guid InquiryId, InquiryStatus NewStatus);
