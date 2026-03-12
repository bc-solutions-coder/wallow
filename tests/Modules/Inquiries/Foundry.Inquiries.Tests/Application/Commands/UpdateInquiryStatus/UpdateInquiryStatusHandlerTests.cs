using Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Commands.UpdateInquiryStatus;

public class UpdateInquiryStatusHandlerTests
{
    private static Inquiry CreateNewInquiry()
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public async Task HandleAsync_WhenInquiryExists_TransitionsAndReturnsDto()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateNewInquiry();
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Reviewed);

        Result<InquiryDto> result = await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Reviewed");
    }

    [Fact]
    public async Task HandleAsync_WhenInquiryNotFound_ReturnsNotFoundError()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns((Inquiry?)null);

        UpdateInquiryStatusCommand command = new(Guid.NewGuid(), InquiryStatus.Reviewed);

        Result<InquiryDto> result = await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().EndWith(".NotFound");
    }

    [Fact]
    public async Task HandleAsync_CallsUpdateOnRepository()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        Inquiry inquiry = CreateNewInquiry();
        repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        UpdateInquiryStatusCommand command = new(inquiry.Id.Value, InquiryStatus.Reviewed);

        await UpdateInquiryStatusHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        await repo.Received(1).UpdateAsync(Arg.Any<Inquiry>(), Arg.Any<CancellationToken>());
    }
}
