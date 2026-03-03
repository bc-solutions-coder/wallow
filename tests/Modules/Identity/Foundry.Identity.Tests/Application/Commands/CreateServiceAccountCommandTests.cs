using Foundry.Identity.Application.Commands.CreateServiceAccount;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Commands;

public class CreateServiceAccountCommandTests
{
    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithValidCommand_CallsServiceWithCorrectParameters()
    {
        // Arrange
        string[] scopes = new[] { "invoices.read", "invoices.write" };
        CreateServiceAccountCommand command = new("Test Service", "Description", scopes);

        ServiceAccountCreatedResult expectedResult = new(
            ServiceAccountMetadataId.New(),
            "sa-test-client",
            "secret123",
            "https://auth.example.com/token",
            scopes.ToList());

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        CreateServiceAccountHandler handler = new(_serviceAccountService);

        // Act
        Result<ServiceAccountCreatedResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);

        await _serviceAccountService.Received(1).CreateAsync(
            Arg.Is<CreateServiceAccountRequest>(r =>
                r.Name == "Test Service" &&
                r.Description == "Description" &&
                r.Scopes.SequenceEqual(scopes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MapsCommandToRequest()
    {
        // Arrange
        string[] scopes = new[] { "scope1", "scope2" };
        CreateServiceAccountCommand command = new("Name", "Desc", scopes);

        _serviceAccountService
            .CreateAsync(Arg.Any<CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceAccountCreatedResult(
                ServiceAccountMetadataId.New(),
                "client",
                "secret",
                "endpoint",
                scopes.ToList()));

        CreateServiceAccountHandler handler = new(_serviceAccountService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await _serviceAccountService.Received().CreateAsync(
            Arg.Is<CreateServiceAccountRequest>(r =>
                r.Name == command.Name &&
                r.Description == command.Description),
            Arg.Any<CancellationToken>());
    }
}
