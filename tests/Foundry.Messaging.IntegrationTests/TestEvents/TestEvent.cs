// ReSharper disable UnusedAutoPropertyAccessor.Global
using Foundry.Shared.Contracts;

namespace Foundry.Messaging.IntegrationTests.TestEvents;

public sealed record TestEvent : IntegrationEvent
{
    public required string Message { get; init; }
    public required int Counter { get; init; }
}

public sealed record TestEventThatFails : IntegrationEvent
{
    public required string Message { get; init; }
    public required int FailAfterAttempts { get; init; }
}

public sealed record TestEventThatFailsImmediately : IntegrationEvent
{
    public required string Message { get; init; }
}
