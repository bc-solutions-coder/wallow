using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Domain;
using Wallow.Tests.Common.Builders;

namespace Wallow.Billing.Tests.Domain.Entities;

public class InvoiceLineItemLineTotalTests
{
    [Fact]
    public void AddLineItem_WithValidData_ComputesLineTotalAsUnitPriceTimesQuantity()
    {
        Invoice invoice = InvoiceBuilder.Create().Build();
        Money unitPrice = Money.Create(10.00m, "USD");

        invoice.AddLineItem("Widget", unitPrice, 3, Guid.NewGuid(), TimeProvider.System);

        InvoiceLineItem lineItem = invoice.LineItems.Should().ContainSingle().Which;
        lineItem.UnitPrice.Amount.Should().Be(10.00m);
        lineItem.Quantity.Should().Be(3);
        lineItem.LineTotal.Amount.Should().Be(30.00m);
        lineItem.LineTotal.Currency.Should().Be("USD");
    }

    [Fact]
    public void AddLineItem_WithQuantityOne_LineTotalEqualsUnitPrice()
    {
        Invoice invoice = InvoiceBuilder.Create().Build();
        Money unitPrice = Money.Create(25.50m, "USD");

        invoice.AddLineItem("Service", unitPrice, 1, Guid.NewGuid(), TimeProvider.System);

        InvoiceLineItem lineItem = invoice.LineItems.Should().ContainSingle().Which;
        lineItem.LineTotal.Amount.Should().Be(25.50m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLineItem_WithBlankDescription_ThrowsBusinessRuleException(string description)
    {
        Invoice invoice = InvoiceBuilder.Create().Build();
        Money unitPrice = Money.Create(10.00m, "USD");

        Action act = () => invoice.AddLineItem(description, unitPrice, 1, Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>();
    }
}
