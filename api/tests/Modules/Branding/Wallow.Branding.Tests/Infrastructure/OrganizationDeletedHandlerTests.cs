using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Tests.Infrastructure;

/// <summary>
/// When Identity announces an organization's deletion, every branding row the tenant owned goes
/// with it — the rows, the logo objects behind them and the cached copies. The handler must
/// address the deleted organization's tenant explicitly, because the envelope restores the
/// publisher's tenant, which is the deleting actor's organization.
/// </summary>
public sealed class OrganizationDeletedHandlerTests
{
    private readonly IClientBrandingRepository _repository = Substitute.For<IClientBrandingRepository>();
    private readonly IClientBrandingService _service = Substitute.For<IClientBrandingService>();
    private readonly IStorageProvider _storage = Substitute.For<IStorageProvider>();
    private readonly OrganizationDeletedHandler _sut;
    private readonly Guid _orgId = Guid.NewGuid();

    public OrganizationDeletedHandlerTests()
    {
        _sut = new OrganizationDeletedHandler(
            _repository, _service, _storage, NullLogger<OrganizationDeletedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_AddressesTheDeletedOrganizationsTenant_NotThePublishers()
    {
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        Received.InOrder(() =>
        {
            _repository.UseTenant(TenantId.Create(_orgId));
            _repository.ListAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsync_RemovesEveryRow_ItsLogo_AndTheCachedCopies()
    {
        ClientBranding withLogo = ClientBranding.Create("app-one", "App One", logoStorageKey: "branding/app-one/logo.png");
        ClientBranding withoutLogo = ClientBranding.Create("app-two", "App Two");
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([withLogo, withoutLogo]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        await _storage.Received(1).DeleteAsync("branding/app-one/logo.png", Arg.Any<CancellationToken>());
        _repository.Received(1).Remove(withLogo);
        _repository.Received(1).Remove(withoutLogo);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _service.Received(1).InvalidateCache("app-one");
        _service.Received(1).InvalidateCache("app-two");
    }

    [Fact]
    public async Task HandleAsync_CommitsTheRowRemovals_BeforeDeletingLogoObjects()
    {
        // A storage failure after the save merely orphans a logo object; the reverse order
        // could leave a live row whose image is already gone.
        ClientBranding withLogo = ClientBranding.Create("app-one", "App One", logoStorageKey: "branding/app-one/logo.png");
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([withLogo]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        Received.InOrder(() =>
        {
            _repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _storage.DeleteAsync("branding/app-one/logo.png", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task HandleAsync_ForATenantWithNoBranding_DoesNothing()
    {
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        _repository.DidNotReceive().Remove(Arg.Any<ClientBranding>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _service.DidNotReceive().InvalidateCache(Arg.Any<string>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private OrganizationDeletedEvent Deleted() => new()
    {
        OrganizationId = _orgId,
        TenantId = _orgId,
        OrganizationName = "Contoso",
        ActorId = Guid.NewGuid(),
        RecipientEmails = []
    };
}
