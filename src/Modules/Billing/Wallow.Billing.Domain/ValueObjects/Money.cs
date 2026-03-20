using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new BusinessRuleException(
                "Billing.InvalidMoney",
                "Money amount cannot be negative");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new BusinessRuleException(
                "Billing.InvalidMoney",
                "Currency must be a 3-letter ISO code");
        }

        return new Money(amount, currency.ToUpperInvariant());
    }

    public static Money Zero(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new BusinessRuleException(
                "Billing.InvalidMoney",
                "Currency must be a 3-letter ISO code");
        }

        return new Money(0, currency.ToUpperInvariant());
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new BusinessRuleException(
                "Billing.InvalidMoney",
                $"Cannot add money with different currencies: {left.Currency} and {right.Currency}");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public override string ToString()
    {
        return $"{Amount:F2} {Currency}";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
