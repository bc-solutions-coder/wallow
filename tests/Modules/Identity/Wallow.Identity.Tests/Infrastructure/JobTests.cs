#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Jobs;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class JobTests
{
    [Fact]
    public async Task TokenPruningJob_ExecuteAsync_PrunesTokensAndAuthorizations()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authManager = Substitute.For<IOpenIddictAuthorizationManager>();
        OpenIddictTokenPruningJob job = new(tokenManager, authManager, NullLogger<OpenIddictTokenPruningJob>.Instance);

        await job.ExecuteAsync();

        await tokenManager.Received(1).PruneAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await authManager.Received(1).PruneAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenPruningJob_ExecuteAsync_WhenThrows_Rethrows()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authManager = Substitute.For<IOpenIddictAuthorizationManager>();
        tokenManager.PruneAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("fail"));
        OpenIddictTokenPruningJob job = new(tokenManager, authManager, NullLogger<OpenIddictTokenPruningJob>.Instance);

        Func<Task> act = () => job.ExecuteAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("fail");
    }

    [Fact]
    public async Task InvitationPruningJob_ExecuteAsync_CallsCleanup()
    {
        IInvitationService svc = Substitute.For<IInvitationService>();
        ExpiredInvitationPruningJob job = new(svc, NullLogger<ExpiredInvitationPruningJob>.Instance);

        await job.ExecuteAsync();

        await svc.Received(1).CleanupExpiredAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvitationPruningJob_ExecuteAsync_WhenThrows_Rethrows()
    {
        IInvitationService svc = Substitute.For<IInvitationService>();
        svc.CleanupExpiredAsync(Arg.Any<CancellationToken>())
            .Returns(x => throw new InvalidOperationException("fail"));
        ExpiredInvitationPruningJob job = new(svc, NullLogger<ExpiredInvitationPruningJob>.Instance);

        Func<Task> act = () => job.ExecuteAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("fail");
    }

    [Fact]
    public async Task TokenPruningJob_ExecuteAsync_WhenAuthorizationManagerThrows_Rethrows()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authManager = Substitute.For<IOpenIddictAuthorizationManager>();
        authManager.PruneAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<long>(_ => throw new InvalidOperationException("auth fail"));
        OpenIddictTokenPruningJob job = new(tokenManager, authManager, NullLogger<OpenIddictTokenPruningJob>.Instance);

        Func<Task> act = () => job.ExecuteAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("auth fail");
    }

    [Fact]
    public async Task TokenPruningJob_ExecuteAsync_UsesCurrentUtcThreshold()
    {
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager authManager = Substitute.For<IOpenIddictAuthorizationManager>();
        DateTimeOffset before = DateTimeOffset.UtcNow;
        OpenIddictTokenPruningJob job = new(tokenManager, authManager, NullLogger<OpenIddictTokenPruningJob>.Instance);

        await job.ExecuteAsync();

        DateTimeOffset after = DateTimeOffset.UtcNow;
        await tokenManager.Received(1).PruneAsync(
            Arg.Is<DateTimeOffset>(d => d >= before && d <= after),
            Arg.Any<CancellationToken>());
        await authManager.Received(1).PruneAsync(
            Arg.Is<DateTimeOffset>(d => d >= before && d <= after),
            Arg.Any<CancellationToken>());
    }
}
