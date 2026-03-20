using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetInquiries;

public sealed class GetInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetAllAsync(cancellationToken);

        IEnumerable<Inquiry> filtered = query.Status is not null
            ? inquiries.Where(i => i.Status == query.Status.Value)
            : inquiries;

        List<InquiryDto> dtos = filtered.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
