#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class PreRegisteredClientSyncServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly IOrganizationService _orgService;
    private readonly IRegisteredClientRepository _registry;
    private readonly PreRegisteredClientSyncService _sut;
    private readonly PreRegisteredClientOptions _options;

    public PreRegisteredClientSyncServiceTests()
    {
        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _orgService = Substitute.For<IOrganizationService>();
        _registry = Substitute.For<IRegisteredClientRepository>();
        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _options = new PreRegisteredClientOptions();
        _sut = new PreRegisteredClientSyncService(
            _appManager, _orgService, _registry, userManager,
            Options.Create(_options), TimeProvider.System,
            NullLogger<PreRegisteredClientSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_NewClient_Creates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "web",
            FirstParty = true,
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
            FirstParty = true,
            DisplayName = "SPA",
            Public = true,
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
    public async Task SyncAsync_SecretlessClientWithoutExplicitPublicFlag_ThrowsWithoutRegisteringAnything()
    {
        // An undeclared secret-less client is the fail-open case: registration must hard-fail
        // at startup rather than quietly minting a public client.
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "silent-public",
            DisplayName = "Silent Public",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("silent-public", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        Func<Task> sync = () => _sut.SyncAsync(CancellationToken.None);

        await sync.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*silent-public*");
        await _appManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SecretlessClientWithoutExplicitPublicFlag_FailsBeforeDeletingRemovedClients()
    {
        // The hard-fail must precede the destructive reconciliation pass, so a misconfigured
        // deployment cannot delete existing registrations on its way to throwing.
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "silent-public",
            DisplayName = "Silent Public",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });

        Func<Task> sync = () => _sut.SyncAsync(CancellationToken.None);

        await sync.Should().ThrowAsync<InvalidOperationException>();
        await _appManager.DidNotReceive().DeleteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ConfidentialClientWithSecret_SetsConfidentialAndKeepsSecret()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "bff",
            FirstParty = true,
            DisplayName = "BFF",
            Secret = "s3cret",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("bff", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.ClientType == ClientTypes.Confidential && d.ClientSecret == "s3cret"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "ex",
            FirstParty = true,
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
    public async Task SyncAsync_NewClientWithFrontchannelLogoutUri_SetsDescriptorProperty()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "fc",
            FirstParty = true,
            DisplayName = "FC",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"],
            FrontchannelLogoutUri = "https://l/bff/frontchannel-logout"
        });
        _appManager.FindByClientIdAsync("fc", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                FrontchannelUriOf(d) == "https://l/bff/frontchannel-logout"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingFrontchannelLogoutUriChanged_Updates()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "fc-up",
            FirstParty = true,
            DisplayName = "FC",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"],
            FrontchannelLogoutUri = "https://l/bff/frontchannel-logout"
        });
        object existing = new object();
        _appManager.FindByClientIdAsync("fc-up", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor d = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "FC";
                d.ClientType = ClientTypes.Confidential;
                d.ConsentType = ConsentTypes.Implicit;
                d.RedirectUris.Add(new Uri("https://l/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                d.Properties["frontchannel_logout_uri"] = JsonSerializer.SerializeToElement("https://l/old");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                FrontchannelUriOf(d) == "https://l/bff/frontchannel-logout"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_FrontchannelLogoutUriRemovedFromConfig_ClearsProperty()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "fc-gone",
            FirstParty = true,
            DisplayName = "FC",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"]
        });
        object existing = new object();
        _appManager.FindByClientIdAsync("fc-gone", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor d = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "FC";
                d.ClientType = ClientTypes.Confidential;
                d.ConsentType = ConsentTypes.Implicit;
                d.RedirectUris.Add(new Uri("https://l/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                d.Properties["frontchannel_logout_uri"] = JsonSerializer.SerializeToElement("https://l/old");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d => FrontchannelUriOf(d) == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_UnchangedFrontchannelLogoutUri_SkipsUpdate()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "fc-same",
            FirstParty = true,
            DisplayName = "FC",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = [],
            Scopes = ["openid"],
            FrontchannelLogoutUri = "https://l/bff/frontchannel-logout"
        });
        object existing = new object();
        _appManager.FindByClientIdAsync("fc-same", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor d = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                d.DisplayName = "FC";
                d.ClientType = ClientTypes.Confidential;
                d.ConsentType = ConsentTypes.Implicit;
                d.RedirectUris.Add(new Uri("https://l/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                d.Properties["frontchannel_logout_uri"] =
                    JsonSerializer.SerializeToElement("https://l/bff/frontchannel-logout");
                d.SetRefreshTokenLifetime(ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds);
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.DidNotReceive().UpdateAsync(
            existing,
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    private static string? FrontchannelUriOf(OpenIddictApplicationDescriptor descriptor) =>
        descriptor.Properties.TryGetValue("frontchannel_logout_uri", out JsonElement element)
            ? element.GetString()
            : null;

    [Fact]
    public async Task SyncAsync_NoChanges_SkipsUpdate()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "same",
            FirstParty = true,
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
                d.ConsentType = ConsentTypes.Implicit;
                d.RedirectUris.Add(new Uri("https://s/cb"));
                d.PostLogoutRedirectUris.Add(new Uri("https://s/so"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                d.SetRefreshTokenLifetime(ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds);
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

    [Fact]
    public async Task SyncAsync_DoesNotDeleteWhileTheApplicationListReaderIsStillOpen()
    {
        _options.Clients.Clear();
        object first = new object();
        object second = new object();
        ReaderBackedApplicationList applications = new([first, second]);
        _appManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => applications.Enumerate());
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<OpenIddictApplicationDescriptor>(0).Properties["source"] =
                    JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });
        _appManager.GetClientIdAsync(first, Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<string?>("removed-first"));
        _appManager.GetClientIdAsync(second, Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<string?>("removed-second"));

        List<object> deleted = [];
        _appManager.DeleteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (applications.ReaderIsOpen)
                {
                    // What Npgsql itself raises when a second command starts on a connection whose
                    // data reader has not been drained.
                    throw new InvalidOperationException("A command is already in progress");
                }

                deleted.Add(callInfo.ArgAt<object>(0));
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        deleted.Should().Equal(first, second);
    }

    [Fact]
    public async Task SyncAsync_TenantNameOnlyClient_RoutesOrgCreationThroughOrganizationService()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "tenant-client",
            DisplayName = "Tenant Client",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            PostLogoutRedirectUris = ["https://l/so"],
            Scopes = ["openid"],
            TenantName = "Acme"
        });
        _appManager.FindByClientIdAsync("tenant-client", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));
        _orgService.GetOrganizationsAsync("Acme", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OrganizationDto>>([]));
        _orgService.CreateOrganizationAsync(
                "Acme", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        await _sut.SyncAsync(CancellationToken.None);

        // Seed-derived orgs must be minted through Organization.Create (via CreateOrganizationAsync),
        // never a bypass path — Organization.Create is the single tenant-id mint point (T5.1).
        await _orgService.Received(1).CreateOrganizationAsync(
            "Acme", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Stands in for the OpenIddict EF Core store's <c>ListAsync</c>, which streams off an open
    /// Npgsql data reader that holds the connection until the enumeration finishes. Exposes whether
    /// that reader is still open so a test can reject any command issued mid-enumeration, the way
    /// the real driver does.
    /// </summary>
    private sealed class ReaderBackedApplicationList(IReadOnlyList<object> applications)
    {
        public bool ReaderIsOpen { get; private set; }

        public async IAsyncEnumerable<object> Enumerate()
        {
            ReaderIsOpen = true;

            try
            {
                foreach (object application in applications)
                {
                    yield return application;
                }

                await Task.CompletedTask;
            }
            finally
            {
                // Disposing the enumerator is what closes the reader; `await foreach` does it on
                // the way out of the loop.
                ReaderIsOpen = false;
            }
        }
    }

    private static async IAsyncEnumerable<object> ToAsync(List<object> items)
    {
        foreach (object item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task SyncAsync_FirstPartyClient_IsCreatedWithImplicitConsent()
    {
        // Consent exemption is OpenIddict's per-application consent type, written by the seed
        // from the explicit flag; the authorize endpoint reads it back and never looks at the id.
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("dashboard", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ConsentType == ConsentTypes.Implicit),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_OrganizationBoundClient_IsCreatedWithExplicitConsent()
    {
        Guid orgId = Guid.NewGuid();
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "wallow-looking-partner",
            DisplayName = "Partner",
            Secret = "s",
            TenantId = orgId,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("wallow-looking-partner", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ConsentType == ConsentTypes.Explicit),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClientWhoseConsentTypeDrifted_IsUpdated()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        object existing = new();
        _appManager.FindByClientIdAsync("dashboard", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                OpenIddictApplicationDescriptor d = call.Arg<OpenIddictApplicationDescriptor>();
                d.ClientId = "dashboard";
                d.DisplayName = "Dashboard";
                d.ClientType = ClientTypes.Confidential;
                d.ConsentType = ConsentTypes.Explicit;
                d.RedirectUris.Add(new Uri("https://l/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ConsentType == ConsentTypes.Implicit),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_FirstPartyClientWithoutADeclaredLifetime_PinsTheSevenDayDefault()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "web",
            FirstParty = true,
            DisplayName = "Web",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("web", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.GetRefreshTokenLifetimeSeconds() == ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_OrganizationBoundClientWithoutADeclaredLifetime_PinsTheOneDayDefault()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "partner",
            DisplayName = "Partner",
            Secret = "s",
            TenantId = Guid.NewGuid(),
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("partner", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d =>
                d.GetRefreshTokenLifetimeSeconds() == ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ServiceAccount_CarriesNoRefreshTokenLifetime()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "sa-worker",
            DisplayName = "Worker",
            Secret = "s",
            TenantId = Guid.NewGuid(),
            Scopes = ["openid"],
            // Meaningless on a client that never holds the refresh grant, so the seeder
            // must drop it rather than write a setting nothing will ever read.
            RefreshTokenLifetime = 3600
        });
        _appManager.FindByClientIdAsync("sa-worker", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.GetRefreshTokenLifetimeSeconds() == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ExistingClientWhoseLifetimeDrifted_IsUpdatedToTheSeedValue()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "dashboard",
            DisplayName = "Dashboard",
            Secret = "s",
            FirstParty = true,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"],
            RefreshTokenLifetime = 7200
        });
        object existing = new();
        _appManager.FindByClientIdAsync("dashboard", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(existing));
        _appManager.PopulateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), existing, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                OpenIddictApplicationDescriptor d = call.Arg<OpenIddictApplicationDescriptor>();
                d.ClientId = "dashboard";
                d.DisplayName = "Dashboard";
                d.ClientType = ClientTypes.Confidential;
                d.ConsentType = ConsentTypes.Implicit;
                d.RedirectUris.Add(new Uri("https://l/cb"));
                d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
                d.Properties["source"] = JsonSerializer.SerializeToElement("config");
                d.SetRefreshTokenLifetime(ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds);
                return ValueTask.CompletedTask;
            });

        await _sut.SyncAsync(CancellationToken.None);

        await _appManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.GetRefreshTokenLifetimeSeconds() == 7200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_LifetimeOutsideTheAcceptedRange_ThrowsWithoutRegisteringAnything()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "too-short",
            FirstParty = true,
            DisplayName = "Too short",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"],
            RefreshTokenLifetime = ClientRefreshTokenLifetimes.MinimumSeconds - 1
        });

        Func<Task> sync = () => _sut.SyncAsync(CancellationToken.None);

        await sync.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*too-short*refreshTokenLifetime*");
        await _appManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_OrganizationBoundClient_WritesARegistryRow()
    {
        // The org-clients management surface addresses clients only through the RegisteredClient
        // registry — without this row a seeded client is invisible to suspend/reinstate/delete.
        Guid orgId = Guid.NewGuid();
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "partner",
            DisplayName = "Partner",
            Secret = "s",
            TenantId = orgId,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("partner", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        _registry.Received(1).Add(Arg.Is<RegisteredClient>(r =>
            r.ClientId == "partner" &&
            r.OrganizationId == orgId &&
            r.Kind == RegisteredClientKind.Application));
        await _registry.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_OrganizationBoundServiceAccount_RegistersAsServiceAccountKind()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "sa-worker",
            DisplayName = "Worker",
            Secret = "s",
            TenantId = Guid.NewGuid(),
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("sa-worker", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        _registry.Received(1).Add(Arg.Is<RegisteredClient>(r =>
            r.ClientId == "sa-worker" && r.Kind == RegisteredClientKind.ServiceAccount));
    }

    [Fact]
    public async Task SyncAsync_FirstPartyClientWithoutATenant_WritesNoRegistryRow()
    {
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "web",
            FirstParty = true,
            DisplayName = "Web",
            Secret = "s",
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("web", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        await _sut.SyncAsync(CancellationToken.None);

        _registry.DidNotReceive().Add(Arg.Any<RegisteredClient>());
    }

    [Fact]
    public async Task SyncAsync_RegistryRowAlreadyUnderTheRightOwner_IsLeftUntouched()
    {
        // A re-seed must never recreate the row: a suspension the organization placed lives on
        // it, and replace-on-sync would silently lift that suspension.
        Guid orgId = Guid.NewGuid();
        _options.Clients.Add(new PreRegisteredClientDefinition
        {
            ClientId = "partner",
            DisplayName = "Partner",
            Secret = "s",
            TenantId = orgId,
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        });
        _appManager.FindByClientIdAsync("partner", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));
        RegisteredClient existingRow = RegisteredClient.Create(
            "partner", orgId, "Partner", RegisteredClientKind.Application, Guid.Empty, TimeProvider.System);
        _registry.GetByClientIdAsync("partner", Arg.Any<CancellationToken>())
            .Returns(existingRow);

        await _sut.SyncAsync(CancellationToken.None);

        _registry.DidNotReceive().Add(Arg.Any<RegisteredClient>());
        _registry.DidNotReceive().Remove(Arg.Any<RegisteredClient>());
    }
}
