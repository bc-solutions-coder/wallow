using Foundry.Inquiries.Application.Commands.SubmitInquiry;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Commands.SubmitInquiry;

public class SubmitInquiryHandlerTests
{
    private static SubmitInquiryCommand BuildCommand(string? honeypot = null) =>
        new("John Doe", "john@example.com", "Acme", "Web App", "$10k", "3 months", "We need help.", "1.2.3.4", honeypot);

    [Fact]
    public async Task HandleAsync_WithValidData_ReturnsSuccessWithDto()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        IRateLimitService rateLimit = Substitute.For<IRateLimitService>();
        rateLimit.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        SubmitInquiryCommand command = BuildCommand();

        Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(command, repo, rateLimit, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(command.Name);
        result.Value.Email.Should().Be(command.Email);
        await repo.Received(1).AddAsync(Arg.Any<Inquiry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithHoneypotFilled_ReturnsFakeSuccess()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        IRateLimitService rateLimit = Substitute.For<IRateLimitService>();

        SubmitInquiryCommand command = BuildCommand(honeypot: "bot-filled-this");

        Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(command, repo, rateLimit, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await repo.DidNotReceive().AddAsync(Arg.Any<Inquiry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRateLimited_ReturnsConflictError()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        IRateLimitService rateLimit = Substitute.For<IRateLimitService>();
        rateLimit.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        SubmitInquiryCommand command = BuildCommand();

        Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(command, repo, rateLimit, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().StartWith("Conflict");
    }

    [Fact]
    public async Task HandleAsync_CallsRateLimitWithIpAddress()
    {
        IInquiryRepository repo = Substitute.For<IInquiryRepository>();
        IRateLimitService rateLimit = Substitute.For<IRateLimitService>();
        rateLimit.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        SubmitInquiryCommand command = BuildCommand();

        await SubmitInquiryHandler.HandleAsync(command, repo, rateLimit, TimeProvider.System, CancellationToken.None);

        await rateLimit.Received(1).IsAllowedAsync(command.SubmitterIpAddress, Arg.Any<CancellationToken>());
    }
}
