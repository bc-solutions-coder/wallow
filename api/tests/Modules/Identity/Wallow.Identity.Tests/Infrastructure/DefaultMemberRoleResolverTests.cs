using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The role a new member starts with, whichever join path brought them: the organization's
/// configured default, else the platform's baseline "user" role.
/// </summary>
public sealed class DefaultMemberRoleResolverTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly DefaultMemberRoleResolver _sut;
    private readonly Guid _orgId = Guid.NewGuid();

    public DefaultMemberRoleResolverTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        IDataProtectionProvider dataProtection = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dataProtection);
        _dbContext.SetTenant(new TenantId(_orgId));
        _sut = new DefaultMemberRoleResolver(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ResolveAsync_WithNoSettings_FallsBackToTheBaselineMemberRole()
    {
        Guid baselineId = await GivenRoleAsync("user", "USER");

        Guid resolved = await _sut.ResolveAsync(_orgId);

        resolved.Should().Be(baselineId);
    }

    [Fact]
    public async Task ResolveAsync_WithAConfiguredDefault_UsesIt()
    {
        await GivenRoleAsync("user", "USER");
        Guid configuredId = await GivenRoleAsync("member", "MEMBER");
        await GivenSettingsAsync(configuredId);

        Guid resolved = await _sut.ResolveAsync(_orgId);

        resolved.Should().Be(configuredId);
    }

    [Fact]
    public async Task ResolveAsync_WhenTheConfiguredRoleWasDeleted_FallsBackRatherThanAdmittingNobody()
    {
        Guid baselineId = await GivenRoleAsync("user", "USER");
        await GivenSettingsAsync(Guid.NewGuid());

        Guid resolved = await _sut.ResolveAsync(_orgId);

        resolved.Should().Be(baselineId);
    }

    [Fact]
    public async Task ResolveAsync_WithNoBaselineRoleSeeded_Throws()
    {
        Func<Task> act = () => _sut.ResolveAsync(_orgId);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    private async Task<Guid> GivenRoleAsync(string name, string normalizedName)
    {
        Guid id = Guid.NewGuid();
        _dbContext.Roles.Add(new WallowRole { Id = id, Name = name, NormalizedName = normalizedName });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    private async Task GivenSettingsAsync(Guid defaultRoleId)
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            OrganizationId.Create(_orgId), new TenantId(_orgId), false, false, 0, Guid.NewGuid(), TimeProvider.System);
        settings.UpdateEnrollment(
            EnrollmentPolicy.Open, null, defaultRoleId, Guid.NewGuid(), TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);
        await _dbContext.SaveChangesAsync();
    }
}
