using System.Collections.Immutable;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ScopeSubsetValidatorTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ScopeSubsetValidator _sut;

    public ScopeSubsetValidatorTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new ScopeSubsetValidator(_applicationManager);
    }

    [Fact]
    public async Task ValidateAsync_ApplicationNotFound_ReturnsFailure()
    {
        _applicationManager.FindByClientIdAsync("sa-unknown", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        ScopeValidationResult result = await _sut.ValidateAsync("sa-unknown", ["users.read"], CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("sa-unknown");
    }

    [Fact]
    public async Task ValidateAsync_RequestedScopesAreSubset_ReturnsSuccess()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Prefixes.Scope + "users.read",
                Permissions.Prefixes.Scope + "users.write",
                Permissions.Prefixes.Scope + "billing.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", ["users.read", "billing.read"], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_RequestedScopesAreExactMatch_ReturnsSuccess()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Prefixes.Scope + "users.read",
                Permissions.Prefixes.Scope + "billing.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", ["users.read", "billing.read"], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_RequestedScopesAreSuperset_ReturnsFailure()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Prefixes.Scope + "users.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", ["users.read", "billing.read"], CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("billing.read");
    }

    [Fact]
    public async Task ValidateAsync_MismatchedScopes_ReturnsFailureWithAllDisallowed()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Prefixes.Scope + "users.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", ["billing.read", "storage.write"], CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("billing.read");
        result.ErrorMessage.Should().Contain("storage.write");
    }

    [Fact]
    public async Task ValidateAsync_EmptyRequestedScopes_ReturnsSuccess()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Prefixes.Scope + "users.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", [], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_NonScopePermissionsIgnored_OnlyChecksScopePrefixed()
    {
        object app = new object();
        _applicationManager.FindByClientIdAsync("sa-test", Arg.Any<CancellationToken>())
            .Returns(app);
        _applicationManager.GetPermissionsAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create(
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.Prefixes.Scope + "users.read"));

        ScopeValidationResult result = await _sut.ValidateAsync("sa-test", ["users.read"], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
