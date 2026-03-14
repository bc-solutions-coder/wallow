using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetInquiries;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Queries.GetInquiries;

public class GetInquiriesHandlerTests
{
    private readonly IInquiryRepository _repo = Substitute.For<IInquiryRepository>();
    private readonly GetInquiriesHandler _handler;

    public GetInquiriesHandlerTests()
    {
        _handler = new GetInquiriesHandler(_repo);
    }

    private static Inquiry CreateInquiry(string name = "Test User") =>
        Inquiry.Create(name, "test@example.com", "555-0100", null, null, "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);

    [Fact]
    public async Task Handle_ReturnsAllInquiries()
    {
        List<Inquiry> inquiries = [CreateInquiry("Alice"), CreateInquiry("Bob")];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsFilteredInquiries()
    {
        Inquiry newInquiry = CreateInquiry("Alice");
        Inquiry reviewedInquiry = CreateInquiry("Bob");
        reviewedInquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        List<Inquiry> inquiries = [newInquiry, reviewedInquiry];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(InquiryStatus.New), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_WithNoInquiries_ReturnsEmptyList()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Inquiry>());

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
