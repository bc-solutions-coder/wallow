using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OrganizationServiceGapTests : IDisposable
{
    private readonly IOrganizationRepository _orgRepo;
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly OrganizationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OrganizationServiceGapTests()
    {
        TenantContext tc = new(); tc.SetTenant(new TenantId(_tenantId));
        DbContextOptions<IdentityDbContext> opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(opts, dp);
        _dbContext.SetTenant(new TenantId(_tenantId));
        _orgRepo = Substitute.For<IOrganizationRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _sut = new OrganizationService(_orgRepo, _dbContext, _messageBus, tc, TimeProvider.System, NullLogger<OrganizationService>.Instance);
    }

    public void Dispose() { _dbContext.Dispose(); }

    [Fact]
    public async Task ArchiveAsync_Archives()
    {
        Guid actorId = Guid.NewGuid();
        Organization org = Organization.Create(new TenantId(_tenantId), "AO", "ao", Guid.NewGuid(), TimeProvider.System);
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns(org);
        await _sut.ArchiveAsync(Guid.NewGuid(), actorId);
        await _orgRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationArchivedEvent>(e => e.ArchivedBy == actorId));
    }

    [Fact]
    public async Task ArchiveAsync_NotFound_Throws()
    {
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns((Organization?)null);
        Func<Task> act = () => _sut.ArchiveAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReactivateAsync_Reactivates()
    {
        Guid actorId = Guid.NewGuid();
        Organization org = Organization.Create(new TenantId(_tenantId), "RO", "ro", Guid.NewGuid(), TimeProvider.System);
        org.Archive(Guid.NewGuid(), TimeProvider.System);
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns(org);
        await _sut.ReactivateAsync(Guid.NewGuid(), actorId);
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationReactivatedEvent>(e => e.ReactivatedBy == actorId));
    }

    [Fact]
    public async Task ReactivateAsync_NotFound_Throws()
    {
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns((Organization?)null);
        Func<Task> act = () => _sut.ReactivateAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_Deletes()
    {
        Organization org = Organization.Create(new TenantId(_tenantId), "DM", "dm", Guid.NewGuid(), TimeProvider.System);
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns(org);
        _dbContext.Organizations.Add(org); await _dbContext.SaveChangesAsync();
        await _sut.DeleteAsync(org.Id.Value, "DM");
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationDeletedEvent>(e => e.Name == "DM"));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns((Organization?)null);
        Func<Task> act = () => _sut.DeleteAsync(Guid.NewGuid(), "x");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSettings_Exists_ReturnsDto()
    {
        Guid oid = Guid.NewGuid();
        OrganizationSettings s = OrganizationSettings.Create(OrganizationId.Create(oid), new TenantId(_tenantId), true, false, 14, Guid.Empty, TimeProvider.System);
        _dbContext.OrganizationSettings.Add(s); await _dbContext.SaveChangesAsync();
        OrganizationSettingsDto? r = await _sut.GetSettingsAsync(oid);
        r.Should().NotBeNull(); r!.RequireMfa.Should().BeTrue(); r.MfaGracePeriodDays.Should().Be(14);
    }

    [Fact]
    public async Task GetSettings_None_ReturnsNull()
    {
        (await _sut.GetSettingsAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_Updates()
    {
        Guid oid = Guid.NewGuid();
        OrganizationSettings s = OrganizationSettings.Create(OrganizationId.Create(oid), new TenantId(_tenantId), false, true, 7, Guid.Empty, TimeProvider.System);
        _dbContext.OrganizationSettings.Add(s); await _dbContext.SaveChangesAsync();
        await _sut.UpdateSettingsAsync(oid, true, false, 30, Guid.NewGuid());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationSettingsUpdatedEvent>(e => e.RequireMfa && e.MfaGracePeriodDays == 30));
    }

    [Fact]
    public async Task UpdateSettings_None_Throws()
    {
        Func<Task> act = () => _sut.UpdateSettingsAsync(Guid.NewGuid(), true, false, 7, Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetBranding_Exists_Returns()
    {
        Guid oid = Guid.NewGuid();
        OrganizationBranding b = OrganizationBranding.Create(OrganizationId.Create(oid), new TenantId(_tenantId), "https://l.png", "#F00", "#0F0", Guid.Empty, TimeProvider.System);
        _dbContext.OrganizationBrandings.Add(b); await _dbContext.SaveChangesAsync();
        OrganizationBrandingDto? r = await _sut.GetBrandingAsync(oid);
        r.Should().NotBeNull(); r!.LogoUrl.Should().Be("https://l.png");
    }

    [Fact]
    public async Task GetBranding_None_ReturnsNull()
    {
        (await _sut.GetBrandingAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task UpdateBranding_New_Creates()
    {
        OrganizationBrandingDto r = await _sut.UpdateBrandingAsync(Guid.NewGuid(), "D", "https://n.png", "#AAA", Guid.NewGuid());
        r.LogoUrl.Should().Be("https://n.png");
    }

    [Fact]
    public async Task UpdateBranding_Existing_Updates()
    {
        Guid oid = Guid.NewGuid();
        OrganizationBranding b = OrganizationBranding.Create(OrganizationId.Create(oid), new TenantId(_tenantId), "https://o.png", "#000", "#FFF", Guid.Empty, TimeProvider.System);
        _dbContext.OrganizationBrandings.Add(b); await _dbContext.SaveChangesAsync();
        OrganizationBrandingDto r = await _sut.UpdateBrandingAsync(oid, null, "https://u.png", "#111", Guid.NewGuid());
        r.LogoUrl.Should().Be("https://u.png");
    }

    [Fact]
    public async Task UploadBrandingLogo_ReturnsPath()
    {
        Guid oid = Guid.NewGuid();
        string r = await _sut.UploadBrandingLogoAsync(oid, Stream.Null, "l.png", "image/png", Guid.NewGuid());
        r.Should().Contain(oid.ToString()); r.Should().Contain("l.png");
    }

    [Fact]
    public async Task GetMembers_WithMembers_ReturnsDtos()
    {
        Guid uid = Guid.NewGuid();
        WallowUser user = WallowUser.Create(_tenantId, "T", "M", "m@t.com", TimeProvider.System);
        typeof(WallowUser).GetProperty("Id")!.SetValue(user, uid);
        _dbContext.Users.Add(user); await _dbContext.SaveChangesAsync();
        Organization org = Organization.Create(new TenantId(_tenantId), "MO", "mo", Guid.NewGuid(), TimeProvider.System);
        org.AddMember(uid, "member", Guid.Empty, TimeProvider.System);
        _orgRepo.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>()).Returns(org);
        IReadOnlyList<UserDto> r = await _sut.GetMembersAsync(Guid.NewGuid());
        r.Should().HaveCount(1); r[0].Email.Should().Be("m@t.com");
    }
}
