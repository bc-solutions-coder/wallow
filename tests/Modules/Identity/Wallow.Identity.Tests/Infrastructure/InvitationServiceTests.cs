using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Time.Testing;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class InvitationServiceTests : IDisposable
{
    private readonly IInvitationRepository _invRepo;
    private readonly IMessageBus _messageBus;
    private readonly IdentityDbContext _dbContext;
    private readonly InvitationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTimeProvider _tp;

    public InvitationServiceTests()
    {
        _invRepo = Substitute.For<IInvitationRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
        TenantContext tc = new(); tc.SetTenant(new TenantId(_tenantId));
        DbContextOptions<IdentityDbContext> opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(opts, dp);
        _dbContext.SetTenant(new TenantId(_tenantId));
        _sut = new InvitationService(_invRepo, _messageBus, tc, _tp, _dbContext);
    }

    public void Dispose() { _dbContext.Dispose(); }

    [Fact]
    public async Task CreateInvitation_PersistsAndPublishes()
    {
        Invitation r = await _sut.CreateInvitationAsync(_tenantId, "i@t.com", Guid.NewGuid());
        r.Email.Should().Be("i@t.com");
        r.Token.Should().NotBeNullOrEmpty();
        _invRepo.Received(1).Add(Arg.Any<Invitation>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<InvitationCreatedEvent>(e => e.Email == "i@t.com"));
    }

    [Fact]
    public async Task RevokeInvitation_WhenExists_Revokes()
    {
        Invitation inv = Invitation.Create(new TenantId(_tenantId), "r@t.com", DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), TimeProvider.System);
        _invRepo.GetByIdAsync(Arg.Any<InvitationId>(), Arg.Any<CancellationToken>()).Returns(inv);
        await _sut.RevokeInvitationAsync(inv.Id.Value, Guid.NewGuid());
        await _invRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeInvitation_NotFound_Throws()
    {
        _invRepo.GetByIdAsync(Arg.Any<InvitationId>(), Arg.Any<CancellationToken>()).Returns((Invitation?)null);
        Func<Task> act = () => _sut.RevokeInvitationAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetByToken_Delegates()
    {
        Invitation inv = Invitation.Create(new TenantId(_tenantId), "g@t.com", DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), TimeProvider.System);
        _invRepo.GetByTokenAsync("tk", Arg.Any<CancellationToken>()).Returns(inv);
        Invitation? r = await _sut.GetInvitationByTokenAsync("tk");
        r.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptInvitation_WhenExists_Accepts()
    {
        Invitation inv = Invitation.Create(new TenantId(_tenantId), "a@t.com", DateTimeOffset.UtcNow.AddDays(7), Guid.NewGuid(), TimeProvider.System);
        _invRepo.GetByTokenAsync(inv.Token, Arg.Any<CancellationToken>()).Returns(inv);
        await _sut.AcceptInvitationAsync(inv.Token, Guid.NewGuid());
        await _invRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptInvitation_NotFound_Throws()
    {
        _invRepo.GetByTokenAsync("bad", Arg.Any<CancellationToken>()).Returns((Invitation?)null);
        Func<Task> act = () => _sut.AcceptInvitationAsync("bad", Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CleanupExpired_MarksExpired()
    {
        Invitation expired = Invitation.Create(new TenantId(_tenantId), "e@t.com", _tp.GetUtcNow().AddDays(-1), Guid.NewGuid(), _tp);
        _dbContext.Invitations.Add(expired); await _dbContext.SaveChangesAsync();
        await _sut.CleanupExpiredAsync();
        Invitation? r = await _dbContext.Invitations.AsTracking().FirstOrDefaultAsync(i => i.Id == expired.Id);
        r!.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public async Task CleanupExpired_DoesNotMarkValid()
    {
        Invitation valid = Invitation.Create(new TenantId(_tenantId), "v@t.com", _tp.GetUtcNow().AddDays(7), Guid.NewGuid(), _tp);
        _dbContext.Invitations.Add(valid); await _dbContext.SaveChangesAsync();
        await _sut.CleanupExpiredAsync();
        Invitation? r = await _dbContext.Invitations.AsTracking().FirstOrDefaultAsync(i => i.Id == valid.Id);
        r!.Status.Should().Be(InvitationStatus.Pending);
    }
}
