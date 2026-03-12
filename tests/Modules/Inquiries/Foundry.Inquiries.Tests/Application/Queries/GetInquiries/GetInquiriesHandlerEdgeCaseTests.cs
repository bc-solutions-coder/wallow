using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetInquiries;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Queries.GetInquiries;

public class GetInquiriesHandlerEdgeCaseTests
{
    private readonly IInquiryRepository _repo = Substitute.For<IInquiryRepository>();
    private readonly GetInquiriesHandler _handler;

    public GetInquiriesHandlerEdgeCaseTests()
    {
        _handler = new GetInquiriesHandler(_repo);
    }

    private static Inquiry CreateInquiry(string name = "Test User") =>
        Inquiry.Create(name, "test@example.com", null, "Web App", "$10k", "3 months", "Need help.", "1.1.1.1", TimeProvider.System);

    [Fact]
    public async Task Handle_WithStatusFilter_WhenNoMatch_ReturnsEmptyList()
    {
        Inquiry newInquiry = CreateInquiry("Alice");
        List<Inquiry> inquiries = [newInquiry];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(InquiryStatus.Reviewed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        Inquiry newInquiry = CreateInquiry("Alice");
        Inquiry reviewedInquiry = CreateInquiry("Bob");
        reviewedInquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        Inquiry contactedInquiry = CreateInquiry("Charlie");
        contactedInquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        contactedInquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);

        List<Inquiry> inquiries = [newInquiry, reviewedInquiry, contactedInquiry];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(InquiryStatus.Contacted), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task Handle_WithNullStatusFilter_ReturnsAllInquiries()
    {
        Inquiry newInquiry = CreateInquiry("Alice");
        Inquiry reviewedInquiry = CreateInquiry("Bob");
        reviewedInquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        List<Inquiry> inquiries = [newInquiry, reviewedInquiry];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_PreservesOrderFromRepository()
    {
        Inquiry first = CreateInquiry("Alice");
        Inquiry second = CreateInquiry("Bob");
        Inquiry third = CreateInquiry("Charlie");

        List<Inquiry> inquiries = [first, second, third];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(inquiries);

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(new GetInquiriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].Name.Should().Be("Alice");
        result.Value[1].Name.Should().Be("Bob");
        result.Value[2].Name.Should().Be("Charlie");
    }
}
