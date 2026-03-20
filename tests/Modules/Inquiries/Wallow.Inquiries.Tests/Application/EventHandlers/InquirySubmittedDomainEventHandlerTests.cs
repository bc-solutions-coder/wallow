using Wallow.Inquiries.Application.EventHandlers;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Contracts.Inquiries.Events;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Wallow.Inquiries.Tests.Application.EventHandlers;

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

        Inquiry inquiry = Inquiry.Create("Alice", "alice@example.com", "555-0100", "Acme Corp", null, "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquirySubmittedDomainEvent domainEvent = new(
            inquiry.Id.Value,
            inquiry.Name,
            inquiry.Email,
            inquiry.Phone,
            inquiry.Company,
            inquiry.SubmitterId,
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
            e.Phone == domainEvent.Phone &&
            e.ProjectType == domainEvent.ProjectType &&
            e.Message == domainEvent.Message &&
            e.AdminEmail == "admin@test.local"));
    }

    [Fact]
    public async Task HandleAsync_RetrievesInquiryFromRepository()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        Inquiry inquiry = Inquiry.Create("Bob", "bob@example.com", "555-0100", null, null, "Mobile App", "$5k", "6 months", "We need a mobile app.", "2.2.2.2", TimeProvider.System);
        InquiryId inquiryId = inquiry.Id;
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquirySubmittedDomainEvent domainEvent = new(
            inquiryId.Value,
            inquiry.Name,
            inquiry.Email,
            inquiry.Phone,
            inquiry.Company,
            inquiry.SubmitterId,
            inquiry.ProjectType,
            inquiry.BudgetRange,
            inquiry.Timeline,
            inquiry.Message);

        await InquirySubmittedDomainEventHandler.HandleAsync(domainEvent, repository, _configuration, bus, CancellationToken.None);

        await repository.Received(1).GetByIdAsync(Arg.Is<InquiryId>(id => id.Value == inquiryId.Value), Arg.Any<CancellationToken>());
    }
}
