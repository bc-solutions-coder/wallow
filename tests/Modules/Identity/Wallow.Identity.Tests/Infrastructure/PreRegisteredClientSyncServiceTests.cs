#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class PreRegisteredClientSyncServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly PreRegisteredClientSyncService _sut;
    private readonly PreRegisteredClientOptions _options;

    public PreRegisteredClientSyncServiceTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        IOrganizationService orgService = Substitute.For<IOrganizationService>();
        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _options = new PreRegisteredClientOptions();
        _sut = new PreRegisteredClientSyncService(
            _appManager, orgService, userManager, Options.Create(_options), NullLogger<PreRegisteredClientSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_NewClient_Creates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "web",
            DisplayName = "Web",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = ["https://l/so"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("web", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientId == "web"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_PublicClient_SetsPublic()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "spa",
            DisplayName = "SPA",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("spa", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientType == ClientTypes.Public),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "ex",
            DisplayName = "New",
            Secret = "s",
            RedirectUris = ["https://new/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });
        object existing = new object();
        _appManager.FindByClientIdAsync("ex", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor d = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Old";
                d.ClientType = ClientTypes.Confidential;
                return ValueTask.CompletedTask;
            });
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.DisplayName == "New"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_NoChanges_SkipsUpdate()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "same",
            DisplayName = "Same",
            Secret = "s",
            RedirectUris = ["https://s/cb"],
            PostLogoutRedirectUris = ["https://s/so"],
            Scopes = ["openid"]
        });
        object existing = new object();
        _appManager.FindByClientIdAsync("same", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor d = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "Same";
                d.ClientType = ClientTypes.Confidential;
                d.RedirectUris.Add(new Uri("https://s/cb"));
                d.PostLogoutRedirectUris.Add(new Uri("https://s/so"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.DidNotReceive().UpdateAsync(
            existing,
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DeletesRemovedConfigClients()
    {
        _options.Clients.Clear();
        object stale = new object();
        IAsyncEnumerable<object> apps = ToAsync(new List<object> { stale });
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(apps);
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), stale, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["source"] =
                    JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(stale, Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<string?>("removed"));
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.Received(1).DeleteAsync(stale, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DoesNotDeleteNonConfigClients()
    {
        _options.Clients.Clear();
        object manual = new object();
        IAsyncEnumerable<object> apps = ToAsync(new List<object> { manual });
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(apps);
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), manual, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);
        await _sut.SyncAsync(CancellationToken.None);
        await _appManager.DidNotReceive().DeleteAsync(manual, Arg.Any<CancellationToken>());
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
