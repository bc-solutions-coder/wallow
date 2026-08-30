using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wallow.Branding.Api.Contracts.Requests;
using Wallow.Branding.Api.Controllers;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Branding.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Branding.Tests.Api;

public sealed class OrganizationClientBrandingControllerTests
{
    private static readonly Guid _orgId = Guid.NewGuid();
    private static readonly Guid _userId = Guid.NewGuid();
    private const string ClientId = "acme-portal";

    private readonly IClientBrandingRepository _repository = Substitute.For<IClientBrandingRepository>();
    private readonly IClientBrandingService _brandingService = Substitute.For<IClientBrandingService>();
    private readonly IStorageProvider _storageProvider = Substitute.For<IStorageProvider>();
    private readonly IOrganizationClientDirectory _directory = Substitute.For<IOrganizationClientDirectory>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly OrganizationClientBrandingController _sut;

    public OrganizationClientBrandingControllerTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(_orgId));
        _directory.FindAsync(_orgId, ClientId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationClientInfo(ClientId, _orgId, OrganizationClientKind.Application));

        _sut = new OrganizationClientBrandingController(
            _repository,
            _brandingService,
            _storageProvider,
            _directory,
            _tenantContext,
            _messageBus,
            Options.Create(new ForkBrandingOptions()),
            TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", _userId.ToString())], "test")),
                },
            },
        };
    }

    private static UpsertClientBrandingRequest Request(
        string displayName = "Acme Portal", string? tagline = null, string? themeJson = null) =>
        new(displayName, tagline, themeJson);

    private static FormFile FormFile(byte[] content, string fileName, string contentType)
    {
        MemoryStream stream = new(content);
        return new FormFile(stream, 0, content.Length, "logo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static byte[] PngBytes(int length = 64)
    {
        byte[] bytes = new byte[length];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        return bytes;
    }

    // -- addressing ------------------------------------------------------------------------------

    [Fact]
    public async Task GetBranding_ForAForeignOrganization_Returns404()
    {
        Guid foreignOrg = Guid.NewGuid();

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(foreignOrg, ClientId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
        await _directory.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBranding_ForAForeignOrganization_WithAManagingMembership_IsAnswered()
    {
        Guid foreignOrg = Guid.NewGuid();
        _directory.CanManageClientsAsync(foreignOrg, _userId, Arg.Any<CancellationToken>()).Returns(true);
        _directory.FindAsync(foreignOrg, ClientId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationClientInfo(ClientId, foreignOrg, OrganizationClientKind.Application));
        _brandingService.GetBrandingAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientBrandingDto(ClientId, "Acme Portal", null, null, null));

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(foreignOrg, ClientId, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBranding_ForAForeignOrganization_AsGlobalAdmin_IsAnswered()
    {
        Guid foreignOrg = Guid.NewGuid();
        _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", _userId.ToString()),
                new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"),
            ],
            "test"));
        _directory.FindAsync(foreignOrg, ClientId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationClientInfo(ClientId, foreignOrg, OrganizationClientKind.Application));
        _brandingService.GetBrandingAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientBrandingDto(ClientId, "Acme Portal", null, null, null));

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(foreignOrg, ClientId, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBranding_ForAClientTheOrganizationDoesNotOwn_Returns404()
    {
        _directory.FindAsync(_orgId, "someone-elses", Arg.Any<CancellationToken>())
            .Returns((OrganizationClientInfo?)null);

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(_orgId, "someone-elses", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBranding_ForAServiceAccount_Returns404()
    {
        _directory.FindAsync(_orgId, "acme-worker", Arg.Any<CancellationToken>())
            .Returns(new OrganizationClientInfo("acme-worker", _orgId, OrganizationClientKind.ServiceAccount));

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(_orgId, "acme-worker", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBranding_ReturnsTheBranding()
    {
        _brandingService.GetBrandingAsync(ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientBrandingDto(ClientId, "Acme Portal", "Tag", null, null));

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding(_orgId, ClientId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ClientBrandingDto>().Which.DisplayName.Should().Be("Acme Portal");
    }

    // -- upsert validation -----------------------------------------------------------------------

    [Fact]
    public async Task UpsertBranding_ForAForeignOrganization_Returns404()
    {
        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            Guid.NewGuid(), ClientId, Request(), null, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpsertBranding_WithoutADisplayName_Returns400()
    {
        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(displayName: "  "), null, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Theory]
    [InlineData("Wallow")]
    [InlineData("wallow")]
    [InlineData("  WALLOW  ")]
    public async Task UpsertBranding_WithTheForkAppName_Returns400(string displayName)
    {
        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(displayName: displayName), null, CancellationToken.None);

        ValidationProblemDetails problem = result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("DisplayName");
        _repository.DidNotReceive().Add(Arg.Any<ClientBranding>());
    }

    [Fact]
    public async Task UpsertBranding_WithAnOversizedLogo_Returns400()
    {
        FormFile logo = FormFile(PngBytes(2 * 1024 * 1024 + 1), "logo.png", "image/png");

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(), logo, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WithADisallowedContentType_Returns400()
    {
        FormFile logo = FormFile(PngBytes(), "logo.gif", "image/gif");

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(), logo, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhoseBytesContradictTheContentType_Returns400()
    {
        byte[] jpegBytes = new byte[64];
        jpegBytes[0] = 0xFF;
        jpegBytes[1] = 0xD8;
        jpegBytes[2] = 0xFF;
        FormFile logo = FormFile(jpegBytes, "logo.png", "image/png");

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(), logo, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey("logo");
    }

    [Theory]
    [InlineData("""{"light":{"background":"#ffffff"}}""")] // key outside the curated pair
    [InlineData("""{"sepia":{"primary":"#ffffff"}}""")] // unknown mode
    [InlineData("""{"light":{"primary":"0.5rem"}}""")] // not a color
    [InlineData("""{"light":"#ffffff"}""")] // mode must be an object
    [InlineData("not json")]
    public async Task UpsertBranding_WithAThemeOutsideTheCuratedShape_Returns400(string themeJson)
    {
        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(themeJson: themeJson), null, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey("ThemeJson");
    }

    // -- upsert behavior -------------------------------------------------------------------------

    [Fact]
    public async Task UpsertBranding_UpdatesTheExistingRow_AndPublishes()
    {
        ClientBranding existing = ClientBranding.Create(ClientId, "Old Name", "Old tag");
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns(existing);
        const string theme = """{"light":{"primary":"oklch(0.6 0.2 260)","primaryForeground":"#ffffff"},"dark":{"primary":"#1e3a8a"}}""";

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(
            _orgId, ClientId, Request(displayName: "New Name", tagline: "New tag", themeJson: theme), null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        existing.DisplayName.Should().Be("New Name");
        existing.Tagline.Should().Be("New tag");
        existing.ThemeJson.Should().Be(theme);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _brandingService.Received(1).InvalidateCache(ClientId);
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<ClientBrandingUpdatedEvent>(e =>
                e.ClientId == ClientId
                && e.OrganizationId == _orgId
                && e.ActorId == _userId
                && e.DisplayName == "New Name"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task UpsertBranding_WithoutARow_CreatesOne_UnderTheOwningTenant()
    {
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns((ClientBranding?)null);

        await _sut.UpsertBranding(_orgId, ClientId, Request(), null, CancellationToken.None);

        _repository.Received(1).UseTenant(TenantId.Create(_orgId));
        _repository.Received(1).Add(Arg.Is<ClientBranding>(b => b.ClientId == ClientId && b.DisplayName == "Acme Portal"));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WithANewLogo_ReplacesTheStoredObject()
    {
        ClientBranding existing = ClientBranding.Create(ClientId, "Old Name", logoStorageKey: "client-logos/acme-portal/old.png");
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns(existing);
        FormFile logo = FormFile(PngBytes(), "logo.png", "image/png");

        await _sut.UpsertBranding(_orgId, ClientId, Request(), logo, CancellationToken.None);

        await _storageProvider.Received(1).DeleteAsync("client-logos/acme-portal/old.png", Arg.Any<CancellationToken>());
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(k => k.StartsWith("client-logos/acme-portal/", StringComparison.Ordinal)),
            "image/png",
            Arg.Any<CancellationToken>());
        existing.LogoStorageKey.Should().StartWith("client-logos/acme-portal/").And.NotBe("client-logos/acme-portal/old.png");
    }

    // -- delete logo -----------------------------------------------------------------------------

    [Fact]
    public async Task DeleteLogo_ClearsTheLogo_AndPublishes()
    {
        ClientBranding existing = ClientBranding.Create(ClientId, "Acme Portal", logoStorageKey: "client-logos/acme-portal/logo.png");
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns(existing);

        IActionResult result = await _sut.DeleteLogo(_orgId, ClientId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        existing.LogoStorageKey.Should().BeNull();
        await _storageProvider.Received(1).DeleteAsync("client-logos/acme-portal/logo.png", Arg.Any<CancellationToken>());
        _brandingService.Received(1).InvalidateCache(ClientId);
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<ClientBrandingUpdatedEvent>(e => e.ClientId == ClientId && e.DisplayName == "Acme Portal"),
            Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task DeleteLogo_WhenThereIsNoLogo_IsIdempotent()
    {
        ClientBranding existing = ClientBranding.Create(ClientId, "Acme Portal");
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns(existing);

        IActionResult result = await _sut.DeleteLogo(_orgId, ClientId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _storageProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _messageBus.DidNotReceive().PublishAsync(
            Arg.Any<ClientBrandingUpdatedEvent>(), Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task DeleteLogo_WithoutABrandingRow_Returns404()
    {
        _repository.GetByClientIdAsync(ClientId, Arg.Any<CancellationToken>()).Returns((ClientBranding?)null);

        IActionResult result = await _sut.DeleteLogo(_orgId, ClientId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteLogo_ForAForeignOrganization_Returns404()
    {
        IActionResult result = await _sut.DeleteLogo(Guid.NewGuid(), ClientId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
