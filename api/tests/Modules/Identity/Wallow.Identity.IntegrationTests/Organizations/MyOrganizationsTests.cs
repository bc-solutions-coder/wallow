using System.Net;
using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// GET /v1/identity/me/organizations answers across tenants or it answers nothing worth having:
/// the caller's token is scoped to one organization, and the list exists to name the others.
///
/// Backend-dependent: only real Postgres runs the tenant query filter this read has to cross.
/// </summary>
[Trait("Category", "Integration")]
public class MyOrganizationsTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task MyOrganizations_NamesEveryOrganizationTheCallerOwns_NotOnlyTheirTokensOwn()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        Guid first = await CreateOrganizationAsync("Mine First");
        Guid second = await CreateOrganizationAsync("Mine Second");

        HttpResponseMessage response = await Client.GetAsync("/identity/me/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<MyOrganizationDto>? mine =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<MyOrganizationDto>>();
        mine.Should().NotBeNull();
        mine!.Where(o => o.OrganizationId == first || o.OrganizationId == second)
            .Should().HaveCount(2)
            .And.OnlyContain(o => o.IsOwner);
    }

    /// <summary>
    /// The other half of the guarantee: belonging to one organization tells the caller nothing
    /// about anyone else's.
    /// </summary>
    [Fact]
    public async Task MyOrganizations_LeavesOutAnOrganizationTheCallerDoesNotBelongTo()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        Guid someoneElses = await CreateOrganizationAsync("Not Mine");

        SetTestUser(IdentityFixture.TestUserId.ToString(), "user");
        HttpResponseMessage response = await Client.GetAsync("/identity/me/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<MyOrganizationDto>? mine =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<MyOrganizationDto>>();
        mine.Should().NotBeNull();
        mine!.Should().NotContain(o => o.OrganizationId == someoneElses);
    }

    private async Task<Guid> CreateOrganizationAsync(string name)
    {
        object request = new { name, domain = (string?)null };
        HttpResponseMessage response = await Client.PostAsJsonAsync("/identity/organizations", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        CreateOrganizationResponseBody? created =
            await response.Content.ReadFromJsonAsync<CreateOrganizationResponseBody>();
        created.Should().NotBeNull();
        return created!.OrganizationId;
    }

    private sealed record CreateOrganizationResponseBody
    {
        public Guid OrganizationId { get; init; }
    }
}
