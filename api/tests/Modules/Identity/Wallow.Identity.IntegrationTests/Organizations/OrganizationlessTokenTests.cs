using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.Errors;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// Checks endpoint admission with a synthetic organization-less principal,
/// including missing-context problems from selected tenant-scoped endpoints.
/// </summary>
public class OrganizationlessTokenTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private void SetOrganizationlessUser()
    {
        SetTestUser(IdentityFixture.TestUserId.ToString(), "User");
        SetTestNoOrganization();
    }

    [Fact]
    public async Task OrganizationlessToken_ReachesTheCallersProfile()
    {
        SetOrganizationlessUser();

        HttpResponseMessage response = await Client.GetAsync("/identity/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OrganizationlessToken_ReachesMyOrganizations()
    {
        SetOrganizationlessUser();

        HttpResponseMessage response = await Client.GetAsync("/identity/me/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OrganizationlessToken_CanFoundAnOrganization_WithoutAnyPermission()
    {
        SetOrganizationlessUser();

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/identity/organizations",
            new { name = $"Founded Without Org {Guid.NewGuid():N}", domain = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task OrganizationlessToken_ReachesAcceptInvitation()
    {
        SetOrganizationlessUser();

        HttpResponseMessage response = await Client.PostAsync("/identity/invitations/no-such-token/accept", content: null);

        // Reaching the unknown-invitation response distinguishes admission from the tenant gate.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/identity/organizations")]
    [InlineData("/identity/organizations/00000000-0000-0000-0000-000000000010")]
    [InlineData("/identity/users")]
    [InlineData("/notification-settings")] // Include a Notifications route to check the shared tenant gate.
    public async Task OrganizationlessToken_IsForbiddenFromTenantScopedEndpoints(string path)
    {
        SetOrganizationlessUser();

        HttpResponseMessage response = await Client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("organization");
        problem.Extensions.Should().ContainKey("code")
            .WhoseValue!.ToString().Should().Be(SharedErrors.Forbidden.Code);
    }

    [Fact]
    public async Task TokenWithAnOrganization_StillReachesTenantScopedEndpoints()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        SetTestTenant(TestConstants.TestOrgId);

        HttpResponseMessage response = await Client.GetAsync("/identity/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
