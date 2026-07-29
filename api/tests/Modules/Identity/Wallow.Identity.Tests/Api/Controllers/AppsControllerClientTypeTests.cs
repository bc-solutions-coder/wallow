using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Caller-supplied client type on developer app registration (bead Wallow-pu6a.6.5, closing
/// finding F8 of the SDK review: "AppsController honours a caller-supplied ClientType: public").
///
/// <para><c>RegisterAppRequest.ClientType</c> is forwarded straight to
/// <c>IDeveloperAppService.RegisterClientAsync</c>, which registers a secret-less OpenIddict
/// public client whenever the string equals "public". A public client authenticates on client id
/// alone, so any caller holding <c>apikeys.create</c> can mint a client that anyone who learns
/// the id can impersonate — the same class of hole that got the seeded public dev client deleted
/// in bead Wallow-pu6a.1.1 (see <c>Wallow.Architecture.Tests.PublicSeedClientRemovalTests</c>).</para>
///
/// <para>The remedy asserted here is rejection rather than silent coercion, matching how this
/// action already handles every other unacceptable input (bad scopes, non-HTTPS redirect URIs):
/// the caller learns the platform does not issue public clients instead of receiving a
/// confidential one it did not ask for. The last test is the invariant that matters even if the
/// surface later changes shape — no request may reach the service with a public client type.</para>
/// </summary>
public class AppsControllerClientTypeTests
{
    private const string ValidClientName = "app-test";
    private static readonly string[] _validScopes = ["inquiries.read"];

    private readonly IDeveloperAppService _developerAppService;
    private readonly AppsController _controller;

    public AppsControllerClientTypeTests()
    {
        _developerAppService = Substitute.For<IDeveloperAppService>();
        _developerAppService.RegisterClientAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyCollection<string>?>(),
                Arg.Any<IReadOnlyCollection<string>?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new DeveloperAppRegistrationResult("client-id", "client-secret", "reg-token"));

        _controller = new AppsController(_developerAppService);

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task Register_WithPublicClientType_ReturnsValidationProblem()
    {
        RegisterAppRequest request = new(ValidClientName, _validScopes, ClientTypes.Public);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>(
                "a caller asking for a public client is asking for a client that authenticates on " +
                "its id alone. The action already answers bad scopes and bad redirect URIs with a " +
                "validation problem; an unacceptable client type belongs in the same category");
    }

    [Fact]
    public async Task Register_WithPublicClientType_DoesNotRegisterAnything()
    {
        RegisterAppRequest request = new(ValidClientName, _validScopes, ClientTypes.Public);

        await _controller.Register(request, CancellationToken.None);

        await _developerAppService.DidNotReceive().RegisterClientAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("PUBLIC")]
    [InlineData("Public")]
    [InlineData("pUbLiC")]
    public async Task Register_WithPublicClientTypeInAnyCasing_DoesNotRegisterAnything(string clientType)
    {
        RegisterAppRequest request = new(ValidClientName, _validScopes, clientType);

        await _controller.Register(request, CancellationToken.None);

        await _developerAppService.DidNotReceive().RegisterClientAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_WithConfidentialClientType_RegistersAConfidentialClient()
    {
        RegisterAppRequest request = new(ValidClientName, _validScopes, ClientTypes.Confidential);

        ActionResult<AppRegistrationResponse> result = await _controller.Register(request, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);

        await _developerAppService.Received(1).RegisterClientAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Is<string?>(clientType => !string.Equals(clientType, ClientTypes.Public, StringComparison.OrdinalIgnoreCase)),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<IReadOnlyCollection<string>?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }
}
