using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute.Core;
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
/// Checks organization addressability for unrelated callers, permitted members, and global admins.
/// An ordinary admin role must not bypass organization permission checks.
/// </summary>
[Trait("Category", "CrossTenant")]
public sealed class OrganizationsControllerCrossTenantTests
{
    private readonly IOrganizationService _orgService = Substitute.For<IOrganizationService>();
    private readonly IMembershipReviewService _membershipReview = Substitute.For<IMembershipReviewService>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    // Default to denying access in the target organization; membership cases override this.
    private readonly IOrganizationAccessPolicy _accessPolicy = Substitute.For<IOrganizationAccessPolicy>();
    private readonly Guid _tenantOrgId = Guid.NewGuid();
    private readonly Guid _otherTenantOrgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public OrganizationsControllerCrossTenantTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantOrgId));

        // Supply successful service responses so addressability failures cannot hide behind missing data.
        _orgService.GetOrganizationByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationDto(callInfo.Arg<Guid>(), "Victim Org", "victim.test", 42));
        _orgService.GetBrandingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationBrandingDto(callInfo.Arg<Guid>(), "Victim", null, null, null));
        _orgService.GetSettingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new OrganizationSettingsDto(
                callInfo.Arg<Guid>(), true, false, 7, EnrollmentPolicy.InviteOnly, null, null));
        _orgService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserDto> { new(Guid.NewGuid(), "victim@other.test", "Victim", "User", true, ["user"]) });
        _membershipReview.GetSuspendedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewedMembershipDto>
            {
                new(Guid.NewGuid(), "victim@other.test", "Victim", "User",
                    MembershipStatus.Suspended, DateTimeOffset.UtcNow)
            });
        _membershipReview.GetDeniedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewedMembershipDto>
            {
                new(Guid.NewGuid(), "victim@other.test", "Victim", "User",
                    MembershipStatus.Denied, DateTimeOffset.UtcNow)
            });
        _orgService.UpdateBrandingAsync(
                Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new OrganizationBrandingDto(Guid.NewGuid(), "Branding", null, null, null));
        _orgService.UploadBrandingLogoAsync(
                Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.test/logo.png");
    }

    /// <summary>
    /// Permissions exercised by the organization endpoint theories.
    /// The inventory assertion compares these with reflected actions and explicit exceptions.
    /// </summary>
    private static readonly Dictionary<string, string> _endpointPermissions = new(StringComparer.Ordinal)
    {
        ["GetById"] = PermissionType.OrganizationsRead,
        ["GetMembers"] = PermissionType.OrganizationsRead,
        ["GetBranding"] = PermissionType.OrganizationsRead,
        ["GetSettings"] = PermissionType.OrganizationsRead,
        ["AddMember"] = PermissionType.OrganizationsManageMembers,
        ["RemoveMember"] = PermissionType.OrganizationsManageMembers,
        ["GetPendingMembers"] = PermissionType.OrganizationsManageMembers,
        ["GetSuspendedMembers"] = PermissionType.OrganizationsManageMembers,
        ["GetDeniedMembers"] = PermissionType.OrganizationsManageMembers,
        ["ApproveMember"] = PermissionType.OrganizationsManageMembers,
        ["DenyMember"] = PermissionType.OrganizationsManageMembers,
        ["ClearDenial"] = PermissionType.OrganizationsManageMembers,
        ["SuspendMember"] = PermissionType.OrganizationsManageMembers,
        ["ReinstateMember"] = PermissionType.OrganizationsManageMembers,
        ["UpdateEnrollment"] = PermissionType.OrganizationsManageMembers,
        ["Archive"] = PermissionType.OrganizationsUpdate,
        ["Reactivate"] = PermissionType.OrganizationsUpdate,
        ["Delete"] = PermissionType.OrganizationsDelete,
        ["UpdateBranding"] = PermissionType.OrganizationsUpdate,
        ["UploadBrandingLogo"] = PermissionType.OrganizationsUpdate,
        ["UpdateSettings"] = PermissionType.OrganizationsUpdate,
    };

    /// <summary>
    /// Self-service actions use the caller identity instead of organization administration permissions.
    /// </summary>
    private static readonly string[] _selfServiceEndpoints = ["Leave"];

    /// <summary>
    /// Platform suspension actions require a global-admin claim inside the action.
    /// </summary>
    private static readonly string[] _platformEndpoints = ["PlacePlatformSuspension", "LiftPlatformSuspension"];

    public static TheoryData<string> OrganizationScopedEndpoints => new(_endpointPermissions.Keys);

    public static TheoryData<string> PlatformEndpoints => new(_platformEndpoints);

    public static TheoryData<string> EndpointsBeyondRead => new(_endpointPermissions
        .Where(pair => !string.Equals(pair.Value, PermissionType.OrganizationsRead, StringComparison.Ordinal))
        .Select(pair => pair.Key));

    public static TheoryData<string, string> EndpointPermissions
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (KeyValuePair<string, string> pair in _endpointPermissions)
            {
                data.Add(pair.Key, pair.Value);
            }

            return data;
        }
    }

    /// <summary>
    /// Checks inventory coverage for declared actions whose first parameter is Guid id.
    /// </summary>
    [Fact]
    public void EveryOrganizationScopedEndpoint_AppearsInTheInventory()
    {
        IEnumerable<string> declared = typeof(OrganizationsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetParameters() is [{ Name: "id" } first, ..]
                && first.ParameterType == typeof(Guid))
            .Select(method => method.Name);

        declared.Should().BeEquivalentTo(
            [.. _endpointPermissions.Keys, .. _selfServiceEndpoints, .. _platformEndpoints]);
    }

    /// <summary>
    /// Checks that each inventoried action declares its expected permission policy.
    /// </summary>
    [Theory]
    [MemberData(nameof(EndpointPermissions))]
    public void EveryOrganizationScopedEndpoint_DemandsItsInventoriedPermission(
        string endpoint, string permission)
    {
        MethodInfo action = typeof(OrganizationsController).GetMethod(endpoint)!;

        action.GetCustomAttributes<HasPermissionAttribute>(inherit: false)
            .Select(attribute => attribute.Policy)
            .Should().ContainSingle().Which.Should().Be(permission);
    }

    [Fact]
    public async Task Leave_ForeignOrganization_IsNotGatedAtAll()
    {
        OrganizationsController controller = CreateController();

        ActionResult result = await controller.Leave(_otherTenantOrgId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _membershipReview.Received(1).LeaveAsync(
            _otherTenantOrgId, _userId, Arg.Any<CancellationToken>());
        await _accessPolicy.DidNotReceive().HasPermissionInOrganizationAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(OrganizationScopedEndpoints))]
    public async Task TenantAdminRole_ForeignOrganization_IsRejected(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "the tenant-assignable \"admin\" role must not address another tenant's organization");
    }

    [Theory]
    [MemberData(nameof(OrganizationScopedEndpoints))]
    public async Task TenantAdminRole_ForeignOrganization_NeverReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        ReceivedServiceCalls().Should().BeEmpty(
            "a rejected cross-tenant call must not touch another tenant's organization data");
    }

    [Theory]
    [MemberData(nameof(OrganizationScopedEndpoints))]
    public async Task GlobalAdminClaim_ForeignOrganization_StillReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(WallowClaims.GlobalAdminClaimType, "true"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        ReceivedServiceCalls().Should().NotBeEmpty(
            "the is_global_admin claim is the cross-tenant escape hatch and must not be locked out");
    }

    [Theory]
    [MemberData(nameof(PlatformEndpoints))]
    public async Task TenantAdminRole_PlatformSuspensionEndpoint_IsForbidden(string endpoint)
    {
        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _tenantOrgId);

        result.Should().BeOfType<ForbidResult>(
            "the platform's own suspension is never a tenant admin's to place or lift, "
            + "not even on their own organization");
        ReceivedServiceCalls().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(PlatformEndpoints))]
    public async Task GlobalAdminClaim_PlatformSuspensionEndpoint_ReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(WallowClaims.GlobalAdminClaimType, "true"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        ReceivedServiceCalls().Should().NotBeEmpty(
            "the is_global_admin claim is the only key to the platform suspension controls");
    }

    [Theory]
    [MemberData(nameof(OrganizationScopedEndpoints))]
    public async Task TenantAdminRole_OwnOrganization_StillReachesTheOrganizationService(string endpoint)
    {
        OrganizationsController controller = CreateController(
            new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _tenantOrgId);

        ReceivedServiceCalls().Should().NotBeEmpty(
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
        ReceivedServiceCalls().Should().BeEmpty();
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
        ReceivedServiceCalls().Should().BeEmpty();
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
        ReceivedServiceCalls().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(OrganizationScopedEndpoints))]
    public async Task PermittedMember_ThatOrganization_ReachesTheOrganizationService(string endpoint)
    {
        // Membership permission can authorize an organization outside the token tenant.
        _accessPolicy.HasPermissionInOrganizationAsync(
                _otherTenantOrgId, _userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        OrganizationsController controller = CreateController();

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        ReceivedServiceCalls().Should().NotBeEmpty(
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
        ReceivedServiceCalls().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(EndpointPermissions))]
    public async Task ForeignOrganization_AsksForTheEndpointsOwnPermission(string endpoint, string permission)
    {
        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        await _accessPolicy.Received(1).HasPermissionInOrganizationAsync(
            _otherTenantOrgId, _userId, permission, Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(EndpointsBeyondRead))]
    public async Task ForeignOrganization_ReadOnlyMember_ReachesNoEndpointBeyondRead(string endpoint)
    {
        _accessPolicy.HasPermissionInOrganizationAsync(
                _otherTenantOrgId, _userId, PermissionType.OrganizationsRead, Arg.Any<CancellationToken>())
            .Returns(true);

        OrganizationsController controller = CreateController(new Claim(ClaimTypes.Role, "admin"));

        ActionResult? result = await InvokeAsync(controller, endpoint, _otherTenantOrgId);

        result.Should().BeOfType<NotFoundResult>(
            "read reach and write reach are separate grants, so one predicate must not answer both");
        ReceivedServiceCalls().Should().BeEmpty();
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
            "GetPendingMembers" => (await controller.GetPendingMembers(orgId, ct)).Result,
            "GetSuspendedMembers" => (await controller.GetSuspendedMembers(orgId, ct)).Result,
            "GetDeniedMembers" => (await controller.GetDeniedMembers(orgId, ct)).Result,
            "ApproveMember" => await controller.ApproveMember(orgId, Guid.NewGuid(), ct),
            "DenyMember" => await controller.DenyMember(orgId, Guid.NewGuid(), ct),
            "ClearDenial" => await controller.ClearDenial(orgId, Guid.NewGuid(), ct),
            "SuspendMember" => await controller.SuspendMember(orgId, Guid.NewGuid(), ct),
            "ReinstateMember" => await controller.ReinstateMember(orgId, Guid.NewGuid(), ct),
            "PlacePlatformSuspension" => await controller.PlacePlatformSuspension(
                orgId, new PlatformSuspensionRequest { Reason = "Fraud investigation" }, ct),
            "LiftPlatformSuspension" => await controller.LiftPlatformSuspension(orgId, ct),
            _ => throw new ArgumentOutOfRangeException(
                nameof(endpoint), endpoint, "Unknown organization-scoped endpoint."),
        };
    }

    /// <summary>
    /// Collects calls to both organization and membership-review services.
    /// </summary>
    private IEnumerable<ICall> ReceivedServiceCalls() =>
        _orgService.ReceivedCalls().Concat(_membershipReview.ReceivedCalls());

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

        return new OrganizationsController(_orgService, _membershipReview, _tenantContext, _accessPolicy)
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
