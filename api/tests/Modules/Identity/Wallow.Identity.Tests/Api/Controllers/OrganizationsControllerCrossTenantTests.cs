using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using WallowClaims = Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// OrganizationsController.CanAddressOrganizationAsync gates every organization-scoped endpoint in
/// the controller. The ordinary "admin" role is tenant-assignable through UsersController.AssignRole,
/// so honouring it as a cross-tenant escape hatch hands any tenant admin full governance
/// (read, membership, branding, settings, archive, delete) over every other tenant's organization
/// by guessing its GUID -- the same F5 hole TenantResolutionMiddleware.HasRealmAdminRole was
/// deleted for. The is_global_admin claim (ClaimsPrincipalExtensions.IsGlobalAdmin) is the only
/// cross-tenant escape hatch, mirroring TenantResolutionMiddleware and PermissionExpansionMiddleware.
/// </summary>
[Trait("Category", "CrossTenant")]
public sealed class OrganizationsControllerCrossTenantTests
{
    private readonly IOrganizationService _orgService = Substitute.For<IOrganizationService>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    // Membership is the only non-blanket path past the tenant check. It answers one question per
    // endpoint -- does this caller hold THAT endpoint's permission in THIS org -- and defaults to
    // false here, so every rejection assertion below is a rejection of an unrelated caller.
    private readonly IOrganizationAccessPolicy _accessPolicy = Substitute.For<IOrganizationAccessPolicy>();
    private readonly Guid _tenantOrgId = Guid.NewGuid();
    private readonly Guid _otherTenantOrgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public OrganizationsControllerCrossTenantTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantOrgId));

        // Every read endpoint returns a real record for ANY organization id, so a caller that slips
        // past the gate gets 200 OK with foreign data rather than an incidental 404 from a null
        // lookup -- the NotFound assertions below then only hold when the gate itself rejects.
        // The write endpoints are configured so nothing throws once the gate lets a caller through.
        // NSubstitute discards these configuration calls, so they do not show up in ReceivedCalls().
        _orgService.GetOrganizationByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationDto(callInfo.Arg<Guid>(), "Victim Org", "victim.test", 42));
        _orgService.GetBrandingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationBrandingDto(callInfo.Arg<Guid>(), "Victim", null, null, null));
        _orgService.GetSettingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationSettingsDto(
                callInfo.Arg<Guid>(), true, false, 7, EnrollmentPolicy.InviteOnly, null, null));
        _orgService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserDto> { new(Guid.NewGuid(), "victim@other.test", "Victim", "User", true, ["user"]) });
        _orgService.UpdateBrandingAsync(
                Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new OrganizationBrandingDto(Guid.NewGuid(), "Branding", null, null, null));
        _orgService.UploadBrandingLogoAsync(
                Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.test/logo.png");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("GetMembers")]
    [InlineData("AddMember")]
    [InlineData("RemoveMember")]
    [InlineData("Archive")]
    [InlineData("Reactivate")]
    [InlineData("Delete")]
    [InlineData("GetBranding")]
    [InlineData("UpdateBranding")]
    [InlineData("UploadBrandingLogo")]
    [InlineData("GetSettings")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    public async Task TenantAdminRole_ForeignOrganization_IsRejected(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "the tenant-assignable \"admin\" role must not address another tenant's organization");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("GetMembers")]
    [InlineData("AddMember")]
    [InlineData("RemoveMember")]
    [InlineData("Archive")]
    [InlineData("Reactivate")]
    [InlineData("Delete")]
    [InlineData("GetBranding")]
    [InlineData("UpdateBranding")]
    [InlineData("UploadBrandingLogo")]
    [InlineData("GetSettings")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    public async Task TenantAdminRole_ForeignOrganization_NeverReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        _orgService.ReceivedCalls().Should().BeEmpty(
            "a rejected cross-tenant call must not touch another tenant's organization data");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("GetMembers")]
    [InlineData("AddMember")]
    [InlineData("RemoveMember")]
    [InlineData("Archive")]
    [InlineData("Reactivate")]
    [InlineData("Delete")]
    [InlineData("GetBranding")]
    [InlineData("UpdateBranding")]
    [InlineData("UploadBrandingLogo")]
    [InlineData("GetSettings")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    public async Task GlobalAdminClaim_ForeignOrganization_StillReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(WallowClaims.GlobalAdminClaimType, "true"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        _orgService.ReceivedCalls().Should().NotBeEmpty(
            "the is_global_admin claim is the cross-tenant escape hatch and must not be locked out");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("GetMembers")]
    [InlineData("AddMember")]
    [InlineData("RemoveMember")]
    [InlineData("Archive")]
    [InlineData("Reactivate")]
    [InlineData("Delete")]
    [InlineData("GetBranding")]
    [InlineData("UpdateBranding")]
    [InlineData("UploadBrandingLogo")]
    [InlineData("GetSettings")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    public async Task TenantAdminRole_OwnOrganization_StillReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _tenantOrgId);

        _orgService.ReceivedCalls().Should().NotBeEmpty(
            "same-tenant access is the legitimate path and must keep working");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("Delete")]
    [InlineData("UpdateSettings")]
    public async Task GlobalAdminClaimNotLiterallyTrue_ForeignOrganization_IsRejected(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(WallowClaims.GlobalAdminClaimType, "false"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "only the literal value \"true\" on is_global_admin grants cross-tenant access");
        _orgService.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("global-admin")]
    [InlineData("global_admin")]
    [InlineData("globaladmin")]
    [InlineData("is_global_admin")]
    public async Task GlobalAdminSpelledAsARole_ForeignOrganization_IsRejected(string roleName)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, roleName));

        ActionResult? result = await InvokeAsync(controller, "GetById", _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "global admin is a claim, never a role; no role string may unlock another tenant");
        _orgService.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("user")]
    [InlineData("manager")]
    [InlineData("owner")]
    public async Task OrdinaryTenantRole_ForeignOrganization_IsRejected(string roleName)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, roleName));

        ActionResult? result = await InvokeAsync(controller, "GetById", _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>();
        _orgService.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("GetMembers")]
    [InlineData("AddMember")]
    [InlineData("RemoveMember")]
    [InlineData("Archive")]
    [InlineData("Reactivate")]
    [InlineData("Delete")]
    [InlineData("GetBranding")]
    [InlineData("UpdateBranding")]
    [InlineData("UploadBrandingLogo")]
    [InlineData("GetSettings")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    public async Task PermittedMember_ThatOrganization_ReachesTheOrganizationService(string endpoint)
    {
        // Creating an organization mints a NEW tenant id, so the creator's own tenant id can never
        // equal it; the membership that creation records is what keeps the creator able to address
        // what they just created.
        _accessPolicy.HasPermissionInOrganizationAsync(
                _otherTenantOrgId, _userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        OrganizationsController controller = CreateController();

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        _orgService.ReceivedCalls().Should().NotBeEmpty(
            "a member holding the endpoint's permission must reach it without any role bypass");
    }

    [Theory]
    [InlineData("GetById")]
    [InlineData("Delete")]
    [InlineData("UpdateSettings")]
    public async Task PermittedMember_ADifferentOrganization_IsRejected(string endpoint)
    {
        Guid unrelatedOrgId = Guid.NewGuid();
        _accessPolicy.HasPermissionInOrganizationAsync(
                _otherTenantOrgId, _userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, unrelatedOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "a grant is per-organization; holding one org's permission grants nothing over any other");
        _orgService.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("GetById", PermissionType.OrganizationsRead)]
    [InlineData("GetMembers", PermissionType.OrganizationsRead)]
    [InlineData("GetBranding", PermissionType.OrganizationsRead)]
    [InlineData("GetSettings", PermissionType.OrganizationsRead)]
    [InlineData("AddMember", PermissionType.OrganizationsManageMembers)]
    [InlineData("RemoveMember", PermissionType.OrganizationsManageMembers)]
    [InlineData("Archive", PermissionType.OrganizationsUpdate)]
    [InlineData("Reactivate", PermissionType.OrganizationsUpdate)]
    [InlineData("Delete", PermissionType.OrganizationsUpdate)]
    [InlineData("UpdateBranding", PermissionType.OrganizationsUpdate)]
    [InlineData("UploadBrandingLogo", PermissionType.OrganizationsUpdate)]
    [InlineData("UpdateSettings", PermissionType.OrganizationsUpdate)]
    [InlineData("UpdateEnrollment", PermissionType.OrganizationsManageMembers)]
    public async Task ForeignOrganization_AsksForTheEndpointsOwnPermission(string endpoint, string permission)
    {
        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        await _accessPolicy.Received(1).HasPermissionInOrganizationAsync(
            _otherTenantOrgId, _userId, permission, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("Archive")]
    [InlineData("UpdateSettings")]
    [InlineData("UpdateEnrollment")]
    [InlineData("AddMember")]
    public async Task ForeignOrganization_ReadOnlyMember_ReachesNoWriteEndpoint(string endpoint)
    {
        _accessPolicy.HasPermissionInOrganizationAsync(
                _otherTenantOrgId, _userId, PermissionType.OrganizationsRead, Arg.Any<CancellationToken>())
            .Returns(true);

        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "read reach and write reach are separate grants, so one predicate must not answer both");
        _orgService.ReceivedCalls().Should().BeEmpty();
    }

    private static async Task<ActionResult?> InvokeAsync(
        OrganizationsController controller, string endpoint, Guid orgId)
    {
        CancellationToken ct = CancellationToken.None;

        return endpoint switch
        {
            "GetById" => (await controller.GetById(orgId, ct)).Result,
            "GetMembers" => (await controller.GetMembers(orgId, ct)).Result,
            "AddMember" => await controller.AddMember(orgId, new AddMemberRequest(Guid.NewGuid(), "user"), ct),
            "RemoveMember" => await controller.RemoveMember(orgId, Guid.NewGuid(), ct),
            "Archive" => await controller.Archive(orgId, ct),
            "Reactivate" => await controller.Reactivate(orgId, ct),
            "Delete" => await controller.Delete(orgId, new DeleteOrganizationRequest("Victim Org"), ct),
            "GetBranding" => (await controller.GetBranding(orgId, ct)).Result,
            "UpdateBranding" => (await controller.UpdateBranding(
                orgId, new UpdateOrganizationBrandingRequest("Pwned", null, "#000000"), ct)).Result,
            "UploadBrandingLogo" => (await controller.UploadBrandingLogo(orgId, CreateLogoFile(), ct)).Result,
            "GetSettings" => (await controller.GetSettings(orgId, ct)).Result,
            "UpdateSettings" => await controller.UpdateSettings(
                orgId, new UpdateOrganizationSettingsRequest(false, 0, null, null), ct),
            "UpdateEnrollment" => await controller.UpdateEnrollment(
                orgId, new UpdateOrganizationEnrollmentRequest(EnrollmentPolicy.Open, null, null), ct),
            _ => throw new ArgumentOutOfRangeException(
                nameof(endpoint), endpoint, "Unknown organization-scoped endpoint."),
        };
    }

    private static IFormFile CreateLogoFile()
    {
        IFormFile file = Substitute.For<IFormFile>();
        file.FileName.Returns("logo.png");
        file.ContentType.Returns("image/png");
        file.OpenReadStream().Returns(_ => new MemoryStream([1, 2, 3]));
        return file;
    }

    private OrganizationsController CreateController(params Claim[] claims)
    {
        List<Claim> allClaims =
        [
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Email, "tenant-admin@test.com"),
            new Claim("org_id", _tenantOrgId.ToString()),
            .. claims,
        ];

        return new OrganizationsController(_orgService, _tenantContext, _accessPolicy)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(allClaims, "TestAuth")),
                },
            },
        };
    }
}
