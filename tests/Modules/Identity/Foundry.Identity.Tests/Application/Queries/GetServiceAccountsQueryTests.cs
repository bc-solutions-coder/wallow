using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Application.Queries.GetServiceAccounts;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Queries;

public class GetServiceAccountsQueryTests
{
    private static readonly string[] _oneScope = ["scope1"];
    private static readonly string[] _twoScopes = ["scope1", "scope2"];

    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_ReturnsAllServiceAccounts()
    {
        // Arrange
        List<ServiceAccountDto> accounts =
        [
            new(
                ServiceAccountMetadataId.New(),
                "sa-client-1",
                "Account 1",
                null,
                ServiceAccountStatus.Active,
                _oneScope,
                DateTime.UtcNow,
                null),
            new(
                ServiceAccountMetadataId.New(),
                "sa-client-2",
                "Account 2",
                "Description",
                ServiceAccountStatus.Active,
                _twoScopes,
                DateTime.UtcNow,
                DateTime.UtcNow)
        ];

        _serviceAccountService.ListAsync(Arg.Any<CancellationToken>())
            .Returns(accounts);

        GetServiceAccountsQuery query = new GetServiceAccountsQuery();
        GetServiceAccountsHandler handler = new(_serviceAccountService);

        // Act
        Result<IReadOnlyList<ServiceAccountDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("Account 1");
        result.Value[1].Name.Should().Be("Account 2");

        await _serviceAccountService.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyList_ReturnsEmptyResult()
    {
        // Arrange
        _serviceAccountService.ListAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        GetServiceAccountsQuery query = new GetServiceAccountsQuery();
        GetServiceAccountsHandler handler = new(_serviceAccountService);

        // Act
        Result<IReadOnlyList<ServiceAccountDto>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        using CancellationTokenSource cts = new CancellationTokenSource();
        _serviceAccountService.ListAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        GetServiceAccountsQuery query = new GetServiceAccountsQuery();
        GetServiceAccountsHandler handler = new(_serviceAccountService);

        // Act
        await handler.Handle(query, cts.Token);

        // Assert
        await _serviceAccountService.Received(1).ListAsync(cts.Token);
    }
}
