# Phase 11: Configuration Module

**Scope:** Complete Configuration module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 94 source files, 44 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Entities/CustomFieldDefinition.cs | Aggregate root for tenant-scoped custom field definitions on entity types | Factory method `Create()` with validation; snake_case FieldKey regex; JSONB-stored validation rules and options; soft-delete via `Deactivate()`; type-aware validation rules (string length for text, min/max for numeric, date ranges, regex patterns) | Shared.Kernel (AggregateRoot, ITenantScoped, CustomFieldType, CustomFieldRegistry) | |
| 2 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Entities/FeatureFlag.cs | Aggregate root for platform-global feature flags (Boolean, Percentage, Variant) | Three static factory methods per flag type; owned VariantWeight collection; Update tracks changed properties; percentage validation 0-100; variant list validation; MarkDeleted raises domain event | Shared.Kernel (AggregateRoot), Domain enums/events/value objects | |
| 3 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Entities/FeatureFlagOverride.cs | Entity for overriding flag defaults per tenant, user, or tenant+user | Three factory methods: CreateForTenant, CreateForUser, CreateForTenantUser; optional ExpiresAt for temporary overrides; IsExpired check via TimeProvider | Shared.Kernel (Entity), FeatureFlagId | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 4 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Enums/FlagType.cs | Enum defining feature flag types | Boolean (0), Percentage (1), Variant (2) | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 5 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/CustomFieldDefinitionCreatedEvent.cs | Domain event raised when a custom field definition is created | Record with DefinitionId, TenantId, EntityType, FieldKey, DisplayName, FieldType | Shared.Kernel (DomainEvent) | |
| 6 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/CustomFieldDefinitionDeactivatedEvent.cs | Domain event raised when a custom field is deactivated | Record with DefinitionId, TenantId, EntityType, FieldKey | Shared.Kernel (DomainEvent) | |
| 7 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/FeatureFlagCreatedEvent.cs | Domain event raised when a feature flag is created | Record with FlagId, Key, FlagType | Shared.Kernel (DomainEvent) | |
| 8 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/FeatureFlagDeletedEvent.cs | Domain event raised when a feature flag is deleted | Record with FlagId, Key | Shared.Kernel (DomainEvent) | |
| 9 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/FeatureFlagEvaluatedEvent.cs | Domain event raised on flag evaluation for telemetry | Record with FlagKey, TenantId, UserId, Result, Reason, Timestamp | Shared.Kernel (DomainEvent) | |
| 10 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Events/FeatureFlagUpdatedEvent.cs | Domain event raised when a feature flag is updated | Record with FlagId, Key, ChangedProperties (comma-separated) | Shared.Kernel (DomainEvent) | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 11 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Exceptions/CustomFieldException.cs | Business rule exception for custom field violations | Extends BusinessRuleException with "Configuration.CustomField" category | Shared.Kernel (BusinessRuleException) | |
| 12 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Exceptions/FeatureFlagException.cs | Business rule exception for feature flag violations | Extends BusinessRuleException with "Configuration.FeatureFlag" category | Shared.Kernel (BusinessRuleException) | |

### Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Identity/CustomFieldDefinitionId.cs | Strongly-typed ID for CustomFieldDefinition | Readonly record struct with Create/New factory methods | Shared.Kernel (IStronglyTypedId) | |
| 14 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Identity/FeatureFlagId.cs | Strongly-typed ID for FeatureFlag | Readonly record struct with Create/New factory methods | Shared.Kernel (IStronglyTypedId) | |
| 15 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/Identity/FeatureFlagOverrideId.cs | Strongly-typed ID for FeatureFlagOverride | Readonly record struct with Create/New factory methods | Shared.Kernel (IStronglyTypedId) | |

### Value Objects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 16 | [ ] | src/Modules/Configuration/Foundry.Configuration.Domain/ValueObjects/VariantWeight.cs | Value object for A/B test variant with weighted distribution | Name (required) and Weight (non-negative) with equality via GetEqualityComponents | Shared.Kernel (ValueObject) | |

## Application Layer

### Commands / CreateCustomFieldDefinition

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionCommand.cs | Command + handler for creating custom field definitions | Checks duplicate FieldKey; creates via factory; sets optional description, required, validation rules, options; returns Result<CustomFieldDefinitionDto> | ICustomFieldDefinitionRepository, ITenantContext, ICurrentUserService | |
| 18 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/CreateCustomFieldDefinition/CreateCustomFieldDefinitionValidator.cs | FluentValidation for CreateCustomFieldDefinitionCommand | EntityType, FieldKey (alphanumeric+underscore), DisplayName required; max lengths; FieldType enum validation | FluentValidation | |

### Commands / DeactivateCustomFieldDefinition

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 19 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/DeactivateCustomFieldDefinition/DeactivateCustomFieldDefinitionCommand.cs | Command + handler for soft-deleting a custom field definition | Loads by ID, throws if not found, calls Deactivate(), saves | ICustomFieldDefinitionRepository | |
| 20 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/DeactivateCustomFieldDefinition/DeactivateCustomFieldDefinitionValidator.cs | FluentValidation for DeactivateCustomFieldDefinitionCommand | Id must not be empty | FluentValidation | |

### Commands / ReorderCustomFields

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 21 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/ReorderCustomFields/ReorderCustomFieldsCommand.cs | Command + handler for reordering custom fields by entity type | Loads all definitions for entity type; iterates FieldIdsInOrder setting DisplayOrder by index; batch update | ICustomFieldDefinitionRepository | |
| 22 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/ReorderCustomFields/ReorderCustomFieldsValidator.cs | FluentValidation for ReorderCustomFieldsCommand | FieldIdsInOrder not null/empty; EntityType required | FluentValidation | |

### Commands / UpdateCustomFieldDefinition

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 23 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/UpdateCustomFieldDefinition/UpdateCustomFieldDefinitionCommand.cs | Command + handler for partial updates to custom field definitions | Nullable update pattern: only non-null fields applied; ClearDescription flag for explicit null; loads by ID, throws if not found | ICustomFieldDefinitionRepository, ICurrentUserService | |
| 24 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Commands/UpdateCustomFieldDefinition/UpdateCustomFieldDefinitionValidator.cs | FluentValidation for UpdateCustomFieldDefinitionCommand | Id required; conditional validation for DisplayName, Description, DisplayOrder when provided | FluentValidation | |

### Contracts / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 25 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Contracts/DTOs/CustomFieldDefinitionDto.cs | DTO record for custom field definition; also defines EntityTypeDto | Immutable record with required properties; includes ValidationRules and Options | Shared.Kernel (CustomFieldType, FieldValidationRules, CustomFieldOption) | |
| 26 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Contracts/DTOs/CustomFieldDefinitionMapper.cs | Extension methods mapping CustomFieldDefinition entity to DTO | ToDto() and ToDtoList() methods; deserializes JSON validation rules and options | Domain entities | |

### Contracts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Contracts/ICustomFieldDefinitionRepository.cs | Repository interface for CustomFieldDefinition persistence | GetByIdAsync, GetByEntityTypeAsync (with includeInactive filter), FieldKeyExistsAsync, Add, Update, SaveChanges | Domain entities/identity | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 28 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Extensions/ApplicationExtensions.cs | DI registration for Configuration application layer | Registers FluentValidation validators from assembly | FluentValidation, Microsoft.Extensions.DependencyInjection | |

### FeatureFlags / Commands / CreateFeatureFlag

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagCommand.cs | Command record for creating a feature flag | Key, Name, Description, FlagType, DefaultEnabled, RolloutPercentage, Variants, DefaultVariant | Domain enums, DTOs | |
| 30 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagHandler.cs | Handler for creating feature flags | Checks duplicate key; dispatches to appropriate factory (Boolean/Percentage/Variant); invalidates cache; returns Result<FeatureFlagDto> | IFeatureFlagRepository, IDistributedCache | |
| 31 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateFeatureFlag/CreateFeatureFlagValidator.cs | FluentValidation for CreateFeatureFlagCommand | Key pattern (alphanumeric+dashes), Name required, FlagType enum, RolloutPercentage 0-100 when present | FluentValidation | |

### FeatureFlags / Commands / CreateOverride

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateOverride/CreateOverrideCommand.cs | Command record for creating a flag override | FlagId, TenantId, UserId, IsEnabled, Variant, ExpiresAt | None | |
| 33 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateOverride/CreateOverrideHandler.cs | Handler for creating flag overrides | Tenant mismatch check; requires TenantId or UserId; checks for existing override; dispatches to correct factory method; invalidates cache | IFeatureFlagRepository, IFeatureFlagOverrideRepository, IDistributedCache, ITenantContext | |
| 34 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/CreateOverride/CreateOverrideValidator.cs | FluentValidation for CreateOverrideCommand | FlagId required; at least one of TenantId/UserId; ExpiresAt must be future | FluentValidation, TimeProvider | |

### FeatureFlags / Commands / DeleteFeatureFlag

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteFeatureFlag/DeleteFeatureFlagCommand.cs | Command record for deleting a feature flag | Single Guid Id property | None | |
| 36 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteFeatureFlag/DeleteFeatureFlagHandler.cs | Handler for deleting feature flags | Loads flag by ID; calls MarkDeleted() for domain event; deletes from repo; invalidates cache | IFeatureFlagRepository, IDistributedCache | |
| 37 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteFeatureFlag/DeleteFeatureFlagValidator.cs | FluentValidation for DeleteFeatureFlagCommand | Id must not be empty | FluentValidation | |

### FeatureFlags / Commands / DeleteOverride

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 38 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteOverride/DeleteOverrideCommand.cs | Command record for deleting a flag override | Single Guid Id property | None | |
| 39 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteOverride/DeleteOverrideHandler.cs | Handler for deleting flag overrides | Loads override; looks up parent flag for cache key; deletes override; invalidates cache | IFeatureFlagOverrideRepository, IFeatureFlagRepository, IDistributedCache | |
| 40 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/DeleteOverride/DeleteOverrideValidator.cs | FluentValidation for DeleteOverrideCommand | Id must not be empty | FluentValidation | |

### FeatureFlags / Commands / UpdateFeatureFlag

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 41 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagCommand.cs | Command record for updating a feature flag | Id, Name, Description, DefaultEnabled, RolloutPercentage | None | |
| 42 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagHandler.cs | Handler for updating feature flags | Loads by ID; calls Update(); conditionally updates percentage for Percentage flags; invalidates cache | IFeatureFlagRepository, IDistributedCache | |
| 43 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Commands/UpdateFeatureFlag/UpdateFeatureFlagValidator.cs | FluentValidation for UpdateFeatureFlagCommand | Id required; Name required with max length; RolloutPercentage 0-100 when present | FluentValidation | |

### FeatureFlags / Contracts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 44 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Contracts/IFeatureFlagOverrideRepository.cs | Repository interface for FeatureFlagOverride persistence | GetByIdAsync, GetOverridesForFlagAsync, GetOverrideAsync (by flag+tenant+user), Add, Delete | Domain entities/identity | |
| 45 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Contracts/IFeatureFlagRepository.cs | Repository interface for FeatureFlag persistence | GetByIdAsync, GetByKeyAsync, GetAllAsync, Add, Update, Delete | Domain entities/identity | |
| 46 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Contracts/IFeatureFlagService.cs | Service interface for flag evaluation | IsEnabledAsync, GetVariantAsync, GetAllFlagsAsync -- all accept tenantId and optional userId | None | |

### FeatureFlags / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 47 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/DTOs/FeatureFlagDto.cs | DTO record for feature flag data transfer | Includes Id, Key, Name, Description, FlagType, DefaultEnabled, RolloutPercentage, Variants, DefaultVariant, timestamps | Domain enums | |
| 48 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/DTOs/FeatureFlagOverrideDto.cs | DTO record for feature flag override data transfer | Id, FlagId, TenantId, UserId, IsEnabled, Variant, ExpiresAt, CreatedAt | None | |
| 49 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/DTOs/FlagEvaluationResultDto.cs | DTO record for flag evaluation result | Key, IsEnabled, Variant | None | |
| 50 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/DTOs/VariantWeightDto.cs | DTO record for variant weight | Name and Weight | None | |

### FeatureFlags / Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 51 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Mappings/FeatureFlagMappings.cs | Extension methods mapping domain entities to DTOs | ToDto() for FeatureFlag (maps variants) and FeatureFlagOverride | Domain entities, DTOs | |

### FeatureFlags / Queries / EvaluateFlag

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 52 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/EvaluateFlag/EvaluateFlagQuery.cs | Query record for evaluating a single flag | Key, TenantId, optional UserId | None | |
| 53 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/EvaluateFlag/EvaluateFlagHandler.cs | Handler that evaluates a flag via IFeatureFlagService | Calls IsEnabledAsync and GetVariantAsync; returns FlagEvaluationResultDto wrapped in Result | IFeatureFlagService | |

### FeatureFlags / Queries / GetAllFlags

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 54 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetAllFlags/GetAllFlagsQuery.cs | Query record for retrieving all feature flags | Empty record (no parameters) | None | |
| 55 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetAllFlags/GetAllFlagsHandler.cs | Handler that retrieves all flags from repository | Loads all flags, maps to DTOs, returns Result | IFeatureFlagRepository | |

### FeatureFlags / Queries / GetFlagByKey

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 56 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetFlagByKey/GetFlagByKeyQuery.cs | Query record for retrieving a flag by key | Single Key string property | None | |
| 57 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetFlagByKey/GetFlagByKeyHandler.cs | Handler that retrieves a flag by key | Loads by key; returns NotFound if null; maps to DTO | IFeatureFlagRepository | |

### FeatureFlags / Queries / GetOverridesForFlag

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 58 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetOverridesForFlag/GetOverridesForFlagQuery.cs | Query record for retrieving overrides for a flag | Single FlagId Guid property | None | |
| 59 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/FeatureFlags/Queries/GetOverridesForFlag/GetOverridesForFlagHandler.cs | Handler that retrieves overrides filtered by caller's tenant | Loads all overrides for flag; filters to caller's tenant via ITenantContext; maps to DTOs | IFeatureFlagOverrideRepository, ITenantContext | |

### Queries (Custom Fields)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 60 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Queries/GetCustomFieldDefinitionById.cs | Query + handler for retrieving a custom field definition by ID | Loads by strongly-typed ID; returns nullable DTO | ICustomFieldDefinitionRepository | |
| 61 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Queries/GetCustomFieldDefinitions.cs | Query + handler for listing custom field definitions by entity type | Supports includeInactive filter; returns list of DTOs | ICustomFieldDefinitionRepository | |
| 62 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Queries/GetSupportedEntityTypes.cs | Query + handler for listing entity types that support custom fields | Reads from CustomFieldRegistry static configuration; maps to EntityTypeDto list | Shared.Kernel (CustomFieldRegistry) | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 63 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Services/CustomFieldValidator.cs | Validates entity custom fields against tenant field definitions | Type validation (number, date, boolean, email, URL); rule validation (min/max length, numeric range, date range, regex pattern); options validation for Dropdown/MultiSelect; handles JsonElement normalization | ICustomFieldDefinitionRepository, ITenantContext, CustomFieldRegistry | |

### Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 64 | [ ] | src/Modules/Configuration/Foundry.Configuration.Application/Telemetry/ConfigurationModuleTelemetry.cs | OpenTelemetry instrumentation for Configuration module | ActivitySource and Meter for "Configuration"; ReadsTotal counter | Shared.Kernel (Diagnostics) | |

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 65 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Extensions/ConfigurationModuleExtensions.cs | Module DI registration and initialization | AddConfigurationModule registers DbContext, repos, FeatureFlagService with CachedFeatureFlagService decorator; InitializeConfigurationModuleAsync runs migrations in dev | Application/Infrastructure services, EF Core | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 66 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Configurations/CustomFieldDefinitionConfiguration.cs | EF Core entity configuration for CustomFieldDefinition | Table "custom_field_definitions"; JSONB columns for validation_rules and options; unique index on (TenantId, EntityType, FieldKey); composite index on (TenantId, EntityType, IsActive) | EF Core, Domain entities/identity | |
| 67 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Configurations/FeatureFlagConfiguration.cs | EF Core entity configuration for FeatureFlag | Table "feature_flags"; unique index on Key; OwnsMany for Variants (separate table "feature_flag_variants"); cascade delete to Overrides | EF Core, Domain entities | |
| 68 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Configurations/FeatureFlagOverrideConfiguration.cs | EF Core entity configuration for FeatureFlagOverride | Table "feature_flag_overrides"; unique index on (TenantId, FlagId) named "ix_configuration_feature_flag_overrides_tenant_flag"; indexes on TenantId, UserId, composite (FlagId, TenantId, UserId) | EF Core, Domain entities | |

### Persistence

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 69 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/ConfigurationDbContext.cs | EF Core DbContext for Configuration module | Extends TenantAwareDbContext; "configuration" schema; DbSets for CustomFieldDefinitions, FeatureFlags, FeatureFlagOverrides; NoTracking default; applies tenant query filters | Shared.Infrastructure (TenantAwareDbContext) | |
| 70 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/ConfigurationDbContextFactory.cs | Design-time factory for EF Core migrations | Creates DbContext with dummy connection string and inner DesignTimeTenantContext class for migration tooling | EF Core Design, ITenantContext | |
| 71 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/DesignTimeTenantContext.cs | Standalone mock ITenantContext for design-time use | Returns placeholder TenantId (Guid.Empty), IsResolved=true | Shared.Kernel (ITenantContext, RegionConfiguration) | |

### Persistence / Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 72 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Repositories/CustomFieldDefinitionRepository.cs | EF Core repository for CustomFieldDefinition | AsTracking for GetById; filters by EntityType with optional inactive; ordered by DisplayOrder then DisplayName; FieldKeyExistsAsync via AnyAsync | ConfigurationDbContext | |
| 73 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Repositories/FeatureFlagOverrideRepository.cs | EF Core repository for FeatureFlagOverride with expiration filtering | ActiveOverrides() base query filters out expired entries; GetOverrideAsync matches on flagId+tenantId+userId; Add/Delete save immediately | ConfigurationDbContext, TimeProvider | |
| 74 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Repositories/FeatureFlagRepository.cs | EF Core repository for FeatureFlag with compiled queries | Compiled async queries for GetById and GetByKey (includes Overrides); GetAllAsync ordered by Key; Add/Update/Delete save immediately | ConfigurationDbContext | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 75 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Services/CachedFeatureFlagService.cs | Distributed cache decorator for IFeatureFlagService | 60-second TTL; cache key pattern "ff:{prefix}:{flagKey}:{tenantId}:{userId}"; ConcurrentDictionary tracks keys per flag for invalidation; GetAllFlagsAsync passes through uncached | IFeatureFlagService, IDistributedCache | |
| 76 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Services/CustomFieldIndexManager.cs | Manages dynamic PostgreSQL GIN indexes for JSONB custom field queries | Creates/drops expression indexes on custom_fields JSONB column; strict SQL identifier validation via regex; CONCURRENTLY for online index creation | EF Core (DbContext.Database), ILogger | |
| 77 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Services/FeatureFlagService.cs | Core feature flag evaluation service | Override resolution priority: user+tenant > user-only > tenant-only; percentage rollout via MD5 hash bucketing; weighted variant selection via MD5 (logged-in) or RandomNumberGenerator (anonymous); publishes FeatureFlagEvaluatedEvent via Wolverine | IFeatureFlagRepository, IMessageBus, TimeProvider | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 78 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260216170543_Initial.cs | Initial migration creating configuration schema tables | Creates feature_flags, feature_flag_overrides, custom_field_definitions tables | EF Core Migrations | |
| 79 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260216170543_Initial.Designer.cs | Designer file for Initial migration | Auto-generated model snapshot for migration | EF Core Migrations | |
| 80 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260222195830_VariantsToOwnedTable.cs | Migration moving variants to owned entity table | Creates feature_flag_variants table for OwnsMany relationship | EF Core Migrations | |
| 81 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260222195830_VariantsToOwnedTable.Designer.cs | Designer file for VariantsToOwnedTable migration | Auto-generated model snapshot | EF Core Migrations | |
| 82 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260303074243_AddFeatureFlagOverrideUniqueConstraint.cs | Migration adding unique constraint on overrides | Adds unique index ix_configuration_feature_flag_overrides_tenant_flag on (TenantId, FlagId) | EF Core Migrations | |
| 83 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/20260303074243_AddFeatureFlagOverrideUniqueConstraint.Designer.cs | Designer file for AddFeatureFlagOverrideUniqueConstraint migration | Auto-generated model snapshot | EF Core Migrations | |
| 84 | [ ] | src/Modules/Configuration/Foundry.Configuration.Infrastructure/Migrations/ConfigurationDbContextModelSnapshot.cs | Current model snapshot for ConfigurationDbContext | Auto-generated snapshot of all entity configurations | EF Core Migrations | |

## Api Layer

### Contracts / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 85 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Enums/ApiFlagType.cs | API-layer enum for feature flag types | Boolean (0), Percentage (1), Variant (2) -- mirrors domain FlagType | None | |

### Contracts / Requests

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 86 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Requests/CreateFeatureFlagRequest.cs | API request record for creating a feature flag | Key, Name, Description, ApiFlagType, DefaultEnabled, RolloutPercentage, Variants, DefaultVariant | Application DTOs (VariantWeightDto) | |
| 87 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Requests/CreateOverrideRequest.cs | API request record for creating a flag override | TenantId, UserId, IsEnabled, Variant, ExpiresAt | None | |
| 88 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Requests/UpdateFeatureFlagRequest.cs | API request record for updating a feature flag | Name, Description, DefaultEnabled, RolloutPercentage | None | |

### Contracts / Responses

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 89 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Responses/FeatureFlagOverrideResponse.cs | API response record for flag override | Id, FlagId, TenantId, UserId, IsEnabled, Variant, ExpiresAt, CreatedAt | None | |
| 90 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Responses/FeatureFlagResponse.cs | API response record for feature flag | Id, Key, Name, Description, ApiFlagType, DefaultEnabled, RolloutPercentage, Variants, DefaultVariant, timestamps | Application DTOs (VariantWeightDto) | |
| 91 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Contracts/Responses/FlagEvaluationResponse.cs | API response record for flag evaluation result | Key, IsEnabled, Variant | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 92 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Controllers/CustomFieldsController.cs | REST controller for custom field CRUD operations | Versioned route "api/v1/configuration/custom-fields"; requires ConfigurationManage permission; endpoints: GetEntityTypes, GetByEntityType, GetById, Create, Update, Deactivate, Reorder; includes inline request DTOs (CreateCustomFieldRequest, UpdateCustomFieldRequest, ReorderFieldsRequest) | Wolverine IMessageBus, Application commands/queries | |
| 93 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Controllers/FeatureFlagsController.cs | REST controller for feature flag admin + evaluation | Versioned route "api/v1/configuration/feature-flags"; admin CRUD (ConfigurationManage permission); evaluate endpoints (ConfigurationRead for single, any auth for bulk); maps between Api and Application DTOs | Wolverine IMessageBus, IFeatureFlagService, ITenantContext, ICurrentUserService | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 94 | [ ] | src/Modules/Configuration/Foundry.Configuration.Api/Mappings/EnumMappings.cs | Bidirectional mapping between ApiFlagType and domain FlagType | ToDomain() and ToApi() extension methods with exhaustive switch | Api enums, Domain enums | |

## Test Files

### Domain Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 95 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/CustomFieldDefinitionTests.cs | Unit tests for CustomFieldDefinition entity | Create, validation, update, deactivate, activate, options, validation rules | |
| 96 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/CustomFieldExceptionTests.cs | Unit tests for CustomFieldException | Exception creation and message formatting | |
| 97 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/DomainEventTests.cs | Unit tests for domain events | Event creation and property initialization | |
| 98 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/FeatureFlagOverrideTests.cs | Unit tests for FeatureFlagOverride entity | Factory methods (tenant, user, tenant+user), expiration check | |
| 99 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/FeatureFlagTests.cs | Unit tests for FeatureFlag entity | Create Boolean/Percentage/Variant, Update, UpdatePercentage, SetVariants, MarkDeleted | |
| 100 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/IdentityTests.cs | Unit tests for strongly-typed IDs | Create, New, equality for all ID types | |
| 101 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Domain/VariantWeightTests.cs | Unit tests for VariantWeight value object | Construction, validation, equality | |

### Application / Handlers Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 102 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/CreateCustomFieldDefinitionHandlerTests.cs | Handler tests for CreateCustomFieldDefinition | Happy path, duplicate key conflict, optional fields | |
| 103 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/CreateFeatureFlagHandlerTests.cs | Handler tests for CreateFeatureFlag | Boolean/Percentage/Variant creation, duplicate key | |
| 104 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/CreateOverrideHandlerTests.cs | Handler tests for CreateOverride | Tenant/user/tenant+user overrides, tenant mismatch, duplicate | |
| 105 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/DeactivateCustomFieldDefinitionHandlerTests.cs | Handler tests for DeactivateCustomFieldDefinition | Deactivation, not found | |
| 106 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/DeleteFeatureFlagHandlerTests.cs | Handler tests for DeleteFeatureFlag | Deletion, not found, cache invalidation | |
| 107 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/DeleteOverrideHandlerTests.cs | Handler tests for DeleteOverride | Deletion, not found, cache invalidation | |
| 108 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetAllFlagsHandlerTests.cs | Handler tests for GetAllFlags | Returns all flags as DTOs | |
| 109 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetCustomFieldDefinitionByIdHandlerTests.cs | Handler tests for GetCustomFieldDefinitionById | Found, not found | |
| 110 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetCustomFieldDefinitionsHandlerTests.cs | Handler tests for GetCustomFieldDefinitions | By entity type, include/exclude inactive | |
| 111 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetFlagByKeyHandlerTests.cs | Handler tests for GetFlagByKey | Found, not found | |
| 112 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetOverridesForFlagHandlerTests.cs | Handler tests for GetOverridesForFlag | Filtered by tenant | |
| 113 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/GetSupportedEntityTypesHandlerTests.cs | Handler tests for GetSupportedEntityTypes | Returns registered entity types | |
| 114 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/ReorderCustomFieldsHandlerTests.cs | Handler tests for ReorderCustomFields | Reordering, field not found | |
| 115 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/UpdateCustomFieldDefinitionHandlerTests.cs | Handler tests for UpdateCustomFieldDefinition | Partial updates, not found | |
| 116 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Handlers/UpdateFeatureFlagHandlerTests.cs | Handler tests for UpdateFeatureFlag | Update, not found, percentage update | |

### Application / Mappings Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 117 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Mappings/CustomFieldDefinitionMapperTests.cs | Unit tests for CustomFieldDefinitionMapper | ToDto, ToDtoList mapping correctness | |
| 118 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Mappings/FeatureFlagMappingsTests.cs | Unit tests for FeatureFlagMappings | ToDto for flags and overrides | |

### Application / Services Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 119 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Services/CustomFieldValidatorTests.cs | Unit tests for CustomFieldValidator | Type validation, rule validation, option validation, required fields | |
| 120 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Application/Services/FeatureFlagServiceTests.cs | Unit tests for FeatureFlagService | IsEnabled, GetVariant, override resolution, percentage rollout, variant selection | |

### Api Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 121 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Contracts/RequestContractTests.cs | Contract tests for API request records | Request record construction and properties | |
| 122 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Contracts/ResponseContractTests.cs | Contract tests for API response records | Response record construction and properties | |
| 123 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Controllers/CustomFieldsControllerTests.cs | Controller tests for CustomFieldsController | CRUD endpoints, authorization | |
| 124 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Controllers/FeatureFlagsControllerTests.cs | Controller tests for FeatureFlagsController | CRUD, override, evaluate endpoints | |
| 125 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Extensions/ResultExtensionsTests.cs | Tests for Result-to-ActionResult extension methods | Success/failure mapping to HTTP responses | |
| 126 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Api/Mappings/EnumMappingsTests.cs | Tests for ApiFlagType/FlagType enum mappings | ToDomain, ToApi round-trip | |

### Infrastructure Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 127 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/CachedFeatureFlagServiceTests.cs | Unit tests for CachedFeatureFlagService | Cache hit/miss, TTL, invalidation | |
| 128 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/ConfigurationModuleExtensionsTests.cs | Tests for module DI registration | Service registration verification | |
| 129 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/CustomFieldIndexManagerTests.cs | Tests for dynamic JSONB index management | Create/drop index, identifier validation | |
| 130 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/FeatureFlagServiceTests.cs | Integration tests for FeatureFlagService | Override resolution, percentage rollout, variant selection | |

### Infrastructure / Persistence Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 131 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/ConfigurationDbContextFactoryTests.cs | Tests for design-time DbContext factory | Factory creates valid context | |
| 132 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/ConfigurationDbContextModelTests.cs | Tests for EF Core model configuration | Schema, table names, indexes, relationships | |
| 133 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/CustomFieldDefinitionRepositoryTests.cs | Integration tests for CustomFieldDefinitionRepository | CRUD operations, query filtering | |
| 134 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/DesignTimeTenantContextTests.cs | Tests for DesignTimeTenantContext | Property values | |
| 135 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/FeatureFlagOverrideRepositoryTests.cs | Integration tests for FeatureFlagOverrideRepository | CRUD, expiration filtering, scope matching | |
| 136 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/Persistence/FeatureFlagRepositoryTests.cs | Integration tests for FeatureFlagRepository | CRUD, compiled query behavior | |
| 137 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/Infrastructure/PostgresDatabaseCollection.cs | xUnit collection fixture for shared Postgres test database | Shared database lifecycle for integration tests | |

### Other Test Files

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 138 | [ ] | tests/Modules/Configuration/Foundry.Configuration.Tests/GlobalUsings.cs | Global using directives for test project | Common test imports | |
