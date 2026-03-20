using Wallow.Shared.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

public class SettingsModelBuilderExtensionsTests
{
    [Fact]
    public void ApplySettingsConfigurations_ReturnsModelBuilder_ForChaining()
    {
        ModelBuilder modelBuilder = new();

        ModelBuilder result = modelBuilder.ApplySettingsConfigurations();

        result.Should().BeSameAs(modelBuilder);
    }

    [Fact]
    public void ApplySettingsConfigurations_AppliesTenantSettingEntityConfiguration()
    {
        ModelBuilder modelBuilder = new();

        modelBuilder.ApplySettingsConfigurations();

        IMutableEntityType? entityType = modelBuilder.Model.FindEntityType(typeof(TenantSettingEntity));
        entityType.Should().NotBeNull();
    }

    [Fact]
    public void ApplySettingsConfigurations_AppliesUserSettingEntityConfiguration()
    {
        ModelBuilder modelBuilder = new();

        modelBuilder.ApplySettingsConfigurations();

        IMutableEntityType? entityType = modelBuilder.Model.FindEntityType(typeof(UserSettingEntity));
        entityType.Should().NotBeNull();
    }
}
