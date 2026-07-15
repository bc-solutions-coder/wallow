using Wolverine;

namespace Wallow.SeederService;

/// <summary>
/// No-op IMessageBus for the seeder service. OrganizationService requires IMessageBus
/// but the seeder never dispatches Wolverine messages.
/// </summary>
internal sealed class NullMessageBus : IMessageBus
{
    public string? TenantId { get; set; }

    // ICommandBus
    public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => Task.CompletedTask;

    public Task InvokeAsync(object message, DeliveryOptions? options, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => Task.CompletedTask;

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support InvokeAsync<T>.");

    public Task<T> InvokeAsync<T>(object message, DeliveryOptions? options, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support InvokeAsync<T>.");

    // IMessageBus
    public Task InvokeForTenantAsync(string tenantId, object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => Task.CompletedTask;

    public Task<T> InvokeForTenantAsync<T>(string tenantId, object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support InvokeForTenantAsync<T>.");

    public IDestinationEndpoint EndpointFor(string endpointName)
        => throw new NotSupportedException("NullMessageBus does not support EndpointFor.");

    public IDestinationEndpoint EndpointFor(Uri uri)
        => throw new NotSupportedException("NullMessageBus does not support EndpointFor.");

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message)
        => [];

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message, DeliveryOptions options)
        => [];

    public ValueTask SendAsync<T>(T message, DeliveryOptions? options = null)
        => ValueTask.CompletedTask;

    public ValueTask PublishAsync<T>(T message, DeliveryOptions? options = null)
        => ValueTask.CompletedTask;

    public ValueTask BroadcastToTopicAsync(string topicName, object message, DeliveryOptions? options = null)
        => ValueTask.CompletedTask;
}
