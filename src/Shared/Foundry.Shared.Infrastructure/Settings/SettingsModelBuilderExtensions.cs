using Microsoft.EntityFrameworkCore;

namespace Foundry.Shared.Infrastructure.Settings;

public static class SettingsModelBuilderExtensions
{
    public static ModelBuilder ApplySettingsConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantSettingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserSettingEntityConfiguration());
        return modelBuilder;
    }
}
