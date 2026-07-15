using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Queries.GetInquiries;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Tests.Application.Queries.GetInquiries;

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

        _repo.GetByStatusAsync(InquiryStatus.New, Arg.Any<CancellationToken>()).Returns(new List<Inquiry> { newInquiry });

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
