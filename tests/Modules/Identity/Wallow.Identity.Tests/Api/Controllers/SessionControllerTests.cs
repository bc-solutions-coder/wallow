using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Tests.Api.Controllers;

public class SessionControllerTests
{
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();
    private readonly SessionController _controller;
    private const string TestUserId = "550e8400-e29b-41d4-a716-446655440000";

    public SessionControllerTests()
    {
        _controller = new SessionController(_sessionService);

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, TestUserId)],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    [Fact]
    public async Task ListSessions_ReturnsActiveSessionsForCurrentUser()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ActiveSession> sessions =
        [
            CreateSession(Guid.NewGuid(), now, now, now.AddHours(24)),
            CreateSession(Guid.NewGuid(), now.AddMinutes(-30), now.AddMinutes(-5), now.AddHours(23))
        ];

        _sessionService.GetActiveSessionsAsync(
                Guid.Parse(TestUserId), Arg.Any<CancellationToken>())
            .Returns(sessions);

        IActionResult result = await _controller.ListSessions(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        List<SessionDto> dtos = ok.Value.Should().BeOfType<List<SessionDto>>().Subject;
        dtos.Should().HaveCount(2);
        dtos[0].Id.Should().Be(sessions[0].Id.Value);
        dtos[0].CreatedAt.Should().Be(sessions[0].CreatedAt);
        dtos[0].LastActivityAt.Should().Be(sessions[0].LastActivityAt);
        dtos[0].ExpiresAt.Should().Be(sessions[0].ExpiresAt);
    }

    [Fact]
    public async Task ListSessions_SessionDto_DoesNotContainSessionToken()
    {
        // SessionDto should only have Id, CreatedAt, LastActivityAt, ExpiresAt -- no SessionToken
        System.Reflection.PropertyInfo[] properties = typeof(SessionDto).GetProperties();
        string[] propertyNames = properties.Select(p => p.Name).ToArray();

        propertyNames.Should().NotContain("SessionToken");
        propertyNames.Should().BeEquivalentTo(["Id", "CreatedAt", "LastActivityAt", "ExpiresAt"]);
    }

    [Fact]
    public async Task RevokeSession_ReturnsNoContent()
    {
        Guid sessionId = Guid.NewGuid();

        IActionResult result = await _controller.RevokeSession(sessionId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _sessionService.Received(1)
            .RevokeSessionAsync(sessionId, Guid.Parse(TestUserId), Arg.Any<CancellationToken>());
    }

    private static ActiveSession CreateSession(
        Guid id, DateTimeOffset createdAt, DateTimeOffset lastActivityAt, DateTimeOffset expiresAt)
    {
        // Use reflection to create ActiveSession for test since constructor is private
        ActiveSession session = (ActiveSession)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(ActiveSession));

        typeof(ActiveSession).GetProperty("Id")!
            .SetValue(session, ActiveSessionId.Create(id));
        typeof(ActiveSession).GetProperty("CreatedAt")!
            .SetValue(session, createdAt);
        typeof(ActiveSession).GetProperty("LastActivityAt")!
            .SetValue(session, lastActivityAt);
        typeof(ActiveSession).GetProperty("ExpiresAt")!
            .SetValue(session, expiresAt);
        typeof(ActiveSession).GetProperty("SessionToken")!
            .SetValue(session, "should-not-be-exposed");

        return session;
    }
}
