using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wolverine.EntityFrameworkCore;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OrganizationClientServiceTests : IDisposable
{
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly IdentityDbContext _dbContext;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IRegisteredClientRepository _registeredClients;
    private readonly IDbContextOutbox _outbox;
    private readonly OrganizationClientService _sut;

    public OrganizationClientServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new IdentityDbContext(options, DataProtectionProvider.Create("test"));
        _dbContext.SetTenant(new TenantId(_organizationId));

        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _applicationManager.FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);
        _registeredClients = Substitute.For<IRegisteredClientRepository>();
        _outbox = Substitute.For<IDbContextOutbox>();

        IOrganizationRepository organizations = Substitute.For<IOrganizationRepository>();
        Organization organization = Organization.Create(
            new TenantId(_organizationId), "Acme", "acme", _actorId, TimeProvider.System);
        organizations.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        _sut = new OrganizationClientService(
            _applicationManager,
            _registeredClients,
            Substitute.For<IAccessRevoker>(),
            _dbContext,
            _outbox,
            organizations,
            Substitute.For<IApiScopeRepository>(),
            TimeProvider.System,
            new ConfigurationBuilder().Build(),
            Options.Create(new ServiceUrlsOptions()),
            NullLogger<OrganizationClientService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static RegisterClientInput ApplicationInput(
        string? brandingDisplayName = null, string? brandingTagline = null) => new(
        RegisteredClientKind.Application,
        "My App",
        new ClientConfigurationInput(
            [new Uri("https://app.example.com/callback")],
            [],
            null,
            ["openid"]),
        brandingDisplayName,
        brandingTagline);

    /// <summary>
    /// The registration event's envelope must ride the registration's own transaction — outbox
    /// enrolled before the writes, event published into it, flushed to subscribers only after the
    /// commit — so a crash between the commit and the publish can no longer leave the client
    /// permanently without its branding row.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_PublishesTheRegistrationEventThroughTheEnrolledOutbox()
    {
        OrganizationClientRegistrationResult result = await _sut.RegisterAsync(
            _organizationId,
            ApplicationInput(brandingDisplayName: "Acme Portal", brandingTagline: "Sign in to Acme"),
            _actorId,
            ipAddress: "203.0.113.9");

        Received.InOrder(() =>
        {
            _outbox.Enroll(_dbContext);
            // AsTask() consumes the ValueTask (CA2012); InOrder only records the call.
            _outbox.PublishAsync(Arg.Any<ClientRegisteredEvent>()).AsTask();
            _outbox.FlushOutgoingMessagesAsync();
        });
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientRegisteredEvent>(e =>
            e.ClientId == result.Client.ClientId
            && e.OrganizationId == _organizationId
            && e.ClientName == "My App"
            && e.Kind == OrganizationClientKind.Application
            && e.ActorId == _actorId
            && e.BrandingDisplayName == "Acme Portal"
            && e.BrandingTagline == "Sign in to Acme"
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task RegisterAsync_ForAServiceAccount_PublishesTheServiceAccountKind()
    {
        RegisterClientInput input = new(
            RegisteredClientKind.ServiceAccount,
            "Reporting Bot",
            new ClientConfigurationInput([], [], null, []));

        await _sut.RegisterAsync(_organizationId, input, _actorId, ipAddress: null);

        await _outbox.Received(1).PublishAsync(Arg.Is<ClientRegisteredEvent>(e =>
            e.Kind == OrganizationClientKind.ServiceAccount
            && e.BrandingDisplayName == null
            && e.IpAddress == null));
    }

    [Fact]
    public async Task RegisterAsync_WhenTheSaveLosesTheUniqueRace_ThrowsTaken_AndPublishesNothing()
    {
        PostgresException violation = new(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        _registeredClients.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new DbUpdateException(
                "An error occurred while saving the entity changes.", violation));

        Func<Task> act = () => _sut.RegisterAsync(
            _organizationId, ApplicationInput(), _actorId, ipAddress: null);

        BusinessRuleException thrown = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        thrown.Code.Should().Be("Identity.ClientIdTaken");
        await _outbox.DidNotReceiveWithAnyArgs().PublishAsync(default(ClientRegisteredEvent)!);
        await _outbox.DidNotReceive().FlushOutgoingMessagesAsync();
    }
}
