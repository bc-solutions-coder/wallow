using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Shared <see cref="ISessionService"/> stub that creates <see cref="ActiveSession"/> objects
/// without persisting them. Active-session queries return an empty list unless a test overrides it.
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
