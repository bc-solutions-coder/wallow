using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Tests.Settings;

public class SettingEntitiesTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public void TenantSettingEntity_Constructor_SetsAllProperties()
    {
        TenantSettingEntity entity = new(_tenantId, "billing", "feature.enabled", "true");

        entity.TenantId.Should().Be(_tenantId);
        entity.ModuleKey.Should().Be("billing");
        entity.SettingKey.Should().Be("feature.enabled");
        entity.Value.Should().Be("true");
        entity.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void TenantSettingEntity_UpdateValue_ChangesValue()
    {
        TenantSettingEntity entity = new(_tenantId, "billing", "feature.enabled", "false");

        entity.UpdateValue("true");

        entity.Value.Should().Be("true");
        entity.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void TenantSettingEntity_CreatedAt_IsSetOnConstruction()
    {
        TenantSettingEntity entity = new(_tenantId, "billing", "some.key", "val");

        entity.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public void UserSettingEntity_Constructor_SetsAllProperties()
    {
        string userId = Guid.NewGuid().ToString("D");

        UserSettingEntity entity = new(_tenantId, userId, "identity", "theme", "dark");

        entity.TenantId.Should().Be(_tenantId);
        entity.UserId.Should().Be(userId);
        entity.ModuleKey.Should().Be("identity");
        entity.SettingKey.Should().Be("theme");
        entity.Value.Should().Be("dark");
        entity.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UserSettingEntity_UpdateValue_ChangesValue()
    {
        string userId = Guid.NewGuid().ToString("D");
        UserSettingEntity entity = new(_tenantId, userId, "identity", "theme", "light");

        entity.UpdateValue("dark");

        entity.Value.Should().Be("dark");
        entity.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UserSettingEntity_CreatedAt_IsSetOnConstruction()
    {
        string userId = Guid.NewGuid().ToString("D");
        UserSettingEntity entity = new(_tenantId, userId, "identity", "some.key", "val");

        entity.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public void TenantSettingId_New_GeneratesUniqueIds()
    {
        TenantSettingId id1 = TenantSettingId.New();
        TenantSettingId id2 = TenantSettingId.New();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void TenantSettingId_Create_PreservesValue()
    {
        Guid guid = Guid.NewGuid();
        TenantSettingId id = TenantSettingId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void UserSettingId_New_GeneratesUniqueIds()
    {
        UserSettingId id1 = UserSettingId.New();
        UserSettingId id2 = UserSettingId.New();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UserSettingId_Create_PreservesValue()
    {
        Guid guid = Guid.NewGuid();
        UserSettingId id = UserSettingId.Create(guid);

        id.Value.Should().Be(guid);
    }
}
