#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The consent screen reads a scope's wording from OpenIddict's scope table and nowhere else, so
/// this mirrors the API-scope catalog into it. The protocol scopes are not catalog rows, so their
/// wording is the service's own.
/// </summary>
public sealed class OpenIddictScopeSyncServiceTests : IDisposable
{
    private static readonly string[] _protocolScopes =
        ["openid", "profile", "email", "roles", "offline_access"];

    private readonly IdentityDbContext _dbContext;
    private readonly IOpenIddictScopeManager _scopeManager = Substitute.For<IOpenIddictScopeManager>();
    private readonly OpenIddictScopeSyncService _sut;

    public OpenIddictScopeSyncServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, DataProtectionProvider.Create("test"));

        _sut = new OpenIddictScopeSyncService(
            _dbContext, _scopeManager, NullLogger<OpenIddictScopeSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_ForACatalogScopeWithNoDescriptor_CreatesOneCarryingItsWording()
    {
        await GivenCatalogScopeAsync("storage.write", "Write Storage", "Access to upload files");

        await _sut.SyncAsync();

        await _scopeManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictScopeDescriptor>(d =>
                d.Name == "storage.write"
                && d.DisplayName == "Write Storage"
                && d.Description == "Access to upload files"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(ProtocolScopes))]
    public async Task SyncAsync_DescribesEveryProtocolScope(string name)
    {
        await _sut.SyncAsync();

        await _scopeManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictScopeDescriptor>(d =>
                d.Name == name
                && !string.IsNullOrWhiteSpace(d.DisplayName)
                && !string.IsNullOrWhiteSpace(d.Description)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ForADescriptorThatMatchesTheCatalog_WritesNothing()
    {
        await GivenCatalogScopeAsync("storage.read", "Read Storage", "Access to read files");
        GivenDescriptor("storage.read", "Read Storage", "Access to read files");

        await _sut.SyncAsync();

        await _scopeManager.DidNotReceive().UpdateAsync(
            Arg.Any<object>(), Arg.Any<OpenIddictScopeDescriptor>(), Arg.Any<CancellationToken>());
        await _scopeManager.DidNotReceive().CreateAsync(
            Arg.Is<OpenIddictScopeDescriptor>(d => d.Name == "storage.read"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ForADescriptorThatDriftedFromTheCatalog_RewritesIt()
    {
        await GivenCatalogScopeAsync("storage.read", "Read Storage", "Access to read files");
        object existing = GivenDescriptor("storage.read", "Read Storage", "Stale wording");

        await _sut.SyncAsync();

        await _scopeManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictScopeDescriptor>(d => d.Description == "Access to read files"),
            Arg.Any<CancellationToken>());
    }

    public static TheoryData<string> ProtocolScopes() => new(_protocolScopes);

    public void Dispose() => _dbContext.Dispose();

    private async Task GivenCatalogScopeAsync(string code, string displayName, string description)
    {
        _dbContext.ApiScopes.Add(ApiScope.Create(code, displayName, "Identity", description));
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// The stored scope is opaque to the manager's callers, so the fixture is any object at all;
    /// what it holds is whatever <c>PopulateAsync</c> is set up to write onto the descriptor.
    /// </summary>
    private object GivenDescriptor(string name, string displayName, string description)
    {
        object stored = new();
        _scopeManager.FindByNameAsync(name, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object?>(stored));

        _scopeManager
            .When(m => m.PopulateAsync(
                Arg.Any<OpenIddictScopeDescriptor>(), stored, Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                OpenIddictScopeDescriptor descriptor = call.Arg<OpenIddictScopeDescriptor>();
                descriptor.Name = name;
                descriptor.DisplayName = displayName;
                descriptor.Description = description;
            });

        return stored;
    }
}
