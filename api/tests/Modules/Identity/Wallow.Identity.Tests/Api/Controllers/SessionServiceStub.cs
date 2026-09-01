using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// The <see cref="ISessionService"/> substitute the authorization controller tests share:
/// creating a session answers with a real <see cref="ActiveSession"/> (so the minted sid is a
/// genuine ledger row id), and the ledger starts empty — a cookie sid counts as dead until a
/// test arranges its row as live.
/// </summary>
internal static class SessionServiceStub
{
    public static ISessionService Create()
    {
        ISessionService sessionService = Substitute.For<ISessionService>();
        sessionService
            .CreateSessionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => ActiveSession.Create(
                call.ArgAt<Guid>(0), call.ArgAt<Guid>(1), TimeSpan.FromHours(24), TimeProvider.System));
        sessionService
            .GetActiveSessionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return sessionService;
    }
}
