using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence.Converters;
using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Infrastructure.Settings;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : TenantAwareDbContext<IdentityDbContext>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public DbSet<ServiceAccountMetadata> ServiceAccountMetadata => Set<ServiceAccountMetadata>();
    public DbSet<ApiScope> ApiScopes => Set<ApiScope>();
    public DbSet<SsoConfiguration> SsoConfigurations => Set<SsoConfiguration>();
    public DbSet<ScimConfiguration> ScimConfigurations => Set<ScimConfiguration>();
    public DbSet<ScimSyncLog> ScimSyncLogs => Set<ScimSyncLog>();
    public DbSet<TenantSettingEntity> TenantSettings => Set<TenantSettingEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantContext tenantContext,
        IDataProtectionProvider dataProtectionProvider) : base(options, tenantContext)
    {
        _dataProtectionProvider = dataProtectionProvider;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplySettingsConfigurations();

        IDataProtector protector = _dataProtectionProvider.CreateProtector("Foundry.Identity.SsoSecrets");
        EncryptedStringConverter encryptedStringConverter = new(protector);

        modelBuilder.Entity<SsoConfiguration>()
            .Property(s => s.OidcClientSecret)
            .HasConversion(encryptedStringConverter);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
