using Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Exceptions;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Tests.Application.Commands.UpdateInquiryStatus;

public class UpdateInquiryStatusAllTransitionsTests
{
    private static Inquiry CreateInquiryAtStatus(InquiryStatus status)
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
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
