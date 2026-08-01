using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Who is told that somebody asked to join: the address the organization nominated, else the
/// active owners, else nobody — resolution never fails a request.
/// </summary>
public sealed class AccessRequestRecipientResolverTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly AccessRequestRecipientResolver _sut;
    private readonly Guid _orgId = Guid.NewGuid();

    public AccessRequestRecipientResolverTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtection = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dataProtection);
        _dbContext.SetTenant(new TenantId(_orgId));
        _sut = new AccessRequestRecipientResolver(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ResolveAsync_WithANominatedAddress_UsesItAlone()
    {
        await GivenSettingsAsync("access@acme.test");
        await GivenOwnerAsync("owner@acme.test");

        IReadOnlyList<string> recipients = await _sut.ResolveAsync(_orgId);

        recipients.Should().ContainSingle().Which.Should().Be("access@acme.test");
    }

    [Fact]
    public async Task ResolveAsync_WithNoNominatedAddress_FallsBackToTheOwners()
    {
        await GivenSettingsAsync(null);
        await GivenOwnerAsync("owner@acme.test");

        IReadOnlyList<string> recipients = await _sut.ResolveAsync(_orgId);

        recipients.Should().ContainSingle().Which.Should().Be("owner@acme.test");
    }

    [Fact]
    public async Task ResolveAsync_IgnoresAnOwnerWhoIsNotAnActiveMember()
    {
        await GivenSettingsAsync(null);
        WallowUser owner = await GivenUserAsync("suspended-owner@acme.test");
        Membership membership = Membership.Enroll(
            owner.Id, OrganizationId.Create(_orgId), Guid.NewGuid(), TimeProvider.System);
        membership.MarkOwner(true, Guid.NewGuid(), TimeProvider.System);
        membership.Suspend(Guid.NewGuid(), TimeProvider.System);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        IReadOnlyList<string> recipients = await _sut.ResolveAsync(_orgId);

        recipients.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WithNobodyToTell_YieldsNothingRatherThanThrowing()
    {
        IReadOnlyList<string> recipients = await _sut.ResolveAsync(_orgId);

        recipients.Should().BeEmpty();
    }

    private async Task GivenSettingsAsync(string? accessRequestEmail)
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            OrganizationId.Create(_orgId), new TenantId(_orgId), false, false, 0, Guid.NewGuid(), TimeProvider.System);
        settings.UpdateEnrollment(
            EnrollmentPolicy.RequestApproval, accessRequestEmail, null, Guid.NewGuid(), TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<WallowUser> GivenUserAsync(string email)
    {
        WallowUser user = WallowUser.Create("Grace", "Hopper", email, TimeProvider.System);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task GivenOwnerAsync(string email)
    {
        WallowUser owner = await GivenUserAsync(email);
        Membership membership = Membership.Enroll(
            owner.Id, OrganizationId.Create(_orgId), Guid.NewGuid(), TimeProvider.System);
        membership.MarkOwner(true, Guid.NewGuid(), TimeProvider.System);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();
    }
}
