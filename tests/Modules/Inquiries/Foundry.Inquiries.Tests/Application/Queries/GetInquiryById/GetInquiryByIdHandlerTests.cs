using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetInquiryById;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Queries.GetInquiryById;

public class GetInquiryByIdHandlerTests
{
    private readonly IInquiryRepository _repo = Substitute.For<IInquiryRepository>();
    private readonly GetInquiryByIdHandler _handler;

    public GetInquiryByIdHandlerTests()
    {
        _handler = new GetInquiryByIdHandler(_repo);
    }

    [Fact]
    public async Task Handle_WhenInquiryExists_ReturnsDto()
    {
        Inquiry inquiry = Inquiry.Create("Jane", "jane@example.com", "555-0100", null, null, "Mobile App", "$5k", "6 months", "Need a mobile app.", "10.0.0.1", TimeProvider.System);
        _repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        GetInquiryByIdQuery query = new(inquiry.Id.Value);

        Result<InquiryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(inquiry.Id.Value);
        result.Value.Name.Should().Be("Jane");
    }

    [Fact]
    public async Task Handle_WhenInquiryNotFound_ReturnsNotFoundError()
    {
        _repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns((Inquiry?)null);

        GetInquiryByIdQuery query = new(Guid.NewGuid());

        Result<InquiryDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().EndWith(".NotFound");
    }
}
