#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// A token names its organization in one of two places: on the client it was issued through, when
/// that client is bound to an organization, or on the authorization it chains to, when a
/// first-party client ran the sign-in under an organization hint. Revoking a membership has to
/// reach both, and must leave the person's tokens for other organizations alone.
/// </summary>
public sealed class AccessRevokerTests
{
    private readonly IOpenIddictTokenManager _tokens = Substitute.For<IOpenIddictTokenManager>();
    private readonly IOpenIddictApplicationManager _applications = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOpenIddictAuthorizationManager _authorizations = Substitute.For<IOpenIddictAuthorizationManager>();
    private readonly IRealtimeAccessRevoker _realtime = Substitute.For<IRealtimeAccessRevoker>();
    private readonly IMembershipRepository _memberships = Substitute.For<IMembershipRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly AccessRevoker _sut;

    public AccessRevokerTests()
    {
        _sut = new AccessRevoker(
            _tokens,
            _applications,
            _authorizations,
            Substitute.For<IRegisteredClientRepository>(),
            _memberships,
            _realtime,
            NullLogger<AccessRevoker>.Instance);
        _tokens.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
        _tokens.TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _authorizations.TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _memberships.GetForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Membership>().AsReadOnly());
    }

    [Fact]
    public async Task RevokeAsync_RevokesTokensChainedToAnAuthorizationNamingTheOrganization()
    {
        object authorization = Authorization("auth-1", _organizationId);
        object token = Token("token-1", applicationId: "unbound-first-party");
        _tokens.FindByAuthorizationIdAsync("auth-1", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(authorization));
        UnboundApplication("unbound-first-party");

        await _sut.RevokeMembershipAsync(_userId, _organizationId);

        await _tokens.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
        await _authorizations.Received(1).TryRevokeAsync(authorization, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_LeavesTokensChainedToAnotherOrganizationsAuthorizationAlone()
    {
        object authorization = Authorization("auth-2", Guid.NewGuid());
        object token = Token("token-2", applicationId: "unbound-first-party");
        _tokens.FindByAuthorizationIdAsync("auth-2", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(authorization));
        _tokens.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));
        UnboundApplication("unbound-first-party");

        await _sut.RevokeMembershipAsync(_userId, _organizationId);

        await _tokens.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _authorizations.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_StillRevokesTokensIssuedThroughAClientBoundToTheOrganization()
    {
        object token = Token("token-3", applicationId: "bound-partner");
        _tokens.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));
        BoundApplication("bound-partner", _organizationId);

        await _sut.RevokeMembershipAsync(_userId, _organizationId);

        await _tokens.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
        await _realtime.Received(1).RevokeAsync(_userId.ToString(), _organizationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSessionAsync_RevokesTheSessionsAuthorizationsAndChainedTokens()
    {
        object authorization = SessionAuthorization("auth-sid", "session-under-test");
        object token = Token("token-sid", applicationId: "any-app");
        _tokens.FindByAuthorizationIdAsync("auth-sid", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token));
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(authorization));

        await _sut.RevokeSessionAsync(_userId, "session-under-test");

        await _tokens.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
        await _authorizations.Received(1).TryRevokeAsync(authorization, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSessionAsync_LeavesOtherSessionsAndConsentRecordsAlone()
    {
        // Another browser session's authorization carries its own sid; the permanent consent
        // record carries none. Ending one session may touch neither.
        object otherSession = SessionAuthorization("auth-other", "some-other-session");
        object consentRecord = SessionAuthorization("auth-consent", sid: null);
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(otherSession, consentRecord));

        await _sut.RevokeSessionAsync(_userId, "session-under-test");

        await _tokens.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _authorizations.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeClientAsync_RevokesEveryTokenTheClientWasIssued_AndHangsUpItsRealtimeConnections()
    {
        object application = new();
        _applications.FindByClientIdAsync("app-one", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applications.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("application-1"));
        object access = Token("token-access", applicationId: "application-1");
        object refresh = Token("token-refresh", applicationId: "application-1");
        object alreadyRevoked = Token("token-stale", applicationId: "application-1");
        _tokens.FindByApplicationIdAsync("application-1", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(access, refresh, alreadyRevoked));
        _tokens.TryRevokeAsync(alreadyRevoked, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(false));

        int revoked = await _sut.RevokeClientAsync("app-one");

        revoked.Should().Be(2);
        await _tokens.Received(1).TryRevokeAsync(access, Arg.Any<CancellationToken>());
        await _tokens.Received(1).TryRevokeAsync(refresh, Arg.Any<CancellationToken>());
        await _realtime.Received(1).RevokeClientAsync("app-one", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeClientAsync_ForAnUnknownClient_RevokesNothing()
    {
        _applications.FindByClientIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        int revoked = await _sut.RevokeClientAsync("missing");

        revoked.Should().Be(0);
        await _tokens.DidNotReceive().TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _realtime.DidNotReceive().RevokeClientAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeUserAsync_RevokesEveryTokenAndPerLoginAuthorization_ButLeavesConsentRecords()
    {
        object adHoc = TypedAuthorization("auth-login", OpenIddictConstants.AuthorizationTypes.AdHoc);
        object consent = TypedAuthorization("auth-consent", OpenIddictConstants.AuthorizationTypes.Permanent);
        object chained = Token("token-chained", applicationId: "any-app");
        object stray = Token("token-stray", applicationId: "any-app");
        _tokens.FindByAuthorizationIdAsync("auth-login", Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(chained));
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(adHoc, consent));
        _tokens.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(chained, stray));

        await _sut.RevokeUserAsync(_userId);

        // The chained token appears in both walks; Received(1) asserts the dedup holds.
        await _tokens.Received(1).TryRevokeAsync(chained, Arg.Any<CancellationToken>());
        await _tokens.Received(1).TryRevokeAsync(stray, Arg.Any<CancellationToken>());
        await _authorizations.Received(1).TryRevokeAsync(adHoc, Arg.Any<CancellationToken>());
        await _authorizations.DidNotReceive().TryRevokeAsync(consent, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeUserAsync_HangsUpRealtimeForActiveMembershipsOnly()
    {
        Guid suspendedOrgId = Guid.NewGuid();
        Membership active = Membership.Enroll(
            _userId, OrganizationId.Create(_organizationId), Guid.NewGuid(), TimeProvider.System);
        Membership suspended = Membership.Enroll(
            _userId, OrganizationId.Create(suspendedOrgId), Guid.NewGuid(), TimeProvider.System);
        suspended.Suspend(Guid.NewGuid(), TimeProvider.System);
        _memberships.GetForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<Membership> { active, suspended }.AsReadOnly());

        await _sut.RevokeUserAsync(_userId);

        await _realtime.Received(1).RevokeAsync(_userId.ToString(), _organizationId, Arg.Any<CancellationToken>());
        await _realtime.DidNotReceive().RevokeAsync(_userId.ToString(), suspendedOrgId, Arg.Any<CancellationToken>());
    }

    private object Authorization(string id, Guid organizationId)
    {
        object authorization = new();
        _authorizations.GetIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(id));
        _authorizations
            .When(a => a.PopulateAsync(Arg.Any<OpenIddictAuthorizationDescriptor>(), authorization, Arg.Any<CancellationToken>()))
            .Do(call => call.Arg<OpenIddictAuthorizationDescriptor>().Properties[AuthorizationProperties.OrganizationId] =
                JsonSerializer.SerializeToElement(organizationId.ToString()));
        return authorization;
    }

    private object SessionAuthorization(string id, string? sid)
    {
        object authorization = new();
        _authorizations.GetIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(id));
        if (sid is not null)
        {
            _authorizations
                .When(a => a.PopulateAsync(Arg.Any<OpenIddictAuthorizationDescriptor>(), authorization, Arg.Any<CancellationToken>()))
                .Do(call => call.Arg<OpenIddictAuthorizationDescriptor>().Properties[AuthorizationProperties.SessionId] =
                    JsonSerializer.SerializeToElement(sid));
        }

        return authorization;
    }

    private object TypedAuthorization(string id, string type)
    {
        object authorization = new();
        _authorizations.GetIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(id));
        _authorizations.GetTypeAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(type));
        return authorization;
    }

    private object Token(string id, string applicationId)
    {
        object token = new();
        _tokens.GetIdAsync(token, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<string?>(id));
        _tokens.GetApplicationIdAsync(token, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(applicationId));
        return token;
    }

    private void UnboundApplication(string applicationId)
    {
        object application = new();
        _applications.FindByIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
    }

    private void BoundApplication(string applicationId, Guid organizationId)
    {
        object application = new();
        _applications.FindByIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applications
            .When(a => a.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), application, Arg.Any<CancellationToken>()))
            .Do(call => call.Arg<OpenIddictApplicationDescriptor>().Properties[ClientApplicationProperties.TenantId] =
                JsonSerializer.SerializeToElement(organizationId.ToString()));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
