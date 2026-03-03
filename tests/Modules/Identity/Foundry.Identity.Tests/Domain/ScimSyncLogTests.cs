using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Events;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Tests.Domain;

public class ScimSyncLogTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public void Create_WithValidParameters_CreatesLogEntry()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            _tenantId,
            ScimOperation.Create,
            ScimResourceType.User,
            "ext-123",
            "int-456",
            true);

        log.TenantId.Should().Be(_tenantId);
        log.Operation.Should().Be(ScimOperation.Create);
        log.ResourceType.Should().Be(ScimResourceType.User);
        log.ExternalId.Should().Be("ext-123");
        log.InternalId.Should().Be("int-456");
        log.Success.Should().BeTrue();
        log.ErrorMessage.Should().BeNull();
        log.RequestBody.Should().BeNull();
        log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithErrorMessage_StoresErrorDetails()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            _tenantId,
            ScimOperation.Update,
            ScimResourceType.Group,
            "ext-789",
            null,
            false,
            "User not found",
            "{\"schemas\":[\"urn:scim\"]}");

        log.Success.Should().BeFalse();
        log.ErrorMessage.Should().Be("User not found");
        log.RequestBody.Should().Be("{\"schemas\":[\"urn:scim\"]}");
        log.InternalId.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyExternalId_ThrowsBusinessRuleException()
    {
        Action act = () => ScimSyncLog.Create(
            _tenantId,
            ScimOperation.Create,
            ScimResourceType.User,
            "",
            null,
            true);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*external ID*");
    }

    [Fact]
    public void Create_RaisesScimSyncCompletedEvent()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            _tenantId,
            ScimOperation.Delete,
            ScimResourceType.User,
            "ext-999",
            null,
            false,
            "Deprovision failed");

        log.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ScimSyncCompletedEvent>()
            .Which.Should().Match<ScimSyncCompletedEvent>(e =>
                e.TenantId == _tenantId.Value &&
                e.Operation == "Delete" &&
                e.ResourceType == "User" &&
!e.Success &&
                e.ErrorMessage == "Deprovision failed");
    }
}
