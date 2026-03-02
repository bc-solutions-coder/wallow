using Foundry.Communications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.Enums;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Results;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Foundry.Communications.Tests.Integration.Sms;

[CollectionDefinition(nameof(SmsCommunicationsTestCollection))]
public class SmsCommunicationsTestCollection : ICollectionFixture<FoundryApiFactory>;

[Collection(nameof(SmsCommunicationsTestCollection))]
[Trait("Category", "Integration")]
public sealed class SmsIntegrationTests : FoundryIntegrationTestBase
{
    public SmsIntegrationTests(FoundryApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SendSms_WithValidCommand_PersistsMessageAndMarksAsSent()
    {
        SendSmsCommand command = new(To: "+15551234567", Body: "Integration test SMS");

        using IServiceScope scope = Factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        Result result = await bus.InvokeAsync<Result>(command);

        result.IsSuccess.Should().BeTrue();

        CommunicationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
        SmsMessage? smsMessage = await dbContext.SmsMessages
            .FirstOrDefaultAsync(m => m.To.Value == "+15551234567");

        smsMessage.Should().NotBeNull();
        smsMessage!.Body.Should().Be("Integration test SMS");
        smsMessage.Status.Should().Be(SmsStatus.Sent);
        smsMessage.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendSms_WithEmptyBody_ReturnsFailure()
    {
        SendSmsCommand command = new(To: "+15551234567", Body: "");

        using IServiceScope scope = Factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        Func<Task> act = async () => await bus.InvokeAsync<Result>(command);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendSms_WithInvalidPhoneNumber_ThrowsException()
    {
        SendSmsCommand command = new(To: "not-a-phone", Body: "Test message");

        using IServiceScope scope = Factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        Func<Task> act = async () => await bus.InvokeAsync<Result>(command);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendSms_MultipleSmsMessages_AllPersistedSuccessfully()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        SendSmsCommand command1 = new(To: "+15559990001", Body: "First SMS");
        SendSmsCommand command2 = new(To: "+15559990002", Body: "Second SMS");

        Result result1 = await bus.InvokeAsync<Result>(command1);
        Result result2 = await bus.InvokeAsync<Result>(command2);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        CommunicationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
        int count = await dbContext.SmsMessages
            .CountAsync(m => m.To.Value == "+15559990001" || m.To.Value == "+15559990002");

        count.Should().Be(2);
    }
}
