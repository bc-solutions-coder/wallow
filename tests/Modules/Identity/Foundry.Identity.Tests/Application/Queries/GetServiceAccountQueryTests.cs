using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Application.Queries.GetServiceAccount;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Queries;

public class GetServiceAccountQueryTests
{
    private static readonly string[] _oneScope = ["scope1"];

    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithExistingAccount_ReturnsAccount()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        ServiceAccountDto expectedDto = new(
            accountId,
            "sa-client-1",
            "Account 1",
            "Description",
            ServiceAccountStatus.Active,
            _oneScope,
            DateTime.UtcNow,
            null);

        _serviceAccountService.GetAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        GetServiceAccountQuery query = new(accountId);
        GetServiceAccountHandler handler = new(_serviceAccountService);

        Result<ServiceAccountDto?> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedDto);
        await _serviceAccountService.Received(1).GetAsync(accountId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistingAccount_ReturnsNull()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();

        _serviceAccountService.GetAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccountDto?)null);

        GetServiceAccountQuery query = new(accountId);
        GetServiceAccountHandler handler = new(_serviceAccountService);

        Result<ServiceAccountDto?> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        using CancellationTokenSource cts = new();

        _serviceAccountService.GetAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountDto?)null);

        GetServiceAccountQuery query = new(accountId);
        GetServiceAccountHandler handler = new(_serviceAccountService);

        await handler.Handle(query, cts.Token);

        await _serviceAccountService.Received(1).GetAsync(accountId, cts.Token);
    }
}
