using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Identity;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using EmailNotificationType = Foundry.Communications.Domain.Channels.Email.Enums.NotificationType;

namespace Foundry.Communications.Tests.Infrastructure.Persistence.Repositories;

public sealed class EmailPreferenceRepositoryTests : IDisposable
{
    private readonly CommunicationsDbContext _dbContext;
    private readonly EmailPreferenceRepository _repository;
    private readonly TenantId _tenantId;

    public EmailPreferenceRepositoryTests()
    {
        _tenantId = TenantId.Create(Guid.NewGuid());

        DbContextOptions<CommunicationsDbContext> options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(_tenantId);

        _dbContext = new CommunicationsDbContext(options, tenantContext);
        _repository = new EmailPreferenceRepository(_dbContext);
    }

    [Fact]
    public async Task Add_AddsPreferenceToDatabase()
    {
        EmailPreference preference = CreatePreference(Guid.NewGuid(), EmailNotificationType.TaskAssigned);

        _repository.Add(preference);
        await _dbContext.SaveChangesAsync();

        int count = await _dbContext.EmailPreferences.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsPreference()
    {
        EmailPreference preference = CreatePreference(Guid.NewGuid(), EmailNotificationType.BillingInvoice);
        await _dbContext.EmailPreferences.AddAsync(preference);
        await _dbContext.SaveChangesAsync();

        EmailPreference? result = await _repository.GetByIdAsync(preference.Id);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        EmailPreference? result = await _repository.GetByIdAsync(EmailPreferenceId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserAndTypeAsync_WhenExists_ReturnsPreference()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = CreatePreference(userId, EmailNotificationType.TaskCompleted);
        await _dbContext.EmailPreferences.AddAsync(preference);
        await _dbContext.SaveChangesAsync();

        EmailPreference? result = await _repository.GetByUserAndTypeAsync(
            userId, EmailNotificationType.TaskCompleted);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByUserAndTypeAsync_WhenNotExists_ReturnsNull()
    {
        EmailPreference? result = await _repository.GetByUserAndTypeAsync(
            Guid.NewGuid(), EmailNotificationType.TaskAssigned);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllPreferencesForUser()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        EmailPreference p1 = CreatePreference(userId, EmailNotificationType.TaskAssigned);
        EmailPreference p2 = CreatePreference(userId, EmailNotificationType.BillingInvoice);
        EmailPreference other = CreatePreference(otherUserId, EmailNotificationType.TaskAssigned);

        await _dbContext.EmailPreferences.AddRangeAsync(p1, p2, other);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<EmailPreference> result = await _repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        EmailPreference preference = CreatePreference(Guid.NewGuid(), EmailNotificationType.SystemNotification);
        _repository.Add(preference);

        await _repository.SaveChangesAsync();

        int count = await _dbContext.EmailPreferences.CountAsync();
        count.Should().Be(1);
    }

    private EmailPreference CreatePreference(Guid userId, EmailNotificationType type)
    {
        EmailPreference preference = EmailPreference.Create(userId, type);
        _dbContext.Entry(preference).Property(nameof(ITenantScoped.TenantId)).CurrentValue = _tenantId;
        return preference;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
