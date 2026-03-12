using Foundry.Inquiries.Application.EventHandlers;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Contracts.Inquiries.Events;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Foundry.Inquiries.Tests.Application.EventHandlers;

public class InquirySubmittedDomainEventHandlerTests
{
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Inquiries:AdminEmail"] = "admin@test.local"
        })
        .Build();

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        Inquiry inquiry = Inquiry.Create("Alice", "alice@example.com", "Acme Corp", "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquirySubmittedDomainEvent domainEvent = new(
            inquiry.Id.Value,
            inquiry.Name,
            inquiry.Email,
            inquiry.Company,
            inquiry.ProjectType,
            inquiry.BudgetRange,
            inquiry.Timeline,
            inquiry.Message);

        await InquirySubmittedDomainEventHandler.HandleAsync(domainEvent, repository, _configuration, bus, CancellationToken.None);

        await bus.Received(1).PublishAsync(Arg.Is<InquirySubmittedEvent>(e =>
            e.InquiryId == domainEvent.InquiryId &&
            e.Name == domainEvent.Name &&
            e.Email == domainEvent.Email &&
            e.Company == domainEvent.Company &&
            e.Subject == domainEvent.ProjectType &&
            e.Message == domainEvent.Message &&
            e.AdminEmail == "admin@test.local"));
    }

    [Fact]
    public async Task HandleAsync_RetrievesInquiryFromRepository()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        Inquiry inquiry = Inquiry.Create("Bob", "bob@example.com", null, "Mobile App", "$5k", "6 months", "We need a mobile app.", "2.2.2.2", TimeProvider.System);
        InquiryId inquiryId = inquiry.Id;
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquirySubmittedDomainEvent domainEvent = new(
            inquiryId.Value,
            inquiry.Name,
            inquiry.Email,
            inquiry.Company,
            inquiry.ProjectType,
            inquiry.BudgetRange,
            inquiry.Timeline,
            inquiry.Message);

        await InquirySubmittedDomainEventHandler.HandleAsync(domainEvent, repository, _configuration, bus, CancellationToken.None);

        await repository.Received(1).GetByIdAsync(Arg.Is<InquiryId>(id => id.Value == inquiryId.Value), Arg.Any<CancellationToken>());
    }
}
