using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetPaymentsByInvoiceId;

public sealed class GetPaymentsByInvoiceIdHandler(IPaymentRepository paymentRepository)
{
    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(
        GetPaymentsByInvoiceIdQuery query,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(query.InvoiceId);
        IReadOnlyList<Payment> payments = await paymentRepository.GetByInvoiceIdAsync(invoiceId, cancellationToken);
        List<PaymentDto> dtos = payments.Select(p => p.ToDto()).ToList();
        return Result.Success<IReadOnlyList<PaymentDto>>(dtos);
    }
}
