using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// T5.3 (Wallow-w6s6.5.3): OrganizationMember.Role must adopt the existing
/// <see cref="OrgMemberRole"/> enum, persisted as a string column (mirroring the
/// InvitationStatus enum-as-string convention in InvitationConfiguration). These tests
/// pin the EF model mapping that drives the column change (and its migration).
/// </summary>
public sealed class OrganizationMemberRoleMappingTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;

    public OrganizationMemberRoleMappingTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private IProperty RoleProperty()
    {
        IEntityType entityType = _dbContext.Model.FindEntityType(typeof(OrganizationMember))
            ?? throw new InvalidOperationException("OrganizationMember is not mapped.");
        return entityType.FindProperty(nameof(OrganizationMember.Role))
            ?? throw new InvalidOperationException("OrganizationMember.Role is not mapped.");
    }

    [Fact]
    public void Role_IsMappedAsOrgMemberRoleEnum()
    {
        RoleProperty().ClrType.Should().Be<OrgMemberRole>();
    }

    [Fact]
    public void Role_PersistsAsBoundedStringColumn()
    {
        IProperty role = RoleProperty();

        ValueConverter? converter = role.GetValueConverter();
        converter.Should().NotBeNull("the enum must persist as a string, per the InvitationStatus convention");
        converter!.ProviderClrType.Should().Be<string>();
        role.GetMaxLength().Should().Be(50);
        role.IsNullable.Should().BeFalse();
    }
}
