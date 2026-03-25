using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Tests.Services;

public class RealtimeEnvelopeTests
{
    [Fact]
    public void Create_WithDefaults_ShouldHaveNullAudienceFields()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { Id = 1 });

        envelope.RequiredPermission.Should().BeNull();
        envelope.RequiredRole.Should().BeNull();
        envelope.TargetUserId.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAudienceFieldsSupplied_ShouldRoundTripCorrectly()
    {
        RealtimeEnvelope envelope = new(
            Type: "TaskAssigned",
            Module: "Notifications",
            Payload: new { Id = 1 },
            Timestamp: DateTime.UtcNow,
            CorrelationId: "corr-123",
            RequiredPermission: "tasks.view",
            RequiredRole: "Admin",
            TargetUserId: "user-42");

        envelope.RequiredPermission.Should().Be("tasks.view");
        envelope.RequiredRole.Should().Be("Admin");
        envelope.TargetUserId.Should().Be("user-42");
    }

    [Fact]
    public void WithExpression_OnRequiredPermission_ShouldProduceIndependentCopy()
    {
        RealtimeEnvelope original = RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { Id = 1 });

        RealtimeEnvelope copy = original with { RequiredPermission = "tasks.edit" };

        copy.RequiredPermission.Should().Be("tasks.edit");
        original.RequiredPermission.Should().BeNull();
        copy.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithExpression_OnRequiredRole_ShouldProduceIndependentCopy()
    {
        RealtimeEnvelope original = RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { Id = 1 });

        RealtimeEnvelope copy = original with { RequiredRole = "Manager" };

        copy.RequiredRole.Should().Be("Manager");
        original.RequiredRole.Should().BeNull();
        copy.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithExpression_OnTargetUserId_ShouldProduceIndependentCopy()
    {
        RealtimeEnvelope original = RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { Id = 1 });

        RealtimeEnvelope copy = original with { TargetUserId = "user-99" };

        copy.TargetUserId.Should().Be("user-99");
        original.TargetUserId.Should().BeNull();
        copy.Should().NotBeSameAs(original);
    }
}
