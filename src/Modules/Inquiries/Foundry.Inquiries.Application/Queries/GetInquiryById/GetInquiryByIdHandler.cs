using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Queries.GetInquiryById;

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
