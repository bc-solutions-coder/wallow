using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class EmailMessageRepositoryTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly EmailMessageRepository _repository;
    private readonly TenantId _tenantId;

    public EmailMessageRepositoryTests()
    {
        _tenantId = TenantId.Create(Guid.NewGuid());

        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new EmailMessageRepository(_dbContext);
    }

    [Fact]
    public async Task Add_AddsEmailMessageToDatabase()
    {
        EmailMessage email = CreateEmailMessage();

        _repository.Add(email);
        await _dbContext.SaveChangesAsync();

        int count = await _dbContext.EmailMessages.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsMessage()
    {
        EmailMessage email = CreateEmailMessage();
        await _dbContext.EmailMessages.AddAsync(email);
        await _dbContext.SaveChangesAsync();

        EmailMessage? result = await _repository.GetByIdAsync(email.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        EmailMessage? result = await _repository.GetByIdAsync(EmailMessageId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingMessages()
    {
        EmailMessage pending = CreateEmailMessage();
        EmailMessage sent = CreateEmailMessage();
        sent.MarkAsSent(TimeProvider.System);

        await _dbContext.EmailMessages.AddRangeAsync(pending, sent);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetPendingAsync(10);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetPendingAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            EmailMessage email = CreateEmailMessage();
            await _dbContext.EmailMessages.AddAsync(email);
        }
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<EmailMessage> result = await _repository.GetPendingAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        EmailMessage email = CreateEmailMessage();
        _repository.Add(email);

        await _repository.SaveChangesAsync();

        int count = await _dbContext.EmailMessages.CountAsync();
        count.Should().Be(1);
    }

    private EmailMessage CreateEmailMessage()
    {
        EmailAddress to = EmailAddress.Create("recipient@test.com");
        EmailAddress from = EmailAddress.Create("sender@test.com");
        EmailContent content = EmailContent.Create("Test Subject", "<p>Test Body</p>");
        EmailMessage email = EmailMessage.Create(to, from, content, TimeProvider.System);
        email.TenantId = _tenantId;
        return email;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
