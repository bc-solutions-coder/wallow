using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Storage;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientDeletedHandlerTests
{
    private readonly IClientBrandingRepository _repository = Substitute.For<IClientBrandingRepository>();
    private readonly IClientBrandingService _service = Substitute.For<IClientBrandingService>();
    private readonly IStorageProvider _storage = Substitute.For<IStorageProvider>();
    private readonly ClientDeletedHandler _sut;

    public ClientDeletedHandlerTests()
    {
        _sut = new ClientDeletedHandler(_repository, _service, _storage, NullLogger<ClientDeletedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_RemovesTheBranding_ItsLogo_AndTheCachedCopy()
    {
        ClientBranding branding = ClientBranding.Create("app-one", "App One", logoStorageKey: "branding/app-one/logo.png");
        _repository.GetByClientIdAsync("app-one", Arg.Any<CancellationToken>()).Returns(branding);

        await _sut.HandleAsync(Deleted("app-one"), CancellationToken.None);

        await _storage.Received(1).DeleteAsync("branding/app-one/logo.png", Arg.Any<CancellationToken>());
        _repository.Received(1).Remove(branding);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _service.Received(1).InvalidateCache("app-one");
    }

    [Fact]
    public async Task HandleAsync_WithoutALogo_TouchesNoStorage()
    {
        ClientBranding branding = ClientBranding.Create("app-one", "App One");
        _repository.GetByClientIdAsync("app-one", Arg.Any<CancellationToken>()).Returns(branding);

        await _sut.HandleAsync(Deleted("app-one"), CancellationToken.None);

        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _repository.Received(1).Remove(branding);
    }

    [Fact]
    public async Task HandleAsync_ForAClientThatNeverHadBranding_DoesNothing()
    {
        _repository.GetByClientIdAsync("app-one", Arg.Any<CancellationToken>()).Returns((ClientBranding?)null);

        await _sut.HandleAsync(Deleted("app-one"), CancellationToken.None);

        _repository.DidNotReceive().Remove(Arg.Any<ClientBranding>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _service.DidNotReceive().InvalidateCache(Arg.Any<string>());
    }

    private static ClientDeletedEvent Deleted(string clientId) => new()
    {
        ClientId = clientId,
        OrganizationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid()
    };
}
