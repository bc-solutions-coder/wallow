using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Tests.Domain.Metering;

public class MeterDefinitionCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsMeterDefinition()
    {
        MeterDefinition meter = MeterDefinition.Create(
            "api.calls",
            "API Calls",
            "requests",
            MeterAggregation.Sum,
            true);

        meter.Code.Should().Be("api.calls");
        meter.DisplayName.Should().Be("API Calls");
        meter.Unit.Should().Be("requests");
        meter.Aggregation.Should().Be(MeterAggregation.Sum);
        meter.IsBillable.Should().BeTrue();
        meter.ValkeyKeyPattern.Should().BeNull();
    }

    [Fact]
    public void Create_WithValkeyKeyPattern_SetsPattern()
    {
        MeterDefinition meter = MeterDefinition.Create(
            "api.calls",
            "API Calls",
            "requests",
            MeterAggregation.Sum,
            true,
            "meter:{tenantId}:api.calls:{period}");

        meter.ValkeyKeyPattern.Should().Be("meter:{tenantId}:api.calls:{period}");
    }

    [Fact]
    public void Create_WithCreatedByUserId_SetsAuditFields()
    {
        Guid userId = Guid.NewGuid();

        MeterDefinition meter = MeterDefinition.Create(
            "api.calls",
            "API Calls",
            "requests",
            MeterAggregation.Sum,
            true,
            null,
            userId);

        meter.Id.Should().NotBeNull();
        meter.Id.Value.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyCode_ThrowsBusinessRuleException(string? code)
    {
        Action act = () => MeterDefinition.Create(
            code!,
            "API Calls",
            "requests",
            MeterAggregation.Sum,
            true);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.CodeRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException(string? displayName)
    {
        Action act = () => MeterDefinition.Create(
            "api.calls",
            displayName!,
            "requests",
            MeterAggregation.Sum,
            true);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.DisplayNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyUnit_ThrowsBusinessRuleException(string? unit)
    {
        Action act = () => MeterDefinition.Create(
            "api.calls",
            "API Calls",
            unit!,
            MeterAggregation.Sum,
            true);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.UnitRequired");
    }
}

public class MeterDefinitionUpdateTests
{
    [Fact]
    public void Update_WithValidData_UpdatesProperties()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        Guid userId = Guid.NewGuid();

        meter.Update("API Requests", "calls", MeterAggregation.Max, false, "new:pattern", userId);

        meter.DisplayName.Should().Be("API Requests");
        meter.Unit.Should().Be("calls");
        meter.Aggregation.Should().Be(MeterAggregation.Max);
        meter.IsBillable.Should().BeFalse();
        meter.ValkeyKeyPattern.Should().Be("new:pattern");
    }

    [Fact]
    public void Update_CodeCannotBeChanged()
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);
        string originalCode = meter.Code;

        meter.Update("Updated", "requests", MeterAggregation.Sum, true, null, Guid.NewGuid());

        meter.Code.Should().Be(originalCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_WithEmptyDisplayName_ThrowsBusinessRuleException(string? displayName)
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        Action act = () => meter.Update(displayName!, "requests", MeterAggregation.Sum, true, null, Guid.NewGuid());

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.DisplayNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Update_WithEmptyUnit_ThrowsBusinessRuleException(string? unit)
    {
        MeterDefinition meter = MeterDefinition.Create("api.calls", "API Calls", "requests", MeterAggregation.Sum, true);

        Action act = () => meter.Update("API Calls", unit!, MeterAggregation.Sum, true, null, Guid.NewGuid());

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.UnitRequired");
    }
}
