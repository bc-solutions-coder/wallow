using Foundry.Inquiries.Application.EventHandlers;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Foundry.Inquiries.Tests.Application.EventHandlers;

public class InquiryStatusChangedDomainEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_PublishesIntegrationEventWithCorrectFields()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        Inquiry inquiry = Inquiry.Create("Alice", "alice@example.com", "555-0100", "Acme Corp", null, "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquiryStatusChangedDomainEvent domainEvent = new(
            inquiry.Id.Value,
            InquiryStatus.New.ToString(),
            InquiryStatus.Reviewed.ToString());

        await InquiryStatusChangedDomainEventHandler.HandleAsync(domainEvent, repository, bus, CancellationToken.None);

        await bus.Received(1).PublishAsync(Arg.Is<InquiryStatusChangedEvent>(e =>
            e.InquiryId == domainEvent.InquiryId &&
            e.OldStatus == "New" &&
            e.NewStatus == "Reviewed" &&
            e.SubmitterEmail == "alice@example.com"));
    }

    [Fact]
    public async Task HandleAsync_RetrievesInquiryFromRepository()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        Inquiry inquiry = Inquiry.Create("Bob", "bob@example.com", "555-0100", null, null, "Mobile App", "$5k", "6 months", "We need a mobile app.", "2.2.2.2", TimeProvider.System);
        InquiryId inquiryId = inquiry.Id;
        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        InquiryStatusChangedDomainEvent domainEvent = new(
            inquiryId.Value,
            InquiryStatus.New.ToString(),
            InquiryStatus.Reviewed.ToString());

        await InquiryStatusChangedDomainEventHandler.HandleAsync(domainEvent, repository, bus, CancellationToken.None);

        await repository.Received(1).GetByIdAsync(
            Arg.Is<InquiryId>(id => id.Value == inquiryId.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenInquiryNotFound_StillPublishesWithFallbacks()
    {
        IInquiryRepository repository = Substitute.For<IInquiryRepository>();
        IMessageBus bus = Substitute.For<IMessageBus>();

        repository.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns((Inquiry?)null);

        Guid inquiryId = Guid.NewGuid();
        InquiryStatusChangedDomainEvent domainEvent = new(
            inquiryId,
            InquiryStatus.New.ToString(),
            InquiryStatus.Reviewed.ToString());

        await InquiryStatusChangedDomainEventHandler.HandleAsync(domainEvent, repository, bus, CancellationToken.None);

        await bus.Received(1).PublishAsync(Arg.Is<InquiryStatusChangedEvent>(e =>
            e.InquiryId == inquiryId &&
            e.SubmitterEmail == string.Empty));
    }
}
