#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.Abstractions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class SsoClientSessionServiceTests : IDisposable
{
    private static readonly Uri _issuer = new("http://localhost:5001");

    private readonly IdentityDbContext _dbContext;
    private readonly IOpenIddictApplicationManager _appManager;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SsoClientSessionService _sut;

    public SsoClientSessionServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: $"SsoSession_{Guid.NewGuid()}")
            .Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dp);

        _appManager = Substitute.For<IOpenIddictApplicationManager>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _sut = new SsoClientSessionService(
            _dbContext, _appManager, _timeProvider, NullLogger<SsoClientSessionService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    private void RegisterClient(string clientId, string? frontchannelLogoutUri)
    {
        object application = new object();
        _appManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>(application));
        _appManager.PopulateAsync(
                Arg.Any<OpenIddictApplicationDescriptor>(), application, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (frontchannelLogoutUri is not null)
                {
                    callInfo.ArgAt<OpenIddictApplicationDescriptor>(0)
                            .Properties["frontchannel_logout_uri"] =
                        JsonSerializer.SerializeToElement(frontchannelLogoutUri);
                }

                return ValueTask.CompletedTask;
            });
    }

    [Fact]
    public async Task RecordAsync_InsertsParticipationRow()
    {
        await _sut.RecordAsync("sid-1", "web", Guid.NewGuid(), CancellationToken.None);

        List<string> clients = await _dbContext.SsoSessionClients
            .Where(s => s.Sid == "sid-1")
            .Select(s => s.ClientId)
            .ToListAsync();
        clients.Should().Equal("web");
    }

    [Fact]
    public async Task RecordAsync_SameSidAndClientTwice_KeepsOneRow()
    {
        Guid userId = Guid.NewGuid();

        await _sut.RecordAsync("sid-1", "web", userId, CancellationToken.None);
        await _sut.RecordAsync("sid-1", "web", userId, CancellationToken.None);

        int count = await _dbContext.SsoSessionClients.CountAsync(s => s.Sid == "sid-1");
        count.Should().Be(1);
    }

    [Fact]
    public async Task BuildLogoutNotificationUrisAsync_AppendsIssuerAndSidToClientUri()
    {
        RegisterClient("web", "http://localhost:3000/bff/frontchannel-logout");
        await _sut.RecordAsync("sid-1", "web", Guid.NewGuid(), CancellationToken.None);

        IReadOnlyList<Uri> uris =
            await _sut.BuildLogoutNotificationUrisAsync("sid-1", _issuer, CancellationToken.None);

        uris.Should().ContainSingle();
        uris[0].GetLeftPart(UriPartial.Path).Should().Be("http://localhost:3000/bff/frontchannel-logout");
        System.Collections.Specialized.NameValueCollection query =
            System.Web.HttpUtility.ParseQueryString(uris[0].Query);
        query["iss"].Should().Be("http://localhost:5001/");
        query["sid"].Should().Be("sid-1");
    }

    [Fact]
    public async Task BuildLogoutNotificationUrisAsync_SkipsClientsWithoutFrontchannelUri()
    {
        RegisterClient("web", "http://localhost:3000/bff/frontchannel-logout");
        RegisterClient("silent", null);
        await _sut.RecordAsync("sid-1", "web", Guid.NewGuid(), CancellationToken.None);
        await _sut.RecordAsync("sid-1", "silent", Guid.NewGuid(), CancellationToken.None);

        IReadOnlyList<Uri> uris =
            await _sut.BuildLogoutNotificationUrisAsync("sid-1", _issuer, CancellationToken.None);

        uris.Should().ContainSingle();
    }

    [Fact]
    public async Task BuildLogoutNotificationUrisAsync_SkipsClientsNoLongerRegistered()
    {
        _appManager.FindByClientIdAsync("gone", Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<object?>((object?)null));
        await _sut.RecordAsync("sid-1", "gone", Guid.NewGuid(), CancellationToken.None);

        IReadOnlyList<Uri> uris =
            await _sut.BuildLogoutNotificationUrisAsync("sid-1", _issuer, CancellationToken.None);

        uris.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildLogoutNotificationUrisAsync_UnknownSid_ReturnsEmpty()
    {
        IReadOnlyList<Uri> uris =
            await _sut.BuildLogoutNotificationUrisAsync("nope", _issuer, CancellationToken.None);

        uris.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgetAsync_DeletesOnlyThatSidsRows()
    {
        await _sut.RecordAsync("sid-1", "web", Guid.NewGuid(), CancellationToken.None);
        await _sut.RecordAsync("sid-1", "bff", Guid.NewGuid(), CancellationToken.None);
        await _sut.RecordAsync("sid-2", "web", Guid.NewGuid(), CancellationToken.None);

        await _sut.ForgetAsync("sid-1", CancellationToken.None);

        List<string> remaining = await _dbContext.SsoSessionClients.Select(s => s.Sid).ToListAsync();
        remaining.Should().Equal("sid-2");
    }
}
