using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetInquiryById;

public sealed class GetInquiryByIdHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<InquiryDto>> Handle(
        GetInquiryByIdQuery query,
        CancellationToken cancellationToken)
    {
        InquiryId inquiryId = InquiryId.Create(query.InquiryId);
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(inquiryId, cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryDto>(Error.NotFound("Inquiry", query.InquiryId));
        }

        return Result.Success(inquiry.ToDto());
    }
}
