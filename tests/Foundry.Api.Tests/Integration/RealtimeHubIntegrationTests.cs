using Foundry.Shared.Contracts.Realtime;
using Foundry.Tests.Common.Factories;
using Foundry.Tests.Common.Helpers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Api.Tests.Integration;

[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class RealtimeHubIntegrationTests
{
    private readonly FoundryApiFactory _factory;

    public RealtimeHubIntegrationTests(FoundryApiFactory factory)
    {
        _factory = factory;
        _ = _factory.Server;
    }

    [Fact]
    public async Task AuthenticatedClient_CanConnect()
    {
        await using HubConnection connection = CreateHubConnection("user-1");

        await connection.StartAsync();

        connection.State.Should().Be(HubConnectionState.Connected);

        await connection.StopAsync();
    }

    [Fact]
    public async Task UnauthenticatedClient_IsRejected()
    {
        await using HubConnection connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/realtime", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        // SignalR with LongPolling may throw HttpRequestException or fail to connect
        // depending on timing - check either for exception OR failed connection state
        try
        {
            await connection.StartAsync();
            // If no exception, connection should not be in Connected state
            connection.State.Should().NotBe(HubConnectionState.Connected,
                "unauthenticated clients should not be able to connect");
        }
        catch (HttpRequestException)
        {
            // Expected - server rejected the connection
        }
        catch (InvalidOperationException)
        {
            // Also acceptable - connection was rejected
        }
    }

    [Fact]
    public async Task Client_ReceivesNotification()
    {
        const string userId = "user-notif";
        await using HubConnection connection = CreateHubConnection(userId);
        TaskCompletionSource<RealtimeEnvelope> tcs = new TaskCompletionSource<RealtimeEnvelope>();

        connection.On<RealtimeEnvelope>("ReceiveNotifications", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync();
        await Task.Delay(500); // Allow LongPolling cycle to establish

        IRealtimeDispatcher dispatcher = _factory.Services.GetRequiredService<IRealtimeDispatcher>();
        await dispatcher.SendToUserAsync(userId, RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { TaskId = 42 }));

        RealtimeEnvelope received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        received.Module.Should().Be("Notifications");
        received.Type.Should().Be("TaskAssigned");

        await connection.StopAsync();
    }

    [Fact]
    public async Task Client_JoinsGroup_ReceivesGroupMessages()
    {
        await using HubConnection connection = CreateHubConnection("user-group");
        TaskCompletionSource<RealtimeEnvelope> tcs = new TaskCompletionSource<RealtimeEnvelope>();

        connection.On<RealtimeEnvelope>("ReceiveBilling", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", "tenant:test-id");
        await Task.Delay(500); // Allow LongPolling cycle to establish

        IRealtimeDispatcher dispatcher = _factory.Services.GetRequiredService<IRealtimeDispatcher>();
        await dispatcher.SendToGroupAsync("tenant:test-id", RealtimeEnvelope.Create("Billing", "InvoiceCreated", new { TaskId = 7 }));

        RealtimeEnvelope received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        received.Module.Should().Be("Billing");
        received.Type.Should().Be("InvoiceCreated");

        await connection.StopAsync();
    }

    [Fact]
    public async Task Client_UpdatesPageContext_OthersNotified()
    {
        await using HubConnection conn1 = CreateHubConnection("user-page-1");
        await using HubConnection conn2 = CreateHubConnection("user-page-2");

        TaskCompletionSource<RealtimeEnvelope> tcs1 = new TaskCompletionSource<RealtimeEnvelope>();
        TaskCompletionSource<RealtimeEnvelope> tcs2 = new TaskCompletionSource<RealtimeEnvelope>();

        conn1.On<RealtimeEnvelope>("ReceivePresence", e =>
        {
            if (e.Type == "PageViewersUpdated")
            {
                tcs1.TrySetResult(e);
            }
        });
        conn2.On<RealtimeEnvelope>("ReceivePresence", e =>
        {
            if (e.Type == "PageViewersUpdated")
            {
                tcs2.TrySetResult(e);
            }
        });

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(500); // Allow LongPolling cycle to establish

        await conn1.InvokeAsync("UpdatePageContext", "/dashboard");
        await conn2.InvokeAsync("UpdatePageContext", "/dashboard");

        RealtimeEnvelope envelope1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(10));
        RealtimeEnvelope envelope2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(10));

        envelope1.Type.Should().Be("PageViewersUpdated");
        envelope2.Type.Should().Be("PageViewersUpdated");

        await conn1.StopAsync();
        await conn2.StopAsync();
    }

    private HubConnection CreateHubConnection(string userId)
    {
        string token = JwtTokenHelper.GenerateToken(userId);

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/realtime", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();
    }
}
