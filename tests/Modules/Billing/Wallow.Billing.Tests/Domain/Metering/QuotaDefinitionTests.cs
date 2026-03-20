using Wallow.Billing.Domain.Metering.Entities;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Tests.Domain.Metering;

public class QuotaDefinitionCreatePlanQuotaTests
{
    [Fact]
    public void CreatePlanQuota_WithValidData_ReturnsQuotaDefinition()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        quota.MeterCode.Should().Be("api.calls");
        quota.PlanCode.Should().Be("free");
        quota.Limit.Should().Be(1000);
        quota.Period.Should().Be(QuotaPeriod.Monthly);
        quota.OnExceeded.Should().Be(QuotaAction.Block);
        quota.TenantId.Value.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreatePlanQuota_WithEmptyMeterCode_ThrowsBusinessRuleException(string? meterCode)
    {
        Action act = () => QuotaDefinition.CreatePlanQuota(
            meterCode!,
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.MeterCodeRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreatePlanQuota_WithEmptyPlanCode_ThrowsBusinessRuleException(string? planCode)
    {
        Action act = () => QuotaDefinition.CreatePlanQuota(
            "api.calls",
            planCode!,
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.PlanCodeRequired");
    }

    [Fact]
    public void CreatePlanQuota_WithNegativeLimit_ThrowsBusinessRuleException()
    {
        Action act = () => QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            -100,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidLimit");
    }
}

public class QuotaDefinitionCreateTenantOverrideTests
{
    [Fact]
    public void CreateTenantOverride_WithValidData_ReturnsQuotaDefinition()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        QuotaDefinition quota = QuotaDefinition.CreateTenantOverride(
            "api.calls",
            tenantId,
            5000,
            QuotaPeriod.Monthly,
            QuotaAction.Warn);

        quota.MeterCode.Should().Be("api.calls");
        quota.TenantId.Should().Be(tenantId);
        quota.PlanCode.Should().BeNull();
        quota.Limit.Should().Be(5000);
        quota.Period.Should().Be(QuotaPeriod.Monthly);
        quota.OnExceeded.Should().Be(QuotaAction.Warn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateTenantOverride_WithEmptyMeterCode_ThrowsBusinessRuleException(string? meterCode)
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        Action act = () => QuotaDefinition.CreateTenantOverride(
            meterCode!,
            tenantId,
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.MeterCodeRequired");
    }

    [Fact]
    public void CreateTenantOverride_WithEmptyTenantId_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.Empty);

        Action act = () => QuotaDefinition.CreateTenantOverride(
            "api.calls",
            tenantId,
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.TenantIdRequired");
    }

    [Fact]
    public void CreateTenantOverride_WithNegativeLimit_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        Action act = () => QuotaDefinition.CreateTenantOverride(
            "api.calls",
            tenantId,
            -100,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidLimit");
    }
}

public class QuotaDefinitionUpdateLimitTests
{
    [Fact]
    public void UpdateLimit_WithValidData_UpdatesProperties()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);
        Guid userId = Guid.NewGuid();

        quota.UpdateLimit(2000, QuotaAction.Warn, userId);

        quota.Limit.Should().Be(2000);
        quota.OnExceeded.Should().Be(QuotaAction.Warn);
    }

    [Fact]
    public void UpdateLimit_WithNegativeLimit_ThrowsBusinessRuleException()
    {
        QuotaDefinition quota = QuotaDefinition.CreatePlanQuota(
            "api.calls",
            "free",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        Action act = () => quota.UpdateLimit(-500, QuotaAction.Block, Guid.NewGuid());

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidLimit");
    }
}
