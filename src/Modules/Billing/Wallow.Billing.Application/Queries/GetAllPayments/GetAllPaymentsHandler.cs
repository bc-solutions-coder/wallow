using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetAllPayments;

public sealed class GetAllPaymentsHandler(IPaymentRepository paymentRepository)
{
    public async Task<Result<PagedResult<PaymentDto>>> Handle(
        GetAllPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        int totalCount = await paymentRepository.CountAllAsync(cancellationToken);
        IReadOnlyList<Payment> payments = await paymentRepository.GetAllAsync(query.Skip, query.Take, cancellationToken);
        List<PaymentDto> dtos = payments.Select(p => p.ToDto()).ToList();
        int page = (query.Skip / query.Take) + 1;
        return Result.Success(new PagedResult<PaymentDto>(dtos, totalCount, page, query.Take));
    }
}
