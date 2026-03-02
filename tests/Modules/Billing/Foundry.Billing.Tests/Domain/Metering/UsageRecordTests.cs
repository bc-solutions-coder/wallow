using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Billing.Tests.Domain.Metering;

public class UsageRecordCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUsageRecord()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1500, TimeProvider.System);

        record.TenantId.Should().Be(tenantId);
        record.MeterCode.Should().Be("api.calls");
        record.PeriodStart.Should().Be(periodStart);
        record.PeriodEnd.Should().Be(periodEnd);
        record.Value.Should().Be(1500);
        record.FlushedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyMeterCode_ThrowsBusinessRuleException(string? meterCode)
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        Action act = () => UsageRecord.Create(tenantId, meterCode!, periodStart, periodEnd, 1500, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.MeterCodeRequired");
    }

    [Fact]
    public void Create_WithPeriodEndBeforeStart_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        Action act = () => UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1500, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidPeriod");
    }

    [Fact]
    public void Create_WithPeriodEndEqualToStart_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = periodStart;

        Action act = () => UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1500, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidPeriod");
    }

    [Fact]
    public void Create_WithNegativeValue_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        Action act = () => UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, -100, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidValue");
    }
}

public class UsageRecordAddValueTests
{
    [Fact]
    public void AddValue_WithPositiveValue_IncreasesValue()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1000, TimeProvider.System);

        record.AddValue(500, TimeProvider.System);

        record.Value.Should().Be(1500);
    }

    [Fact]
    public async Task AddValue_UpdatesFlushedAt()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1000, TimeProvider.System);
        DateTime originalFlushedAt = record.FlushedAt;

        await Task.Delay(10);
        record.AddValue(500, TimeProvider.System);

        record.FlushedAt.Should().BeAfter(originalFlushedAt);
    }

    [Fact]
    public void AddValue_WithZero_IncreasesValueByZero()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1000, TimeProvider.System);

        record.AddValue(0, TimeProvider.System);

        record.Value.Should().Be(1000);
    }

    [Fact]
    public void AddValue_WithNegativeValue_ThrowsBusinessRuleException()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        DateTime periodStart = new(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", periodStart, periodEnd, 1000, TimeProvider.System);

        Action act = () => record.AddValue(-100, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Metering.InvalidValue");
    }
}
