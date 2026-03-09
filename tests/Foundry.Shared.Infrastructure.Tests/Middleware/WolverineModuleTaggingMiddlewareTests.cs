using System.Diagnostics;
using Foundry.Billing.Application.Commands;
using Foundry.Identity.Infrastructure.Consumers;
using Foundry.Shared.Infrastructure.Core.Middleware;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Tests.Middleware
{
    public class WolverineModuleTaggingMiddlewareTests
    {
        [Fact]
        public void Before_WithApplicationNamespace_SetsModuleTag()
        {
            using Activity activity = new("test");
            activity.Start();
            Activity.Current = activity;

            Envelope envelope = new Envelope(new FakeBillingApplicationMessage());

            WolverineModuleTaggingMiddleware.Before(envelope);

            activity.GetTagItem("foundry.module").Should().Be("Billing");
        }

        [Fact]
        public void Before_WithInfrastructureNamespace_SetsModuleTag()
        {
            using Activity activity = new("test");
            activity.Start();
            Activity.Current = activity;

            Envelope envelope = new Envelope(new FakeIdentityInfrastructureMessage());

            WolverineModuleTaggingMiddleware.Before(envelope);

            activity.GetTagItem("foundry.module").Should().Be("Identity");
        }

        [Fact]
        public void Before_WithNonFoundryNamespace_DoesNotSetTag()
        {
            using Activity activity = new("test");
            activity.Start();
            Activity.Current = activity;

            Envelope envelope = new Envelope("a plain string message");

            WolverineModuleTaggingMiddleware.Before(envelope);

            activity.GetTagItem("foundry.module").Should().BeNull();
        }

        [Fact]
        public void Before_WithNoActivity_DoesNotThrow()
        {
            Activity.Current = null;
            Envelope envelope = new Envelope(new FakeBillingApplicationMessage());

            Action act = () => WolverineModuleTaggingMiddleware.Before(envelope);

            act.Should().NotThrow();
        }

        [Fact]
        public void Before_WithNullMessage_DoesNotThrow()
        {
            using Activity activity = new("test");
            activity.Start();
            Activity.Current = activity;

            Envelope envelope = new Envelope { Message = null };

            Action act = () => WolverineModuleTaggingMiddleware.Before(envelope);

            act.Should().NotThrow();
            activity.GetTagItem("foundry.module").Should().BeNull();
        }
    }
}

namespace Foundry.Billing.Application.Commands
{
    public sealed class FakeBillingApplicationMessage;
}

namespace Foundry.Identity.Infrastructure.Consumers
{
    public sealed class FakeIdentityInfrastructureMessage;
}
