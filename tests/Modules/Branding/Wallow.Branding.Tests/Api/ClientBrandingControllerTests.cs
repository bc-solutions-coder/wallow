#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Wallow.Branding.Api.Contracts.Requests;
using Wallow.Branding.Api.Controllers;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Storage;

namespace Wallow.Branding.Tests.Api;

public class ClientBrandingControllerGetTests
{
    private readonly IClientBrandingService _brandingService;
    private readonly ClientBrandingController _sut;

    public ClientBrandingControllerGetTests()
    {
        IClientBrandingRepository repository = Substitute.For<IClientBrandingRepository>();
        _brandingService = Substitute.For<IClientBrandingService>();
        IStorageProvider storageProvider = Substitute.For<IStorageProvider>();
        IOpenIddictApplicationManager applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new ClientBrandingController(repository, _brandingService, storageProvider, applicationManager);
    }

    [Fact]
    public async Task GetBranding_WhenBrandingExists_Returns200WithDto()
    {
        ClientBrandingDto dto = new("client-1", "My App", "Tagline", null, null);
        _brandingService.GetBrandingAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(dto);

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding("client-1", CancellationToken.None);

        OkObjectResult okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ClientBrandingDto returnedDto = okResult.Value.Should().BeOfType<ClientBrandingDto>().Subject;
        returnedDto.ClientId.Should().Be("client-1");
        returnedDto.DisplayName.Should().Be("My App");
    }

    [Fact]
    public async Task GetBranding_WhenBrandingNotFound_Returns404()
    {
        _brandingService.GetBrandingAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((ClientBrandingDto?)null);

        ActionResult<ClientBrandingDto> result = await _sut.GetBranding("unknown", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}

public class ClientBrandingControllerUpsertTests
{
    private readonly IClientBrandingRepository _repository;
    private readonly IClientBrandingService _brandingService;
    private readonly IStorageProvider _storageProvider;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ClientBrandingController _sut;

    private const string TestUserId = "user-123";
    private const string TestClientId = "client-1";

    public ClientBrandingControllerUpsertTests()
    {
        _repository = Substitute.For<IClientBrandingRepository>();
        _brandingService = Substitute.For<IClientBrandingService>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new ClientBrandingController(_repository, _brandingService, _storageProvider, _applicationManager);
    }

    [Fact]
    public async Task UpsertBranding_WhenNotClientOwner_Returns403()
    {
        SetupControllerUser(TestUserId);
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenNoUserClaim_Returns403()
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenCreatorUserIdDoesNotMatch_Returns403()
    {
        SetupControllerUser(TestUserId);

        object application = new object();
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(application);

        _applicationManager.PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.Properties["creatorUserId"] = JsonSerializer.SerializeToElement("different-user");
                return ValueTask.CompletedTask;
            });

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenCreatorUserIdPropertyMissing_Returns403()
    {
        SetupControllerUser(TestUserId);

        object application = new object();
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(application);

        _applicationManager.PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenDisplayNameEmpty_ReturnsValidationProblem()
    {
        SetupOwnership();
        UpsertClientBrandingRequest request = new("", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpsertBranding_WhenDisplayNameWhitespace_ReturnsValidationProblem()
    {
        SetupOwnership();
        UpsertClientBrandingRequest request = new("   ", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpsertBranding_WhenInvalidThemeJson_ReturnsValidationProblem()
    {
        SetupOwnership();
        UpsertClientBrandingRequest request = new("My App", null, "not-json");

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasInvalidColorValue_ReturnsValidationProblem()
    {
        SetupOwnership();
        string themeJson = """{"primary": "not-a-color"}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasInvalidNestedColorValue_ReturnsValidationProblem()
    {
        SetupOwnership();
        string themeJson = """{"dark": {"primary": "rgb(255,0,0)"}}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasValidOklchColor_Succeeds()
    {
        SetupOwnership();
        string themeJson = """{"primary": "oklch(0.7 0.15 200)"}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, themeJson);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasValidHexColor_Succeeds()
    {
        SetupOwnership();
        string themeJson = """{"primary": "#ff0000", "background": "#fff"}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, themeJson);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasValidRemValue_Succeeds()
    {
        SetupOwnership();
        string themeJson = """{"border": "0.5rem"}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, themeJson);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenThemeJsonHasNonColorProperty_IgnoresValidation()
    {
        SetupOwnership();
        string themeJson = """{"radius": "anything", "fontFamily": "Inter"}""";
        UpsertClientBrandingRequest request = new("My App", null, themeJson);
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, themeJson);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpsertBranding_WhenCreatingNew_Returns200WithDto()
    {
        SetupOwnership();
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        OkObjectResult okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<ClientBrandingDto>();
        _repository.Received(1).Add(Arg.Any<ClientBranding>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenUpdatingExisting_Returns200AndDoesNotCallAdd()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "Old Name");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        ClientBrandingDto expectedDto = new(TestClientId, "New Name", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("New Name", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        OkObjectResult okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<ClientBrandingDto>();
        _repository.DidNotReceive().Add(Arg.Any<ClientBranding>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenCreatingNew_InvalidatesCacheAfterSave()
    {
        SetupOwnership();
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        _brandingService.Received(1).InvalidateCache(TestClientId);
    }

    [Fact]
    public async Task UpsertBranding_WhenLogoTooLarge_ReturnsValidationProblem()
    {
        SetupOwnership();
        IFormFile logo = CreateFormFile(new byte[3 * 1024 * 1024], "logo.png", "image/png");

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        ValidationProblemDetails details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhenLogoHasDisallowedContentType_ReturnsValidationProblem()
    {
        SetupOwnership();
        IFormFile logo = CreateFormFile(new byte[100], "logo.gif", "image/gif");

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        ValidationProblemDetails details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhenPngMagicBytesMismatch_ReturnsValidationProblem()
    {
        SetupOwnership();
        byte[] fakeContent = new byte[100];
        fakeContent[0] = 0x00; // Not a valid PNG header
        IFormFile logo = CreateFormFile(fakeContent, "logo.png", "image/png");

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        ValidationProblemDetails details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhenJpegMagicBytesMismatch_ReturnsValidationProblem()
    {
        SetupOwnership();
        byte[] fakeContent = new byte[100];
        fakeContent[0] = 0x00; // Not a valid JPEG header
        IFormFile logo = CreateFormFile(fakeContent, "logo.jpg", "image/jpeg");

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        ValidationProblemDetails details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhenWebpRiffHeaderValidButMissingWebpMarker_ReturnsValidationProblem()
    {
        SetupOwnership();
        byte[] content = new byte[100];
        // RIFF header
        content[0] = 0x52; content[1] = 0x49; content[2] = 0x46; content[3] = 0x46;
        // Missing WEBP at offset 8
        content[8] = 0x00; content[9] = 0x00; content[10] = 0x00; content[11] = 0x00;
        IFormFile logo = CreateFormFile(content, "logo.webp", "image/webp");

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        ObjectResult objectResult = result.Result.Should().BeAssignableTo<ObjectResult>().Subject;
        ValidationProblemDetails details = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("logo");
    }

    [Fact]
    public async Task UpsertBranding_WhenValidPngLogo_UploadsToStorageAndReturns200()
    {
        SetupOwnership();
        byte[] pngContent = new byte[100];
        pngContent[0] = 0x89; pngContent[1] = 0x50; pngContent[2] = 0x4E; pngContent[3] = 0x47;
        IFormFile logo = CreateFormFile(pngContent, "logo.png", "image/png");

        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, "https://storage/logo.png", null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(k => k.StartsWith("client-logos/")),
            "image/png",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenValidJpegLogo_UploadsToStorage()
    {
        SetupOwnership();
        byte[] jpegContent = new byte[100];
        jpegContent[0] = 0xFF; jpegContent[1] = 0xD8; jpegContent[2] = 0xFF;
        IFormFile logo = CreateFormFile(jpegContent, "photo.jpg", "image/jpeg");

        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(k => k.StartsWith("client-logos/") && k.EndsWith(".jpg")),
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenValidWebpLogo_UploadsToStorage()
    {
        SetupOwnership();
        byte[] webpContent = new byte[100];
        // RIFF header
        webpContent[0] = 0x52; webpContent[1] = 0x49; webpContent[2] = 0x46; webpContent[3] = 0x46;
        // WEBP marker at offset 8
        byte[] webpMarker = Encoding.ASCII.GetBytes("WEBP");
        webpMarker.CopyTo(webpContent, 8);
        IFormFile logo = CreateFormFile(webpContent, "image.webp", "image/webp");

        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);
        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(k => k.StartsWith("client-logos/") && k.EndsWith(".webp")),
            "image/webp",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenUpdatingExistingWithLogo_DeletesOldLogoFromStorage()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "Old Name", logoStorageKey: "client-logos/old.png");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        byte[] pngContent = new byte[100];
        pngContent[0] = 0x89; pngContent[1] = 0x50; pngContent[2] = 0x4E; pngContent[3] = 0x47;
        IFormFile logo = CreateFormFile(pngContent, "new-logo.png", "image/png");

        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        await _storageProvider.Received(1).DeleteAsync("client-logos/old.png", Arg.Any<CancellationToken>());
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(k => k.StartsWith("client-logos/") && k.EndsWith(".png")),
            "image/png",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenUpdatingExistingWithLogoButNoOldLogo_DoesNotDeleteFromStorage()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "Old Name");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        byte[] pngContent = new byte[100];
        pngContent[0] = 0x89; pngContent[1] = 0x50; pngContent[2] = 0x4E; pngContent[3] = 0x47;
        IFormFile logo = CreateFormFile(pngContent, "new-logo.png", "image/png");

        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        await _sut.UpsertBranding(TestClientId, request, logo, CancellationToken.None);

        await _storageProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertBranding_WhenCreatingNewWithTagline_PassesTaglineToEntity()
    {
        SetupOwnership();
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        ClientBrandingDto expectedDto = new(TestClientId, "My App", "A cool tagline", null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", "A cool tagline", null);

        ActionResult<ClientBrandingDto> result = await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _repository.Received(1).Add(Arg.Is<ClientBranding>(b => b.Tagline == "A cool tagline"));
    }

    [Fact]
    public async Task UpsertBranding_WhenNoLogoProvided_DoesNotUploadToStorage()
    {
        SetupOwnership();
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        ClientBrandingDto expectedDto = new(TestClientId, "My App", null, null, null);
        _brandingService.GetBrandingAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        UpsertClientBrandingRequest request = new("My App", null, null);

        await _sut.UpsertBranding(TestClientId, request, null, CancellationToken.None);

        await _storageProvider.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private void SetupControllerUser(string userId)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId)
        ], "test"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private void SetupOwnership()
    {
        SetupControllerUser(TestUserId);

        object application = new object();
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(application);

        _applicationManager.PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.Properties["creatorUserId"] = JsonSerializer.SerializeToElement(TestUserId);
                return ValueTask.CompletedTask;
            });
    }

    private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        MemoryStream stream = new(content);
        FormFile formFile = new(stream, 0, content.Length, "logo", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return formFile;
    }
}

public class ClientBrandingControllerDeleteTests
{
    private readonly IClientBrandingRepository _repository;
    private readonly IClientBrandingService _brandingService;
    private readonly IStorageProvider _storageProvider;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ClientBrandingController _sut;

    private const string TestUserId = "user-123";
    private const string TestClientId = "client-1";

    public ClientBrandingControllerDeleteTests()
    {
        _repository = Substitute.For<IClientBrandingRepository>();
        _brandingService = Substitute.For<IClientBrandingService>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _sut = new ClientBrandingController(_repository, _brandingService, _storageProvider, _applicationManager);
    }

    [Fact]
    public async Task DeleteBranding_WhenNotClientOwner_Returns403()
    {
        SetupControllerUser(TestUserId);
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        IActionResult result = await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteBranding_WhenNoUserClaim_Returns403()
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        IActionResult result = await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteBranding_WhenBrandingNotFound_Returns404()
    {
        SetupOwnership();
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        IActionResult result = await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteBranding_WhenBrandingExists_Returns204AndRemoves()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "My App");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        IActionResult result = await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _repository.Received(1).Remove(existing);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _brandingService.Received(1).InvalidateCache(TestClientId);
    }

    [Fact]
    public async Task DeleteBranding_WhenBrandingHasLogo_DeletesLogoFromStorage()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "My App", logoStorageKey: "logos/key.png");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        await _storageProvider.Received(1).DeleteAsync("logos/key.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBranding_WhenBrandingHasNoLogo_DoesNotDeleteFromStorage()
    {
        SetupOwnership();
        ClientBranding existing = ClientBranding.Create(TestClientId, "My App");
        _repository.GetByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        await _sut.DeleteBranding(TestClientId, CancellationToken.None);

        await _storageProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private void SetupControllerUser(string userId)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId)
        ], "test"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private void SetupOwnership()
    {
        SetupControllerUser(TestUserId);

        object application = new object();
        _applicationManager.FindByClientIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(application);

        _applicationManager.PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                OpenIddictApplicationDescriptor descriptor = callInfo.ArgAt<OpenIddictApplicationDescriptor>(0);
                descriptor.Properties["creatorUserId"] = JsonSerializer.SerializeToElement(TestUserId);
                return ValueTask.CompletedTask;
            });
    }
}
