using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Queries.GetSubmittedInquiries;

public class GetSubmittedInquiriesHandlerTests
{
    private readonly IInquiryRepository _repo = Substitute.For<IInquiryRepository>();
    private readonly GetSubmittedInquiriesHandler _handler;

    public GetSubmittedInquiriesHandlerTests()
    {
        _handler = new GetSubmittedInquiriesHandler(_repo);
    }

    private static Inquiry CreateInquiry(string submitterId, string name = "Test User") =>
        Inquiry.Create(name, "test@example.com", "555-0100", null, submitterId, "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);

    [Fact]
    public async Task Handle_WithMatchingSubmitterId_ReturnsInquiries()
    {
        string submitterId = "user-123";
        List<Inquiry> inquiries = [CreateInquiry(submitterId, "Alice"), CreateInquiry(submitterId, "Bob")];
        _repo.GetBySubmitterAsync(submitterId, Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(
            new GetSubmittedInquiriesQuery(submitterId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("Alice");
        result.Value[1].Name.Should().Be("Bob");
    }

    [Fact]
    public async Task Handle_WithNoMatches_ReturnsEmptyList()
    {
        string submitterId = "user-no-matches";
        _repo.GetBySubmitterAsync(submitterId, Arg.Any<CancellationToken>()).Returns(new List<Inquiry>());

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(
            new GetSubmittedInquiriesQuery(submitterId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsInquiriesToDtos()
    {
        string submitterId = "user-456";
        List<Inquiry> inquiries = [CreateInquiry(submitterId, "Charlie")];
        _repo.GetBySubmitterAsync(submitterId, Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(
            new GetSubmittedInquiriesQuery(submitterId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        InquiryDto dto = result.Value[0];
        dto.Name.Should().Be("Charlie");
        dto.Email.Should().Be("test@example.com");
        dto.SubmitterId.Should().Be(submitterId);
        dto.Status.Should().Be("New");
    }

    [Fact]
    public async Task Handle_PassesSubmitterIdToRepository()
    {
        string submitterId = "user-789";
        _repo.GetBySubmitterAsync(submitterId, Arg.Any<CancellationToken>()).Returns(new List<Inquiry>());

        await _handler.Handle(new GetSubmittedInquiriesQuery(submitterId), CancellationToken.None);

        await _repo.Received(1).GetBySubmitterAsync(submitterId, Arg.Any<CancellationToken>());
    }
}
