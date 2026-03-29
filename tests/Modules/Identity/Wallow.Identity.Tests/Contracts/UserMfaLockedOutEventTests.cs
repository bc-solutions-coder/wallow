using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Tests.Contracts;

public class UserMfaLockedOutEventTests
{
    [Fact]
    public void Can_be_constructed_with_valid_property_values()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        int lockoutCount = 3;

        UserMfaLockedOutEvent sut = new()
        {
            UserId = userId,
            TenantId = tenantId,
            LockoutCount = lockoutCount
        };

        sut.UserId.Should().Be(userId);
        sut.TenantId.Should().Be(tenantId);
        sut.LockoutCount.Should().Be(lockoutCount);
        sut.EventId.Should().NotBeEmpty();
        sut.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LockoutCount_reflects_the_value_passed_in()
    {
        UserMfaLockedOutEvent sut = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LockoutCount = 7
        };

        sut.LockoutCount.Should().Be(7);
    }

    [Fact]
    public void Inherits_from_IntegrationEvent()
    {
        UserMfaLockedOutEvent sut = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LockoutCount = 1
        };

        sut.Should().BeAssignableTo<IntegrationEvent>();
        sut.Should().BeAssignableTo<IIntegrationEvent>();
    }
}
