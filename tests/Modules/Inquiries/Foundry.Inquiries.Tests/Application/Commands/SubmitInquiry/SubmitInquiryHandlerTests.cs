using Foundry.Inquiries.Application.Commands.SubmitInquiry;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Commands.SubmitInquiry;

public class SubmitInquiryHandlerTests
{
    private static SubmitInquiryCommand BuildCommand() =>
        new("John Doe", "john@example.com", "555-0100", "Acme", null, "Web App", "$10k", "3 months", "We need help.");

    [Fact]
    public async Task HandleAsync_WithValidData_ReturnsSuccessWithDto()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();

        SubmitInquiryCommand command = BuildCommand();

        Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(command.Name);
        result.Value.Email.Should().Be(command.Email);
        await repo.Received(1).AddAsync(Arg.Any<Inquiry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CreatesInquiryWithCorrectFields()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();

        SubmitInquiryCommand command = BuildCommand();

        Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(command, repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectType.Should().Be(command.ProjectType);
        result.Value.BudgetRange.Should().Be(command.BudgetRange);
        result.Value.Timeline.Should().Be(command.Timeline);
    }
}
