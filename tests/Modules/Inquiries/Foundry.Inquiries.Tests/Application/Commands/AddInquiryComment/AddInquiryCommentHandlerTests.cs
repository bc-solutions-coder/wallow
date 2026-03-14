using Foundry.Inquiries.Application.Commands.AddInquiryComment;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Commands.AddInquiryComment;

public class AddInquiryCommentHandlerTests
{
    private readonly IInquiryCommentRepository _repo = Substitute.For<IInquiryCommentRepository>();

    private static AddInquiryCommentCommand BuildCommand() =>
        new(InquiryId.New(), "user-123", "John Doe", "This is a comment.", false, Guid.NewGuid());

    [Fact]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessWithId()
    {
        AddInquiryCommentCommand command = BuildCommand();

        Result<InquiryCommentId> result = await AddInquiryCommentHandler.HandleAsync(command, _repo, TimeProvider.System, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_CallsAddAsync()
    {
        AddInquiryCommentCommand command = BuildCommand();

        await AddInquiryCommentHandler.HandleAsync(command, _repo, TimeProvider.System, CancellationToken.None);

        await _repo.Received(1).AddAsync(Arg.Any<InquiryComment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CreatesCommentWithCorrectFields()
    {
        AddInquiryCommentCommand command = BuildCommand();
        InquiryComment? capturedComment = null;
        await _repo.AddAsync(Arg.Do<InquiryComment>(c => capturedComment = c), Arg.Any<CancellationToken>());

        await AddInquiryCommentHandler.HandleAsync(command, _repo, TimeProvider.System, CancellationToken.None);

        capturedComment.Should().NotBeNull();
        capturedComment!.InquiryId.Should().Be(command.InquiryId);
        capturedComment.AuthorId.Should().Be(command.AuthorId);
        capturedComment.AuthorName.Should().Be(command.AuthorName);
        capturedComment.Content.Should().Be(command.Content);
        capturedComment.IsInternal.Should().Be(command.IsInternal);
    }
}
