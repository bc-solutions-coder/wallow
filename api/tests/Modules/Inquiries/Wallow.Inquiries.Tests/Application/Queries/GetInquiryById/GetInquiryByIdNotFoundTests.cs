using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Queries.GetInquiryById;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Tests.Application.Queries.GetInquiryById;

public class GetInquiryByIdNotFoundTests
{
    private readonly IInquiryRepository _repo = Substitute.For<IInquiryRepository>();
    private readonly GetInquiryByIdHandler _handler;

    public GetInquiryByIdNotFoundTests()
    {
        _handler = new GetInquiryByIdHandler(_repo);
    }

    [Fact]
    public async Task Handle_WhenInquiryNotFound_ErrorContainsInquiryId()
    {
        _repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns((Inquiry?)null);
        Guid missingId = Guid.NewGuid();

        Result<InquiryDto> result = await _handler.Handle(new GetInquiryByIdQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().EndWith(".NotFound");
    }

    [Fact]
    public async Task Handle_WhenInquiryNotFound_CallsRepositoryWithCorrectId()
    {
        _repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns((Inquiry?)null);
        Guid queryId = Guid.NewGuid();

        await _handler.Handle(new GetInquiryByIdQuery(queryId), CancellationToken.None);

        await _repo.Received(1).GetByIdAsync(
            Arg.Is<InquiryId>(id => id.Value == queryId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInquiryExists_MapsAllFieldsCorrectly()
    {
        Inquiry inquiry = Inquiry.Create("Jane Doe", "jane@example.com", "555-0100", "Acme Corp", null, "Mobile App", "$50k", "6 months", "Build us a mobile app.", "10.0.0.1", TimeProvider.System);
        _repo.GetByIdAsync(Arg.Any<InquiryId>(), Arg.Any<CancellationToken>()).Returns(inquiry);

        Result<InquiryDto> result = await _handler.Handle(new GetInquiryByIdQuery(inquiry.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        InquiryDto dto = result.Value;
        dto.Id.Should().Be(inquiry.Id.Value);
        dto.Name.Should().Be("Jane Doe");
        dto.Email.Should().Be("jane@example.com");
        dto.Company.Should().Be("Acme Corp");
        dto.ProjectType.Should().Be("Mobile App");
        dto.BudgetRange.Should().Be("$50k");
        dto.Timeline.Should().Be("6 months");
        dto.Message.Should().Be("Build us a mobile app.");
        dto.SubmitterIpAddress.Should().Be("10.0.0.1");
        dto.Status.Should().Be("New");
    }
}
