# Configuration Module

## Module Responsibility

Owns feature flag management (with evaluation, overrides, and caching) and tenant-scoped custom field definitions. Feature flags are global; overrides provide tenant/user specificity. Custom fields allow tenants to extend entity schemas with JSONB-stored data.

## Layer Rules

- **Domain** (`Foundry.Configuration.Domain`): Aggregate roots (`FeatureFlag` with owned `VariantWeight` collection, `CustomFieldDefinition`), entities (`FeatureFlagOverride` with tenant/user/tenant+user factory methods), strongly-typed IDs (`FeatureFlagId`, `FeatureFlagOverrideId`, `CustomFieldDefinitionId`), enums (`FlagType`: Boolean/Percentage/Variant), value objects (`VariantWeight`), domain events (`FeatureFlagCreatedEvent`, `FeatureFlagUpdatedEvent`, `FeatureFlagDeletedEvent`, `FeatureFlagEvaluatedEvent`, `CustomFieldDefinitionCreatedEvent`, `CustomFieldDefinitionDeactivatedEvent`), and domain exceptions (`FeatureFlagException`, `CustomFieldException`). Domain depends only on `Shared.Kernel`.
- **Application** (`Foundry.Configuration.Application`): CQRS commands for feature flags (`CreateFeatureFlag`, `UpdateFeatureFlag`, `DeleteFeatureFlag`, `CreateOverride`, `DeleteOverride`) and custom fields (`CreateCustomFieldDefinition`, `UpdateCustomFieldDefinition`, `DeactivateCustomFieldDefinition`, `ReorderCustomFields`). Queries include `GetAllFlags`, `GetFlagByKey`, `GetOverridesForFlag`, `EvaluateFlag`, `GetCustomFieldDefinitions`, `GetCustomFieldDefinitionById`, `GetSupportedEntityTypes`. Defines `IFeatureFlagService`, `IFeatureFlagRepository`, `IFeatureFlagOverrideRepository`, `ICustomFieldDefinitionRepository`. Includes `CustomFieldValidator` service.
- **Infrastructure** (`Foundry.Configuration.Infrastructure`): `ConfigurationDbContext` (EF Core, `configuration` schema), entity configurations, repositories, `FeatureFlagService` (evaluation with override resolution, percentage rollout via MD5 hashing, weighted variant selection), `CachedFeatureFlagService` (decorator using `IDistributedCache` with 60s TTL), `CustomFieldIndexManager` (PostgreSQL GIN indexes for JSONB queries).
- **Api** (`Foundry.Configuration.Api`): `FeatureFlagsController` (admin CRUD + evaluate endpoints), `CustomFieldsController` (admin + user). Extension methods `AddConfigurationModule` / `InitializeConfigurationModuleAsync`.

## Key Patterns

- **Override unique constraint**: `FeatureFlagOverride` has a unique index on `(TenantId, FlagId)` (`ix_configuration_feature_flag_overrides_tenant_flag`) preventing duplicate tenant-level overrides per flag.
- **Override resolution priority**: User+tenant > user-only > tenant-only > default. Expired overrides (`ExpiresAt`) are filtered out.
- **Evaluate endpoint convention**: Two evaluation endpoints exist: `GET /api/v1/configuration/feature-flags/{key}/evaluate` (single flag, requires `ConfigurationRead` permission) and `GET /api/feature-flags/evaluate` (all flags, any authenticated user). The bulk endpoint bypasses versioned routing for simpler client consumption.
- **Cached evaluation**: `CachedFeatureFlagService` decorates `FeatureFlagService` with distributed cache (Valkey). Cache keys follow `ff:{flagKey}:{tenantId}:{userId}` pattern. Static `InvalidateAsync` method tracks and evicts cached entries by flag key.
- **Percentage rollout**: `hash(flagKey + userId) % 100` using MD5 for stable, deterministic bucketing.
- **Variant selection**: Weighted random selection using MD5 hash for logged-in users, `RandomNumberGenerator` for anonymous.
- **Custom field validation**: `FieldKey` must be snake_case (`^[a-z][a-z0-9_]*$`), max 50 chars. Entity types validated against `CustomFieldRegistry`. Options only for Dropdown/MultiSelect. Validation rules are type-aware (string length for text, min/max for numeric, date ranges for dates, regex patterns for text).
- **Nullable update pattern**: `UpdateCustomFieldDefinition` command uses nullable properties -- only non-null fields are applied, allowing partial updates without overwriting unchanged values.

## Dependencies

- **Depends on**: `Foundry.Shared.Kernel` (base entities, `ITenantScoped`, `CustomFieldType`, `CustomFieldRegistry`, `FieldValidationRules`, `CustomFieldOption`, Result pattern), `Foundry.Shared.Contracts` (no integration events published yet -- domain events only).
- **Depended on by**: `Foundry.Api` (registers module). Other modules consume custom field definitions via `CustomFieldRegistry` in `Shared.Kernel`.

## Constraints

- Feature flags are global (not tenant-scoped). Overrides provide tenant/user specificity.
- `CustomFieldDefinition` is tenant-scoped via `ITenantScoped`.
- Do not reference other modules. Use `Shared.Contracts` for cross-module communication.
- This module uses the `configuration` PostgreSQL schema.
- Custom field JSONB data lives on entities in other modules; this module only manages definitions.
