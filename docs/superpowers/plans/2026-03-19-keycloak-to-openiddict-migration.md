# Keycloak to OpenIddict Migration — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Keycloak with ASP.NET Core Identity + OpenIddict as Foundry's identity and authorization server. Eliminate the external JVM process, own the user data model in PostgreSQL, and modernize OAuth2 to Authorization Code + PKCE.

**Architecture:** Six chunks covering the migration in dependency order. Each chunk produces compilable code. The Identity module's Application layer interfaces are renamed/replaced, Infrastructure implementations swapped from Keycloak HTTP calls to in-process ASP.NET Core Identity + OpenIddict, and all Keycloak infrastructure removed.

**Tech Stack:** .NET 10, ASP.NET Core Identity, OpenIddict 6.x, EF Core 10, PostgreSQL, xUnit, NSubstitute, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-19-keycloak-to-openiddict-migration.md`

---

## Chunk Overview

| Chunk | Name | Tasks | Dependencies |
|-------|------|-------|-------------|
| 1 | Foundation (Entities + DB) | 1.1–1.5 | None |
| 2 | OpenIddict + Auth Pipeline | 2.1–2.4 | Chunk 1 |
| 3 | User + Organization Services | 3.1–3.4 | Chunk 2 |
| 4 | Service Accounts + DCR + SSO | 4.1–4.3 | Chunk 3 |
| 5 | SCIM Repointing + Middleware | 5.1–5.4 | Chunk 4 |
| 6 | Client Admin API + Cleanup + Tests | 6.1–6.5 | Chunk 5 |

---

## Chunk 1: Foundation (Entities + Database)

**Goal:** Add ASP.NET Core Identity user/role entities, Organization domain entity, OpenIddict EF Core stores, and generate the database migration. No behavioral changes yet — just the data model.

### Task 1.1: Add NuGet packages

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Foundry.Identity.Infrastructure.csproj`

- [ ] **Step 1: Add package versions to Directory.Packages.props**

Add these entries to the `<ItemGroup>` in `Directory.Packages.props`:

```xml
<PackageVersion Include="OpenIddict.AspNetCore" Version="6.3.0" />
<PackageVersion Include="OpenIddict.EntityFrameworkCore" Version="6.3.0" />
<PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.3" />
```

- [ ] **Step 2: Add package references to Identity.Infrastructure.csproj**

Replace the Keycloak package references:

Remove:
```xml
<PackageReference Include="Keycloak.AuthServices.Authentication" />
<PackageReference Include="Keycloak.AuthServices.Sdk" />
```

Add:
```xml
<PackageReference Include="OpenIddict.AspNetCore" />
<PackageReference Include="OpenIddict.EntityFrameworkCore" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
```

- [ ] **Step 3: Verify project compiles (will have errors — that's expected, just check package restore)**

```bash
dotnet restore src/Modules/Identity/Foundry.Identity.Infrastructure
```

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props src/Modules/Identity/Foundry.Identity.Infrastructure/Foundry.Identity.Infrastructure.csproj
git commit -m "chore(identity): swap Keycloak NuGet packages for OpenIddict + ASP.NET Core Identity"
```

---

### Task 1.2: Create FoundryUser and FoundryRole entities

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryUser.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryRole.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/FoundryUserTests.cs`

**Note:** `FoundryUser` extends `IdentityUser<Guid>` (from `Microsoft.AspNetCore.Identity`). This means the Domain layer needs a reference to the Identity package. Add `Microsoft.Extensions.Identity.Stores` to `Foundry.Identity.Domain.csproj` — this is the minimal package containing `IdentityUser<T>` without pulling in the full ASP.NET Core stack.

- [ ] **Step 1: Add package reference to Domain project**

Add to `src/Modules/Identity/Foundry.Identity.Domain/Foundry.Identity.Domain.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Identity.Stores" />
```

And to `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.Extensions.Identity.Stores" Version="10.0.3" />
```

- [ ] **Step 2: Write failing tests for FoundryUser**

```csharp
// tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/FoundryUserTests.cs
namespace Foundry.Identity.Domain.Tests.Entities;

public class FoundryUserTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        FoundryUser user = FoundryUser.Create(
            "jane@example.com", "Jane", "Doe");

        user.Email.Should().Be("jane@example.com");
        user.UserName.Should().Be("jane@example.com");
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Doe");
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        FoundryUser user = FoundryUser.Create("jane@example.com", "Jane", "Doe");

        user.Deactivate();

        user.IsActive.Should().BeFalse();
        user.DeactivatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        FoundryUser user = FoundryUser.Create("jane@example.com", "Jane", "Doe");
        user.Deactivate();

        user.Activate();

        user.IsActive.Should().BeTrue();
        user.DeactivatedAt.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Domain.Tests --filter "FoundryUserTests" -v n
```

Expected: FAIL — `FoundryUser` class does not exist.

- [ ] **Step 4: Implement FoundryUser**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryUser.cs
using Microsoft.AspNetCore.Identity;

namespace Foundry.Identity.Domain.Entities;

public sealed class FoundryUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    private FoundryUser() { } // EF Core

    public static FoundryUser Create(string email, string firstName, string lastName)
    {
        return new FoundryUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        DeactivatedAt = null;
    }
}
```

- [ ] **Step 5: Implement FoundryRole**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryRole.cs
using Microsoft.AspNetCore.Identity;

namespace Foundry.Identity.Domain.Entities;

public sealed class FoundryRole : IdentityRole<Guid>
{
    private FoundryRole() { } // EF Core

    public static FoundryRole Create(string name)
    {
        return new FoundryRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant()
        };
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Domain.Tests --filter "FoundryUserTests" -v n
```

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props \
    src/Modules/Identity/Foundry.Identity.Domain/Foundry.Identity.Domain.csproj \
    src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryUser.cs \
    src/Modules/Identity/Foundry.Identity.Domain/Entities/FoundryRole.cs \
    tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/FoundryUserTests.cs
git commit -m "feat(identity): add FoundryUser and FoundryRole Identity entities"
```

---

### Task 1.3: Create Organization domain entity

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Identity/OrganizationId.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Entities/Organization.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Entities/OrganizationMember.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Domain/Enums/OrgMemberRole.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/OrganizationTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/OrganizationTests.cs
namespace Foundry.Identity.Domain.Tests.Entities;

public class OrganizationTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        Guid creatorId = Guid.NewGuid();

        Organization org = Organization.Create("Acme Corp", "acme.com", tenantId, creatorId);

        org.Name.Should().Be("Acme Corp");
        org.Domain.Should().Be("acme.com");
        org.TenantId.Should().Be(tenantId);
        org.Members.Should().HaveCount(1);
        org.Members[0].Role.Should().Be(OrgMemberRole.Owner);
        org.Members[0].UserId.Should().Be(creatorId);
    }

    [Fact]
    public void AddMember_AddsWithMemberRole()
    {
        Organization org = Organization.Create("Acme", null, TenantId.New(), Guid.NewGuid());
        Guid newUserId = Guid.NewGuid();

        org.AddMember(newUserId);

        org.Members.Should().HaveCount(2);
        org.Members[1].UserId.Should().Be(newUserId);
        org.Members[1].Role.Should().Be(OrgMemberRole.Member);
    }

    [Fact]
    public void AddMember_DuplicateUserId_Throws()
    {
        Guid userId = Guid.NewGuid();
        Organization org = Organization.Create("Acme", null, TenantId.New(), userId);

        Action act = () => org.AddMember(userId);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void RemoveMember_RemovesUser()
    {
        Guid ownerId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();
        Organization org = Organization.Create("Acme", null, TenantId.New(), ownerId);
        org.AddMember(memberId);

        org.RemoveMember(memberId);

        org.Members.Should().HaveCount(1);
        org.Members[0].UserId.Should().Be(ownerId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Domain.Tests --filter "OrganizationTests" -v n
```

- [ ] **Step 3: Create OrganizationId**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Identity/OrganizationId.cs
namespace Foundry.Identity.Domain.Identity;

public readonly record struct OrganizationId(Guid Value) : IStronglyTypedId<OrganizationId>
{
    public static OrganizationId Create(Guid value) => new(value);
    public static OrganizationId New() => new(Guid.NewGuid());
}
```

- [ ] **Step 4: Create OrgMemberRole enum**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Enums/OrgMemberRole.cs
namespace Foundry.Identity.Domain.Enums;

public enum OrgMemberRole
{
    Member = 0,
    Admin = 1,
    Owner = 2
}
```

- [ ] **Step 5: Create OrganizationMember**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Entities/OrganizationMember.cs
namespace Foundry.Identity.Domain.Entities;

public sealed class OrganizationMember
{
    public Guid UserId { get; init; }
    public OrganizationId OrganizationId { get; init; }
    public OrgMemberRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; init; }

    private OrganizationMember() { } // EF Core

    internal static OrganizationMember Create(
        OrganizationId orgId, Guid userId, OrgMemberRole role)
    {
        return new OrganizationMember
        {
            OrganizationId = orgId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        };
    }

    public void ChangeRole(OrgMemberRole newRole)
    {
        Role = newRole;
    }
}
```

- [ ] **Step 6: Create Organization aggregate root**

```csharp
// src/Modules/Identity/Foundry.Identity.Domain/Entities/Organization.cs
namespace Foundry.Identity.Domain.Entities;

public sealed class Organization : AuditableEntity<OrganizationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string? Domain { get; private set; }

    private readonly List<OrganizationMember> _members = [];
    public IReadOnlyList<OrganizationMember> Members => _members.AsReadOnly();

    private Organization() { } // EF Core

    public static Organization Create(
        string name, string? domain, TenantId tenantId, Guid creatorUserId)
    {
        Organization org = new()
        {
            Id = OrganizationId.New(),
            TenantId = tenantId,
            Name = name,
            Domain = domain
        };
        org.SetCreated(DateTimeOffset.UtcNow, creatorUserId);

        // Creator becomes the owner
        org._members.Add(OrganizationMember.Create(org.Id, creatorUserId, OrgMemberRole.Owner));

        return org;
    }

    public void AddMember(Guid userId, OrgMemberRole role = OrgMemberRole.Member)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new BusinessRuleException("Identity.MemberAlreadyExists",
                "User is already a member of this organization");

        _members.Add(OrganizationMember.Create(Id, userId, role));
    }

    public void RemoveMember(Guid userId)
    {
        OrganizationMember? member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
            throw new BusinessRuleException("Identity.MemberNotFound",
                "User is not a member of this organization");

        if (member.Role == OrgMemberRole.Owner)
            throw new BusinessRuleException("Identity.CannotRemoveOwner",
                "Cannot remove the organization owner");

        _members.Remove(member);
    }

    public void UpdateName(string name)
    {
        Name = name;
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Domain.Tests --filter "OrganizationTests" -v n
```

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Domain/Identity/OrganizationId.cs \
    src/Modules/Identity/Foundry.Identity.Domain/Entities/Organization.cs \
    src/Modules/Identity/Foundry.Identity.Domain/Entities/OrganizationMember.cs \
    src/Modules/Identity/Foundry.Identity.Domain/Enums/OrgMemberRole.cs \
    tests/Modules/Identity/Foundry.Identity.Domain.Tests/Entities/OrganizationTests.cs
git commit -m "feat(identity): add Organization aggregate root with member management"
```

---

### Task 1.4: Update IdentityDbContext for Identity + OpenIddict + Organization

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Configurations/OrganizationMemberConfiguration.cs`

**Important:** `IdentityDbContext` currently inherits from `TenantAwareDbContext<IdentityDbContext>`. It needs to also inherit from `IdentityDbContext<FoundryUser, FoundryRole, Guid>` for ASP.NET Core Identity. Since C# doesn't support multiple inheritance, the approach is:

1. Change `IdentityDbContext` to inherit from `IdentityDbContext<FoundryUser, FoundryRole, Guid>` (from Microsoft.AspNetCore.Identity.EntityFrameworkCore)
2. Re-apply the tenant-aware behavior that `TenantAwareDbContext` provides (schema configuration, tenant query filters)

Check `TenantAwareDbContext` to understand what it provides before modifying.

- [ ] **Step 1: Read TenantAwareDbContext to understand what it provides**

```bash
cat src/Shared/Foundry.Shared.Infrastructure.Core/Persistence/TenantAwareDbContext.cs
```

Understand what methods/behaviors need to be preserved when switching the base class.

- [ ] **Step 2: Update IdentityDbContext to inherit from IdentityDbContext**

Update the class declaration in `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/IdentityDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Foundry.Identity.Domain.Entities;

public class IdentityDbContext : IdentityDbContext<FoundryUser, FoundryRole, Guid>
```

Re-apply tenant-aware behavior (schema, query filters) that was previously provided by `TenantAwareDbContext`. Preserve existing `OnModelCreating` logic including `ApplyConfigurationsFromAssembly`, `ApplyTenantQueryFilters`, and `ApplySettingsConfigurations`.

Add in `OnModelCreating`:
```csharp
// Configure Identity tables to use the identity schema
builder.Entity<FoundryUser>(b => b.ToTable("asp_net_users", "identity"));
builder.Entity<FoundryRole>(b => b.ToTable("asp_net_roles", "identity"));
builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("asp_net_user_roles", "identity"));
builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("asp_net_user_claims", "identity"));
builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("asp_net_user_logins", "identity"));
builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("asp_net_user_tokens", "identity"));
builder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("asp_net_role_claims", "identity"));

// FoundryUser custom columns
builder.Entity<FoundryUser>(b =>
{
    b.Property(u => u.FirstName).HasMaxLength(128).IsRequired();
    b.Property(u => u.LastName).HasMaxLength(128).IsRequired();
    b.Property(u => u.IsActive).HasDefaultValue(true);
    b.Property(u => u.CreatedAt).IsRequired();
});
```

Add OpenIddict to `OnModelCreating`:
```csharp
// OpenIddict tables in identity schema
builder.UseOpenIddict<Guid>();
```

Add new DbSets:
```csharp
public DbSet<Organization> Organizations => Set<Organization>();
public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
```

- [ ] **Step 3: Create Organization EF configuration**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs
namespace Foundry.Identity.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", "identity");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, v => OrganizationId.Create(v));
        builder.Property(o => o.TenantId)
            .HasConversion(id => id.Value, v => TenantId.Create(v));
        builder.Property(o => o.Name).HasMaxLength(256).IsRequired();
        builder.Property(o => o.Domain).HasMaxLength(256);
        builder.HasIndex(o => o.TenantId).IsUnique();
        builder.HasIndex(o => o.Domain).IsUnique()
            .HasFilter("\"Domain\" IS NOT NULL");

        builder.HasMany(o => o.Members)
            .WithOne()
            .HasForeignKey(m => m.OrganizationId)
            .HasPrincipalKey(o => o.Id);

        builder.Navigation(o => o.Members).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Note: The OrganizationId value converter is applied on both this entity's Id
        // and OrganizationMember's OrganizationId (in OrganizationMemberConfiguration).
        // EF Core resolves the FK relationship correctly when both sides have matching converters.
    }
}
```

- [ ] **Step 4: Create OrganizationMember EF configuration**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Configurations/OrganizationMemberConfiguration.cs
namespace Foundry.Identity.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members", "identity");
        builder.HasKey(m => new { m.OrganizationId, m.UserId });
        builder.Property(m => m.OrganizationId)
            .HasConversion(id => id.Value, v => OrganizationId.Create(v));
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(m => m.UserId);
    }
}
```

- [ ] **Step 5: Verify the project compiles**

```bash
dotnet build src/Modules/Identity/Foundry.Identity.Infrastructure
```

Note: There will be compilation errors from Keycloak-dependent code. That's expected — we're only verifying the new EF Core configuration compiles. If needed, temporarily comment out Keycloak references to verify.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/
git commit -m "feat(identity): update IdentityDbContext for ASP.NET Core Identity + OpenIddict + Organizations"
```

---

### Task 1.5: Generate EF Core migration

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Migrations/<timestamp>_AddIdentityAndOpenIddict.cs`

- [ ] **Step 1: Generate migration**

```bash
dotnet ef migrations add AddIdentityAndOpenIddict \
    --project src/Modules/Identity/Foundry.Identity.Infrastructure \
    --startup-project src/Foundry.Api \
    --context IdentityDbContext
```

Note: This may fail if Keycloak services can't resolve at startup. If so, temporarily register placeholder services in DI or use `IDesignTimeDbContextFactory<IdentityDbContext>`.

- [ ] **Step 2: Review generated migration**

Verify it creates:
- `asp_net_users`, `asp_net_roles`, `asp_net_user_roles`, `asp_net_user_claims`, `asp_net_user_logins`, `asp_net_user_tokens`, `asp_net_role_claims` tables
- `openiddict_applications`, `openiddict_authorizations`, `openiddict_scopes`, `openiddict_tokens` tables
- `organizations`, `organization_members` tables
- All in the `identity` schema

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Migrations/
git commit -m "feat(identity): add EF migration for Identity + OpenIddict + Organization tables"
```

---

## Chunk 2: OpenIddict + Auth Pipeline

**Goal:** Configure OpenIddict server, implement the Authorization/Token/Logout controllers, add Razor login pages, and wire up JWT validation. After this chunk, the new auth pipeline is functional.

### Task 2.1: Configure OpenIddict server in DI

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Options/IdentityServerOptions.cs`

- [ ] **Step 1: Create IdentityServerOptions**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Options/IdentityServerOptions.cs
namespace Foundry.Identity.Infrastructure.Options;

public sealed class IdentityServerOptions
{
    public const string SectionName = "Identity";

    public PasswordOptions Password { get; set; } = new();
    public LockoutOptions Lockout { get; set; } = new();
    public SigningKeyOptions SigningKey { get; set; } = new();

    public sealed class PasswordOptions
    {
        public int RequiredLength { get; set; } = 8;
        public bool RequireNonAlphanumeric { get; set; } = true;
    }

    public sealed class LockoutOptions
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public int DefaultLockoutMinutes { get; set; } = 15;
    }

    public sealed class SigningKeyOptions
    {
        public string Type { get; set; } = "Development"; // "Development" or "Certificate"
        public string? Path { get; set; }
        public string? Password { get; set; }
    }
}
```

- [ ] **Step 2: Replace Keycloak auth configuration with OpenIddict**

In `IdentityInfrastructureExtensions.cs`, replace the `AddKeycloakWebApiAuthentication` call and add OpenIddict + Identity configuration. Remove `AddKeycloakAdmin()` method entirely. Replace with:

```csharp
public static IServiceCollection AddIdentityServer(
    this IServiceCollection services, IConfiguration configuration)
{
    // ASP.NET Core Identity — use AddIdentityCore (not AddIdentity) to avoid
    // overriding the default authentication scheme. AddIdentity sets DefaultScheme
    // to cookies, which conflicts with OpenIddict's JWT validation scheme.
    services.AddIdentityCore<FoundryUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<FoundryRole>()
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

    // OpenIddict
    services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>();
        })
        .AddServer(options =>
        {
            options.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetLogoutEndpointUris("/connect/logout")
                .SetUserinfoEndpointUris("/connect/userinfo");

            options.AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange();

            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();

            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableLogoutEndpointPassthrough()
                .EnableUserinfoEndpointPassthrough();
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

    return services;
}
```

- [ ] **Step 3: Update authentication scheme in Program.cs**

In `src/Foundry.Api/Program.cs`, replace the Keycloak authentication setup:

Remove:
```csharp
services.AddKeycloakWebApiAuthentication(configuration, options => { ... }, "Keycloak");
```

Update authentication to use OpenIddict:
```csharp
services.AddAuthentication(options =>
{
    options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});
```

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Options/IdentityServerOptions.cs \
    src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs \
    src/Foundry.Api/Program.cs
git commit -m "feat(identity): configure OpenIddict server and ASP.NET Core Identity DI"
```

---

### Task 2.2: Implement OpenIddict AuthorizationController

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthorizationController.cs`

- [ ] **Step 1: Implement AuthorizationController**

```csharp
// src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthorizationController.cs
using System.Security.Claims;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Foundry.Identity.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    SignInManager<FoundryUser> signInManager,
    UserManager<FoundryUser> userManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // If the user is not authenticated, redirect to login page
        AuthenticateResult result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!result.Succeeded)
        {
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                });
        }

        FoundryUser user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("The user details cannot be retrieved.");

        ClaimsIdentity identity = new(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, $"{user.FirstName} {user.LastName}")
            .SetClaim("given_name", user.FirstName)
            .SetClaim("family_name", user.LastName);

        // Add roles
        IList<string> roles = await userManager.GetRolesAsync(user);
        identity.SetClaims(Claims.Role, [.. roles]);

        // Set scopes
        identity.SetScopes(request.GetScopes());
        identity.SetResources("foundry-api");
        identity.SetDestinations(GetDestinations);

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name or Claims.Email or "given_name" or "family_name":
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case "org_id" or "tenant_id":
                yield return Destinations.AccessToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthorizationController.cs
git commit -m "feat(identity): add OpenIddict AuthorizationController for auth code + PKCE flow"
```

---

### Task 2.3: Implement TokenController and LogoutController

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Api/Controllers/TokenController.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Api/Controllers/LogoutController.cs`

- [ ] **Step 1: Implement TokenController**

```csharp
// src/Modules/Identity/Foundry.Identity.Api/Controllers/TokenController.cs
using System.Security.Claims;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Foundry.Identity.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class TokenController(
    IOpenIddictApplicationManager applicationManager,
    SignInManager<FoundryUser> signInManager,
    UserManager<FoundryUser> userManager) : Controller
{
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            return await HandleAuthorizationCodeOrRefreshAsync();
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsAsync(request);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    private async Task<IActionResult> HandleAuthorizationCodeOrRefreshAsync()
    {
        AuthenticateResult result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        FoundryUser? user = await userManager.FindByIdAsync(
            result.Principal!.GetClaim(Claims.Subject)!);

        if (user is null || !user.IsActive)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user is no longer allowed to sign in."
                }));
        }

        ClaimsIdentity identity = new(result.Principal!.Claims,
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetDestinations(AuthorizationController.GetDestinations);

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleClientCredentialsAsync(OpenIddictRequest request)
    {
        object? application = await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("The application details cannot be retrieved.");

        ClaimsIdentity identity = new(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, request.ClientId);

        string? displayName = await applicationManager.GetDisplayNameAsync(application);
        identity.SetClaim(Claims.Name, displayName);

        // Add tenant claims for service accounts (sa-{tenantId}-{name} pattern)
        if (request.ClientId!.StartsWith("sa-", StringComparison.Ordinal))
        {
            string[] parts = request.ClientId.Split('-', 3);
            if (parts.Length >= 2 && Guid.TryParse(parts[1], out Guid tenantGuid))
            {
                identity.SetClaim("tenant_id", tenantGuid.ToString());
                identity.SetClaim("org_id", tenantGuid.ToString());
            }
        }

        identity.SetScopes(request.GetScopes());
        identity.SetResources("foundry-api");
        identity.SetDestinations(static claim => [Destinations.AccessToken]);

        return SignIn(new ClaimsPrincipal(identity),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
```

**Note:** `GetDestinations` in `AuthorizationController` needs to be made `internal static` so `TokenController` can reference it.

- [ ] **Step 2: Implement LogoutController**

```csharp
// src/Modules/Identity/Foundry.Identity.Api/Controllers/LogoutController.cs
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace Foundry.Identity.Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class LogoutController(
    SignInManager<FoundryUser> signInManager) : Controller
{
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Api/Controllers/TokenController.cs \
    src/Modules/Identity/Foundry.Identity.Api/Controllers/LogoutController.cs
git commit -m "feat(identity): add TokenController (auth code + client credentials) and LogoutController"
```

---

### Task 2.4: Add Razor login pages

**Files:**
- Create: `src/Foundry.Api/Pages/Account/Login.cshtml`
- Create: `src/Foundry.Api/Pages/Account/Login.cshtml.cs`
- Create: `src/Foundry.Api/Pages/Account/Logout.cshtml`
- Create: `src/Foundry.Api/Pages/Account/Logout.cshtml.cs`

- [ ] **Step 1: Enable Razor Pages in Program.cs**

Add to `Program.cs` service configuration:
```csharp
builder.Services.AddRazorPages();
```

Add to middleware pipeline (before `app.MapControllers()`):
```csharp
app.MapRazorPages();
```

- [ ] **Step 2: Create Login page model**

```csharp
// src/Foundry.Api/Pages/Account/Login.cshtml.cs
using System.ComponentModel.DataAnnotations;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Foundry.Api.Pages.Account;

public class LoginModel(SignInManager<FoundryUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");

        // Clear the existing external cookie
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Microsoft.AspNetCore.Identity.SignInResult result =
            await signInManager.PasswordSignInAsync(
                Input.Email, Input.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return Page();
    }
}
```

- [ ] **Step 3: Create Login Razor view**

```html
@* src/Foundry.Api/Pages/Account/Login.cshtml *@
@page
@model Foundry.Api.Pages.Account.LoginModel

<!DOCTYPE html>
<html>
<head>
    <title>Sign In — Foundry</title>
    <style>
        body { font-family: system-ui, sans-serif; max-width: 400px; margin: 80px auto; padding: 0 20px; }
        .form-group { margin-bottom: 16px; }
        label { display: block; margin-bottom: 4px; font-weight: 500; }
        input[type="email"], input[type="password"] { width: 100%; padding: 8px; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; }
        button { width: 100%; padding: 10px; background: #2563eb; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 16px; }
        button:hover { background: #1d4ed8; }
        .error { color: #dc2626; font-size: 14px; margin-bottom: 12px; }
    </style>
</head>
<body>
    <h1>Sign In</h1>
    <form method="post">
        <div asp-validation-summary="ModelOnly" class="error"></div>
        <div class="form-group">
            <label asp-for="Input.Email">Email</label>
            <input asp-for="Input.Email" autofocus />
            <span asp-validation-for="Input.Email" class="error"></span>
        </div>
        <div class="form-group">
            <label asp-for="Input.Password">Password</label>
            <input asp-for="Input.Password" />
            <span asp-validation-for="Input.Password" class="error"></span>
        </div>
        <button type="submit">Sign In</button>
        <input type="hidden" name="ReturnUrl" value="@Model.ReturnUrl" />
    </form>
</body>
</html>
```

- [ ] **Step 4: Create Logout page**

```csharp
// src/Foundry.Api/Pages/Account/Logout.cshtml.cs
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Foundry.Api.Pages.Account;

public class LogoutModel(SignInManager<FoundryUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await signInManager.SignOutAsync();
        return returnUrl is not null ? LocalRedirect(returnUrl) : RedirectToPage("/Account/Login");
    }
}
```

```html
@* src/Foundry.Api/Pages/Account/Logout.cshtml *@
@page
@model Foundry.Api.Pages.Account.LogoutModel

<!DOCTYPE html>
<html>
<head><title>Signed Out — Foundry</title></head>
<body>
    <h1>You have been signed out.</h1>
    <p><a href="/Account/Login">Sign in again</a></p>
</body>
</html>
```

- [ ] **Step 5: Commit**

```bash
git add src/Foundry.Api/Pages/ \
    src/Foundry.Api/Program.cs
git commit -m "feat(identity): add Razor login/logout pages for OpenIddict auth flow"
```

---

## Chunk 3: User + Organization Services

**Goal:** Replace `IKeycloakAdminService` and `IOrganizationService` with ASP.NET Core Identity-backed implementations. Rename interfaces to drop the Keycloak prefix.

### Task 3.1: Rename IKeycloakAdminService to IUserManagementService

**Files:**
- Rename: `src/Modules/Identity/Foundry.Identity.Application/Interfaces/IKeycloakAdminService.cs` → keep file, rename interface
- Modify: `src/Modules/Identity/Foundry.Identity.Api/Controllers/UsersController.cs` — update injection
- Modify: All files referencing `IKeycloakAdminService`

- [ ] **Step 1: Rename interface**

In `src/Modules/Identity/Foundry.Identity.Application/Interfaces/IKeycloakAdminService.cs`, rename the interface:

```csharp
public interface IUserManagementService
{
    // All existing methods stay the same
}
```

Rename the file to `IUserManagementService.cs`.

- [ ] **Step 2: Update all references**

Use IDE-assisted rename or find-and-replace across:
- `UsersController.cs` — change constructor parameter type
- `UserService.cs` (Shared.Contracts implementation) — change constructor parameter type
- `IdentityInfrastructureExtensions.cs` — update DI registration
- Any test files referencing the old interface

```bash
grep -rl "IKeycloakAdminService" src/ tests/ --include="*.cs"
```

Fix each reference.

- [ ] **Step 3: Verify compilation**

```bash
dotnet build src/Modules/Identity/
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(identity): rename IKeycloakAdminService to IUserManagementService"
```

---

### Task 3.2: Implement UserManagementService with ASP.NET Core Identity

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/UserManagementService.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/UserManagementServiceTests.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakAdminService.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/UserManagementServiceTests.cs
namespace Foundry.Identity.Tests.Infrastructure;

public class UserManagementServiceTests
{
    private readonly UserManager<FoundryUser> _userManager;
    private readonly RoleManager<FoundryRole> _roleManager;
    private readonly UserManagementService _service;
    private readonly IMediator _mediator;

    public UserManagementServiceTests()
    {
        // Setup UserManager and RoleManager mocks using NSubstitute
        IUserStore<FoundryUser> userStore = Substitute.For<IUserStore<FoundryUser>>();
        _userManager = Substitute.For<UserManager<FoundryUser>>(
            userStore, null, null, null, null, null, null, null, null);
        IRoleStore<FoundryRole> roleStore = Substitute.For<IRoleStore<FoundryRole>>();
        _roleManager = Substitute.For<RoleManager<FoundryRole>>(
            roleStore, null, null, null, null);
        _mediator = Substitute.For<IMediator>();

        _service = new UserManagementService(_userManager, _roleManager, _mediator);
    }

    [Fact]
    public async Task CreateUserAsync_ValidInput_ReturnsUserId()
    {
        _userManager.CreateAsync(Arg.Any<FoundryUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<FoundryUser>(), "user")
            .Returns(IdentityResult.Success);

        Guid result = await _service.CreateUserAsync("test@example.com", "Test", "User", "Password1!");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(
            Arg.Is<FoundryUser>(u => u.Email == "test@example.com"), "Password1!");
    }

    [Fact]
    public async Task GetUserByIdAsync_ExistingUser_ReturnsUserDto()
    {
        FoundryUser user = FoundryUser.Create("test@example.com", "Test", "User");
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "admin" });

        UserDto? result = await _service.GetUserByIdAsync(user.Id);

        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
        result.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task DeactivateUserAsync_ActiveUser_SetsIsActiveFalse()
    {
        FoundryUser user = FoundryUser.Create("test@example.com", "Test", "User");
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _service.DeactivateUserAsync(user.Id);

        user.IsActive.Should().BeFalse();
        await _userManager.Received(1).UpdateAsync(user);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "UserManagementServiceTests" -v n
```

- [ ] **Step 3: Implement UserManagementService**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Services/UserManagementService.cs
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Wolverine;

namespace Foundry.Identity.Infrastructure.Services;

public sealed class UserManagementService(
    UserManager<FoundryUser> userManager,
    RoleManager<FoundryRole> roleManager,
    IMessageBus messageBus) : IUserManagementService
{
    public async Task<Guid> CreateUserAsync(
        string email, string firstName, string lastName,
        string? password = null, CancellationToken ct = default)
    {
        FoundryUser user = FoundryUser.Create(email, firstName, lastName);

        IdentityResult result = password is not null
            ? await userManager.CreateAsync(user, password)
            : await userManager.CreateAsync(user);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, "user");

        await messageBus.PublishAsync(new UserRegisteredEvent(
            user.Id, user.Email!, user.FirstName, user.LastName));

        return user.Id;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        IList<string> roles = await userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        FoundryUser? user = await userManager.FindByEmailAsync(email);
        if (user is null) return null;

        IList<string> roles = await userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        string? search = null, int first = 0, int max = 20, CancellationToken ct = default)
    {
        IQueryable<FoundryUser> query = userManager.Users;

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.ToLowerInvariant();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(searchLower) ||
                u.FirstName.ToLower().Contains(searchLower) ||
                u.LastName.ToLower().Contains(searchLower));
        }

        List<FoundryUser> users = await query
            .Skip(first)
            .Take(max)
            .ToListAsync(ct);

        List<UserDto> dtos = [];
        foreach (FoundryUser user in users)
        {
            IList<string> roles = await userManager.GetRolesAsync(user);
            dtos.Add(MapToDto(user, roles));
        }

        return dtos;
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);
        user.Deactivate();
        await userManager.UpdateAsync(user);
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);
        user.Activate();
        await userManager.UpdateAsync(user);
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);

        if (!await roleManager.RoleExistsAsync(roleName))
            throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        await userManager.AddToRoleAsync(user, roleName);

        await messageBus.PublishAsync(new UserRoleChangedEvent(userId, roleName, "assigned"));
    }

    public async Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);
        await userManager.RemoveFromRoleAsync(user, roleName);

        await messageBus.PublishAsync(new UserRoleChangedEvent(userId, roleName, "removed"));
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);
        IList<string> roles = await userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser user = await FindUserOrThrowAsync(userId);
        await userManager.DeleteAsync(user);
    }

    private async Task<FoundryUser> FindUserOrThrowAsync(Guid userId)
    {
        FoundryUser? user = await userManager.FindByIdAsync(userId.ToString());
        return user ?? throw new InvalidOperationException($"User '{userId}' not found.");
    }

    private static UserDto MapToDto(FoundryUser user, IList<string> roles)
    {
        return new UserDto(
            user.Id, user.Email!, user.FirstName, user.LastName,
            user.IsActive, roles.ToList());
    }
}
```

- [ ] **Step 4: Update UserService (Shared.Contracts implementation)**

**Important:** This is `Shared.Contracts.Identity.IUserService` — the cross-module contract for user lookups. It is a **separate interface** from `IUserManagementService` (the Identity module's own interface). `UserService.cs` wraps `IUserManagementService` to satisfy the cross-module contract. Do not confuse the two.

In `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/UserService.cs`, change constructor:

```csharp
public class UserService(IUserManagementService userManagement) : IUserService
{
    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        UserDto? user = await userManagement.GetUserByIdAsync(userId, ct);
        if (user == null) return null;
        return new UserInfo(user.Id, user.Email, user.FirstName, user.LastName, user.Enabled);
    }

    public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        UserDto? user = await userManagement.GetUserByEmailAsync(email, ct);
        if (user == null) return null;
        return new UserInfo(user.Id, user.Email, user.FirstName, user.LastName, user.Enabled);
    }
}
```

- [ ] **Step 5: Delete KeycloakAdminService.cs**

```bash
rm src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakAdminService.cs
```

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "UserManagementServiceTests" -v n
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(identity): implement UserManagementService with ASP.NET Core Identity

Replaces KeycloakAdminService. User CRUD now uses UserManager<FoundryUser>
instead of Keycloak Admin API HTTP calls."
```

---

### Task 3.3: Rename IOrganizationService to IOrganizationService

**Files:**
- Rename: `src/Modules/Identity/Foundry.Identity.Application/Interfaces/IOrganizationService.cs` → `IOrganizationService.cs`
- Modify: All references

- [ ] **Step 1: Rename interface and file**

Same approach as Task 3.1. Rename `IOrganizationService` → `IOrganizationService` in the interface file and all references.

```bash
grep -rl "IOrganizationService" src/ tests/ --include="*.cs"
```

- [ ] **Step 2: Verify compilation**

```bash
dotnet build src/Modules/Identity/
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(identity): rename IOrganizationService to IOrganizationService"
```

---

### Task 3.4: Implement OrganizationService with EF Core

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OrganizationService.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Application/Interfaces/IOrganizationRepository.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Repositories/OrganizationRepository.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/OrganizationServiceTests.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakOrganizationService.cs`

- [ ] **Step 1: Create IOrganizationRepository**

```csharp
// src/Modules/Identity/Foundry.Identity.Application/Interfaces/IOrganizationRepository.cs
namespace Foundry.Identity.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct = default);
    Task<Organization?> GetByTenantIdAsync(TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> GetAllAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(Organization entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement OrganizationRepository**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/Repositories/OrganizationRepository.cs
namespace Foundry.Identity.Infrastructure.Persistence.Repositories;

internal sealed class OrganizationRepository(IdentityDbContext context) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken ct)
        => context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Organization?> GetByTenantIdAsync(TenantId tenantId, CancellationToken ct)
        => context.Organizations
            .Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Organization>> GetAllAsync(
        string? search, int first, int max, CancellationToken ct)
    {
        IQueryable<Organization> query = context.Organizations.Include(o => o.Members);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o => o.Name.ToLower().Contains(search.ToLower()));
        return await query.Skip(first).Take(max).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => await context.Organizations
            .Include(o => o.Members)
            .Where(o => o.Members.Any(m => m.UserId == userId))
            .ToListAsync(ct);

    public void Add(Organization entity) => context.Organizations.Add(entity);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
```

- [ ] **Step 3: Write failing tests for OrganizationService**

```csharp
// tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/OrganizationServiceTests.cs
namespace Foundry.Identity.Tests.Infrastructure;

public class OrganizationServiceTests
{
    private readonly IOrganizationRepository _repository = Substitute.For<IOrganizationRepository>();
    private readonly IUserManagementService _userManagement = Substitute.For<IUserManagementService>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly OrganizationService _service;

    public OrganizationServiceTests()
    {
        _service = new OrganizationService(_repository, _userManagement, _messageBus);
    }

    [Fact]
    public async Task CreateOrganizationAsync_ReturnsOrgId()
    {
        _userManagement.GetUserByEmailAsync("creator@test.com", Arg.Any<CancellationToken>())
            .Returns(new UserDto(Guid.NewGuid(), "creator@test.com", "Creator", "User", true, ["admin"]));

        Guid result = await _service.CreateOrganizationAsync("Acme Corp", "acme.com", "creator@test.com");

        result.Should().NotBeEmpty();
        _repository.Received(1).Add(Arg.Is<Organization>(o => o.Name == "Acme Corp"));
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_ExistingOrg_ReturnsDto()
    {
        Organization org = Organization.Create("Acme", null, TenantId.New(), Guid.NewGuid());
        _repository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        OrganizationDto? result = await _service.GetOrganizationByIdAsync(org.Id.Value);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Acme");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "OrganizationServiceTests" -v n
```

- [ ] **Step 5: Implement OrganizationService**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OrganizationService.cs
namespace Foundry.Identity.Infrastructure.Services;

public sealed class OrganizationService(
    IOrganizationRepository repository,
    IUserManagementService userManagement,
    IMessageBus messageBus) : IOrganizationService
{
    public async Task<Guid> CreateOrganizationAsync(
        string name, string? domain = null, string? creatorEmail = null,
        CancellationToken ct = default)
    {
        Guid creatorId = Guid.Empty;
        if (creatorEmail is not null)
        {
            UserDto? creator = await userManagement.GetUserByEmailAsync(creatorEmail, ct);
            creatorId = creator?.Id ?? Guid.Empty;
        }

        TenantId tenantId = TenantId.New();
        Organization org = Organization.Create(name, domain, tenantId, creatorId);
        repository.Add(org);
        await repository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationCreatedEvent(
            org.Id.Value, org.Name, org.Domain));

        return org.Id.Value;
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(
        Guid orgId, CancellationToken ct = default)
    {
        Organization? org = await repository.GetByIdAsync(OrganizationId.Create(orgId), ct);
        return org is null ? null : MapToDto(org);
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(
        string? search = null, int first = 0, int max = 20, CancellationToken ct = default)
    {
        IReadOnlyList<Organization> orgs = await repository.GetAllAsync(search, first, max, ct);
        return orgs.Select(MapToDto).ToList();
    }

    public async Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        Organization org = await repository.GetByIdAsync(OrganizationId.Create(orgId), ct)
            ?? throw new InvalidOperationException($"Organization '{orgId}' not found.");

        org.AddMember(userId);
        await repository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationMemberAddedEvent(orgId, userId));
    }

    public async Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        Organization org = await repository.GetByIdAsync(OrganizationId.Create(orgId), ct)
            ?? throw new InvalidOperationException($"Organization '{orgId}' not found.");

        org.RemoveMember(userId);
        await repository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new OrganizationMemberRemovedEvent(orgId, userId));
    }

    public async Task<IReadOnlyList<UserDto>> GetMembersAsync(
        Guid orgId, CancellationToken ct = default)
    {
        Organization org = await repository.GetByIdAsync(OrganizationId.Create(orgId), ct)
            ?? throw new InvalidOperationException($"Organization '{orgId}' not found.");

        List<UserDto> users = [];
        foreach (OrganizationMember member in org.Members)
        {
            UserDto? user = await userManagement.GetUserByIdAsync(member.UserId, ct);
            if (user is not null) users.Add(user);
        }
        return users;
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(
        Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<Organization> orgs = await repository.GetByUserIdAsync(userId, ct);
        return orgs.Select(MapToDto).ToList();
    }

    private static OrganizationDto MapToDto(Organization org)
    {
        return new OrganizationDto(org.Id.Value, org.Name, org.Domain, org.Members.Count);
    }
}
```

- [ ] **Step 6: Delete KeycloakOrganizationService.cs**

```bash
rm src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakOrganizationService.cs
```

- [ ] **Step 7: Run tests**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "OrganizationServiceTests" -v n
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(identity): implement OrganizationService with EF Core

Replaces KeycloakOrganizationService. Organization CRUD now uses local
PostgreSQL storage via Organization aggregate root."
```

---

## Chunk 4: Service Accounts + DCR + SSO

**Goal:** Replace `KeycloakServiceAccountService`, `KeycloakDeveloperAppService`, and `KeycloakSsoService` with OpenIddict-backed implementations.

### Task 4.1: Implement ServiceAccountService with OpenIddict

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OpenIddictServiceAccountService.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/OpenIddictServiceAccountServiceTests.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakServiceAccountService.cs`

The `IServiceAccountService` interface stays unchanged. The new implementation creates OpenIddict applications with `client_credentials` grant instead of Keycloak clients.

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/OpenIddictServiceAccountServiceTests.cs
namespace Foundry.Identity.Tests.Infrastructure;

public class OpenIddictServiceAccountServiceTests
{
    private readonly IOpenIddictApplicationManager _appManager = Substitute.For<IOpenIddictApplicationManager>();
    private readonly IServiceAccountRepository _repository = Substitute.For<IServiceAccountRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly OpenIddictServiceAccountService _service;

    public OpenIddictServiceAccountServiceTests()
    {
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        _service = new OpenIddictServiceAccountService(_appManager, _repository, _tenantContext);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesOpenIddictApp()
    {
        CreateServiceAccountRequest request = new("My Service", "Test", ["showcases.read"]);
        _appManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new object()); // OpenIddict returns opaque object

        ServiceAccountCreatedResult result = await _service.CreateAsync(request);

        result.ClientId.Should().StartWith("sa-11111111");
        result.ClientSecret.Should().NotBeNullOrEmpty();
        await _appManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(d => d.ClientId!.StartsWith("sa-")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        _repository.Received(1).Add(Arg.Any<ServiceAccountMetadata>());
    }

    [Fact]
    public async Task RevokeAsync_ExistingAccount_DeletesOpenIddictApp()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            TenantId.New(), "sa-test-svc", "Test", null, ["showcases.read"]);
        _repository.GetByIdAsync(metadata.Id, Arg.Any<CancellationToken>()).Returns(metadata);
        _appManager.FindByClientIdAsync("sa-test-svc", Arg.Any<CancellationToken>())
            .Returns(new object());

        await _service.RevokeAsync(metadata.Id);

        await _appManager.Received(1).DeleteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "OpenIddictServiceAccountServiceTests" -v n
```

- [ ] **Step 3: Implement OpenIddictServiceAccountService**

```csharp
// src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OpenIddictServiceAccountService.cs
using System.Security.Cryptography;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Foundry.Identity.Infrastructure.Services;

public sealed class OpenIddictServiceAccountService(
    IOpenIddictApplicationManager applicationManager,
    IServiceAccountRepository repository,
    ITenantContext tenantContext) : IServiceAccountService
{
    public async Task<ServiceAccountCreatedResult> CreateAsync(
        CreateServiceAccountRequest request, CancellationToken ct = default)
    {
        string slug = request.Name.ToLowerInvariant().Replace(' ', '-');
        string clientId = $"sa-{tenantContext.TenantId.Value}-{slug}";

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            DisplayName = request.Name,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        foreach (string scope in request.Scopes)
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await applicationManager.CreateAsync(descriptor, secret, ct);

        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            tenantContext.TenantId, clientId, request.Name,
            request.Description, request.Scopes);
        repository.Add(metadata);
        await repository.SaveChangesAsync(ct);

        return new ServiceAccountCreatedResult(
            metadata.Id.Value, clientId, secret,
            "/connect/token", request.Scopes.ToList());
    }

    public Task<IReadOnlyList<ServiceAccountDto>> ListAsync(CancellationToken ct = default)
        => repository.GetAllAsync(ct).ContinueWith(t =>
            (IReadOnlyList<ServiceAccountDto>)t.Result.Select(MapToDto).ToList(), ct);

    public async Task<ServiceAccountDto?> GetAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        return metadata is null ? null : MapToDto(metadata);
    }

    public async Task<SecretRotatedResult> RotateSecretAsync(
        ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata metadata = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Service account not found.");

        object? app = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct)
            ?? throw new InvalidOperationException("OpenIddict application not found.");

        string newSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await applicationManager.UpdateAsync(app, newSecret, ct);

        return new SecretRotatedResult(newSecret, DateTimeOffset.UtcNow);
    }

    public async Task UpdateScopesAsync(
        ServiceAccountMetadataId id, IEnumerable<string> scopes, CancellationToken ct = default)
    {
        ServiceAccountMetadata metadata = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Service account not found.");

        metadata.UpdateScopes(scopes);
        await repository.SaveChangesAsync(ct);

        // Update OpenIddict application permissions
        object? app = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct);
        if (app is not null)
        {
            OpenIddictApplicationDescriptor descriptor = new();
            await applicationManager.PopulateAsync(descriptor, app, ct);
            // Remove old scope permissions, add new ones
            descriptor.Permissions.RemoveWhere(p => p.StartsWith(Permissions.Prefixes.Scope));
            foreach (string scope in scopes)
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
            await applicationManager.UpdateAsync(app, descriptor, ct);
        }
    }

    public async Task RevokeAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata metadata = await repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Service account not found.");

        metadata.Revoke();
        await repository.SaveChangesAsync(ct);

        object? app = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct);
        if (app is not null)
            await applicationManager.DeleteAsync(app, ct);
    }

    private static ServiceAccountDto MapToDto(ServiceAccountMetadata m)
    {
        return new ServiceAccountDto(
            m.Id.Value, m.KeycloakClientId, m.Name, m.Description,
            m.Status.ToString(), m.Scopes.ToList(),
            m.LastUsedAt, m.CreatedAt);
    }
}
```

- [ ] **Step 4: Delete KeycloakServiceAccountService.cs**

```bash
rm src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakServiceAccountService.cs
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "OpenIddictServiceAccountServiceTests" -v n
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(identity): implement ServiceAccountService with OpenIddict

Service accounts now created as OpenIddict applications with
client_credentials grant. Replaces KeycloakServiceAccountService."
```

---

### Task 4.2: Implement DeveloperAppService with OpenIddict

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OpenIddictDeveloperAppService.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakDeveloperAppService.cs`

- [ ] **Step 1: Implement**

```csharp
public sealed class OpenIddictDeveloperAppService(
    IOpenIddictApplicationManager applicationManager) : IDeveloperAppService
{
    public async Task<DeveloperAppRegistrationResult> RegisterClientAsync(
        string clientId, string clientName, CancellationToken ct = default)
    {
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            DisplayName = clientName,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        };

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

        string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await applicationManager.CreateAsync(descriptor, secret, ct);

        return new DeveloperAppRegistrationResult(clientId, secret, null);
    }
}
```

- [ ] **Step 2: Delete KeycloakDeveloperAppService.cs, run tests, commit**

```bash
git add -A
git commit -m "feat(identity): implement DeveloperAppService with OpenIddict

Developer app registration now writes to OpenIddict application store
instead of Keycloak DCR endpoint."
```

---

### Task 4.3: Implement OidcFederationService (replace KeycloakSsoService)

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/OidcFederationService.cs`
- Modify: `src/Modules/Identity/Foundry.Identity.Application/Interfaces/ISsoService.cs` — remove SAML methods
- Modify: `src/Modules/Identity/Foundry.Identity.Api/Controllers/SsoController.cs` — remove SAML endpoints
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakSsoService.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakIdpService.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/SsoClaimsSyncService.cs`

- [ ] **Step 1: Remove SAML methods from ISsoService**

Remove:
- `SaveSamlConfigurationAsync`
- `GetSamlServiceProviderMetadataAsync`

- [ ] **Step 2: Remove SAML endpoints from SsoController**

Delete the `POST /sso/saml` and `GET /sso/saml/metadata` action methods.

- [ ] **Step 3: Implement OidcFederationService**

The OIDC federation service manages external IdP connections. Instead of creating Keycloak IdP instances, it:
- Stores OIDC config in `sso_configurations` table (same as before)
- Dynamically registers ASP.NET Core OIDC authentication schemes per tenant
- Uses `IOptionsMonitor<OpenIdConnectOptions>` for dynamic scheme management

- [ ] **Step 4: Delete Keycloak SSO/IdP service files, run tests, commit**

```bash
git add -A
git commit -m "feat(identity): implement OidcFederationService replacing KeycloakSsoService

OIDC federation now uses ASP.NET Core's dynamic authentication schemes.
SAML federation removed. Deletes KeycloakSsoService, KeycloakIdpService,
and SsoClaimsSyncService."
```

---

## Chunk 5: SCIM Repointing + Middleware + Keycloak Cleanup

**Goal:** Repoint SCIM services from Keycloak admin API to ASP.NET Core Identity, update middleware, and remove all remaining Keycloak code.

### Task 5.1: Repoint ScimUserService to UserManager

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/ScimUserService.cs`

- [ ] **Step 1: Update constructor dependencies**

Replace `IHttpClientFactory` + `KeycloakOptions` with `UserManager<FoundryUser>` + `IOrganizationService`.

- [ ] **Step 2: Reimplement SCIM user operations**

| SCIM Operation | Before (Keycloak) | After (Identity) |
|---|---|---|
| CreateUser | POST `/admin/realms/{realm}/users` | `UserManager.CreateAsync()` |
| UpdateUser | PUT `/admin/realms/{realm}/users/{id}` | `UserManager.UpdateAsync()` |
| DeleteUser | DELETE `/admin/realms/{realm}/users/{id}` | `UserManager.DeleteAsync()` |
| GetUser | GET `/admin/realms/{realm}/users/{id}` | `UserManager.FindByIdAsync()` |
| ListUsers | GET `/admin/realms/{realm}/users?search=` | EF Core query on `asp_net_users` |

Keep all `ScimSyncLog` audit logging unchanged.

- [ ] **Step 3: Run SCIM tests, commit**

```bash
git add -A
git commit -m "feat(identity): repoint ScimUserService from Keycloak to UserManager"
```

---

### Task 5.2: Repoint ScimGroupService to OrganizationService

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/ScimGroupService.cs`

- [ ] **Step 1: Update constructor and reimplement group operations**

Replace Keycloak HTTP calls with `IOrganizationService` calls. SCIM groups map to Organizations.

- [ ] **Step 2: Rename ScimToKeycloakTranslator to ScimAttributeMapper**

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Scim/ScimToKeycloakTranslator.cs` → rename to `ScimAttributeMapper.cs`

The current translator maps SCIM attributes to Keycloak user representation JSON (e.g., `userName` → Keycloak `username`, `name.givenName` → Keycloak `firstName`). After migration, map to `FoundryUser` properties instead:

| SCIM Attribute | Before (Keycloak JSON) | After (FoundryUser property) |
|---|---|---|
| `userName` | `username` | `UserName` / `Email` |
| `name.givenName` | `firstName` | `FirstName` |
| `name.familyName` | `lastName` | `LastName` |
| `active` | `enabled` | `IsActive` |
| `emails[0].value` | `email` | `Email` |

Rename the class and file, update the mapping methods to return `FoundryUser` property assignments instead of Keycloak JSON property names. Update all references in `ScimUserService` and `ScimGroupService`.

- [ ] **Step 3: Run tests, commit**

```bash
git add -A
git commit -m "feat(identity): repoint ScimGroupService from Keycloak to OrganizationService"
```

---

### Task 5.3: Reimplement UserQueryService with EF Core

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/UserQueryService.cs`

`UserQueryService` currently calls Keycloak's Admin API via HTTP client and depends on `KeycloakOptions`. It implements `Shared.Contracts.Identity.IUserQueryService` (cross-module contract for user counts and email lookup).

- [ ] **Step 1: Update constructor dependencies**

Replace `IHttpClientFactory`, `HybridCache`, `IOptions<KeycloakOptions>` with `UserManager<FoundryUser>`, `IOrganizationRepository`.

- [ ] **Step 2: Reimplement methods using EF Core**

```csharp
public sealed class UserQueryService(
    UserManager<FoundryUser> userManager,
    IOrganizationRepository orgRepository) : IUserQueryService
{
    public async Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        FoundryUser? user = await userManager.FindByIdAsync(userId.ToString());
        return user?.Email ?? throw new InvalidOperationException($"User '{userId}' not found.");
    }

    public async Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        Organization? org = await orgRepository.GetByTenantIdAsync(TenantId.Create(tenantId), ct);
        if (org is null) return 0;

        HashSet<Guid> memberIds = org.Members.Select(m => m.UserId).ToHashSet();
        return await userManager.Users
            .Where(u => memberIds.Contains(u.Id) && u.CreatedAt >= from && u.CreatedAt <= to)
            .CountAsync(ct);
    }

    public async Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        Organization? org = await orgRepository.GetByTenantIdAsync(TenantId.Create(tenantId), ct);
        if (org is null) return 0;

        HashSet<Guid> memberIds = org.Members.Select(m => m.UserId).ToHashSet();
        return await userManager.Users
            .Where(u => memberIds.Contains(u.Id) && u.IsActive)
            .CountAsync(ct);
    }

    public async Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        Organization? org = await orgRepository.GetByTenantIdAsync(TenantId.Create(tenantId), ct);
        if (org is null) return 0;

        return org.Members.Count;
    }
}
```

- [ ] **Step 3: Run tests, commit**

```bash
git add -A
git commit -m "feat(identity): reimplement UserQueryService with EF Core

Replaces Keycloak Admin API HTTP calls with UserManager and
OrganizationRepository queries."
```

---

### Task 5.4: Update TenantResolutionMiddleware and remove Keycloak remnants

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/KeycloakOptions.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakTokenService.cs`
- Modify: `src/Modules/Identity/Foundry.Identity.Application/Interfaces/` — delete `ITokenService.cs`
- Delete: `src/Modules/Identity/Foundry.Identity.Api/Controllers/AuthController.cs`

- [ ] **Step 1: Simplify TenantResolutionMiddleware**

Remove the Keycloak JSON fallback path for parsing the `organization` claim. Keep only the simple GUID parsing path. Update to read `org_id` claim:

```csharp
string? orgId = user.FindFirst("org_id")?.Value;
if (Guid.TryParse(orgId, out Guid orgGuid))
    tenantContext.SetTenant(TenantId.Create(orgGuid));
```

- [ ] **Step 2: Delete AuthController.cs and ITokenService.cs**

These are fully replaced by OpenIddict's `/connect/token` endpoint.

- [ ] **Step 3: Delete KeycloakOptions.cs and KeycloakTokenService.cs**

- [ ] **Step 4: Verify no remaining Keycloak references in src/**

```bash
grep -rl "Keycloak" src/ --include="*.cs" | grep -v "Migration"
```

Expected: zero results (migrations may reference old snapshot data — that's OK).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(identity): remove all Keycloak code

Deletes AuthController, ITokenService, KeycloakTokenService,
KeycloakOptions. Simplifies TenantResolutionMiddleware to parse
flat org_id claim."
```

---

## Chunk 6: Client Admin API + Docker + Tests + Final Cleanup

**Goal:** Add ClientsController admin API, remove Keycloak from Docker, update test infrastructure, seed development data, and verify everything compiles and tests pass.

### Task 6.1: Implement ClientsController

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Api/Controllers/ClientsController.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Api/Contracts/Requests/CreateClientRequest.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Api/Contracts/Responses/ClientResponse.cs`
- Create: `tests/Modules/Identity/Foundry.Identity.Tests/Api/ClientsControllerTests.cs`

- [ ] **Step 1: Create request/response contracts**

- [ ] **Step 2: Write failing tests for CRUD operations**

- [ ] **Step 3: Implement ClientsController**

Endpoints:
- `GET /api/v1/identity/clients` — list all clients
- `POST /api/v1/identity/clients` — create client
- `GET /api/v1/identity/clients/{id}` — get client details
- `PUT /api/v1/identity/clients/{id}` — update client
- `DELETE /api/v1/identity/clients/{id}` — delete client
- `POST /api/v1/identity/clients/{id}/rotate-secret` — rotate secret

All protected with `[HasPermission(PermissionType.AdminAccess)]`.

- [ ] **Step 4: Run tests, commit**

```bash
git add -A
git commit -m "feat(identity): add ClientsController for OAuth2 client management"
```

---

### Task 6.2: Update DI registration

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`

- [ ] **Step 1: Update all service registrations**

Replace:
```csharp
services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();
services.AddScoped<IOrganizationService, KeycloakOrganizationService>();
services.AddScoped<ITokenService, KeycloakTokenService>();
services.AddScoped<IServiceAccountService, KeycloakServiceAccountService>();
services.AddScoped<ISsoService, KeycloakSsoService>();
services.AddScoped<IDeveloperAppService, KeycloakDeveloperAppService>();
```

With:
```csharp
services.AddScoped<IUserManagementService, UserManagementService>();
services.AddScoped<IOrganizationService, OrganizationService>();
services.AddScoped<IOrganizationRepository, OrganizationRepository>();
services.AddScoped<IServiceAccountService, OpenIddictServiceAccountService>();
services.AddScoped<ISsoService, OidcFederationService>();
services.AddScoped<IDeveloperAppService, OpenIddictDeveloperAppService>();
```

Remove `ITokenService` registration entirely.

Remove all `AddHttpClient("KeycloakAdminClient")`, `AddHttpClient("KeycloakTokenClient")`, `AddHttpClient("KeycloakDcrClient")` registrations.

- [ ] **Step 2: Remove `KeycloakOptions` configuration**

Remove:
```csharp
services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(identity): update DI to register OpenIddict-backed services"
```

---

### Task 6.3: Remove Keycloak from Docker and config

**Files:**
- Modify: `docker/docker-compose.yml` — remove `keycloak` and `keycloak-setup` services
- Modify: `docker/.env` — remove `KEYCLOAK_*` variables
- Delete: `docker/keycloak/` — realm export, DCR setup script
- Modify: `src/Foundry.Api/appsettings.json` — remove `Identity:Keycloak` section, add new `Identity` config
- Modify: `src/Foundry.Api/appsettings.Development.json` — same changes
- Update: `CLAUDE.md` — remove Keycloak from local dev services table

- [ ] **Step 1: Remove Keycloak from docker-compose.yml**

Delete the `keycloak` and `keycloak-setup` service blocks.

- [ ] **Step 2: Remove Keycloak env vars from .env**

- [ ] **Step 3: Delete docker/keycloak/ directory**

```bash
rm -rf docker/keycloak/
```

- [ ] **Step 4: Update appsettings.json**

Remove `Identity:Keycloak` section. Add:
```json
"Identity": {
  "SigningKey": {
    "Type": "Development"
  }
}
```

- [ ] **Step 5: Update CLAUDE.md local dev table**

Remove the Keycloak Admin and Keycloak Realm rows from the services table.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: remove Keycloak from Docker, config, and documentation"
```

---

### Task 6.4: Update test infrastructure

**Files:**
- Modify: `tests/Foundry.Tests.Common/Foundry.Tests.Common.csproj` — remove `Testcontainers.Keycloak`
- Delete: `tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs`
- Modify: `tests/Modules/Identity/Foundry.Identity.IntegrationTests/Foundry.Identity.IntegrationTests.csproj` — remove `Testcontainers.Keycloak`
- Create: `tests/Foundry.Tests.Common/Fixtures/IdentityFixture.cs`
- Modify: `tests/Foundry.Tests.Common/Helpers/TestAuthHandler.cs` — update org claim format

- [ ] **Step 1: Remove Testcontainers.Keycloak from test projects**

Remove from both `.csproj` files:
```xml
<PackageReference Include="Testcontainers.Keycloak" />
```

Also remove from `Directory.Packages.props`.

- [ ] **Step 2: Delete KeycloakFixture.cs**

- [ ] **Step 3: Create IdentityFixture**

In-process test fixture that seeds test users and OAuth2 clients via `UserManager<FoundryUser>` and `IOpenIddictApplicationManager`. See spec Section 12.2 for the implementation.

- [ ] **Step 4: Update TestAuthHandler**

Update the `organization` claim to use the flat `org_id` format instead of Keycloak JSON.

- [ ] **Step 5: Update integration tests referencing KeycloakFixture**

```bash
grep -rl "KeycloakFixture" tests/ --include="*.cs"
```

Replace with `IdentityFixture`.

- [ ] **Step 6: Run full test suite**

```bash
dotnet test
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "test(identity): replace KeycloakFixture with in-process IdentityFixture

Integration tests now use in-process OpenIddict — no Docker containers needed."
```

---

### Task 6.5: Seed development data and update Identity CLAUDE.md

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Infrastructure/Data/IdentityDataSeeder.cs`
- Modify: `src/Modules/Identity/CLAUDE.md`

- [ ] **Step 1: Create IdentityDataSeeder**

Seeds on startup (development only):
- Default roles: `admin`, `manager`, `user`
- Default admin user: `admin@foundry.dev` / `Admin123!`
- Default OAuth2 client: `foundry-dev-client` (for development testing)

```csharp
public sealed class IdentityDataSeeder(
    UserManager<FoundryUser> userManager,
    RoleManager<FoundryRole> roleManager,
    IOpenIddictApplicationManager applicationManager)
{
    public async Task SeedAsync()
    {
        // Seed roles
        foreach (string roleName in new[] { "admin", "manager", "user" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(FoundryRole.Create(roleName));
        }

        // Seed admin user
        if (await userManager.FindByEmailAsync("admin@foundry.dev") is null)
        {
            FoundryUser admin = FoundryUser.Create("admin@foundry.dev", "Admin", "User");
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "admin");
        }

        // Seed dev OAuth2 client
        if (await applicationManager.FindByClientIdAsync("foundry-dev-client") is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "foundry-dev-client",
                ClientSecret = "foundry-dev-secret",
                DisplayName = "Foundry Development Client",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                },
                RedirectUris = { new Uri("http://localhost:5000/callback") }
            }, "foundry-dev-secret");
        }
    }
}
```

- [ ] **Step 2: Register seeder in Program.cs**

```csharp
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    IdentityDataSeeder seeder = scope.ServiceProvider.GetRequiredService<IdentityDataSeeder>();
    await seeder.SeedAsync();
}
```

- [ ] **Step 3: Update Identity CLAUDE.md**

Replace the module description to reflect OpenIddict instead of Keycloak. Remove all references to Keycloak Admin API, Keycloak OIDC, and Keycloak-specific patterns.

- [ ] **Step 4: Run full test suite and verify API starts**

```bash
dotnet test
dotnet run --project src/Foundry.Api
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(identity): add IdentityDataSeeder and update module documentation

Seeds default roles, admin user, and dev OAuth2 client on startup.
Updates Identity CLAUDE.md to reflect OpenIddict architecture."
```

---

## Task Dependencies Summary

```
Chunk 1: Foundation (Tasks 1.1–1.5)
  └─► Chunk 2: OpenIddict + Auth Pipeline (Tasks 2.1–2.4)
       └─► Chunk 3: User + Org Services (Tasks 3.1–3.4)
            └─► Chunk 4: Service Accounts + DCR + SSO (Tasks 4.1–4.3)
                 └─► Chunk 5: SCIM + Middleware + Cleanup (Tasks 5.1–5.3)
                      └─► Chunk 6: Admin API + Docker + Tests (Tasks 6.1–6.5)
```

All chunks are sequential — each depends on the previous. Within each chunk, tasks should be executed in order.

## Parallelizable Work

Within chunks, some tasks can run in parallel:
- **Chunk 1:** Tasks 1.2 and 1.3 can run in parallel (both create domain entities)
- **Chunk 3:** Tasks 3.1 and 3.3 can run in parallel (both are interface renames)
- **Chunk 4:** Tasks 4.1, 4.2, and 4.3 can run in parallel (independent service replacements)
- **Chunk 5:** Tasks 5.1 and 5.2 can run in parallel (independent SCIM service repointing)
- **Chunk 6:** Tasks 6.1, 6.3, and 6.4 can run in parallel (independent cleanup work)
