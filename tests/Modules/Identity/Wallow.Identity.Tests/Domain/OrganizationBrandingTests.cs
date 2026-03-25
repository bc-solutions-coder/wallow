using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationBrandingTests
{
    private static readonly OrganizationId _orgId = OrganizationId.New();
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithAllValues_SetsProperties()
    {
        OrganizationBranding branding = OrganizationBranding.Create(
            _orgId, _tenantId, "https://logo.png", "#FF0000", "#00FF00", _userId, _timeProvider);

        branding.OrganizationId.Should().Be(_orgId);
        branding.TenantId.Should().Be(_tenantId);
        branding.LogoUrl.Should().Be("https://logo.png");
        branding.PrimaryColor.Should().Be("#FF0000");
        branding.AccentColor.Should().Be("#00FF00");
    }

    [Fact]
    public void Create_WithNullValues_AllowsNulls()
    {
        OrganizationBranding branding = OrganizationBranding.Create(
            _orgId, _tenantId, null, null, null, _userId, _timeProvider);

        branding.LogoUrl.Should().BeNull();
        branding.PrimaryColor.Should().BeNull();
        branding.AccentColor.Should().BeNull();
    }

    [Fact]
    public void Update_ChangesAllProperties()
    {
        OrganizationBranding branding = OrganizationBranding.Create(
            _orgId, _tenantId, "https://old.png", "#000000", "#111111", _userId, _timeProvider);

        branding.Update("https://new.png", "#FFFFFF", "#EEEEEE", _userId, _timeProvider);

        branding.LogoUrl.Should().Be("https://new.png");
        branding.PrimaryColor.Should().Be("#FFFFFF");
        branding.AccentColor.Should().Be("#EEEEEE");
    }

    [Fact]
    public void Update_CanSetValuesToNull()
    {
        OrganizationBranding branding = OrganizationBranding.Create(
            _orgId, _tenantId, "https://logo.png", "#FF0000", "#00FF00", _userId, _timeProvider);

        branding.Update(null, null, null, _userId, _timeProvider);

        branding.LogoUrl.Should().BeNull();
        branding.PrimaryColor.Should().BeNull();
        branding.AccentColor.Should().BeNull();
    }
}
