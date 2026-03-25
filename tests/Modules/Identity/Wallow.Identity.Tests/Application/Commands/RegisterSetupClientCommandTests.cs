using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Commands.RegisterSetupClient;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

public class RegisterSetupClientCommandTests
{
    private readonly ISetupClientService _setupClientService = Substitute.For<ISetupClientService>();
    private readonly ILogger<RegisterSetupClientHandler> _logger = Substitute.For<ILogger<RegisterSetupClientHandler>>();

    [Fact]
    public async Task Handle_WhenClientAlreadyExists_ReturnsConflictFailure()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(true);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Conflict.Error");

        await _setupClientService.DidNotReceive()
            .CreateConfidentialClientAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenClientDoesNotExist_CreatesClientAndReturnsSecret()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        _setupClientService
            .CreateConfidentialClientAsync(command.ClientId, Arg.Any<string>(), command.RedirectUris, Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<string>(1));

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientSecret.Should().NotBeNullOrWhiteSpace();
        // The generated secret should be a base64-encoded 32-byte value (44 chars with padding)
        result.Value.ClientSecret.Length.Should().Be(44);

        await _setupClientService.Received(1)
            .CreateConfidentialClientAsync(command.ClientId, Arg.Any<string>(), command.RedirectUris, Arg.Any<CancellationToken>());
    }
}
