# Phase 1: Shared Kernel

**Scope:** `src/Shared/Foundry.Shared.Kernel/` and `tests/Foundry.Shared.Kernel.Tests/`
**Status:** Not Started
**Files:** 42 source files, 22 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Auditing

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Foundry.Shared.Kernel/Auditing/AuditIgnoreAttribute.cs` | Attribute to mark entity properties that should be excluded from audit logging | `[AttributeUsage(AttributeTargets.Property)]` on sealed class | None (pure .NET) | |

### BackgroundJobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 2 | [ ] | `src/Shared/Foundry.Shared.Kernel/BackgroundJobs/IJobScheduler.cs` | Abstraction for scheduling background jobs (Hangfire wrapper interface) | `Enqueue`, `Enqueue<T>`, `AddRecurring`, `RemoveRecurring` methods using `Expression<Func<Task>>` | System.Linq.Expressions | |

### CustomFields

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 3 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/CustomFieldOption.cs` | Record representing an option for dropdown or multi-select custom fields with value, label, order, and active status | Sealed record with `required` properties `Value` and `Label` | None | |
| 4 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/CustomFieldRegistry.cs` | Static registry of entity types that support custom fields, with module-level registration | `Register`, `IsSupported`, `GetEntityType`, `GetSupportedEntityTypes`; pre-registers Invoice, Payment, Subscription | None | |
| 5 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/CustomFieldType.cs` | Enum defining supported custom field data types (Text, Number, Date, Boolean, Dropdown, etc.) | 11+ enum values covering text, numeric, date, boolean, dropdown, multi-select, email, URL types | None | |
| 6 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/FieldValidationRules.cs` | Record defining validation constraints for custom fields (min/max length, numeric range, regex pattern, date range) | All properties nullable; type-specific rules for text, numeric, and date fields | None | |
| 7 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/ICustomFieldValidator.cs` | Interface for validating entity custom fields against tenant field definitions, plus result/error types | `ValidateAsync<T>` generic method; `CustomFieldValidationResult` with `IsValid`/`Errors`; `CustomFieldValidationError` record | None | |
| 8 | [ ] | `src/Shared/Foundry.Shared.Kernel/CustomFields/IHasCustomFields.cs` | Marker interface for entities that support tenant-configurable custom fields stored as JSONB | `CustomFields` dictionary property and `SetCustomFields` method | None | |

### Diagnostics

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Shared/Foundry.Shared.Kernel/Diagnostics.cs` | Central OpenTelemetry instrumentation: shared Meter, ActivitySource, and messaging metrics counters | `Meter`, `ActivitySource`, `MessagesTotal`, `MessageDuration`, `DomainEventsPublishedTotal`; factory methods `CreateActivitySource`, `CreateMeter` | System.Diagnostics, System.Diagnostics.Metrics | |

### Domain

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/Entity.cs` | Base class for all entities with strongly-typed ID, identity-based equality, and EF Core support | `Id` property, `Equals`, `GetHashCode` via `IStronglyTypedId<TId>` constraint | Kernel.Identity | |
| 11 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/AuditableEntity.cs` | Entity base with creation/modification audit fields (`CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`) | `SetCreated`, `SetUpdated` methods; extends `Entity<TId>` | Kernel.Identity | |
| 12 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/AggregateRoot.cs` | Base class for aggregate roots with domain event collection for event-driven patterns | `RaiseDomainEvent`, `ClearDomainEvents`, `DomainEvents` read-only list; extends `AuditableEntity<TId>` | Kernel.Identity, Kernel.Domain.IDomainEvent | |
| 13 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/IDomainEvent.cs` | Marker interface and base record for domain events with EventId and OccurredAt timestamps | `IDomainEvent` interface; `DomainEvent` abstract record with auto-generated `EventId` and `OccurredAt` | None | |
| 14 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/ValueObject.cs` | Base class for value objects with structural equality via `GetEqualityComponents()` pattern | `Equals`, `GetHashCode`, operator overloads; abstract `GetEqualityComponents` | None | |
| 15 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/DomainException.cs` | Abstract base for domain exceptions representing business rule violations with machine-readable error codes | `Code` property; constructors with code+message validation via `ArgumentException.ThrowIfNullOrEmpty` | None | |
| 16 | [ ] | `src/Shared/Foundry.Shared.Kernel/Domain/ExternalServiceException.cs` | Exception for external service call failures with HTTP status code and response body | `StatusCode`, `ResponseBody` properties; multiple constructor overloads | None | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Shared/Foundry.Shared.Kernel/Extensions/ServiceCollectionExtensions.cs` | DI registration for shared kernel services: TimeProvider, TenantContext, TenantSaveChangesInterceptor | `AddSharedKernel` extension method; registers scoped tenant services and singleton TimeProvider | Kernel.MultiTenancy, Microsoft.Extensions.DependencyInjection | |

### Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 18 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/IStronglyTypedId.cs` | Interface hierarchy for strongly-typed IDs preventing ID type mix-ups across entities | `IStronglyTypedId` with `Value` (Guid); `IStronglyTypedId<T>` with `static abstract Create(Guid)` and `New()` | None | |
| 19 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/StronglyTypedIdConverter.cs` | EF Core ValueConverter for strongly-typed IDs, plus `EnsureId` extension for empty-Guid protection | `StronglyTypedIdConverter<TId>` converts between `TId` and `Guid`; `EnsureId` creates new ID if empty | Microsoft.EntityFrameworkCore | |
| 20 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/TenantId.cs` | Strongly-typed ID for tenants as a readonly record struct | `TenantId(Guid Value)` implementing `IStronglyTypedId<TenantId>` | Kernel.Identity.IStronglyTypedId | |
| 21 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/UserId.cs` | Strongly-typed ID for users as a readonly record struct | `UserId(Guid Value)` implementing `IStronglyTypedId<UserId>` | Kernel.Identity.IStronglyTypedId | |

### Identity/Authorization

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 22 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/Authorization/HasPermissionAttribute.cs` | Authorization attribute for permission-based access control on controllers/actions | Extends `AuthorizeAttribute` with `Permission` property; supports multiple attributes per target | Microsoft.AspNetCore.Authorization | |
| 23 | [ ] | `src/Shared/Foundry.Shared.Kernel/Identity/Authorization/PermissionType.cs` | Static class defining all system RBAC permission constants organized by domain area | String constants for Users, Roles, Billing, Organizations, API Keys, Notifications, Webhooks permissions | None | |

### MultiTenancy

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/ITenantContext.cs` | Read-only interface exposing current tenant information (ID, name, region, resolution status) | `TenantId`, `TenantName`, `Region`, `IsResolved` properties | Kernel.Identity | |
| 25 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/ITenantContextSetter.cs` | Interface for setting/clearing the current tenant context (used by middleware) | `SetTenant` overloads (with/without name+region), `Clear` method | Kernel.Identity | |
| 26 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/ITenantContextFactory.cs` | Factory for creating scoped tenant contexts in background jobs, returning IDisposable scopes | `CreateScope(TenantId)` returns `IDisposable` that clears context on dispose | Kernel.Identity | |
| 27 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/ITenantScoped.cs` | Marker interface for entities that belong to a tenant; enables automatic query filtering | Single `TenantId` property with `init` setter | Kernel.Identity | |
| 28 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantContext.cs` | Mutable implementation of both ITenantContext and ITenantContextSetter, used as scoped service | `SetTenant`, `Clear` methods; implements both read and write tenant interfaces | Kernel.Identity, Kernel.MultiTenancy interfaces | |
| 29 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantContextFactory.cs` | Factory creating disposable tenant scopes that auto-clear on dispose | `CreateScope` sets tenant via setter; nested `TenantContextScope` calls `Clear` on dispose | Kernel.Identity, Kernel.MultiTenancy.ITenantContextSetter | |
| 30 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/RegionConfiguration.cs` | Constants for supported deployment regions and a record for region settings | `UsEast`, `EuWest`, `ApSoutheast` constants; `PrimaryRegion`; `RegionSettings` record | None | |
| 31 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantQueryExtensions.cs` | Extension method to bypass tenant global query filters for admin/cross-tenant queries | `AllTenants<T>` calls `IgnoreQueryFilters()` on IQueryable | Microsoft.EntityFrameworkCore | |
| 32 | [ ] | `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs` | EF Core SaveChanges interceptor that auto-stamps TenantId on new entities and prevents TenantId modification | Intercepts `SavingChanges`/`SavingChangesAsync`; sets TenantId on Added entries, blocks modification on Modified entries | Kernel.MultiTenancy.ITenantContext, Microsoft.EntityFrameworkCore | |

### Pagination

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 33 | [ ] | `src/Shared/Foundry.Shared.Kernel/Pagination/PagedResult.cs` | Generic paged result record with computed pagination properties | `Items`, `TotalCount`, `Page`, `PageSize`; computed `TotalPages`, `HasNextPage`, `HasPreviousPage` | None | |

### Plugins

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 34 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/IFoundryPlugin.cs` | Core plugin interface defining the lifecycle contract for Foundry plugins | `Manifest`, `AddServices`, `InitializeAsync`, `ShutdownAsync` methods | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Configuration | |
| 35 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/IPluginPermissionValidator.cs` | Interface for checking plugin permission grants at runtime | `HasPermission(pluginId, permission)`, `GetGrantedPermissions(pluginId)` | None | |
| 36 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/PluginContext.cs` | Runtime context passed to plugins during initialization with DI, config, and logging | Constructor-injected `IServiceProvider`, `IConfiguration`, `ILogger<PluginContext>` | Microsoft.Extensions.Configuration, Microsoft.Extensions.Logging | |
| 37 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/PluginLifecycleState.cs` | Enum representing plugin lifecycle states from discovery through uninstallation | `Discovered`, `Installed`, `Enabled`, `Disabled`, `Uninstalled` | None | |
| 38 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/PluginManifest.cs` | Records defining plugin metadata: manifest with dependencies, permissions, and exported services | `PluginManifest` record with Id, Name, Version, etc.; `PluginDependency` record with Id and VersionRange | None | |
| 39 | [ ] | `src/Shared/Foundry.Shared.Kernel/Plugins/PluginPermission.cs` | Static constants for known plugin capability permission strings (module:action pattern) | `BillingRead`, `NotificationsSend`, `StorageRead`, `StorageWrite`, `IdentityRead` | None | |

### Results

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 40 | [ ] | `src/Shared/Foundry.Shared.Kernel/Results/Error.cs` | Structured error record with factory methods for common error types (NotFound, Validation, Conflict, etc.) | Static factories: `NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden`, `BusinessRule`; sentinel `None` and `NullValue` | None | |
| 41 | [ ] | `src/Shared/Foundry.Shared.Kernel/Results/Result.cs` | Result monad for operation outcomes without exceptions, with generic typed-value variant | `Result` and `Result<TValue>` with `IsSuccess`/`IsFailure`/`Error`/`Value`; static `Success`/`Failure` factories | Kernel.Results.Error | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 42 | [ ] | `src/Shared/Foundry.Shared.Kernel/Services/ICurrentUserService.cs` | Interface for retrieving the current authenticated user's ID from the request context | `GetCurrentUserId()` returning `Guid?`; default `UserId` property | None | |

## Test Files

### CustomFields

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 1 | [ ] | `tests/Foundry.Shared.Kernel.Tests/GlobalUsings.cs` | Global using directives for NSubstitute | Test infrastructure setup | |
| 2 | [ ] | `tests/Foundry.Shared.Kernel.Tests/CustomFields/CustomFieldRegistryTests.cs` | Tests for entity type registration and lookup | `CustomFieldRegistry.Register`, `IsSupported`, `GetSupportedEntityTypes` | |
| 3 | [ ] | `tests/Foundry.Shared.Kernel.Tests/CustomFields/CustomFieldValidationResultTests.cs` | Tests for validation result construction and IsValid logic | `CustomFieldValidationResult.Success`, `Failure`, `IsValid` | |
| 4 | [ ] | `tests/Foundry.Shared.Kernel.Tests/CustomFields/FieldValidationRulesTests.cs` | Tests for default null properties on FieldValidationRules | `FieldValidationRules` default constructor and property initialization | |

### Diagnostics

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 5 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Diagnostics/DiagnosticsTests.cs` | Tests for OpenTelemetry meter and activity source initialization | `Diagnostics.Meter`, `Diagnostics.ActivitySource` names and nullability | |

### Domain

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 6 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Domain/AggregateRootTests.cs` | Tests for domain event raising and clearing on aggregates | `RaiseDomainEvent`, `ClearDomainEvents`, `DomainEvents` collection | |
| 7 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Domain/AuditableEntityTests.cs` | Tests for audit field stamping (created/updated timestamps and user IDs) | `SetCreated`, `SetUpdated` with timestamps and user IDs | |
| 8 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Domain/DomainExceptionTests.cs` | Tests for domain exception construction with codes and messages | `DomainException` constructor variants, `Code` property, inner exceptions | |
| 9 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Domain/EntityTests.cs` | Tests for entity identity, equality, and hash code behavior | Constructor, `Id` property, `Equals`, `GetHashCode`, operator overloads | |
| 10 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Domain/ValueObjectTests.cs` | Tests for value object structural equality and hash code consistency | `Equals` with same/different values, `GetHashCode`, operator overloads | |

### Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 11 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Extensions/ServiceCollectionExtensionsTests.cs` | Tests for DI registration of shared kernel services | `AddSharedKernel` registers TimeProvider, TenantContext, ITenantContext, ITenantContextSetter | |

### Identity

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 12 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Identity/StronglyTypedIdTests.cs` | Tests for TenantId and UserId creation, equality, and value semantics | `Create`, `New`, value equality, Guid roundtrip | |

### Messaging

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 13 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Messaging/WolverineErrorHandlingExtensionsTests.cs` | Tests that Wolverine error handling configuration does not throw | `ConfigureStandardErrorHandling`, `ConfigureMessageLogging` on WolverineOptions | |

### MultiTenancy

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 14 | [ ] | `tests/Foundry.Shared.Kernel.Tests/MultiTenancy/RegionConfigurationTests.cs` | Tests for region constant values | `UsEast`, `EuWest`, `ApSoutheast`, `PrimaryRegion` string values | |
| 15 | [ ] | `tests/Foundry.Shared.Kernel.Tests/MultiTenancy/TenantContextFactoryTests.cs` | Tests for scoped tenant context creation and disposal | `CreateScope` sets tenant, dispose clears it | |
| 16 | [ ] | `tests/Foundry.Shared.Kernel.Tests/MultiTenancy/TenantContextTests.cs` | Tests for tenant context set/clear operations | `SetTenant`, `Clear`, `IsResolved` transitions | |
| 17 | [ ] | `tests/Foundry.Shared.Kernel.Tests/MultiTenancy/TenantQueryExtensionsTests.cs` | Tests for bypassing tenant query filters | `AllTenants` calls `IgnoreQueryFilters` | |
| 18 | [ ] | `tests/Foundry.Shared.Kernel.Tests/MultiTenancy/TenantSaveChangesInterceptorTests.cs` | Tests for automatic TenantId stamping and modification prevention | Interceptor sets TenantId on Added, blocks changes on Modified | |

### Plugins

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 19 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Plugins/PluginModelTests.cs` | Tests for plugin manifest and context record construction | `PluginManifest` properties, `PluginContext` service provider access | |

### Results

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 20 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Results/ErrorTests.cs` | Tests for error record creation and factory methods | Constructor, `NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden` | |
| 21 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Results/PagedResultTests.cs` | Tests for paged result computation properties | `TotalPages`, `HasNextPage`, `HasPreviousPage` calculations | |
| 22 | [ ] | `tests/Foundry.Shared.Kernel.Tests/Results/ResultTests.cs` | Tests for Result monad success/failure states and value access | `Success`, `Failure`, `IsSuccess`/`IsFailure`, `Value` access on failure throws | |
