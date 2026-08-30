#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// A token names its organization in one of two places: on the client it was issued through, when
/// that client is bound to an organization, or on the authorization it chains to, when a
/// first-party client ran the sign-in under an organization hint. Revoking a membership has to
/// reach both, and must leave the person's tokens for other organizations alone.
/// </summary>
public sealed class MembershipAccessRevokerTests
{
    private readonly IOpenIddictTokenManager _tokens = Substitute.For<IOpenIddictTokenManager>();
    private readonly IOpenIddictApplicationManager _applications = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOpenIddictAuthorizationManager _authorizations = Substitute.For<IOpenIddictAuthorizationManager>();
    private readonly IRealtimeAccessRevoker _realtime = Substitute.For<IRealtimeAccessRevoker>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly MembershipAccessRevoker _sut;

    public MembershipAccessRevokerTests()
    {
        _sut = new MembershipAccessRevoker(
            _tokens, _applications, _authorizations, _realtime, NullLogger<MembershipAccessRevoker>.Instance);
        _tokens.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
        _authorizations.FindBySubjectAsync(_userId.ToString(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
        _tokens.TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _authorizations.TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
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

        await _sut.RevokeAsync(_userId, _organizationId);

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

        await _sut.RevokeAsync(_userId, _organizationId);

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

        await _sut.RevokeAsync(_userId, _organizationId);

        await _tokens.Received(1).TryRevokeAsync(token, Arg.Any<CancellationToken>());
        await _realtime.Received(1).RevokeAsync(_userId.ToString(), _organizationId, Arg.Any<CancellationToken>());
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
