using Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Exceptions;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Commands.UpdateInquiryStatus;

public class UpdateInquiryStatusAllTransitionsTests
{
    private static Inquiry CreateInquiryAtStatus(InquiryStatus status)
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();

        if (status >= InquiryStatus.Reviewed)
        {
            inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
            inquiry.ClearDomainEvents();
        }
        if (status >= InquiryStatus.Contacted)
        {
            inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
            inquiry.ClearDomainEvents();
        }
        if (status >= InquiryStatus.Closed)
        {
            inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);
            inquiry.ClearDomainEvents();
        }

        return inquiry;
    }

    [Fact]
    public async Task HandleAsync_NewToReviewed_Succeeds()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.New);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Reviewed);
        Result<InquiryDto> result = await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Reviewed");
    }

    [Fact]
    public async Task HandleAsync_ReviewedToContacted_Succeeds()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.Reviewed);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Contacted);
        Result<InquiryDto> result = await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Contacted");
    }

    [Fact]
    public async Task HandleAsync_ContactedToClosed_Succeeds()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.Contacted);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Closed);
        Result<InquiryDto> result = await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task HandleAsync_NewToContacted_ThrowsInvalidTransition()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.New);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Contacted);

        Func<Task> act = () => UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidInquiryStatusTransitionException>();
    }

    [Fact]
    public async Task HandleAsync_NewToClosed_ThrowsInvalidTransition()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.New);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Closed);

        Func<Task> act = () => UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidInquiryStatusTransitionException>();
    }

    [Fact]
    public async Task HandleAsync_ReviewedToClosed_ThrowsInvalidTransition()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.Reviewed);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Closed);

        Func<Task> act = () => UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidInquiryStatusTransitionException>();
    }

    [Fact]
    public async Task HandleAsync_ReviewedToNew_ThrowsInvalidTransition()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.Reviewed);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.New);

        Func<Task> act = () => UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidInquiryStatusTransitionException>();
    }

    [Fact]
    public async Task HandleAsync_SameStatus_ThrowsInvalidTransition()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateInquiryAtStatus(InquiryStatus.New);
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.New);

        Func<Task> act = () => UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidInquiryStatusTransitionException>();
    }
}
