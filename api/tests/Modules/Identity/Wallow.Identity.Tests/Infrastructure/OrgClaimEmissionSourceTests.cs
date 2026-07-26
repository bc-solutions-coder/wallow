using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// T5.3 (Wallow-w6s6.5.3) regression guard, pinned BEFORE the membership-read change per the
/// plan's explicit risk: the <c>org_id</c>/<c>org_name</c> claims emitted by
/// AuthorizationController.BuildClaimsIdentityAsync are sourced from the
/// <see cref="ClientTenantResolver"/> — <c>org_id == ClientTenantInfo.TenantId</c> and
/// <c>org_name == ClientTenantInfo.TenantName</c> (which is the resolved organization's Name).
/// If a membership change were to break tenant resolution, these guards catch the claim
/// regression immediately.
/// </summary>
public sealed class OrgClaimEmissionSourceTests
{
    private readonly IOpenIddictApplicationManager _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly ClientTenantResolver _sut;

    public OrgClaimEmissionSourceTests()
    {
        _sut = new ClientTenantResolver(_applicationManager, _organizationService);
    }

    [Fact]
    public async Task ResolvedTenant_ProvidesOrgIdAndOrgNameClaimSources()
    {
        Guid orgId = Guid.NewGuid();
        const string clientId = "org-scoped-client";
        object application = new();

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(application);
        await _applicationManager.PopulateAsync(
            Arg.Do<OpenIddictApplicationDescriptor>(descriptor =>
                descriptor.Properties["tenant_id"] = JsonSerializer.SerializeToElement(orgId.ToString())),
            application,
            Arg.Any<CancellationToken>());
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(orgId, "Contoso", null, 3));

        ClientTenantInfo? tenantInfo = await _sut.ResolveAsync(clientId);

        tenantInfo.Should().NotBeNull();

        // Mirrors AuthorizationController.BuildClaimsIdentityAsync: org_id and org_name come
        // straight off the resolved ClientTenantInfo.
        string orgIdClaim = tenantInfo!.TenantId.ToString();
        string? orgNameClaim = tenantInfo.TenantName;

        orgIdClaim.Should().Be(orgId.ToString());
        orgNameClaim.Should().Be("Contoso");
    }

    [Fact]
    public async Task ResolvedTenantWithoutName_OmitsOrgNameClaim()
    {
        Guid orgId = Guid.NewGuid();
        const string clientId = "nameless-org-client";
        object application = new();

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(application);
        await _applicationManager.PopulateAsync(
            Arg.Do<OpenIddictApplicationDescriptor>(descriptor =>
                descriptor.Properties["tenant_id"] = JsonSerializer.SerializeToElement(orgId.ToString())),
            application,
            Arg.Any<CancellationToken>());
        _organizationService.GetOrganizationByIdAsync(orgId, Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);

        ClientTenantInfo? tenantInfo = await _sut.ResolveAsync(clientId);

        tenantInfo.Should().NotBeNull();
        tenantInfo!.TenantId.Should().Be(orgId);
        // org_name claim is only added when TenantName is non-null (AuthorizationController.cs).
        tenantInfo.TenantName.Should().BeNull();
    }
}
