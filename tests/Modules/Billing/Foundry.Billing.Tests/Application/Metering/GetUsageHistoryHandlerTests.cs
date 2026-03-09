using Foundry.Billing.Application.Metering.DTOs;
using Foundry.Billing.Application.Metering.Interfaces;
using Foundry.Billing.Application.Metering.Queries.GetUsageHistory;
using Foundry.Billing.Domain.Metering.Entities;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Metering;

public class GetUsageHistoryHandlerTests
{
    private readonly IUsageRecordRepository _usageRepository;
    private readonly GetUsageHistoryHandler _handler;

    public GetUsageHistoryHandlerTests()
    {
        _usageRepository = Substitute.For<IUsageRecordRepository>();
        _handler = new GetUsageHistoryHandler(_usageRepository);
    }

    [Fact]
    public async Task Handle_WhenRecordsExist_ReturnsUsageRecordDtos()
    {
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow;
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        UsageRecord record1 = UsageRecord.Create(tenantId, "api.calls", from, from.AddDays(1), 100, TimeProvider.System);
        UsageRecord record2 = UsageRecord.Create(tenantId, "api.calls", from.AddDays(1), from.AddDays(2), 200, TimeProvider.System);

        _usageRepository.GetHistoryAsync("api.calls", from, to, Arg.Any<CancellationToken>())
            .Returns(new[] { record1, record2 });

        GetUsageHistoryQuery query = new("api.calls", from, to);

        Result<IReadOnlyList<UsageRecordDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].MeterCode.Should().Be("api.calls");
        result.Value[0].Value.Should().Be(100);
        result.Value[0].TenantId.Should().Be(tenantId.Value);
        result.Value[1].Value.Should().Be(200);
    }

    [Fact]
    public async Task Handle_WhenNoRecords_ReturnsEmptyList()
    {
        DateTime from = DateTime.UtcNow.AddDays(-30);
        DateTime to = DateTime.UtcNow;

        _usageRepository.GetHistoryAsync("api.calls", from, to, Arg.Any<CancellationToken>())
            .Returns([]);

        GetUsageHistoryQuery query = new("api.calls", from, to);

        Result<IReadOnlyList<UsageRecordDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectParametersToRepository()
    {
        DateTime from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime to = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        _usageRepository.GetHistoryAsync("storage.bytes", from, to, Arg.Any<CancellationToken>())
            .Returns([]);

        GetUsageHistoryQuery query = new("storage.bytes", from, to);

        await _handler.Handle(query, CancellationToken.None);

        await _usageRepository.Received(1).GetHistoryAsync("storage.bytes", from, to, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MapsRecordIdsCorrectly()
    {
        DateTime from = DateTime.UtcNow.AddDays(-7);
        DateTime to = DateTime.UtcNow;
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        UsageRecord record = UsageRecord.Create(tenantId, "api.calls", from, to, 500, TimeProvider.System);

        _usageRepository.GetHistoryAsync("api.calls", from, to, Arg.Any<CancellationToken>())
            .Returns(new[] { record });

        GetUsageHistoryQuery query = new("api.calls", from, to);

        Result<IReadOnlyList<UsageRecordDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Id.Should().Be(record.Id.Value);
    }
}
