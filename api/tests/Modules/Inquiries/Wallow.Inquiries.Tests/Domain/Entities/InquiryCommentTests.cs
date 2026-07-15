using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Identity;
namespace Wallow.Inquiries.Tests.Domain.Entities;

public class InquiryCommentTests
{
    [Fact]
    public void Create_WithValidData_RaisesInquiryCommentAddedDomainEvent()
    {
        InquiryId inquiryId = InquiryId.New();
        string authorId = "user-123";
        string authorName = "John Doe";
        string content = "This is a comment.";
        bool isInternal = true;
        InquiryComment comment = InquiryComment.Create(inquiryId, authorId, authorName, content, isInternal, TimeProvider.System);

        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquiryCommentAddedDomainEvent>()
            .Which.Should().Match<InquiryCommentAddedDomainEvent>(e =>
                e.InquiryCommentId == comment.Id.Value &&
                e.InquiryId == inquiryId.Value &&
                e.AuthorId == authorId &&
                e.IsInternal == isInternal);
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        InquiryId inquiryId = InquiryId.New();
        string authorId = "user-456";
        string authorName = "Jane Smith";
        string content = "Another comment.";
        bool isInternal = false;
        InquiryComment comment = InquiryComment.Create(inquiryId, authorId, authorName, content, isInternal, TimeProvider.System);

        comment.InquiryId.Should().Be(inquiryId);
        comment.AuthorId.Should().Be(authorId);
        comment.AuthorName.Should().Be(authorName);
        comment.Content.Should().Be(content);
        comment.IsInternal.Should().Be(isInternal);
        comment.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        InquiryComment comment = InquiryComment.Create(InquiryId.New(), "user-1", "User", "Content", false, TimeProvider.System);

        comment.CreatedAt.Should().BeOnOrAfter(before);
        comment.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }
}
