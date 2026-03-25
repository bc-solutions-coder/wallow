using Microsoft.EntityFrameworkCore;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Inquiries.Tests.Infrastructure.Persistence;

[Collection("InquiriesPostgresDatabase")]
[Trait("Category", "Integration")]
public class InquiryCommentRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<InquiriesDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    protected override InquiriesDbContext CreateDbContext(DbContextOptions<InquiriesDbContext> options, ITenantContext tenantContext)
    {
        return new InquiriesDbContext(options);
    }

    private InquiryCommentRepository CreateRepository() => new(DbContext);

    private static Inquiry CreateInquiry(string name = "Test User")
    {
        Inquiry inquiry = Inquiry.Create(name, "test@example.com", "555-0100", "Acme", null, "Web App", "$10k", "3 months", "We need help.", "1.2.3.4", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    private static InquiryComment CreateComment(InquiryId inquiryId, string content = "Test comment", bool isInternal = false)
    {
        InquiryComment comment = InquiryComment.Create(inquiryId, "user-1", "Author Name", content, isInternal, TenantId.New(), TimeProvider.System);
        comment.ClearDomainEvents();
        return comment;
    }

    private async Task<Inquiry> SeedInquiryAsync()
    {
        InquiryRepository inquiryRepository = new(DbContext);
        Inquiry inquiry = CreateInquiry();
        await inquiryRepository.AddAsync(inquiry, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        return inquiry;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsComment()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();
        InquiryComment comment = CreateComment(inquiry.Id, "Hello world");

        await repository.AddAsync(comment, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        InquiryComment? result = await repository.GetByIdAsync(comment.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Content.Should().Be("Hello world");
        result.AuthorId.Should().Be("user-1");
        result.AuthorName.Should().Be("Author Name");
        result.InquiryId.Should().Be(inquiry.Id);
        result.IsInternal.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        InquiryCommentRepository repository = CreateRepository();

        InquiryComment? result = await repository.GetByIdAsync(InquiryCommentId.New(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_PersistsAllFields()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();
        InquiryComment comment = InquiryComment.Create(inquiry.Id, "author-42", "Jane Doe", "Detailed note", true, TenantId.New(), TimeProvider.System);
        comment.ClearDomainEvents();

        await repository.AddAsync(comment, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        InquiryComment? result = await repository.GetByIdAsync(comment.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.InquiryId.Should().Be(inquiry.Id);
        result.AuthorId.Should().Be("author-42");
        result.AuthorName.Should().Be("Jane Doe");
        result.Content.Should().Be("Detailed note");
        result.IsInternal.Should().BeTrue();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByInquiryIdAsync_IncludeInternal_ReturnsAllComments()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();

        InquiryComment publicComment = CreateComment(inquiry.Id, "Public note", isInternal: false);
        InquiryComment internalComment = CreateComment(inquiry.Id, "Internal note", isInternal: true);

        await repository.AddAsync(publicComment, CancellationToken.None);
        await repository.AddAsync(internalComment, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<InquiryComment> result = await repository.GetByInquiryIdAsync(inquiry.Id, includeInternal: true, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(c => c.Content).Should().Contain("Public note").And.Contain("Internal note");
    }

    [Fact]
    public async Task GetByInquiryIdAsync_ExcludeInternal_ReturnsOnlyPublicComments()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();

        InquiryComment publicComment = CreateComment(inquiry.Id, "Public note", isInternal: false);
        InquiryComment internalComment = CreateComment(inquiry.Id, "Internal note", isInternal: true);

        await repository.AddAsync(publicComment, CancellationToken.None);
        await repository.AddAsync(internalComment, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<InquiryComment> result = await repository.GetByInquiryIdAsync(inquiry.Id, includeInternal: false, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be("Public note");
        result[0].IsInternal.Should().BeFalse();
    }

    [Fact]
    public async Task GetByInquiryIdAsync_ReturnsOrderedByCreatedAtAscending()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();

        InquiryComment first = CreateComment(inquiry.Id, "First");
        await repository.AddAsync(first, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        await Task.Delay(10);

        InquiryComment second = CreateComment(inquiry.Id, "Second");
        await repository.AddAsync(second, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<InquiryComment> result = await repository.GetByInquiryIdAsync(inquiry.Id, includeInternal: true, CancellationToken.None);

        result[0].Content.Should().Be("First");
        result[1].Content.Should().Be("Second");
    }

    [Fact]
    public async Task GetByInquiryIdAsync_WhenNoComments_ReturnsEmptyList()
    {
        Inquiry inquiry = await SeedInquiryAsync();
        InquiryCommentRepository repository = CreateRepository();

        IReadOnlyList<InquiryComment> result = await repository.GetByInquiryIdAsync(inquiry.Id, includeInternal: true, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByInquiryIdAsync_DoesNotReturnCommentsFromOtherInquiries()
    {
        Inquiry inquiry1 = await SeedInquiryAsync();
        Inquiry inquiry2 = CreateInquiry("Other User");
        InquiryRepository inquiryRepo = new(DbContext);
        await inquiryRepo.AddAsync(inquiry2, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        InquiryCommentRepository repository = CreateRepository();
        InquiryComment comment1 = CreateComment(inquiry1.Id, "Comment on inquiry 1");
        InquiryComment comment2 = CreateComment(inquiry2.Id, "Comment on inquiry 2");

        await repository.AddAsync(comment1, CancellationToken.None);
        await repository.AddAsync(comment2, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<InquiryComment> result = await repository.GetByInquiryIdAsync(inquiry1.Id, includeInternal: true, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be("Comment on inquiry 1");
    }
}
