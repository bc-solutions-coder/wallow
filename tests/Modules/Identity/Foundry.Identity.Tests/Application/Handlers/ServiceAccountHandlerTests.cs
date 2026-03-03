using Foundry.Identity.Application.Commands.CreateServiceAccount;
using Foundry.Identity.Application.Commands.RevokeServiceAccount;
using Foundry.Identity.Application.Commands.RotateServiceAccountSecret;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;
using NSubstitute.ExceptionExtensions;

namespace Foundry.Identity.Tests.Application.Handlers;

public class CreateServiceAccountHandlerTests
{
    private readonly IServiceAccountService _serviceAccountService;
    private readonly CreateServiceAccountHandler _handler;

    public CreateServiceAccountHandlerTests()
    {
        _serviceAccountService = Substitute.For<IServiceAccountService>();
        _handler = new CreateServiceAccountHandler(_serviceAccountService);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        string[] scopes = ["invoices.read", "invoices.write"];
        CreateServiceAccountCommand command = new("My Service", "A test service", scopes);

        ServiceAccountCreatedResult expectedResult = new(
            ServiceAccountMetadataId.New(),
            "sa-my-service",
            "generated-secret-abc",
            "https://auth.example.com/realms/foundry/protocol/openid-connect/token",
            scopes.ToList());

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        Result<ServiceAccountCreatedResult> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClientId.Should().Be("sa-my-service");
        result.Value.ClientSecret.Should().Be("generated-secret-abc");
        result.Value.Scopes.Should().BeEquivalentTo(scopes);
    }

    [Fact]
    public async Task Handle_MapsCommandFieldsToRequest()
    {
        string[] scopes = ["billing.read"];
        CreateServiceAccountCommand command = new("Billing Reader", "Reads billing data", scopes);

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceAccountCreatedResult(
                ServiceAccountMetadataId.New(), "client", "secret", "endpoint", scopes.ToList()));

        await _handler.Handle(command, CancellationToken.None);

        await _serviceAccountService.Received(1).CreateAsync(
            Arg.Is<CreateServiceAccountRequest>(r =>
                r.Name == "Billing Reader" &&
                r.Description == "Reads billing data" &&
                r.Scopes.SequenceEqual(scopes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateName_PropagatesServiceException()
    {
        CreateServiceAccountCommand command = new("Existing Service", null, ["scope1"]);

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Service account with name 'Existing Service' already exists"));

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_WithNullDescription_PassesNullToService()
    {
        string[] scopes = ["scope1"];
        CreateServiceAccountCommand command = new("No Desc Service", null, scopes);

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceAccountCreatedResult(
                ServiceAccountMetadataId.New(), "client", "secret", "endpoint", scopes.ToList()));

        await _handler.Handle(command, CancellationToken.None);

        await _serviceAccountService.Received(1).CreateAsync(
            Arg.Is<CreateServiceAccountRequest>(r => r.Description == null),
            Arg.Any<CancellationToken>());
    }
}

public class RotateServiceAccountSecretHandlerTests
{
    private readonly IServiceAccountService _serviceAccountService;
    private readonly RotateServiceAccountSecretHandler _handler;

    public RotateServiceAccountSecretHandlerTests()
    {
        _serviceAccountService = Substitute.For<IServiceAccountService>();
        _handler = new RotateServiceAccountSecretHandler(_serviceAccountService);
    }

    [Fact]
    public async Task Handle_WithValidId_ReturnsNewSecret()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RotateServiceAccountSecretCommand command = new(accountId);
        DateTime rotatedAt = DateTime.UtcNow;

        SecretRotatedResult expectedResult = new("new-rotated-secret", rotatedAt);

        _serviceAccountService
            .RotateSecretAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        Result<SecretRotatedResult> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewClientSecret.Should().Be("new-rotated-secret");
        result.Value.RotatedAt.Should().Be(rotatedAt);
    }

    [Fact]
    public async Task Handle_CallsServiceWithCorrectId()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RotateServiceAccountSecretCommand command = new(accountId);

        _serviceAccountService
            .RotateSecretAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(new SecretRotatedResult("secret", DateTime.UtcNow));

        await _handler.Handle(command, CancellationToken.None);

        await _serviceAccountService.Received(1).RotateSecretAsync(accountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentId_PropagatesServiceException()
    {
        ServiceAccountMetadataId nonExistentId = ServiceAccountMetadataId.New();
        RotateServiceAccountSecretCommand command = new(nonExistentId);

        _serviceAccountService
            .RotateSecretAsync(nonExistentId, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Service account not found"));

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}

public class RevokeServiceAccountHandlerTests
{
    private readonly IServiceAccountService _serviceAccountService;
    private readonly RevokeServiceAccountHandler _handler;

    public RevokeServiceAccountHandlerTests()
    {
        _serviceAccountService = Substitute.For<IServiceAccountService>();
        _handler = new RevokeServiceAccountHandler(_serviceAccountService);
    }

    [Fact]
    public async Task Handle_WithValidId_ReturnsSuccess()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RevokeServiceAccountCommand command = new(accountId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _serviceAccountService.Received(1).RevokeAsync(accountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CalledTwice_IsIdempotent()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RevokeServiceAccountCommand command = new(accountId);

        Result result1 = await _handler.Handle(command, CancellationToken.None);
        Result result2 = await _handler.Handle(command, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        await _serviceAccountService.Received(2).RevokeAsync(accountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RevokeServiceAccountCommand command = new(accountId);
        using CancellationTokenSource cts = new();

        await _handler.Handle(command, cts.Token);

        await _serviceAccountService.Received(1).RevokeAsync(accountId, cts.Token);
    }
}
