#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class OpenIddictDeveloperAppServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IOpenIddictScopeManager _scopeManager = Substitute.For<IOpenIddictScopeManager>();
    private readonly OpenIddictDeveloperAppService _sut;

    public OpenIddictDeveloperAppServiceTests()
    {
        _sut = new OpenIddictDeveloperAppService(_appManager, _scopeManager);
    }

    [Fact]
    public async Task GetConsentInfoAsync_UnknownClient_ReturnsNull()
    {
        _appManager.FindByClientIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        ConsentInfoDto? info = await _sut.GetConsentInfoAsync("missing", ["openid"]);

        info.Should().BeNull();
    }

    [Fact]
    public async Task GetConsentInfoAsync_KnownClient_DescribesClientAndScopes()
    {
        object app = new();
        object scope = new();
        _appManager.FindByClientIdAsync("app-acme-portal", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(app));
        _appManager.GetDisplayNameAsync(app, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("Acme Portal"));
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["logoUrl"] =
                    JsonSerializer.SerializeToElement("https://cdn.example.com/logo.png");
                return ValueTask.CompletedTask;
            });
        _scopeManager.FindByNameAsync("storage.read", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(scope));
        _scopeManager.GetDescriptionAsync(scope, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>("Read your files"));
        _scopeManager.FindByNameAsync("openid", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(null));

        ConsentInfoDto? info = await _sut.GetConsentInfoAsync("app-acme-portal", ["openid", "storage.read"]);

        info.Should().NotBeNull();
        info!.DisplayName.Should().Be("Acme Portal");
        info.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
        info.RequestedScopes.Should().BeEquivalentTo(
        [
            new ConsentScopeDto("openid", null),
            new ConsentScopeDto("storage.read", "Read your files"),
        ]);
    }
}
