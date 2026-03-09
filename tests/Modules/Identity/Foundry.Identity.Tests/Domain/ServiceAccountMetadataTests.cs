using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Tests.Domain;

public class ServiceAccountMetadataTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();
    private static readonly string[] _oldScopeArray = ["old.scope"];
    private static readonly string[] _singleScopeArray = ["scope"];

    [Fact]
    public void Create_WithValidParameters_CreatesActiveServiceAccount()
    {
        // Arrange
        string[] scopes = new[] { "invoices.read", "invoices.write" };

        // Act
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test Service Account",
            "Test description",
            scopes, _testUserId, TimeProvider.System);

        // Assert
        metadata.TenantId.Should().Be(_tenantId);
        metadata.KeycloakClientId.Should().Be("sa-test-client");
        metadata.Name.Should().Be("Test Service Account");
        metadata.Description.Should().Be("Test description");
        metadata.Status.Should().Be(ServiceAccountStatus.Active);
        metadata.Scopes.Should().BeEquivalentTo(scopes);
        metadata.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyClientId_ThrowsBusinessRuleException()
    {
        // Act
        Func<ServiceAccountMetadata> act = () => ServiceAccountMetadata.Create(
            _tenantId,
            "",
            "Test Service Account",
            null,
            [],
            _testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*client ID*");
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsBusinessRuleException()
    {
        // Act
        Func<ServiceAccountMetadata> act = () => ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "",
            null,
            [],
            _testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void MarkUsed_SetsLastUsedAtToCurrentTime()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);
        DateTime beforeMark = DateTime.UtcNow;

        // Act
        metadata.MarkUsed(TimeProvider.System);

        // Assert
        metadata.LastUsedAt.Should().NotBeNull();
        metadata.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        metadata.LastUsedAt.Should().BeOnOrAfter(beforeMark);
    }

    [Fact]
    public void Revoke_SetsStatusToRevoked()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);

        // Act
        metadata.Revoke(_testUserId, TimeProvider.System);

        // Assert
        metadata.Status.Should().Be(ServiceAccountStatus.Revoked);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsBusinessRuleException()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);
        metadata.Revoke(_testUserId, TimeProvider.System);

        // Act
        Action act = () => metadata.Revoke(_testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already revoked*");
    }

    [Fact]
    public void UpdateScopes_WithValidScopes_UpdatesScopesList()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            _oldScopeArray, _testUserId, TimeProvider.System);
        string[] newScopes = ["new.scope1", "new.scope2"];

        // Act
        metadata.UpdateScopes(newScopes, _testUserId, TimeProvider.System);

        // Assert
        metadata.Scopes.Should().BeEquivalentTo(newScopes);
    }

    [Fact]
    public void UpdateScopes_WhenRevoked_ThrowsBusinessRuleException()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);
        metadata.Revoke(_testUserId, TimeProvider.System);

        // Act
        Action act = () => metadata.UpdateScopes(_singleScopeArray, _testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public void UpdateDetails_WithValidName_UpdatesNameAndDescription()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Original Name",
            "Original Description",
            [],
            _testUserId, TimeProvider.System);

        // Act
        metadata.UpdateDetails("New Name", "New Description", _testUserId, TimeProvider.System);

        // Assert
        metadata.Name.Should().Be("New Name");
        metadata.Description.Should().Be("New Description");
    }

    [Fact]
    public void UpdateDetails_WhenRevoked_ThrowsBusinessRuleException()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);
        metadata.Revoke(_testUserId, TimeProvider.System);

        // Act
        Action act = () => metadata.UpdateDetails("New Name", null, _testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public void UpdateDetails_WithEmptyName_ThrowsBusinessRuleException()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId,
            "sa-test-client",
            "Test",
            null,
            [],
            _testUserId, TimeProvider.System);

        // Act
        Action act = () => metadata.UpdateDetails("", null, _testUserId, TimeProvider.System);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*name*");
    }
}
