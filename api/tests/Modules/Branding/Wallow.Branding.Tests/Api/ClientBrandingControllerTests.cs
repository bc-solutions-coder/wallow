using Microsoft.AspNetCore.Mvc;
using Wallow.Branding.Api.Controllers;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Tests.Api;

public sealed class ClientBrandingControllerTests
{
    private readonly IClientBrandingService _brandingService = Substitute.For<IClientBrandingService>();
    private readonly ClientBrandingController _sut;

    public ClientBrandingControllerTests()
    {
        _sut = new ClientBrandingController(_brandingService);
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
