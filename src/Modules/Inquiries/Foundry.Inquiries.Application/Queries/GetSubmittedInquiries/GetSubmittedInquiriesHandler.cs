using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;

public sealed class GetSubmittedInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetSubmittedInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetBySubmitterAsync(
            query.SubmitterId, cancellationToken);

        List<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
