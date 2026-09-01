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
    private readonly ClientActorContext _actor;
    private readonly IdentityDbContext _dbContext;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IRegisteredClientRepository _registeredClients;
    private readonly IOrganizationAdminEmailResolver _adminEmails;
    private readonly IDbContextOutbox _outbox;
    private readonly OrganizationClientService _sut;

    public OrganizationClientServiceTests()
    {
        _actor = new ClientActorContext(_actorId, "203.0.113.9");

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
        _adminEmails = Substitute.For<IOrganizationAdminEmailResolver>();
        _adminEmails.ResolveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
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
            _adminEmails,
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

    /// <summary>Seeds a client the fixture organization owns and teaches the repository to find it.</summary>
    private RegisteredClient OwnedClient(string clientId = "acme-app-my-app")
    {
        RegisteredClient record = RegisteredClient.Create(
            clientId, _organizationId, "My App", RegisteredClientKind.Application, _actorId, TimeProvider.System);
        _registeredClients.GetByClientIdAsync(clientId, Arg.Any<CancellationToken>()).Returns(record);
        return record;
    }

    /// <summary>Gives the client an OpenIddict application, for the paths that require one.</summary>
    private void ApplicationExistsFor(string clientId) =>
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new object());

    private void AssertPublishedThroughTheEnrolledOutbox<TEvent>() where TEvent : class
    {
        Received.InOrder(() =>
        {
            _outbox.Enroll(_dbContext);
            // AsTask() consumes the ValueTask (CA2012); InOrder only records the call.
            _outbox.PublishAsync(Arg.Any<TEvent>()).AsTask();
            _outbox.FlushOutgoingMessagesAsync();
        });
    }

    private async Task AssertPublishedNothingAsync<TEvent>() where TEvent : class
    {
        await _outbox.DidNotReceiveWithAnyArgs().PublishAsync(default(TEvent)!);
        await _outbox.DidNotReceive().FlushOutgoingMessagesAsync();
    }

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
            _actor);

        AssertPublishedThroughTheEnrolledOutbox<ClientRegisteredEvent>();
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

        await _sut.RegisterAsync(_organizationId, input, new ClientActorContext(_actorId, null));

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

        Func<Task> act = () => _sut.RegisterAsync(_organizationId, ApplicationInput(), _actor);

        BusinessRuleException thrown = (await act.Should().ThrowAsync<BusinessRuleException>()).Which;
        thrown.Code.Should().Be("Identity.ClientIdTaken");
        await _outbox.DidNotReceiveWithAnyArgs().PublishAsync(default(ClientRegisteredEvent)!);
        await _outbox.DidNotReceive().FlushOutgoingMessagesAsync();
    }

    /// <summary>
    /// Every lifecycle event below rides its operation's own transaction the same way the
    /// registration event does: published into the enrolled outbox before the commit, flushed
    /// after it, so a crash between commit and publish can no longer drop the audit row or
    /// notification the event carries.
    /// </summary>
    [Fact]
    public async Task RotateSecretAsync_PublishesTheRotationEventThroughTheEnrolledOutbox()
    {
        RegisteredClient record = OwnedClient();
        ApplicationExistsFor(record.ClientId);

        OrganizationClientRegistrationResult? result = await _sut.RotateSecretAsync(
            _organizationId, record.ClientId, revokeActiveTokens: true, _actor);

        result.Should().NotBeNull();
        AssertPublishedThroughTheEnrolledOutbox<ClientSecretRotatedEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientSecretRotatedEvent>(e =>
            e.ClientId == record.ClientId
            && e.OrganizationId == _organizationId
            && e.ActorId == _actorId
            && e.ActiveTokensRevoked
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task RotateSecretAsync_ForAClientTheOrganizationDoesNotOwn_PublishesNothing()
    {
        OrganizationClientRegistrationResult? result = await _sut.RotateSecretAsync(
            _organizationId, "someone-elses-client", revokeActiveTokens: false, _actor);

        result.Should().BeNull();
        await AssertPublishedNothingAsync<ClientSecretRotatedEvent>();
    }

    [Fact]
    public async Task SuspendAsync_PublishesTheSuspensionEventThroughTheEnrolledOutbox()
    {
        RegisteredClient record = OwnedClient();
        ApplicationExistsFor(record.ClientId);

        OrganizationClientDto? result = await _sut.SuspendAsync(_organizationId, record.ClientId, _actor);

        result.Should().NotBeNull();
        AssertPublishedThroughTheEnrolledOutbox<ClientSuspendedEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientSuspendedEvent>(e =>
            e.ClientId == record.ClientId
            && e.OrganizationId == _organizationId
            && e.ActorId == _actorId
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task ReinstateAsync_PublishesTheReinstatementEventThroughTheEnrolledOutbox()
    {
        RegisteredClient record = OwnedClient();
        record.Suspend();
        ApplicationExistsFor(record.ClientId);

        OrganizationClientDto? result = await _sut.ReinstateAsync(_organizationId, record.ClientId, _actor);

        result.Should().NotBeNull();
        AssertPublishedThroughTheEnrolledOutbox<ClientReinstatedEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientReinstatedEvent>(e =>
            e.ClientId == record.ClientId
            && e.OrganizationId == _organizationId
            && e.ActorId == _actorId
            && e.IpAddress == "203.0.113.9"));
    }

    /// <summary>
    /// The platform-suspension event additionally carries what its email notification needs — the
    /// admin recipients, the organization's name and the operator's reason — all resolved before
    /// the transaction so nothing is published unless the commit succeeds.
    /// </summary>
    [Fact]
    public async Task SuspendByPlatformAsync_PublishesTheEventWithRecipientsAndOrganizationName()
    {
        RegisteredClient record = OwnedClient();
        ApplicationExistsFor(record.ClientId);
        _adminEmails.ResolveAsync(_organizationId, Arg.Any<CancellationToken>())
            .Returns(["owner@acme.test"]);

        OrganizationClientDto? result = await _sut.SuspendByPlatformAsync(
            _organizationId, record.ClientId, "Terms violation", _actor);

        result.Should().NotBeNull();
        AssertPublishedThroughTheEnrolledOutbox<ClientSuspendedByPlatformEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientSuspendedByPlatformEvent>(e =>
            e.ClientId == record.ClientId
            && e.ClientName == "My App"
            && e.OrganizationId == _organizationId
            && e.OrganizationName == "Acme"
            && e.ActorId == _actorId
            && e.Reason == "Terms violation"
            && e.RecipientEmails.Contains("owner@acme.test")
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task ReinstateByPlatformAsync_PublishesTheReinstatementEventThroughTheEnrolledOutbox()
    {
        RegisteredClient record = OwnedClient();
        record.SuspendByPlatform("Terms violation", _actorId, TimeProvider.System);
        ApplicationExistsFor(record.ClientId);

        OrganizationClientDto? result = await _sut.ReinstateByPlatformAsync(
            _organizationId, record.ClientId, _actor);

        result.Should().NotBeNull();
        AssertPublishedThroughTheEnrolledOutbox<ClientReinstatedByPlatformEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientReinstatedByPlatformEvent>(e =>
            e.ClientId == record.ClientId
            && e.OrganizationId == _organizationId
            && e.ActorId == _actorId
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task DeleteAsync_PublishesTheDeletionEventThroughTheEnrolledOutbox()
    {
        RegisteredClient record = OwnedClient();

        bool deleted = await _sut.DeleteAsync(_organizationId, record.ClientId, _actor);

        deleted.Should().BeTrue();
        AssertPublishedThroughTheEnrolledOutbox<ClientDeletedEvent>();
        await _outbox.Received(1).PublishAsync(Arg.Is<ClientDeletedEvent>(e =>
            e.ClientId == record.ClientId
            && e.OrganizationId == _organizationId
            && e.ActorId == _actorId
            && e.IpAddress == "203.0.113.9"));
    }

    [Fact]
    public async Task DeleteAsync_ForAClientTheOrganizationDoesNotOwn_PublishesNothing()
    {
        RegisteredClient foreign = RegisteredClient.Create(
            "other-org-app", Guid.NewGuid(), "Their App", RegisteredClientKind.Application,
            _actorId, TimeProvider.System);
        _registeredClients.GetByClientIdAsync(foreign.ClientId, Arg.Any<CancellationToken>())
            .Returns(foreign);

        bool deleted = await _sut.DeleteAsync(_organizationId, foreign.ClientId, _actor);

        deleted.Should().BeFalse();
        await AssertPublishedNothingAsync<ClientDeletedEvent>();
    }
}
