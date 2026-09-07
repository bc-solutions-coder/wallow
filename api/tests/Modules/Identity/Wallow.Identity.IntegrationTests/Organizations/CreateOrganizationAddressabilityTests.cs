using System.Net;
using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// Checks read, member-management, and settings access using a newly created organization ID
/// without switching the caller tenant to that organization.
/// </summary>
[Trait("Category", "Integration")]
public class CreateOrganizationAddressabilityTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private async Task<Guid> CreateOrganizationAsRealmAdminAsync(string name)
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        object createRequest = new { name, domain = (string?)null };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/identity/organizations", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        CreateOrganizationResponseBody? created =
            await createResponse.Content.ReadFromJsonAsync<CreateOrganizationResponseBody>();
        created.Should().NotBeNull();
        created!.OrganizationId.Should().NotBe(Guid.Empty);
        return created.OrganizationId;
    }

    /// <summary>
    /// Reads the created organization by the returned ID.
    /// </summary>
    [Fact]
    public async Task PostOrganization_ThenGetById_ViaReturnedId_Succeeds()
    {
        Guid orgId = await CreateOrganizationAsRealmAdminAsync("Addressable Read Org");

        HttpResponseMessage getResponse = await Client.GetAsync($"/identity/organizations/{orgId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationDto? fetched = await getResponse.Content.ReadFromJsonAsync<OrganizationDto>();
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(orgId);
    }

    /// <summary>
    /// Adds a seeded user and checks that the member listing includes them.
    /// </summary>
    [Fact]
    public async Task PostOrganization_ThenAddAndListMember_ViaReturnedId_Succeeds()
    {
        Guid orgId = await CreateOrganizationAsRealmAdminAsync("Addressable Member Org");
        Guid memberId = IdentityFixture.TestUserId;

        object addMemberRequest = new { userId = memberId, role = "user" };
        HttpResponseMessage addMemberResponse =
            await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage membersResponse = await Client.GetAsync($"/identity/organizations/{orgId}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<UserDto>? members = await membersResponse.Content.ReadFromJsonAsync<IReadOnlyList<UserDto>>();
        members.Should().NotBeNull();
        members!.Select(m => m.Id).Should().Contain(memberId);
    }

    /// <summary>
    /// Checks that a settings update to the created organization is accepted.
    /// </summary>
    [Fact]
    public async Task PostOrganization_ThenUpdateSettings_ViaReturnedId_Succeeds()
    {
        Guid orgId = await CreateOrganizationAsRealmAdminAsync("Addressable Update Org");

        object settingsRequest = new { requireMfa = false, mfaGracePeriodDays = 0 };
        HttpResponseMessage updateResponse =
            await Client.PutAsJsonAsync($"/identity/organizations/{orgId}/settings", settingsRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record CreateOrganizationResponseBody
    {
        public Guid OrganizationId { get; init; }
    }
}
