using Wallow.Inquiries.Application.Commands.AddInquiryComment;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Inquiries.Tests.Application.Commands.AddInquiryComment;

public class AddInquiryCommentHandlerTests
{
    private readonly IInquiryCommentRepository _commentRepo = Substitute.For<IInquiryCommentRepository>();
    private readonly IInquiryRepository _inquiryRepo = Substitute.For<IInquiryRepository>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private static AddInquiryCommentCommand BuildCommand() =>
        new(InquiryId.New(), "user-123", "John Doe", "This is a comment.", false);

    [Fact]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessWithId()
    {
        AddInquiryCommentCommand command = BuildCommand();

        Result<InquiryCommentId> result = await AddInquiryCommentHandler.HandleAsync(
            command, _commentRepo, _inquiryRepo, _bus, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_CallsAddAsync()
    {
        AddInquiryCommentCommand command = BuildCommand();

        await AddInquiryCommentHandler.HandleAsync(
            command, _commentRepo, _inquiryRepo, _bus, TimeProvider.System, CancellationToken.None);

        await _commentRepo.Received(1).AddAsync(Arg.Any<InquiryComment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CreatesCommentWithCorrectFields()
    {
        AddInquiryCommentCommand command = BuildCommand();
        InquiryComment? capturedComment = null;
        await _commentRepo.AddAsync(Arg.Do<InquiryComment>(c => capturedComment = c), Arg.Any<CancellationToken>());

        await AddInquiryCommentHandler.HandleAsync(
            command, _commentRepo, _inquiryRepo, _bus, TimeProvider.System, CancellationToken.None);

        capturedComment.Should().NotBeNull();
        capturedComment!.InquiryId.Should().Be(command.InquiryId);
        capturedComment.AuthorId.Should().Be(command.AuthorId);
        capturedComment.AuthorName.Should().Be(command.AuthorName);
        capturedComment.Content.Should().Be(command.Content);
        capturedComment.IsInternal.Should().Be(command.IsInternal);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        AddInquiryCommentCommand command = BuildCommand();

        await AddInquiryCommentHandler.HandleAsync(
            command, _commentRepo, _inquiryRepo, _bus, TimeProvider.System, CancellationToken.None);

        await _bus.Received(1).PublishAsync(Arg.Is<InquiryCommentAddedEvent>(e =>
            e.InquiryId == command.InquiryId.Value &&
            e.AuthorId == command.AuthorId &&
            e.IsInternal == command.IsInternal &&
            e.CommentContent == command.Content));
    }
}
