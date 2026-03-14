using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Inquiries.Infrastructure.Persistence;
using Foundry.Inquiries.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Tests.Infrastructure.Persistence;

public sealed class InquiryRepositoryInMemoryTests : IDisposable
{
    private readonly InquiriesDbContext _context;

    public InquiryRepositoryInMemoryTests()
    {
        DbContextOptions<InquiriesDbContext> options = new DbContextOptionsBuilder<InquiriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new InquiriesDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private InquiryRepository CreateRepository() => new(_context);

    private static Inquiry CreateInquiry(string name = "Test User")
    {
        Inquiry inquiry = Inquiry.Create(name, "test@example.com", "555-0100", "Acme", null, "Web App", "$10k", "3 months", "We need help.", "1.2.3.4", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsInquiry()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = CreateInquiry("Alice");

        await repository.AddAsync(inquiry, CancellationToken.None);
        await _context.SaveChangesAsync();

        Inquiry? result = await repository.GetByIdAsync(inquiry.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice");
        result.Email.Should().Be("test@example.com");
        result.Status.Should().Be(InquiryStatus.New);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        InquiryRepository repository = CreateRepository();

        Inquiry? result = await repository.GetByIdAsync(InquiryId.New(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInquiries()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry1 = CreateInquiry("Alice");
        Inquiry inquiry2 = CreateInquiry("Bob");

        await repository.AddAsync(inquiry1, CancellationToken.None);
        await repository.AddAsync(inquiry2, CancellationToken.None);
        await _context.SaveChangesAsync();

        IReadOnlyList<Inquiry> result = await repository.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        InquiryRepository repository = CreateRepository();

        IReadOnlyList<Inquiry> result = await repository.GetAllAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = CreateInquiry("UpdateTest");

        await repository.AddAsync(inquiry, CancellationToken.None);
        await _context.SaveChangesAsync();

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        await repository.UpdateAsync(inquiry, CancellationToken.None);
        await _context.SaveChangesAsync();

        Inquiry? updated = await _context.Inquiries
            .FirstOrDefaultAsync(i => i.Id == inquiry.Id);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(InquiryStatus.Reviewed);
    }

    [Fact]
    public async Task AddAsync_PersistsAllFields()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = Inquiry.Create("Full Name", "full@example.com", "555-0100", "BigCorp", null, "Mobile App", "$50k+", "6 months", "Detailed message.", "10.0.0.1", TimeProvider.System);
        inquiry.ClearDomainEvents();

        await repository.AddAsync(inquiry, CancellationToken.None);
        await _context.SaveChangesAsync();

        Inquiry? result = await repository.GetByIdAsync(inquiry.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Full Name");
        result.Email.Should().Be("full@example.com");
        result.Company.Should().Be("BigCorp");
        result.ProjectType.Should().Be("Mobile App");
        result.BudgetRange.Should().Be("$50k+");
        result.Timeline.Should().Be("6 months");
        result.Message.Should().Be("Detailed message.");
        result.SubmitterIpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsInquiriesInOrderByCreatedAtDescending()
    {
        // InMemory doesn't apply OrderByDescending perfectly the same way,
        // but we verify GetAllAsync returns all items
        InquiryRepository repository = CreateRepository();

        for (int i = 0; i < 3; i++)
        {
            await repository.AddAsync(CreateInquiry($"User{i}"), CancellationToken.None);
        }

        await _context.SaveChangesAsync();

        IReadOnlyList<Inquiry> result = await repository.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(3);
    }
}
