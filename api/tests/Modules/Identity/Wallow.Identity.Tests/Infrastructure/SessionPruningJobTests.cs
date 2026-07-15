using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Jobs;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class SessionPruningJobTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider;

    public SessionPruningJobTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_DeletesExpiredSessions()
    {
        ActiveSession expired = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(1), _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(2));
        ActiveSession active = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);

        _dbContext.ActiveSessions.AddRange(expired, active);
        await _dbContext.SaveChangesAsync();

        _timeProvider.Advance(TimeSpan.FromHours(1));

        SessionPruningJob job = new(_dbContext, _timeProvider, NullLogger<SessionPruningJob>.Instance);
        int deleted = await job.ExecuteAsync();

        deleted.Should().Be(1);
        List<ActiveSession> remaining = await _dbContext.ActiveSessions.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesRevokedSessions()
    {
        ActiveSession revoked = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);
        revoked.Revoke();
        ActiveSession active = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);

        _dbContext.ActiveSessions.AddRange(revoked, active);
        await _dbContext.SaveChangesAsync();

        SessionPruningJob job = new(_dbContext, _timeProvider, NullLogger<SessionPruningJob>.Instance);
        int deleted = await job.ExecuteAsync();

        deleted.Should().Be(1);
        List<ActiveSession> remaining = await _dbContext.ActiveSessions.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDeleteActiveNonExpiredSessions()
    {
        ActiveSession active1 = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);
        ActiveSession active2 = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(8), _timeProvider);

        _dbContext.ActiveSessions.AddRange(active1, active2);
        await _dbContext.SaveChangesAsync();

        SessionPruningJob job = new(_dbContext, _timeProvider, NullLogger<SessionPruningJob>.Instance);
        int deleted = await job.ExecuteAsync();

        deleted.Should().Be(0);
        List<ActiveSession> remaining = await _dbContext.ActiveSessions.ToListAsync();
        remaining.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesBothExpiredAndRevokedSessions()
    {
        ActiveSession expired = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(1), _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(2));
        ActiveSession revoked = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);
        revoked.Revoke();
        ActiveSession active = ActiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromHours(4), _timeProvider);

        _dbContext.ActiveSessions.AddRange(expired, revoked, active);
        await _dbContext.SaveChangesAsync();

        SessionPruningJob job = new(_dbContext, _timeProvider, NullLogger<SessionPruningJob>.Instance);
        int deleted = await job.ExecuteAsync();

        deleted.Should().Be(2);
        List<ActiveSession> remaining = await _dbContext.ActiveSessions.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsZeroWhenNoSessionsExist()
    {
        SessionPruningJob job = new(_dbContext, _timeProvider, NullLogger<SessionPruningJob>.Instance);
        int deleted = await job.ExecuteAsync();

        deleted.Should().Be(0);
    }
}
