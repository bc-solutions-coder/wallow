using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Wallow.SeederService;

/// <summary>
/// No-op IMessageBus and IDbContextOutbox for the seeder service. OrganizationService requires
/// both, but the seeder never dispatches Wolverine messages and never deletes an organization —
/// the outbox path is unreachable here, so enrolling and flushing are harmless no-ops.
/// </summary>
internal sealed class NullMessageBus : IMessageBus, IDbContextOutbox
{
    public string? TenantId { get; set; }

    // IDbContextOutbox
    public DbContext? ActiveContext { get; private set; }

    public void Enroll(DbContext dbContext) => ActiveContext = dbContext;

    public Task SaveChangesAndFlushMessagesAsync(CancellationToken token = default)
        => ActiveContext?.SaveChangesAsync(token) ?? Task.CompletedTask;

    public Task SaveChangesAndFlushMessagesAsync(Wolverine.Runtime.MultiFlushMode multiFlushMode, CancellationToken token = default)
        => ActiveContext?.SaveChangesAsync(token) ?? Task.CompletedTask;

    public Task FlushOutgoingMessagesAsync() => Task.CompletedTask;

    // ICommandBus
    public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => Task.CompletedTask;

    public Task InvokeAsync(object message, DeliveryOptions? options, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => Task.CompletedTask;

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support InvokeAsync<T>.");

    public Task<T> InvokeAsync<T>(object message, DeliveryOptions? options, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support InvokeAsync<T>.");

    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(object message, CancellationToken cancellation = default)
        => throw new NotSupportedException("NullMessageBus does not support StreamAsync.");

    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(object message, DeliveryOptions options, CancellationToken cancellation = default)
        => throw new NotSupportedException("NullMessageBus does not support StreamAsync.");

    public Task<TResponse> StreamAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> messages, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support StreamAsync.");

    public Task<TResponse> StreamAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> messages, DeliveryOptions options, CancellationToken cancellation = default, TimeSpan? timeout = null)
        => throw new NotSupportedException("NullMessageBus does not support StreamAsync.");

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
