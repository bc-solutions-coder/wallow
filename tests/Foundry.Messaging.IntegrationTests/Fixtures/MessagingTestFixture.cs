using Foundry.Tests.Common.Factories;
using Foundry.Messaging.IntegrationTests.Helpers;
using Foundry.Messaging.IntegrationTests.TestHandlers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Foundry.Messaging.IntegrationTests.Fixtures;

public class MessagingTestFixture : FoundryApiFactory
{
    private readonly MessageTracker _messageTracker = new();
    private readonly CrossModuleEventTracker _crossModuleTracker = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMessageTracker>(_messageTracker);
            services.AddSingleton<ICrossModuleEventTracker>(_crossModuleTracker);
            services.AddSingleton<MessageWaiter>();
        });

        // Tell Program.cs to discover test handlers via Wolverine configuration
        builder.UseSetting("Wolverine:TestAssembly", typeof(MessagingTestFixture).Assembly.FullName);

        // Enable RabbitMQ transport for messaging integration tests
        builder.UseSetting("ModuleMessaging:Transport", "RabbitMq");
    }

    public IMessageBus MessageBus => Services.GetRequiredService<IMessageBus>();

    public MessageWaiter MessageWaiter => Services.GetRequiredService<MessageWaiter>();

    public IMessageTracker MessageTracker => _messageTracker;

    public ICrossModuleEventTracker CrossModuleTracker => _crossModuleTracker;
}
