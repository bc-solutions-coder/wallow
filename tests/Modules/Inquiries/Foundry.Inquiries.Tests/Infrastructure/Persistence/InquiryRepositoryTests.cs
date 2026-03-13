using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Inquiries.Infrastructure.Persistence;
using Foundry.Inquiries.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Tests.Infrastructure.Persistence;

[Collection("InquiriesPostgresDatabase")]
[Trait("Category", "Integration")]
public class InquiryRepositoryTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<InquiriesDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    protected override InquiriesDbContext CreateDbContext(DbContextOptions<InquiriesDbContext> options, ITenantContext tenantContext)
    {
        return new InquiriesDbContext(options);
    }

    private InquiryRepository CreateRepository() => new(DbContext);

    private static Inquiry CreateInquiry(string name = "Test User", string ip = "1.2.3.4")
    {
        Inquiry inquiry = Inquiry.Create(name, "test@example.com", "Acme", "Web App", "$10k", "3 months", "We need help.", ip, TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ReturnsInquiry()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = CreateInquiry("Alice");

        await repository.AddAsync(inquiry, CancellationToken.None);
        await DbContext.SaveChangesAsync();

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
        await DbContext.SaveChangesAsync();

        IReadOnlyList<Inquiry> result = await repository.GetAllAsync(CancellationToken.None);

        result.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByCreatedAtDescending()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry first = CreateInquiry("FirstCreated");
        Inquiry second = CreateInquiry("SecondCreated");

        await repository.AddAsync(first, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        // Small delay to ensure different timestamps
        await Task.Delay(10);
        await repository.AddAsync(second, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        IReadOnlyList<Inquiry> result = await repository.GetAllAsync(CancellationToken.None);

        // Most recent should come first
        result[0].Name.Should().Be("SecondCreated");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = CreateInquiry("UpdateTest");

        await repository.AddAsync(inquiry, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        await repository.UpdateAsync(inquiry, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Inquiry? result = await repository.GetByIdAsync(inquiry.Id, CancellationToken.None);
        result.Should().NotBeNull();
        result!.Status.Should().Be(InquiryStatus.Reviewed);
    }

    [Fact]
    public async Task AddAsync_PersistsAllFields()
    {
        InquiryRepository repository = CreateRepository();
        Inquiry inquiry = Inquiry.Create("Full Name", "full@example.com", "BigCorp", "Mobile App", "$50k+", "6 months", "Detailed message here.", "10.0.0.1", TimeProvider.System);
        inquiry.ClearDomainEvents();

        await repository.AddAsync(inquiry, CancellationToken.None);
        await DbContext.SaveChangesAsync();

        Inquiry? result = await repository.GetByIdAsync(inquiry.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Full Name");
        result.Email.Should().Be("full@example.com");
        result.Company.Should().Be("BigCorp");
        result.ProjectType.Should().Be("Mobile App");
        result.BudgetRange.Should().Be("$50k+");
        result.Timeline.Should().Be("6 months");
        result.Message.Should().Be("Detailed message here.");
        result.SubmitterIpAddress.Should().Be("10.0.0.1");
    }
}
