using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientRegisteredHandlerTests
{
    private static readonly Guid _orgId = Guid.NewGuid();

    private readonly IClientBrandingRepository _repository = Substitute.For<IClientBrandingRepository>();
    private readonly IClientBrandingService _service = Substitute.For<IClientBrandingService>();
    private readonly ClientRegisteredHandler _sut;

    public ClientRegisteredHandlerTests()
    {
        _sut = new ClientRegisteredHandler(
            _repository, _service, TimeProvider.System,
            NullLogger<ClientRegisteredHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_CreatesTheBrandingRow_UnderTheOwningTenant()
    {
        _repository.GetByClientIdAsync("acme-portal", Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        await _sut.HandleAsync(Registered("acme-portal"), CancellationToken.None);

        _repository.Received(1).UseTenant(TenantId.Create(_orgId));
        _repository.Received(1).Add(Arg.Is<ClientBranding>(b =>
            b.ClientId == "acme-portal" && b.DisplayName == "Acme Portal" && b.Tagline == null));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _service.Received(1).InvalidateCache("acme-portal");
    }

    [Fact]
    public async Task HandleAsync_PrefersTheChosenBrandingOverTheClientName()
    {
        _repository.GetByClientIdAsync("acme-portal", Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientRegisteredEvent message = Registered("acme-portal") with
        {
            BrandingDisplayName = "Shiny Portal",
            BrandingTagline = "Sign in to shine",
        };

        await _sut.HandleAsync(message, CancellationToken.None);

        _repository.Received(1).Add(Arg.Is<ClientBranding>(b =>
            b.DisplayName == "Shiny Portal" && b.Tagline == "Sign in to shine"));
    }

    [Fact]
    public async Task HandleAsync_ForAServiceAccount_DoesNothing()
    {
        ClientRegisteredEvent message = Registered("acme-worker") with { Kind = OrganizationClientKind.ServiceAccount };

        await _sut.HandleAsync(message, CancellationToken.None);

        _repository.DidNotReceive().Add(Arg.Any<ClientBranding>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheRowAlreadyExists_LeavesItAlone()
    {
        _repository.GetByClientIdAsync("acme-portal", Arg.Any<CancellationToken>())
            .Returns(ClientBranding.Create("acme-portal", "Already Here"));

        await _sut.HandleAsync(Registered("acme-portal"), CancellationToken.None);

        _repository.DidNotReceive().Add(Arg.Any<ClientBranding>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The reverse of the controller's race: a concurrent branding PUT inserts the row between
    /// this handler's existence check and its save. The row exists with values the caller chose
    /// explicitly — that is the handler's goal state, so it must complete without throwing.
    /// </summary>
    [Fact]
    public async Task HandleAsync_LosingTheRaceToAConcurrentUpsert_TreatsTheRowAsCreated()
    {
        _repository.GetByClientIdAsync("acme-portal", Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        _repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DuplicateClientBrandingException(
                "acme-portal", new InvalidOperationException())));

        Func<Task> act = () => _sut.HandleAsync(Registered("acme-portal"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static ClientRegisteredEvent Registered(string clientId) => new()
    {
        ClientId = clientId,
        OrganizationId = _orgId,
        ClientName = "Acme Portal",
        Kind = OrganizationClientKind.Application,
        ActorId = Guid.NewGuid(),
    };
}
