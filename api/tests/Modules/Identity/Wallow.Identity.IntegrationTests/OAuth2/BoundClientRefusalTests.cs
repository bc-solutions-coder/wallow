using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// A third-party client bound to an organization is a relying party, not a screen of ours: a
/// user the organization refuses is sent back to the client's redirect URI with
/// <c>access_denied</c> and an <c>error_description</c> naming why, never to the auth host's
/// own pages. A pending request is refused the same way, but the request stays recorded.
/// </summary>
public sealed class BoundClientRefusalTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "BoundRefusal1234!";
    private const string ClientSecret = "bound-refusal-client-secret";
    private const string Scope = "openid profile email";

    private static readonly string[] _clientScopes = ["openid", "profile", "email"];

    [Fact]
    public async Task Authorize_ForANonMember_ReturnsAccessDeniedNotAMember()
    {
        Seed seed = await SeedAsync();

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        ShouldRefuseToRelyingParty(authorize, "not_a_member");
    }

    [Fact]
    public async Task Authorize_ForASuspendedMembership_ReturnsAccessDeniedMembershipSuspended()
    {
        Seed seed = await SeedAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");
        IMembershipReviewService review = ScopedServices.GetRequiredService<IMembershipReviewService>();
        await review.SuspendAsync(seed.OrganizationId, seed.UserId, seed.OwnerId);

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        ShouldRefuseToRelyingParty(authorize, "membership_suspended");
    }

    [Fact]
    public async Task Authorize_ForADeniedMembership_ReturnsAccessDeniedMembershipDenied()
    {
        Seed seed = await SeedAsync();
        Membership membership = Membership.RequestAccess(
            seed.UserId, OrganizationId.Create(seed.OrganizationId), TimeProvider.System);
        membership.Deny(seed.OwnerId, TimeProvider.System);
        await AddMembershipAsync(membership);

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        ShouldRefuseToRelyingParty(authorize, "membership_denied");
    }

    [Fact]
    public async Task Authorize_UnderRequestApproval_RecordsTheRequestAndReturnsAccessDeniedMembershipPending()
    {
        Seed seed = await SeedAsync();
        await ScopedServices.GetRequiredService<IOrganizationService>().UpdateEnrollmentAsync(
            seed.OrganizationId, EnrollmentPolicy.RequestApproval, null, null, seed.OwnerId);

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        ShouldRefuseToRelyingParty(authorize, "membership_pending");
        IMembershipRepository memberships = ScopedServices.GetRequiredService<IMembershipRepository>();
        Membership? recorded = await memberships.GetAsync(seed.UserId, seed.OrganizationId);
        recorded.Should().NotBeNull("the refusal still records the access request for review");
        recorded!.Status.Should().Be(MembershipStatus.Pending);
    }

    private static void ShouldRefuseToRelyingParty(AuthorizeOutcome authorize, string reason)
    {
        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Location?.ToString().Should().StartWith(AuthorizationCodeFlowHarness.RedirectUri);
        authorize.Error.Should().Be("access_denied");
        authorize.ErrorDescription.Should().Be(reason);
    }

    private async Task<AuthorizeOutcome> AuthorizeAsync(Seed seed)
    {
        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return await harness.AuthorizeAsync(seed.ClientId, Scope);
    }

    private async Task AddMembershipAsync(Membership membership)
    {
        IMembershipRepository memberships = ScopedServices.GetRequiredService<IMembershipRepository>();
        memberships.Add(membership);
        await memberships.SaveChangesAsync();
    }

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"bound-refusal-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"bound-refusal-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Bound Refusal {suffix}", ownerId);

        string clientId = $"partner-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, ownerId, organizationId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId, Guid OwnerId, Guid OrganizationId);
}
