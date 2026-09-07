using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Invitations;

/// <summary>
/// Checks invitation ownership and organization-specific listing despite a different ambient tenant.
/// </summary>
[Trait("Category", "Integration")]
public class InvitationScopingTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IInvitationRepository Invitations => ScopedServices.GetRequiredService<IInvitationRepository>();

    private IdentityDbContext DbContext => ScopedServices.GetRequiredService<IdentityDbContext>();

    [Fact]
    public async Task TheInvitationList_ReturnsOnlyTheNamedOrganizationsRows()
    {
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();
        string inA = await SeedInvitationAsync(orgA);
        string inB = await SeedInvitationAsync(orgB);

        // A different ambient tenant distinguishes explicit list scoping from the query filter.
        DbContext.SetTenant(TenantId.Create(Guid.NewGuid()));

        List<Invitation> listed = await Invitations.GetPagedByTenantAsync(orgA, take: 100);

        listed.Select(i => i.Email).Should().Contain(inA);
        listed.Select(i => i.Email).Should().NotContain(inB);
        listed.Should().OnlyContain(i => i.TenantId.Value == orgA);
    }

    [Fact]
    public async Task GetInvitations_AsAnAdminOfOneOrganization_ExcludesAnothersRows()
    {
        Guid orgA = Guid.NewGuid();
        Guid orgB = Guid.NewGuid();
        string inA = await SeedInvitationAsync(orgA);
        string inB = await SeedInvitationAsync(orgB);

        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        SetTestTenant(orgA);

        HttpResponseMessage response = await Client.GetAsync("/identity/invitations?skip=0&take=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<InvitationRow>? rows = await response.Content.ReadFromJsonAsync<List<InvitationRow>>();
        rows.Should().NotBeNull();
        rows!.Select(r => r.Email).Should().Contain(inA);
        rows!.Select(r => r.Email).Should().NotContain(inB);
    }

    [Fact]
    public async Task CreatingAnInvitation_LandsItInTheCallersOwnOrganization()
    {
        Guid organizationId = Guid.NewGuid();
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";

        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");
        SetTestTenant(organizationId);

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            "/identity/invitations", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Invitation created = await DbContext.Invitations
            .IgnoreQueryFilters()
            .SingleAsync(i => i.Email == email);

        created.TenantId.Value.Should().Be(organizationId);
    }

    /// <summary>
    /// Seeds in the owning tenant and returns a unique email for lookup in the shared database.
    /// </summary>
    private async Task<string> SeedInvitationAsync(Guid organizationId)
    {
        string email = $"invited-{Guid.NewGuid():N}@wallow.dev";
        DbContext.SetTenant(TenantId.Create(organizationId));

        Invitations.Add(Invitation.Create(
            TenantId.Create(organizationId),
            email,
            TimeProvider.System.GetUtcNow().AddDays(7),
            Guid.NewGuid(),
            TimeProvider.System));

        await Invitations.SaveChangesAsync();

        return email;
    }

    private sealed record InvitationRow(Guid Id, string Email, string Status);
}
