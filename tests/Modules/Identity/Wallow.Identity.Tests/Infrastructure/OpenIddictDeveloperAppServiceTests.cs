#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Infrastructure.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Infrastructure;

public class OpenIddictDeveloperAppServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly OpenIddictDeveloperAppService _sut;

    public OpenIddictDeveloperAppServiceTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new OpenIddictDeveloperAppService(_appManager, NullLogger<OpenIddictDeveloperAppService>.Instance);
    }

    [Fact]
    public async Task RegisterClientAsync_Confidential_CreatesWithSecret()
    {
        DeveloperAppRegistrationResult result = await _sut.RegisterClientAsync(
            "app-test", "Test App", ["openid", "profile"],
            clientType: ClientTypes.Confidential,
            creatorUserId: "user-1");

        result.ClientId.Should().Be("app-test");
        result.ClientSecret.Should().NotBeNullOrEmpty();
        result.RegistrationAccessToken.Should().Be(result.ClientSecret);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.ClientId == "app-test" &&
                d.DisplayName == "Test App" &&
                d.ClientType == ClientTypes.Confidential &&
                d.ClientSecret != null &&
                d.Permissions.Contains(Permissions.Prefixes.Scope + "openid") &&
                d.Permissions.Contains(Permissions.Prefixes.Scope + "profile")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterClientAsync_Public_CreatesWithoutSecret()
    {
        DeveloperAppRegistrationResult result = await _sut.RegisterClientAsync(
            "app-public", "Public App", ["openid"],
            clientType: ClientTypes.Public);

        result.ClientId.Should().Be("app-public");

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.ClientType == ClientTypes.Public &&
                d.ClientSecret == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterClientAsync_WithRedirectUris_AddsAuthorizationCodeFlow()
    {
        await _sut.RegisterClientAsync(
            "app-redir", "Redirect App", ["openid"],
            redirectUris: ["https://app/callback", "https://app/callback2"]);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.Permissions.Contains(Permissions.Endpoints.Authorization) &&
                d.Permissions.Contains(Permissions.GrantTypes.AuthorizationCode) &&
                d.Permissions.Contains(Permissions.ResponseTypes.Code) &&
                d.RedirectUris.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterClientAsync_NoRedirectUris_ClientCredentialsOnly()
    {
        await _sut.RegisterClientAsync(
            "app-creds", "Creds App", ["openid"]);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.Permissions.Contains(Permissions.Endpoints.Token) &&
                d.Permissions.Contains(Permissions.GrantTypes.ClientCredentials) &&
                !d.Permissions.Contains(Permissions.Endpoints.Authorization)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterClientAsync_WithCreatorUserId_SetsProperty()
    {
        await _sut.RegisterClientAsync(
            "app-creator", "Creator App", ["openid"],
            creatorUserId: "creator-42");

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.Properties.ContainsKey("creatorUserId")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterClientAsync_NoCreatorUserId_NoProperty()
    {
        await _sut.RegisterClientAsync(
            "app-nocreator", "No Creator App", ["openid"]);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                !d.Properties.ContainsKey("creatorUserId")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserAppsAsync_ReturnsOnlyUserApps()
    {
        object app1 = new();
        object app2 = new();
        IAsyncEnumerable<object> apps = ToAsync([app1, app2]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(apps);

        // app1 belongs to user-1
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app1, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "App1";
                d.ClientType = ClientTypes.Confidential;
                d.Properties["creatorUserId"] = JsonSerializer.SerializeToElement("user-1");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(app1, Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>("app-1"));

        // app2 belongs to user-2
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app2, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "App2";
                d.ClientType = ClientTypes.Public;
                d.Properties["creatorUserId"] = JsonSerializer.SerializeToElement("user-2");
                return ValueTask.CompletedTask;
            });

        IReadOnlyList<DeveloperAppInfo> result = await _sut.GetUserAppsAsync("user-1");

        result.Should().HaveCount(1);
        result[0].ClientId.Should().Be("app-1");
        result[0].DisplayName.Should().Be("App1");
    }

    [Fact]
    public async Task GetUserAppsAsync_NoCreatorProperty_SkipsApp()
    {
        object app = new();
        IAsyncEnumerable<object> apps = ToAsync([app]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(apps);

        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "NoCreator";
                return ValueTask.CompletedTask;
            });

        IReadOnlyList<DeveloperAppInfo> result = await _sut.GetUserAppsAsync("user-1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAppAsync_AppNotFound_ReturnsNull()
    {
        _appManager.FindByClientIdAsync("missing", Arg.Any<CancellationToken>()).Returns((object?)null);

        DeveloperAppInfo? result = await _sut.GetUserAppAsync("user-1", "missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAppAsync_AppBelongsToUser_ReturnsInfo()
    {
        object app = new();
        _appManager.FindByClientIdAsync("app-1", Arg.Any<CancellationToken>()).Returns(app);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "My App";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://app/cb"));
                d.Properties["creatorUserId"] = JsonSerializer.SerializeToElement("user-1");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(app, Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>("app-1"));

        DeveloperAppInfo? result = await _sut.GetUserAppAsync("user-1", "app-1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("app-1");
        result.DisplayName.Should().Be("My App");
        result.RedirectUris.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserAppAsync_AppBelongsToDifferentUser_ReturnsNull()
    {
        object app = new();
        _appManager.FindByClientIdAsync("app-1", Arg.Any<CancellationToken>()).Returns(app);
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), app, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                OpenIddictApplicationDescriptor d = ci.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.Properties["creatorUserId"] = JsonSerializer.SerializeToElement("user-2");
                return ValueTask.CompletedTask;
            });

        DeveloperAppInfo? result = await _sut.GetUserAppAsync("user-1", "app-1");

        result.Should().BeNull();
    }

    private static async IAsyncEnumerable<object> ToAsync(List<object> items)
    {
        foreach (object item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
