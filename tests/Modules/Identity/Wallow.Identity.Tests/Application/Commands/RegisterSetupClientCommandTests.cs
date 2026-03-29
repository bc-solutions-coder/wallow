using Microsoft.Extensions.Logging;
#pragma warning disable IDE0005
using NSubstitute.ExceptionExtensions;
#pragma warning restore IDE0005
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

    [Fact]
    public async Task Handle_WhenClientAlreadyExists_IncludesClientIdInErrorMessage()
    {
        RegisterSetupClientCommand command = new("my-unique-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(true);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.Error.Message.Should().Contain("my-unique-client");
    }

    [Fact]
    public async Task Handle_WhenClientDoesNotExist_GeneratesUniqueSecretPerCall()
    {
        _setupClientService
            .ClientExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        List<string> capturedSecrets = [];
        _setupClientService
            .CreateConfidentialClientAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedSecrets.Add(callInfo.ArgAt<string>(1));
                return callInfo.ArgAt<string>(1);
            });

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        RegisterSetupClientCommand command1 = new("client-1", ["https://localhost/callback"]);
        RegisterSetupClientCommand command2 = new("client-2", ["https://localhost/callback"]);

        await handler.Handle(command1, CancellationToken.None);
        await handler.Handle(command2, CancellationToken.None);

        capturedSecrets.Should().HaveCount(2);
        capturedSecrets[0].Should().NotBe(capturedSecrets[1]);
    }

    [Fact]
    public async Task Handle_WhenClientDoesNotExist_PassesRedirectUrisToService()
    {
        List<string> redirectUris = ["https://app.example.com/callback", "https://app.example.com/silent-renew"];
        RegisterSetupClientCommand command = new("setup-client", redirectUris);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        await handler.Handle(command, CancellationToken.None);

        await _setupClientService.Received(1)
            .CreateConfidentialClientAsync(
                "setup-client",
                Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(uris => uris.SequenceEqual(redirectUris)),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCreateClientThrows_PropagatesException()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        _setupClientService
            .CreateConfidentialClientAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("OpenIddict failure"));

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("OpenIddict failure");
    }

    [Fact]
    public async Task Handle_WhenClientDoesNotExist_ReturnsBase64EncodedSecret()
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

        // Verify the secret is valid base64
        Action decodeAction = () => Convert.FromBase64String(result.Value.ClientSecret);
        decodeAction.Should().NotThrow();

        byte[] decoded = Convert.FromBase64String(result.Value.ClientSecret);
        decoded.Should().HaveCount(32);
    }

    [Fact]
    public async Task Handle_WhenClientExistsCheckThrows_PropagatesException()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("Database unavailable"));

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database unavailable");
    }

    [Fact]
    public async Task Handle_WhenClientDoesNotExist_WithEmptyRedirectUris_CreatesClient()
    {
        RegisterSetupClientCommand command = new("setup-client", []);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientSecret.Should().NotBeNullOrWhiteSpace();

        await _setupClientService.Received(1)
            .CreateConfidentialClientAsync(
                "setup-client",
                Arg.Any<string>(),
                Arg.Is<IReadOnlyList<string>>(uris => uris.Count == 0),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenClientAlreadyExists_DoesNotCheckOrGenerateSecret()
    {
        RegisterSetupClientCommand command = new("existing-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(true);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        await _setupClientService.DidNotReceive()
            .CreateConfidentialClientAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenClientAlreadyExists_ReturnsConflictErrorCode()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        _setupClientService
            .ClientExistsAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(true);

        RegisterSetupClientHandler handler = new(_setupClientService, _logger);

        Result<RegisterSetupClientResult> result = await handler.Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be("Conflict.Error");
        result.Error.Message.Should().Contain("setup-client");
        result.Error.Message.Should().Contain("already exists");
    }
}
