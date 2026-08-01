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
/// Which organization an invitation belongs to, and which organization's list a caller sees.
/// Both are asserted with the tenant query filter BYPASSED, so a version that leans on the
/// ambient filter instead of on its own scoping fails here. <c>CreateInvitationAsync</c> takes
/// no tenant argument at all — the ambient tenant is the only one an invitation can land in.
///
/// Backend-dependent: requires the WallowApiFactory stack (Postgres + seeded identity data).
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

        // A third organization: if the list scoped on the ambient filter rather than its
        // parameter, this would return nothing at all instead of org A's row.
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
    /// Seeds under the owning organization's tenant, because the save interceptor stamps
    /// <c>TenantId</c> from the context rather than from the entity. Returns the email, which is
    /// unique per call and so identifies the row in a shared database.
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
