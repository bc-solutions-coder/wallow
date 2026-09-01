using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Branding;
using Wallow.Shared.Contracts.Branding.Events;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ClientBrandingUpdatedHandlerTests
{
    private const string ClientId = "acme-portal";

    private readonly IOpenIddictApplicationManager _applicationManager =
        Substitute.For<IOpenIddictApplicationManager>();
    private readonly IClientBrandingProvider _brandingProvider = Substitute.For<IClientBrandingProvider>();
    private readonly ClientBrandingUpdatedHandler _sut;

    public ClientBrandingUpdatedHandlerTests()
    {
        _sut = new ClientBrandingUpdatedHandler(
            _applicationManager,
            _brandingProvider,
            NullLogger<ClientBrandingUpdatedHandler>.Instance);
    }

    private static ClientBrandingUpdatedEvent Event(string displayName = "Payload Name") => new()
    {
        ClientId = ClientId,
        OrganizationId = Guid.NewGuid(),
        ActorId = Guid.NewGuid(),
        DisplayName = displayName,
    };

    /// <summary>
    /// The event is a trigger, not a payload: two rapid writes delivered out of order must still
    /// leave the OpenIddict application at the row's CURRENT name, so the handler applies what
    /// the Branding read contract says now — not what the (possibly older) event carried.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AppliesTheCurrentDisplayName_NotTheEventPayload()
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(application);
        await _applicationManager.PopulateAsync(
            Arg.Do<OpenIddictApplicationDescriptor>(d => d.DisplayName = "Older Name"),
            application,
            Arg.Any<CancellationToken>());
        _brandingProvider.FindCurrentDisplayNameAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns("Current Name");

        await _sut.HandleAsync(Event(displayName: "Stale Payload Name"), CancellationToken.None);

        await _applicationManager.Received(1).UpdateAsync(
            application,
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.DisplayName == "Current Name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyCurrent_DoesNotUpdate()
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(application);
        await _applicationManager.PopulateAsync(
            Arg.Do<OpenIddictApplicationDescriptor>(d => d.DisplayName = "Current Name"),
            application,
            Arg.Any<CancellationToken>());
        _brandingProvider.FindCurrentDisplayNameAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns("Current Name");

        await _sut.HandleAsync(Event(), CancellationToken.None);

        await _applicationManager.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default(OpenIddictApplicationDescriptor)!, default);
    }

    /// <summary>
    /// A redelivered event can arrive after the client (and its branding row) were deleted; the
    /// deletion cascade owns the OpenIddict cleanup, so the sync backs off instead of writing.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenTheBrandingRowIsGone_DoesNotUpdate()
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(application);
        _brandingProvider.FindCurrentDisplayNameAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        await _sut.HandleAsync(Event(), CancellationToken.None);

        await _applicationManager.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default(OpenIddictApplicationDescriptor)!, default);
    }

    [Fact]
    public async Task HandleAsync_WhenTheApplicationIsUnknown_DoesNothing()
    {
        _applicationManager.FindByClientIdAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        await _sut.HandleAsync(Event(), CancellationToken.None);

        await _brandingProvider.DidNotReceiveWithAnyArgs().FindCurrentDisplayNameAsync(default!, default);
        await _applicationManager.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default(OpenIddictApplicationDescriptor)!, default);
    }
}
