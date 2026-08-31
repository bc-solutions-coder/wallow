#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Branding;

namespace Wallow.Identity.Tests.Infrastructure;

public class AuthorizeContextServiceTests
{
    private const string ClientId = "app-acme-portal";
    private const string RedirectUri = "https://portal.example.com/callback";

    private readonly IOpenIddictApplicationManager _appManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOpenIddictScopeManager _scopeManager = Substitute.For<IOpenIddictScopeManager>();
    private readonly IClientAccessPolicy _accessPolicy = Substitute.For<IClientAccessPolicy>();
    private readonly IClientTenantResolver _tenantResolver = Substitute.For<IClientTenantResolver>();
    private readonly IClientBrandingProvider _brandingProvider = Substitute.For<IClientBrandingProvider>();
    private readonly AuthorizeContextService _sut;
    private readonly object _app = new();

    public AuthorizeContextServiceTests()
    {
        _sut = new AuthorizeContextService(
            _appManager, _scopeManager, _accessPolicy, _tenantResolver, _brandingProvider);

        _appManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(_app));
        _appManager.ValidateRedirectUriAsync(_app, RedirectUri, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(true));
        _appManager.GetConsentTypeAsync(_app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(OpenIddictConstants.ConsentTypes.Explicit));
        _appManager.GetDisplayNameAsync(_app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("Acme Portal"));
        _accessPolicy.EvaluateAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns((ClientAccessRefusal?)null);
        _tenantResolver.ResolveAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(Guid.NewGuid(), "Acme Corp"));
        _brandingProvider.FindAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns((PublicClientBranding?)null);
    }

    [Fact]
    public async Task ResolveAsync_UnknownClient_ReturnsNull()
    {
        _appManager.FindByClientIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        AuthorizeContextDto? context = await _sut.ResolveAsync("missing", RedirectUri, ["openid"]);

        context.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_RedirectUriNotRegistered_ReturnsNull()
    {
        _appManager.ValidateRedirectUriAsync(_app, "https://attacker.example.com/", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(false));

        AuthorizeContextDto? context = await _sut.ResolveAsync(
            ClientId, "https://attacker.example.com/", ["openid"]);

        context.Should().BeNull();
        await _brandingProvider.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
    }

    [Fact]
    public async Task ResolveAsync_RefusedClient_ReturnsNull()
    {
        _accessPolicy.EvaluateAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientAccessRefusal("client_suspended", "The client is suspended."));

        AuthorizeContextDto? context = await _sut.ResolveAsync(ClientId, RedirectUri, ["openid"]);

        context.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_BrandedClient_DescribesClientOrganizationAndScopes()
    {
        object scope = new();
        _brandingProvider.FindAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new PublicClientBranding(
                ClientId, "Acme Portal Branded", "Sign in to shine",
                "https://cdn.example.com/logo.png", """{"light":{}}"""));
        _scopeManager.FindByNameAsync("storage.read", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(scope));
        _scopeManager.GetDescriptionAsync(scope, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("Read your files"));
        _scopeManager.FindByNameAsync("openid", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        AuthorizeContextDto? context = await _sut.ResolveAsync(
            ClientId, RedirectUri, ["openid", "storage.read"]);

        context.Should().NotBeNull();
        context!.DisplayName.Should().Be("Acme Portal Branded");
        context.Tagline.Should().Be("Sign in to shine");
        context.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        context.ThemeJson.Should().Be("""{"light":{}}""");
        context.OrganizationName.Should().Be("Acme Corp");
        context.FirstParty.Should().BeFalse();
        context.Scopes.Should().BeEquivalentTo(
        [
            new ConsentScopeDto("openid", null),
            new ConsentScopeDto("storage.read", "Read your files"),
        ]);
    }

    [Fact]
    public async Task ResolveAsync_NoBrandingRow_FallsBackToOpenIddictDisplayName()
    {
        AuthorizeContextDto? context = await _sut.ResolveAsync(ClientId, RedirectUri, []);

        context.Should().NotBeNull();
        context!.DisplayName.Should().Be("Acme Portal");
        context.Tagline.Should().BeNull();
        context.LogoUrl.Should().BeNull();
        context.ThemeJson.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoBrandingAndNoDisplayName_FallsBackToClientId()
    {
        _appManager.GetDisplayNameAsync(_app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(null));

        AuthorizeContextDto? context = await _sut.ResolveAsync(ClientId, RedirectUri, []);

        context!.DisplayName.Should().Be(ClientId);
    }

    [Fact]
    public async Task ResolveAsync_FirstPartyClient_IsMarkedFirstPartyWithoutOrganization()
    {
        _appManager.GetConsentTypeAsync(_app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(OpenIddictConstants.ConsentTypes.Implicit));
        _tenantResolver.ResolveAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(Guid.Empty, null));

        AuthorizeContextDto? context = await _sut.ResolveAsync(ClientId, RedirectUri, []);

        context.Should().NotBeNull();
        context!.FirstParty.Should().BeTrue();
        context.OrganizationName.Should().BeNull();
    }
}
