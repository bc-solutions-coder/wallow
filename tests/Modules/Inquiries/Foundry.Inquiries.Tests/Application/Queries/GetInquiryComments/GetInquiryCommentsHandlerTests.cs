using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetInquiryComments;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Tests.Application.Queries.GetInquiryComments;

public class GetInquiryCommentsHandlerTests
{
    private readonly IInquiryCommentRepository _repo = Substitute.For<IInquiryCommentRepository>();
    private readonly GetInquiryCommentsHandler _handler;

    public GetInquiryCommentsHandlerTests()
    {
        _handler = new GetInquiryCommentsHandler(_repo);
    }

    private static InquiryComment CreateComment(InquiryId inquiryId, bool isInternal = false) =>
        InquiryComment.Create(inquiryId, "user-1", "Author", "Comment content", isInternal, TimeProvider.System);

    [Fact]
    public async Task Handle_ReturnsAllComments()
    {
        InquiryId inquiryId = InquiryId.New();
        List<InquiryComment> comments = [CreateComment(inquiryId), CreateComment(inquiryId)];
        _repo.GetByInquiryIdAsync(inquiryId, true, Arg.Any<CancellationToken>()).Returns(comments);

        IReadOnlyList<InquiryCommentDto> result = await _handler.Handle(new GetInquiryCommentsQuery(inquiryId, true), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithIncludeInternalTrue_PassesTrueToRepository()
    {
        InquiryId inquiryId = InquiryId.New();
        _repo.GetByInquiryIdAsync(inquiryId, true, Arg.Any<CancellationToken>()).Returns(new List<InquiryComment>());

        await _handler.Handle(new GetInquiryCommentsQuery(inquiryId, true), CancellationToken.None);

        await _repo.Received(1).GetByInquiryIdAsync(inquiryId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIncludeInternalFalse_PassesFalseToRepository()
    {
        InquiryId inquiryId = InquiryId.New();
        _repo.GetByInquiryIdAsync(inquiryId, false, Arg.Any<CancellationToken>()).Returns(new List<InquiryComment>());

        await _handler.Handle(new GetInquiryCommentsQuery(inquiryId, false), CancellationToken.None);

        await _repo.Received(1).GetByInquiryIdAsync(inquiryId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoComments_ReturnsEmptyList()
    {
        InquiryId inquiryId = InquiryId.New();
        _repo.GetByInquiryIdAsync(inquiryId, false, Arg.Any<CancellationToken>()).Returns(new List<InquiryComment>());

        IReadOnlyList<InquiryCommentDto> result = await _handler.Handle(new GetInquiryCommentsQuery(inquiryId, false), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsCommentFieldsCorrectly()
    {
        InquiryId inquiryId = InquiryId.New();
        InquiryComment comment = CreateComment(inquiryId, isInternal: true);
        _repo.GetByInquiryIdAsync(inquiryId, true, Arg.Any<CancellationToken>()).Returns(new List<InquiryComment> { comment });

        IReadOnlyList<InquiryCommentDto> result = await _handler.Handle(new GetInquiryCommentsQuery(inquiryId, true), CancellationToken.None);

        InquiryCommentDto dto = result.Should().ContainSingle().Which;
        dto.Id.Should().Be(comment.Id.Value);
        dto.InquiryId.Should().Be(inquiryId.Value);
        dto.AuthorId.Should().Be("user-1");
        dto.AuthorName.Should().Be("Author");
        dto.Content.Should().Be("Comment content");
        dto.IsInternal.Should().BeTrue();
    }
}
