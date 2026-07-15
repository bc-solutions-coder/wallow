using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Shared.Infrastructure.Tests.Auditing;

public sealed class AuthAuditServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuthAuditDbContext> _options;

    public AuthAuditServiceTests()
    {
        // Shared in-memory SQLite connection so all contexts share the same database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AuthAuditDbContext>()
            .UseSqlite(_connection)
            .Options;

        using AuthAuditDbContext context = new(_options);
        context.Database.EnsureCreated();
    }

    private (AuthAuditService Service, AuthAuditDbContext VerifyContext) CreateSut()
    {
        IDbContextFactory<AuthAuditDbContext> factory = Substitute.For<IDbContextFactory<AuthAuditDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AuthAuditDbContext(_options)));

        ILogger<AuthAuditService> logger = Substitute.For<ILogger<AuthAuditService>>();
        AuthAuditService service = new(factory, logger);

        // Separate context for verification queries
        AuthAuditDbContext verifyContext = new(_options);
        return (service, verifyContext);
    }

    [Fact]
    public async Task RecordAsync_InsertsEntryIntoDatabase()
    {
        (AuthAuditService sut, AuthAuditDbContext verifyContext) = CreateSut();

        AuthAuditRecord record = new()
        {
            EventType = "LoginSuccess",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "192.168.1.1",
            UserAgent = "TestAgent",
            OccurredAt = DateTimeOffset.UtcNow
        };

        await sut.RecordAsync(record, CancellationToken.None);

        List<AuthAuditEntry> entries = await verifyContext.AuthAuditEntries.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be("LoginSuccess");
        entries[0].UserId.Should().Be(record.UserId);
        entries[0].TenantId.Should().Be(record.TenantId);
        entries[0].IpAddress.Should().Be("192.168.1.1");
        entries[0].UserAgent.Should().Be("TestAgent");
    }

    [Fact]
    public async Task RecordAsync_WithNullOptionalFields_InsertsSuccessfully()
    {
        (AuthAuditService sut, AuthAuditDbContext verifyContext) = CreateSut();

        AuthAuditRecord record = new()
        {
            EventType = "LoginFailed",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow
        };

        await sut.RecordAsync(record, CancellationToken.None);

        List<AuthAuditEntry> entries = await verifyContext.AuthAuditEntries.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].IpAddress.Should().BeNull();
        entries[0].UserAgent.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_GeneratesNewId()
    {
        (AuthAuditService sut, AuthAuditDbContext verifyContext) = CreateSut();

        AuthAuditRecord record = new()
        {
            EventType = "LoginSuccess",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow
        };

        await sut.RecordAsync(record, CancellationToken.None);

        List<AuthAuditEntry> entries = await verifyContext.AuthAuditEntries.ToListAsync();
        entries[0].Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RecordAsync_WhenDbFails_DoesNotThrow()
    {
        IDbContextFactory<AuthAuditDbContext> factory = Substitute.For<IDbContextFactory<AuthAuditDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<AuthAuditDbContext>(_ => throw new InvalidOperationException("DB is down"));

        ILogger<AuthAuditService> logger = Substitute.For<ILogger<AuthAuditService>>();
        AuthAuditService sut = new(factory, logger);

        AuthAuditRecord record = new()
        {
            EventType = "LoginSuccess",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow
        };

        // Should not throw -- audit is non-blocking
        await sut.Invoking(s => s.RecordAsync(record, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_MultipleRecords_AllPersisted()
    {
        (AuthAuditService sut, AuthAuditDbContext verifyContext) = CreateSut();

        for (int i = 0; i < 3; i++)
        {
            await sut.RecordAsync(new AuthAuditRecord
            {
                EventType = $"Event{i}",
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow
            }, CancellationToken.None);
        }

        int count = await verifyContext.AuthAuditEntries.CountAsync();
        count.Should().Be(3);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
