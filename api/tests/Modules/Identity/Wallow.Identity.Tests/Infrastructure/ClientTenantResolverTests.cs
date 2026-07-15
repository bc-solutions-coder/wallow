using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class ClientTenantResolverTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOrganizationService _organizationService;
    private readonly ClientTenantResolver _sut;

    public ClientTenantResolverTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _organizationService = Substitute.For<IOrganizationService>();
        _sut = new ClientTenantResolver(_applicationManager, _organizationService);
    }

    [Fact]
    public async Task ResolveAsync_WithValidClientAndTenant_ReturnsClientTenantInfo()
    {
        Guid tenantId = Guid.NewGuid();
        string clientId = "test-client";
        object application = new object();

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(application);

        await _applicationManager.PopulateAsync(
            Arg.Do<OpenIddictApplicationDescriptor>(descriptor =>
            {
                descriptor.Properties["tenant_id"] = JsonSerializer.SerializeToElement(tenantId.ToString());
            }),
            application,
            Arg.Any<CancellationToken>());

        _organizationService.GetOrganizationByIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationDto(tenantId, "Acme Corp", null, 5));

        ClientTenantInfo? result = await _sut.ResolveAsync(clientId);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(tenantId);
        result.TenantName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task ResolveAsync_WithUnknownClient_ReturnsNull()
    {
        _applicationManager.FindByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((object?)null);

        ClientTenantInfo? result = await _sut.ResolveAsync("unknown-client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_WithClientWithoutTenant_ReturnsEmptyTenant()
    {
        string clientId = "no-tenant-client";
        object application = new object();

        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(application);

        // PopulateAsync leaves descriptor with no tenant_id property
        await _applicationManager.PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>());

        ClientTenantInfo? result = await _sut.ResolveAsync(clientId);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(Guid.Empty);
        result.TenantName.Should().BeNull();
    }
}
