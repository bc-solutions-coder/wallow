using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetPaymentById;

public sealed class GetPaymentByIdHandler(IPaymentRepository paymentRepository)
{
    public async Task<Result<PaymentDto>> Handle(
        GetPaymentByIdQuery query,
        CancellationToken cancellationToken)
    {
        PaymentId paymentId = PaymentId.Create(query.PaymentId);
        Payment? payment = await paymentRepository.GetByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            return Result.Failure<PaymentDto>(Error.NotFound("Payment", query.PaymentId));
        }

        return Result.Success(payment.ToDto());
    }
}
