using Wallow.Api.Middleware;
using Wolverine;

namespace Wallow.Api.Tests.Middleware;

public sealed class WolverineAuthorizationMiddlewareTests
{
    [Fact]
    public void Before_LocalMessage_DoesNotThrow()
    {
        Envelope envelope = new()
        {
            Destination = new Uri("local://queue")
        };

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().NotThrow();
    }

    [Fact]
    public void Before_NullDestination_DoesNotThrow()
    {
        Envelope envelope = new()
        {
            Destination = null
        };

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().NotThrow();
    }

    [Fact]
    public void Before_ExternalMessageWithTenantId_DoesNotThrow()
    {
        Envelope envelope = new()
        {
            Destination = new Uri("rabbitmq://queue/test")
        };
        envelope.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().NotThrow();
    }

    [Fact]
    public void Before_ExternalMessageWithoutTenantId_ThrowsUnauthorizedAccessException()
    {
        Envelope envelope = new()
        {
            Destination = new Uri("rabbitmq://queue/test")
        };

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*X-Tenant-Id*");
    }

    [Fact]
    public void Before_ExternalMessageWithEmptyTenantId_ThrowsUnauthorizedAccessException()
    {
        Envelope envelope = new()
        {
            Destination = new Uri("rabbitmq://queue/test")
        };
        envelope.Headers["X-Tenant-Id"] = "";

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void Before_ExternalMessageWithWhitespaceTenantId_ThrowsUnauthorizedAccessException()
    {
        Envelope envelope = new()
        {
            Destination = new Uri("rabbitmq://queue/test")
        };
        envelope.Headers["X-Tenant-Id"] = "   ";

        Action act = () => WolverineAuthorizationMiddleware.Before(envelope);

        act.Should().Throw<UnauthorizedAccessException>();
    }
}
