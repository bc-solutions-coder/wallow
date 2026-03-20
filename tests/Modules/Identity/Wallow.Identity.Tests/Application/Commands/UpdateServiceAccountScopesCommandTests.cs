using Wallow.Identity.Application.Commands.UpdateServiceAccountScopes;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

public class UpdateServiceAccountScopesCommandTests
{
    private static readonly string[] _twoScopes = ["invoices.read", "invoices.write"];
    private static readonly string[] _oneScope = ["scope1"];

    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithValidCommand_CallsServiceWithCorrectParameters()
    {
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        UpdateServiceAccountScopesCommand command = new(accountId, _twoScopes);
        UpdateServiceAccountScopesHandler handler = new(_serviceAccountService);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _serviceAccountService.Received(1).UpdateScopesAsync(
            accountId,
            Arg.Is<IEnumerable<string>>(s => s.SequenceEqual(_twoScopes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccessResult()
    {
        UpdateServiceAccountScopesCommand command = new(
            ServiceAccountMetadataId.New(), _oneScope);
        UpdateServiceAccountScopesHandler handler = new(_serviceAccountService);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        UpdateServiceAccountScopesCommand command = new(
            ServiceAccountMetadataId.New(), _oneScope);
        UpdateServiceAccountScopesHandler handler = new(_serviceAccountService);
        using CancellationTokenSource cts = new CancellationTokenSource();

        await handler.Handle(command, cts.Token);

        await _serviceAccountService.Received(1).UpdateScopesAsync(
            Arg.Any<ServiceAccountMetadataId>(),
            Arg.Any<IEnumerable<string>>(),
            cts.Token);
    }
}
