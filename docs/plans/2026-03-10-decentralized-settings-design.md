# Decentralized Settings and Custom Fields

> **Date:** 2026-03-10
> **Status:** Draft
> **Branch:** expansion

## Problem

The centralized Configuration module creates a scaling bottleneck. All tenant settings, feature flags, and custom field definitions live in one schema and one set of tables. As modules grow, this single point of contention limits database scalability and violates module autonomy.

## Decision

Retire the Configuration module. Each module owns its own settings and custom field definitions. Shared infrastructure in `Shared.Infrastructure` provides standardized base classes, repositories, and caching. The Identity module owns global (non-module) settings.

Development-level feature gating uses `Microsoft.FeatureManagement` via `appsettings.json`, enabling trunk-based development where incomplete modules and features ship behind gates controlled at deploy time. Tenant-level configuration uses the per-module settings system.

## Architecture

### Separation of Concerns

| Concern | Tool | Controlled by | Changed via |
|---------|------|---------------|-------------|
| Dev feature gates | `Microsoft.FeatureManagement` | Developers / DevOps | `appsettings.json`, deploy |
| Tenant feature toggles | Tenant settings (`custom.*`) | Tenant admins | API at runtime |
| User preferences | User settings | Users | API at runtime |
| Kill switches | Tenant settings (`system.*`) | Platform admins | API at runtime |

### Two Data Concerns, One Pattern

Each module manages two types of runtime configuration using the same shared infrastructure:

| Concern | Scope | Storage | Cache |
|---------|-------|---------|-------|
| Tenant/user settings | Per-module, two tables | Module's DB schema | Valkey |
| Custom field definitions | Per-module, tenant-scoped | Module's DB schema | Valkey |

### Resolution Chain

Settings resolve through a fallback chain, most specific first:

```
Module user setting  →  Module tenant setting  →  Code default
```

For global settings owned by Identity:

```
Global user setting  →  Global tenant setting  →  Code default
```

Modules can override global settings. A user's Billing date format overrides their global date format set in Identity.

DB rows only exist when someone explicitly sets a value. Unset keys resolve to the code-defined default. No seeding or initialization required.

---

## Development Feature Gates (Microsoft.FeatureManagement)

### Purpose

Enable trunk-based development. Incomplete modules and in-progress features merge to main behind feature gates. Gates are toggled at deploy time, not at runtime by tenants.

### Configuration

```json
{
    "FeatureManagement": {
        "Modules": {
            "Billing": true,
            "Showcases": false,
            "Inquiries": false
        },
        "Features": {
            "Billing.NewInvoiceExport": false,
            "Identity.SsoIntegration": false
        }
    }
}
```

### Usage

Feature gates control module registration and endpoint availability:

```csharp
// Module registration at startup
if (featureManager.IsEnabledAsync("Modules.Billing"))
{
    services.AddBillingModule();
}

// Within a module, gate a specific feature
[FeatureGate("Billing.NewInvoiceExport")]
public async Task<IResult> ExportInvoices() { ... }
```

### When to Use Each

| Scenario | Use |
|----------|-----|
| Module not yet ready for production | `FeatureManagement` - gate the entire module |
| Feature in progress within a module | `FeatureManagement` - gate the specific feature |
| Tenant A wants invoicing off | Tenant settings - `custom.invoicing_enabled = false` |
| Disable a failing service in production | Tenant settings - `system.payment_processing_enabled = false` |
| User prefers dark mode | User settings - `custom.theme = "dark"` |
| Rolling out a new API version | API versioning - ship v2, migrate tenants, retire v1 |

---

## Tenant and User Settings

### Defined in Code, Values in DB

Each module declares its settings as a strongly-typed class:

```csharp
public static class BillingSettingKeys
{
    public static readonly SettingDefinition<string> DefaultCurrency =
        new("default_currency", defaultValue: "USD", description: "Default currency for new invoices");

    public static readonly SettingDefinition<string> InvoicePrefix =
        new("invoice_prefix", defaultValue: "INV-", description: "Prefix for invoice numbers");

    public static readonly SettingDefinition<string> DateFormat =
        new("date_format", defaultValue: "YYYY-MM-DD", description: "Date display format");
}
```

Code-defined settings always have a default value. If neither the user nor the tenant has set a value, the code default is returned. No rows are created in the database until someone explicitly changes a setting.

### Key Namespacing

Settings keys follow three validation rules:

- **Code-defined keys** (e.g., `default_currency`, `date_format`) are validated against the module's strongly-typed definitions. Unknown keys are rejected.
- **Custom keys** use a `custom.` prefix (e.g., `custom.show_new_nav`, `custom.brand_color`) and bypass validation. Any value is accepted and stored as-is. These serve as frontend feature flags and tenant-scoped configuration.
- **System keys** use a `system.` prefix (e.g., `system.payment_processing_enabled`) and are restricted to platform admins. These serve as kill switches.

Maximum of **100 custom keys** per tenant per module. This limit is configurable.

### Two Tables Per Module

Tenant settings and user settings are separate tables. No nullable `UserId` columns.

```sql
-- {module}_tenant_settings
CREATE TABLE {schema}.tenant_settings (
    id         UUID PRIMARY KEY,
    tenant_id  UUID NOT NULL,
    key        VARCHAR(100) NOT NULL,
    value      TEXT NOT NULL,  -- JSON-serialized
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ,
    UNIQUE (tenant_id, key)
);

-- {module}_user_settings
CREATE TABLE {schema}.user_settings (
    id         UUID PRIMARY KEY,
    tenant_id  UUID NOT NULL,
    user_id    UUID NOT NULL,
    key        VARCHAR(100) NOT NULL,
    value      TEXT NOT NULL,  -- JSON-serialized
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ,
    UNIQUE (tenant_id, user_id, key)
);
```

### Resolution Chain

The settings service merges both tables into a single resolved object:

1. Check user settings for the key → found? Return it with `source: "user"`
2. Fall back to tenant settings → found? Return it with `source: "tenant"`
3. Fall back to code-defined default → Return it with `source: "default"`

The merged result is cached in Valkey as one object: `settings:{module}:{tenantId}:{userId}`

---

## Caching Strategy

### Valkey (Redis-Compatible)

All reads go through Valkey first. Cache is populated lazily on first request.

| Data | Key Pattern | TTL |
|------|-------------|-----|
| Tenant settings | `settings:{module}:{tenantId}` | 5 min |
| User settings (merged) | `settings:{module}:{tenantId}:{userId}` | 5 min |

### Invalidation

When a setting changes via a command handler, the handler publishes a Wolverine event (e.g., `TenantSettingChanged`, `UserSettingChanged`). A handler invalidates the relevant Valkey keys.

For user settings changes, invalidate only that user's cache key. For tenant settings changes, invalidate the tenant key and all user keys for that tenant (since the merged result changes).

---

## Custom Field Definitions

Custom field definitions move from the Configuration module into the modules that own the entity types. Billing owns Invoice and Payment custom fields. The existing shared infrastructure in `Shared.Kernel` (`CustomFieldType`, `CustomFieldRegistry`, etc.) remains unchanged.

Each module that supports custom fields adds the `CustomFieldDefinition` entity to its domain layer and the corresponding table to its schema.

---

## Global Settings (Identity Module)

The Identity module owns cross-cutting settings that apply outside any specific module: timezone, locale, date format, theme. These use the same two-table pattern (`tenant_settings` and `user_settings`) in Identity's schema.

Module-level settings override global settings when both define the same key. A user's Billing-specific date format takes precedence over their global date format set in Identity.

---

## API Surface (Per Module)

All endpoints require authentication. Tenant settings require admin authorization. System keys require platform admin authorization.

### Frontend Config (Read-Only)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/{module}/config` | Merged settings for the current user |

A single lightweight call for frontend page load. Returns resolved values with no metadata:

```json
{
    "settings": {
        "default_currency": "EUR",
        "date_format": "DD/MM/YYYY",
        "custom.show_new_nav": "true",
        "custom.brand_color": "#3B82F6",
        "system.payment_processing_enabled": "true"
    }
}
```

One Valkey hit. No source info, no descriptions - just the resolved values the frontend needs.

### Settings (Admin)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/api/{module}/settings/tenant` | List all tenant settings (admin) |
| `PUT` | `/api/{module}/settings/tenant` | Batch update tenant settings (admin) |
| `DELETE` | `/api/{module}/settings/tenant` | Batch delete custom/system keys (admin) |
| `GET` | `/api/{module}/settings/user` | List all user settings (merged with tenant defaults) |
| `PUT` | `/api/{module}/settings/user` | Batch update user settings |
| `DELETE` | `/api/{module}/settings/user` | Batch delete custom keys (user) |

**Delete rules:**
- Code-defined keys cannot be deleted (they always exist with at least their default value)
- `custom.*` keys can be deleted by tenant admins (tenant settings) or by the owning user (user settings)
- `system.*` keys can only be deleted by platform admins

**GET response shape (user settings, merged):**
```json
[
    {
        "key": "default_currency",
        "displayName": "Default Currency",
        "description": "Default currency for new invoices",
        "value": "EUR",
        "source": "user",
        "defaultValue": "USD"
    },
    {
        "key": "custom.show_new_nav",
        "value": "true",
        "source": "tenant"
    }
]
```

The `source` field indicates where the value came from: `"user"`, `"tenant"`, or `"default"`. Custom and system keys have no `displayName`, `description`, or `defaultValue` metadata.

**PUT request shape:**
```json
{
    "settings": [
        { "key": "default_currency", "value": "EUR" },
        { "key": "custom.show_new_nav", "value": "true" }
    ]
}
```

**DELETE request shape:**
```json
{
    "keys": ["custom.show_new_nav", "custom.old_feature"]
}
```

---

## Audit

The existing `AuditInterceptor` in `Shared.Infrastructure.Core` automatically captures every insert, update, and delete on all EF Core entities. It records the entity type, old/new values, UserId, TenantId, and timestamp. No additional audit infrastructure is needed.

---

## Shared Infrastructure Components

These live in `Shared.Infrastructure` and `Shared.Kernel`:

### Shared.Kernel
- `SettingDefinition<T>` - defines a setting's key, default value, type, and description
- `ISettingsService` - interface for reading merged settings

### Shared.Infrastructure
- `TenantSettingEntity` / `UserSettingEntity` - base EF Core entities
- `TenantSettingsRepository<TModule>` / `UserSettingsRepository<TModule>` - generic repositories
- `CachedSettingsService<TModule>` - Valkey-backed merged reads with lazy loading
- EF Core configurations for all entity types
- Key namespace validation logic (`custom.`, `system.`, code-defined)
- Custom key limit enforcement (100 per tenant per module)

### Per Module (added by module developers)
- Static class defining setting keys (e.g., `BillingSettingKeys`)
- DbContext registration of settings tables
- API controller inheriting from a shared base or using minimal APIs
- EF Core migration adding the tables to the module's schema

---

## Migration Path

### Phase 1: Build Shared Infrastructure
Create the base entities, repositories, services, caching layer, and key validation in `Shared.Infrastructure` and `Shared.Kernel`. Add `Microsoft.FeatureManagement` to the API host with initial module gates.

### Phase 2: Add Settings to Existing Modules
Each module adopts the pattern: define keys, register tables, add API endpoints. Start with one module (e.g., Billing) as a reference implementation.

### Phase 3: Migrate Custom Field Definitions
Move `CustomFieldDefinition` from Configuration into the modules that own entity types. Update references.

### Phase 4: Retire Configuration Module
Remove the Configuration module's projects, DbContext, migrations, and API controllers. Remove the module registration from the API host.

### Phase 5: Add Setting Encryption (Future)
Add encryption support for sensitive settings. Encrypted values are readable only by the owning user, not tenant admins. Specific keys opt into encryption via the `SettingDefinition<T>` declaration or a naming convention.

---

## What We Are Not Building

- **Custom feature flag system** - development gates use `Microsoft.FeatureManagement`; tenant toggles use settings
- **Percentage rollouts** - not needed; use API versioning for gradual rollout
- **A/B testing** - out of scope
- **Admin dashboard UI** - API only; frontend is a separate concern
- **Setting encryption** - deferred to Phase 5
