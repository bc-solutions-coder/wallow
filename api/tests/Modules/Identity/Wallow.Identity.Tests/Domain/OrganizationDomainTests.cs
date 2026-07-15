using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationDomainTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly OrganizationId _orgId = OrganizationId.New();
    private static readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidData_ReturnsUnverifiedDomain()
    {
        OrganizationDomain domain = OrganizationDomain.Create(
            _tenantId, _orgId, "example.com", "verify-token-123", _userId, _timeProvider);

        domain.TenantId.Should().Be(_tenantId);
        domain.OrganizationId.Should().Be(_orgId);
        domain.Domain.Should().Be("example.com");
        domain.IsVerified.Should().BeFalse();
        domain.VerificationToken.Should().Be("verify-token-123");
    }

    [Fact]
    public void Create_NormalizesDomainToLowerCase()
    {
        OrganizationDomain domain = OrganizationDomain.Create(
            _tenantId, _orgId, "EXAMPLE.COM", "token", _userId, _timeProvider);

        domain.Domain.Should().Be("example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankDomain_ThrowsBusinessRuleException(string? domainName)
    {
        Action act = () => OrganizationDomain.Create(
            _tenantId, _orgId, domainName!, "token", _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Domain*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankVerificationToken_ThrowsBusinessRuleException(string? token)
    {
        Action act = () => OrganizationDomain.Create(
            _tenantId, _orgId, "example.com", token!, _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Verification token*");
    }

    [Fact]
    public void Verify_WhenUnverified_SetsIsVerifiedToTrue()
    {
        OrganizationDomain domain = CreateUnverifiedDomain();

        domain.Verify(_userId, _timeProvider);

        domain.IsVerified.Should().BeTrue();
    }

    [Fact]
    public void Verify_WhenAlreadyVerified_ThrowsBusinessRuleException()
    {
        OrganizationDomain domain = CreateUnverifiedDomain();
        domain.Verify(_userId, _timeProvider);

        Action act = () => domain.Verify(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already verified*");
    }

    private OrganizationDomain CreateUnverifiedDomain() =>
        OrganizationDomain.Create(_tenantId, _orgId, "example.com", "verify-token", _userId, _timeProvider);
}
