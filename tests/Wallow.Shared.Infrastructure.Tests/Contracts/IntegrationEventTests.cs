using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Shared.Infrastructure.Tests.Contracts;

public class IntegrationEventTests
{
    [Fact]
    public void IntegrationEvent_DefaultConstructor_GeneratesNewEventId()
    {
        ConcreteIntegrationEvent evt1 = new();
        ConcreteIntegrationEvent evt2 = new();

        evt1.EventId.Should().NotBe(Guid.Empty);
        evt2.EventId.Should().NotBe(Guid.Empty);
        evt1.EventId.Should().NotBe(evt2.EventId);
    }

    [Fact]
    public void IntegrationEvent_DefaultConstructor_SetsOccurredAtToUtcNow()
    {
        DateTime before = DateTime.UtcNow;
        ConcreteIntegrationEvent evt = new();
        DateTime after = DateTime.UtcNow;

        evt.OccurredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void IntegrationEvent_ImplementsIIntegrationEvent()
    {
        ConcreteIntegrationEvent evt = new();

        evt.Should().BeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void UserRegisteredEvent_WithAllProperties_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        UserRegisteredEvent evt = new()
        {
            UserId = userId,
            TenantId = tenantId,
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "+1-555-0100"
        };

        evt.UserId.Should().Be(userId);
        evt.TenantId.Should().Be(tenantId);
        evt.Email.Should().Be("john@example.com");
        evt.FirstName.Should().Be("John");
        evt.LastName.Should().Be("Doe");
        evt.PhoneNumber.Should().Be("+1-555-0100");
    }

    [Fact]
    public void UserRegisteredEvent_PhoneNumber_IsOptional()
    {
        UserRegisteredEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Jane",
            LastName = "Smith"
        };

        evt.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void RealtimeEnvelope_Create_SetsAllProperties()
    {
        object payload = new { Data = "test" };

        RealtimeEnvelope envelope = RealtimeEnvelope.Create("billing", "InvoicePaid", payload, "correlation-123");

        envelope.Module.Should().Be("billing");
        envelope.Type.Should().Be("InvoicePaid");
        envelope.Payload.Should().Be(payload);
        envelope.CorrelationId.Should().Be("correlation-123");
    }

    [Fact]
    public void RealtimeEnvelope_Create_SetsTimestampToUtcNow()
    {
        DateTime before = DateTime.UtcNow;
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("mod", "type", new object());
        DateTime after = DateTime.UtcNow;

        envelope.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RealtimeEnvelope_Create_WithoutCorrelationId_IsNull()
    {
        RealtimeEnvelope envelope = RealtimeEnvelope.Create("mod", "type", new object());

        envelope.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void RealtimeEnvelope_IsRecord_SupportsValueEquality()
    {
        DateTime ts = DateTime.UtcNow;
        object payload = "same";

        RealtimeEnvelope a = new("type", "mod", payload, ts, "corr");
        RealtimeEnvelope b = new("type", "mod", payload, ts, "corr");

        a.Should().Be(b);
    }

    [Fact]
    public void UserPresence_WithAllProperties_HasCorrectValues()
    {
        List<string> connections = ["conn-1", "conn-2"];
        List<string> pages = ["/dashboard", "/settings"];

        UserPresence presence = new("user-123", "John Doe", connections, pages);

        presence.UserId.Should().Be("user-123");
        presence.DisplayName.Should().Be("John Doe");
        presence.ConnectionIds.Should().BeEquivalentTo(connections);
        presence.CurrentPages.Should().BeEquivalentTo(pages);
    }

    [Fact]
    public void UserPresence_DisplayName_IsOptional()
    {
        UserPresence presence = new("user-123", null, [], []);

        presence.DisplayName.Should().BeNull();
    }

    [Fact]
    public void UserInfo_Constructor_SetsAllProperties()
    {
        Guid id = Guid.NewGuid();

        UserInfo info = new(id, "user@example.com", "John", "Doe", true);

        info.Id.Should().Be(id);
        info.Email.Should().Be("user@example.com");
        info.FirstName.Should().Be("John");
        info.LastName.Should().Be("Doe");
        info.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UserInfo_OptionalNames_CanBeNull()
    {
        UserInfo info = new(Guid.NewGuid(), "user@example.com", null, null, false);

        info.FirstName.Should().BeNull();
        info.LastName.Should().BeNull();
        info.IsActive.Should().BeFalse();
    }

    private sealed record ConcreteIntegrationEvent : IntegrationEvent;
}
