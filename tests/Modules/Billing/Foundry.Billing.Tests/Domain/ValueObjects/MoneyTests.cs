using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Tests.Domain.ValueObjects;

public class MoneyTests
{
    [Theory]
    [InlineData(0, "USD")]
    [InlineData(100, "USD")]
    [InlineData(99999.99, "EUR")]
    [InlineData(0.01, "GBP")]
    public void Create_WithValidData_ReturnsMoney(decimal amount, string currency)
    {
#pragma warning disable CA1062 // Test data is controlled via InlineData
        Money money = Money.Create(amount, currency);

        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency.ToUpperInvariant());
#pragma warning restore CA1062
    }

    [Fact]
    public void Create_WithLowercaseCurrency_NormalizesToUppercase()
    {
        Money money = Money.Create(100, "usd");

        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_ThrowsBusinessRuleException()
    {
        Func<Money> act = () => Money.Create(-1, "USD");

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.InvalidMoney")
            .WithMessage("*negative*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U")]
    [InlineData("   ")]
    public void Create_WithInvalidCurrency_ThrowsBusinessRuleException(string currency)
    {
        Func<Money> act = () => Money.Create(100, currency);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.InvalidMoney")
            .WithMessage("*3-letter*");
    }

    [Fact]
    public void Create_WithNullCurrency_ThrowsBusinessRuleException()
    {
        Func<Money> act = () => Money.Create(100, null!);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Zero_ReturnsMoneyWithZeroAmount()
    {
        Money money = Money.Zero("USD");

        money.Amount.Should().Be(0);
        money.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Zero_WithInvalidCurrency_ThrowsBusinessRuleException(string currency)
    {
        Func<Money> act = () => Money.Zero(currency);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedAmount()
    {
        Money a = Money.Create(100, "USD");
        Money b = Money.Create(50, "USD");

        Money result = a + b;

        result.Amount.Should().Be(150);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_SameCurrency_MultipleAdditions()
    {
        Money a = Money.Create(10, "USD");
        Money b = Money.Create(20, "USD");
        Money c = Money.Create(30, "USD");

        Money result = a + b + c;

        result.Amount.Should().Be(60);
    }

    [Fact]
    public void Add_DifferentCurrencies_ThrowsBusinessRuleException()
    {
        Money usd = Money.Create(100, "USD");
        Money eur = Money.Create(50, "EUR");

        Func<Money> act = () => usd + eur;

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.InvalidMoney")
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Equals_SameAmountAndCurrency_ReturnsTrue()
    {
        Money a = Money.Create(100, "USD");
        Money b = Money.Create(100, "USD");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentAmount_ReturnsFalse()
    {
        Money a = Money.Create(100, "USD");
        Money b = Money.Create(200, "USD");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentCurrency_ReturnsFalse()
    {
        Money a = Money.Create(100, "USD");
        Money b = Money.Create(100, "EUR");

        a.Should().NotBe(b);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        Money a = Money.Create(100, "USD");
        Money b = Money.Create(100, "USD");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        Money money = Money.Create(100.50m, "USD");

        money.ToString().Should().Be("100.50 USD");
    }

    [Fact]
    public void ToString_WithWholeNumber_ShowsTwoDecimals()
    {
        Money money = Money.Create(100, "EUR");

        money.ToString().Should().Be("100.00 EUR");
    }
}
