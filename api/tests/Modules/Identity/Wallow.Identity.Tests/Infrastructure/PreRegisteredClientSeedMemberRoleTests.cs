#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Seed members are enrolled with the role their client's <c>seedMemberRoles</c> map names.
/// Roles are granted per (user, organization), so a seed member the map does not name gets the
/// baseline <c>user</c> role rather than whatever they hold elsewhere.
/// </summary>
public sealed class PreRegisteredClientSeedMemberRoleTests
{
    private const string SeedEmail = "admin@wallow.dev";

    private readonly IOrganizationService _orgService = Substitute.For<IOrganizationService>();
    private readonly UserManager<WallowUser> _userManager = Substitute.For<UserManager<WallowUser>>(
        Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
    private readonly PreRegisteredClientOptions _options = new();
    private readonly PreRegisteredClientSyncService _sut;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId;

    public PreRegisteredClientSeedMemberRoleTests()
    {
        IOpenIddictApplicationManager appManager = Substitute.For<IOpenIddictApplicationManager>();
        appManager.FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));

        _orgService.GetOrganizationsAsync("Wallow", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new OrganizationDto(_orgId, "Wallow", null, 0)]);
        _orgService.GetMembersAsync(_orgId, Arg.Any<CancellationToken>())
            .Returns([]);

        WallowUser seedUser = WallowUser.Create("Admin", "User", SeedEmail, TimeProvider.System);
        _userId = seedUser.Id;
        _userManager.FindByEmailAsync(SeedEmail).Returns(seedUser);

        _sut = new PreRegisteredClientSyncService(
            appManager, _orgService, _userManager, Options.Create(_options),
            NullLogger<PreRegisteredClientSyncService>.Instance);
    }

    private void AddClient(Dictionary<string, string>? seedMemberRoles)
    {
        PreRegisteredClientDefinition client = new()
        {
            ClientId = "web",
            DisplayName = "Web",
            Secret = "s",
            TenantName = "Wallow",
            SeedMembers = [SeedEmail],
            RedirectUris = ["https://l/cb"],
            Scopes = ["openid"]
        };

        if (seedMemberRoles is not null)
        {
            foreach (KeyValuePair<string, string> entry in seedMemberRoles)
            {
                client.SeedMemberRoles[entry.Key] = entry.Value;
            }
        }

        _options.Clients.Add(client);
    }

    [Fact]
    public async Task SyncAsync_WhenSeedMemberRoleIsMapped_EnrollsWithThatRole()
    {
        AddClient(new Dictionary<string, string>(StringComparer.Ordinal) { [SeedEmail] = "admin" });

        await _sut.SyncAsync(CancellationToken.None);

        await _orgService.Received().AddMemberAsync(_orgId, _userId, "admin", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenSeedMemberRoleIsUnmapped_EnrollsWithTheBaselineRole()
    {
        AddClient(seedMemberRoles: null);

        await _sut.SyncAsync(CancellationToken.None);

        await _orgService.Received().AddMemberAsync(_orgId, _userId, "user", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_MatchesTheSeedMemberRoleKeyCaseInsensitively()
    {
        AddClient(new Dictionary<string, string>(StringComparer.Ordinal) { ["ADMIN@WALLOW.DEV"] = "admin" });

        await _sut.SyncAsync(CancellationToken.None);

        await _orgService.Received().AddMemberAsync(_orgId, _userId, "admin", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Seeding has no human behind it, so the audit actor is the system sentinel rather than the
    /// member being enrolled. Passing the member would make every seeded enrollment read as
    /// self-granted.
    /// </summary>
    [Fact]
    public async Task SyncAsync_EnrollsWithTheSystemActorNotTheMember()
    {
        AddClient(new Dictionary<string, string>(StringComparer.Ordinal) { [SeedEmail] = "admin" });

        await _sut.SyncAsync(CancellationToken.None);

        await _orgService.Received().AddMemberAsync(_orgId, _userId, "admin", Guid.Empty, Arg.Any<CancellationToken>());
    }
}
