using System.Linq.Expressions;
using System.Reflection;
using OpenIddict.EntityFrameworkCore.Models;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence.Converters;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using AspNetIdentityDbContext = Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<
    Wallow.Identity.Domain.Entities.WallowUser,
    Wallow.Identity.Domain.Entities.WallowRole,
    System.Guid>;

namespace Wallow.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : AspNetIdentityDbContext
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    // ReSharper disable once InconsistentNaming — Field must be accessible for expression tree access in query filters
#pragma warning disable SA1401, CA1051, IDE1006, IDE0052
    private TenantId _tenantId;
#pragma warning restore SA1401, CA1051, IDE1006, IDE0052

    public DbSet<ServiceAccountMetadata> ServiceAccountMetadata => Set<ServiceAccountMetadata>();
    public DbSet<ApiScope> ApiScopes => Set<ApiScope>();
    public DbSet<SsoConfiguration> SsoConfigurations => Set<SsoConfiguration>();
    public DbSet<ScimConfiguration> ScimConfigurations => Set<ScimConfiguration>();
    public DbSet<ScimSyncLog> ScimSyncLogs => Set<ScimSyncLog>();
    public DbSet<TenantSettingEntity> TenantSettings => Set<TenantSettingEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<InitialAccessToken> InitialAccessTokens => Set<InitialAccessToken>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<OrganizationDomain> OrganizationDomains => Set<OrganizationDomain>();
    public DbSet<MembershipRequest> MembershipRequests => Set<MembershipRequest>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<OrganizationBranding> OrganizationBrandings => Set<OrganizationBranding>();

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        IDataProtectionProvider dataProtectionProvider) : base(options)
    {
        _tenantId = default;
        _dataProtectionProvider = dataProtectionProvider;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public void SetTenant(TenantId tenantId)
    {
        _tenantId = tenantId;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");

        builder.UseOpenIddict<Guid>();

        ConfigureIdentityTables(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        builder.ApplySettingsConfigurations();

        IDataProtector protector = _dataProtectionProvider.CreateProtector("Wallow.Identity.SsoSecrets");
        EncryptedStringConverter encryptedStringConverter = new(protector);

        builder.Entity<SsoConfiguration>()
            .Property(s => s.OidcClientSecret)
            .HasConversion(encryptedStringConverter);

        // Indexes for efficient token cleanup (pruning expired/revoked tokens)
        builder.Entity<OpenIddictEntityFrameworkCoreToken<Guid>>(b =>
        {
            b.HasIndex(t => t.ExpirationDate);
            b.HasIndex("Status", "ExpirationDate");
        });

        ApplyTenantQueryFilters(builder);
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WallowUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.TenantId).HasColumnName("tenant_id");
            b.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(256);
            b.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(256);
            b.Property(u => u.IsActive).HasColumnName("is_active");
            b.Property(u => u.CreatedAt).HasColumnName("created_at");
            b.Property(u => u.DeactivatedAt).HasColumnName("deactivated_at");
            b.Property(u => u.MfaEnabled).HasColumnName("mfa_enabled");
            b.Property(u => u.MfaMethod).HasColumnName("mfa_method").HasMaxLength(50);
            b.Property(u => u.TotpSecretEncrypted).HasColumnName("totp_secret_encrypted").HasMaxLength(512);
            b.Property(u => u.BackupCodesHash).HasColumnName("backup_codes_hash").HasMaxLength(1024);
            b.Property(u => u.Id).HasColumnName("id");
            b.Property(u => u.UserName).HasColumnName("user_name");
            b.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name");
            b.Property(u => u.Email).HasColumnName("email");
            b.Property(u => u.NormalizedEmail).HasColumnName("normalized_email");
            b.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
            b.Property(u => u.PasswordHash).HasColumnName("password_hash");
            b.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
            b.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            b.Property(u => u.PhoneNumber).HasColumnName("phone_number");
            b.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            b.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            b.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
            b.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
            b.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");

            b.HasIndex(u => new { u.TenantId, u.NormalizedEmail });
            b.HasIndex(u => new { u.TenantId, u.IsActive });
            b.HasIndex(u => new { u.TenantId, u.Id });
        });

        modelBuilder.Entity<WallowRole>(b =>
        {
            b.ToTable("roles");
            b.Property(r => r.TenantId).HasColumnName("tenant_id");
            b.Property(r => r.Id).HasColumnName("id");
            b.Property(r => r.Name).HasColumnName("name");
            b.Property(r => r.NormalizedName).HasColumnName("normalized_name");
            b.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp");

            b.HasIndex(r => new { r.TenantId, r.NormalizedName });
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(b =>
        {
            b.ToTable("user_roles");
            b.Property(ur => ur.UserId).HasColumnName("user_id");
            b.Property(ur => ur.RoleId).HasColumnName("role_id");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("user_claims");
            b.Property(uc => uc.Id).HasColumnName("id");
            b.Property(uc => uc.UserId).HasColumnName("user_id");
            b.Property(uc => uc.ClaimType).HasColumnName("claim_type");
            b.Property(uc => uc.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("user_logins");
            b.Property(ul => ul.LoginProvider).HasColumnName("login_provider");
            b.Property(ul => ul.ProviderKey).HasColumnName("provider_key");
            b.Property(ul => ul.ProviderDisplayName).HasColumnName("provider_display_name");
            b.Property(ul => ul.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("user_tokens");
            b.Property(ut => ut.UserId).HasColumnName("user_id");
            b.Property(ut => ut.LoginProvider).HasColumnName("login_provider");
            b.Property(ut => ut.Name).HasColumnName("name");
            b.Property(ut => ut.Value).HasColumnName("value");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b =>
        {
            b.ToTable("role_claims");
            b.Property(rc => rc.Id).HasColumnName("id");
            b.Property(rc => rc.RoleId).HasColumnName("role_id");
            b.Property(rc => rc.ClaimType).HasColumnName("claim_type");
            b.Property(rc => rc.ClaimValue).HasColumnName("claim_value");
        });
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
                MemberExpression property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

                ConstantExpression contextExpression = Expression.Constant(this);
                MemberExpression tenantIdField = Expression.Field(
                    contextExpression,
                    typeof(IdentityDbContext).GetField("_tenantId",
                        BindingFlags.NonPublic | BindingFlags.Instance)!);

                BinaryExpression equals = Expression.Equal(property, tenantIdField);
                LambdaExpression lambda = Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
