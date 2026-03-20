# Phase 8: Identity Module

**Scope:** Complete Identity module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 143 source files, 97 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Entities/ApiScope.cs` | System-defined API scope entity for OAuth2 service account permissions | `Create()` factory method; properties: Code, DisplayName, Category, Description, IsDefault | Shared.Kernel (Entity base, IStronglyTypedId) | |
| 2 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Entities/ScimConfiguration.cs` | SCIM provisioning configuration per tenant with bearer token management | `Create()` factory, `Enable()`, `Disable()`, `UpdateSettings()`, `RegenerateToken()`, `MarkSynced()`; tenant-scoped aggregate root | Shared.Kernel (AggregateRoot, ITenantScoped, TenantId) | |
| 3 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Entities/ScimSyncLog.cs` | Audit log entry for individual SCIM sync operations | `Create()` factory with operation/resource/success tracking; raises `ScimSyncCompletedEvent` | Shared.Kernel (AggregateRoot, ITenantScoped) | |
| 4 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Entities/ServiceAccountMetadata.cs` | Local metadata reference to a Keycloak service account client | `Create()`, `Revoke()`, `MarkUsed()`, `UpdateScopes()`; tracks KeycloakClientId, Status, LastUsedAt, Scopes JSON | Shared.Kernel (AuditableEntity, ITenantScoped) | |
| 5 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Entities/SsoConfiguration.cs` | SSO identity provider configuration supporting SAML and OIDC protocols | State machine: Draft->Testing->Active->Disabled; `ConfigureSaml()`, `ConfigureOidc()`, `Activate()`, `Disable()`, `MoveToTesting()`; raises `SsoConfigurationActivatedEvent` | Shared.Kernel (AggregateRoot, ITenantScoped) | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/SamlNameIdFormat.cs` | SAML NameID format options: Email, Persistent, Transient, Unspecified | Simple enum | None | |
| 7 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/ScimOperation.cs` | SCIM operation types: Create, Read, Update, Delete, Patch | Simple enum | None | |
| 8 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/ScimResourceType.cs` | SCIM resource types: User, Group | Simple enum | None | |
| 9 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/ServiceAccountStatus.cs` | Service account lifecycle states: Active, Revoked | Simple enum | None | |
| 10 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/SsoProtocol.cs` | SSO protocol types: Saml, Oidc | Simple enum | None | |
| 11 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Enums/SsoStatus.cs` | SSO configuration state machine: Draft, Testing, Active, Disabled | Simple enum | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 12 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Events/ScimSyncCompletedEvent.cs` | Domain event raised when a SCIM sync operation completes | Record with TenantId, Operation, ResourceType, Success, ErrorMessage | Shared.Kernel (DomainEvent) | |
| 13 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Events/SsoConfigurationActivatedEvent.cs` | Domain event raised when SSO configuration is activated for a tenant | Record with SsoConfigurationId, TenantId, DisplayName, Protocol | Shared.Kernel (DomainEvent) | |

### Identity (Strongly-Typed IDs)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 14 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Identity/ApiScopeId.cs` | Strongly-typed ID for ApiScope entity | `Create(Guid)`, `New()` factory methods | Shared.Kernel (IStronglyTypedId) | |
| 15 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Identity/ScimConfigurationId.cs` | Strongly-typed ID for ScimConfiguration entity | `Create(Guid)`, `New()` factory methods | Shared.Kernel (IStronglyTypedId) | |
| 16 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Identity/ScimSyncLogId.cs` | Strongly-typed ID for ScimSyncLog entity | `Create(Guid)`, `New()` factory methods | Shared.Kernel (IStronglyTypedId) | |
| 17 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Identity/ServiceAccountMetadataId.cs` | Strongly-typed ID for ServiceAccountMetadata entity | `Create(Guid)`, `New()` factory methods | Shared.Kernel (IStronglyTypedId) | |
| 18 | [ ] | `src/Modules/Identity/Wallow.Identity.Domain/Identity/SsoConfigurationId.cs` | Strongly-typed ID for SsoConfiguration entity | `Create(Guid)`, `New()` factory methods | Shared.Kernel (IStronglyTypedId) | |

---

## Application Layer

### Commands / CreateServiceAccount

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 19 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/CreateServiceAccount/CreateServiceAccountCommand.cs` | Command record for creating a service account | Record with Name, Description, Scopes | None | |
| 20 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/CreateServiceAccount/CreateServiceAccountHandler.cs` | Handles CreateServiceAccountCommand by delegating to IServiceAccountService | `Handle()` maps command to DTO and calls `serviceAccountService.CreateAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |
| 21 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/CreateServiceAccount/CreateServiceAccountValidator.cs` | FluentValidation rules for CreateServiceAccountCommand | Validates Name (required, max 100), Description (max 500), Scopes (not empty) | FluentValidation | |

### Commands / RevokeServiceAccount

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 22 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/RevokeServiceAccount/RevokeServiceAccountCommand.cs` | Command record for revoking a service account | Record with ServiceAccountMetadataId | Domain (ServiceAccountMetadataId) | |
| 23 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/RevokeServiceAccount/RevokeServiceAccountHandler.cs` | Handles RevokeServiceAccountCommand by delegating to IServiceAccountService | `Handle()` calls `serviceAccountService.RevokeAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |

### Commands / RotateServiceAccountSecret

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/RotateServiceAccountSecret/RotateServiceAccountSecretCommand.cs` | Command record for rotating a service account secret | Record with ServiceAccountMetadataId | Domain (ServiceAccountMetadataId) | |
| 25 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/RotateServiceAccountSecret/RotateServiceAccountSecretHandler.cs` | Handles RotateServiceAccountSecretCommand by delegating to IServiceAccountService | `Handle()` calls `serviceAccountService.RotateSecretAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |

### Commands / UpdateServiceAccountScopes

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 26 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/UpdateServiceAccountScopes/UpdateServiceAccountScopesCommand.cs` | Command record for updating service account scopes | Record with ServiceAccountMetadataId, Scopes | Domain (ServiceAccountMetadataId) | |
| 27 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/UpdateServiceAccountScopes/UpdateServiceAccountScopesHandler.cs` | Handles UpdateServiceAccountScopesCommand by delegating to IServiceAccountService | `Handle()` calls `serviceAccountService.UpdateScopesAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |
| 28 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Commands/UpdateServiceAccountScopes/UpdateServiceAccountScopesValidator.cs` | FluentValidation rules for UpdateServiceAccountScopesCommand | Validates Scopes (not empty) | FluentValidation | |

### Constants

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Constants/ApiScopes.cs` | Static set of valid API scope codes for validation | `ValidScopes` HashSet with scopes like invoices.read, payments.write, webhooks.manage | None | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 30 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/ApiScopeDto.cs` | DTO for API scope data transfer | Record with Id, Code, DisplayName, Category, Description, IsDefault | Domain (ApiScopeId) | |
| 31 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/CreateServiceAccountRequest.cs` | Application-level request DTO for creating a service account | Record with Name, Description, Scopes | None | |
| 32 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/OrganizationDto.cs` | DTO for Keycloak organization data | Record with Id, Name, Domain, MemberCount | None | |
| 33 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/ScimDtos.cs` | Collection of SCIM 2.0 DTOs: ScimConfigurationDto, EnableScimRequest/Response, ScimUser, ScimGroup, ScimPatchRequest, ScimListRequest/Response, etc. | Multiple records following RFC 7644 SCIM schema with JSON property names | Domain (ScimConfigurationId, ScimOperation, ScimResourceType) | |
| 34 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/SecretRotatedResult.cs` | DTO returned after rotating a service account secret | Record with NewClientSecret, RotatedAt | None | |
| 35 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/ServiceAccountCreatedResult.cs` | DTO returned after creating a service account | Record with Id, ClientId, ClientSecret, TokenEndpoint, Scopes | Domain (ServiceAccountMetadataId) | |
| 36 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/ServiceAccountDto.cs` | DTO for service account listing/details | Record with Id, ClientId, Name, Description, Status, Scopes, CreatedAt, LastUsedAt | Domain (ServiceAccountMetadataId, ServiceAccountStatus) | |
| 37 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/SsoConfigurationDto.cs` | DTOs for SSO configuration: SsoConfigurationDto, SaveSamlConfigRequest, SaveOidcConfigRequest, SsoTestResult, OidcCallbackInfo | Multiple records covering SAML/OIDC config, SP metadata URLs, test results | Domain (SsoConfigurationId, SsoProtocol, SsoStatus, SamlNameIdFormat) | |
| 38 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/DTOs/UserDto.cs` | DTO for Keycloak user data | Record with Id, Email, FirstName, LastName, Enabled, Roles | None | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 39 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Exceptions/KeycloakConflictException.cs` | Exception thrown when Keycloak returns a conflict (e.g., duplicate user) | Standard exception with message constructors | None | |
| 40 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Exceptions/ScimFilterException.cs` | Exception for invalid SCIM filter expressions with position tracking | Includes `Position` property for error location in filter string | None | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 41 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Extensions/ApplicationExtensions.cs` | DI registration for Identity Application layer | `AddIdentityApplication()` registers FluentValidation validators from assembly | FluentValidation, Microsoft.Extensions.DependencyInjection | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 42 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IApiKeyService.cs` | Interface for API key management (create, validate, list, revoke) plus result/metadata records | `CreateApiKeyAsync()`, `ValidateApiKeyAsync()`, `ListApiKeysAsync()`, `RevokeApiKeyAsync()`; defines `ApiKeyCreateResult`, `ApiKeyValidationResult`, `ApiKeyMetadata` records | None | |
| 43 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IApiScopeRepository.cs` | Repository interface for API scope CRUD operations | `GetAllAsync()`, `GetByCodesAsync()`, `Add()`, `SaveChangesAsync()` | Domain (ApiScope) | |
| 44 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IKeycloakAdminService.cs` | Interface for Keycloak user management admin operations | `CreateUserAsync()`, `GetUserByIdAsync()`, `GetUserByEmailAsync()`, `GetUsersAsync()`, `DeactivateUserAsync()`, `ActivateUserAsync()`, `AssignRoleAsync()`, `RemoveRoleAsync()`, `GetUserRolesAsync()`, `DeleteUserAsync()` | Application.DTOs (UserDto) | |
| 45 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IOrganizationService.cs` | Interface for Keycloak organization management operations | `CreateOrganizationAsync()`, `GetOrganizationByIdAsync()`, `GetOrganizationsAsync()`, `AddMemberAsync()`, `RemoveMemberAsync()`, `GetMembersAsync()`, `GetUserOrganizationsAsync()` | Application.DTOs (OrganizationDto, UserDto) | |
| 46 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IRolePermissionLookup.cs` | Interface for expanding roles into granular permissions | `GetPermissions(IEnumerable<string> roles)` returns permission collection | None | |
| 47 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IScimConfigurationRepository.cs` | Repository interfaces for SCIM configuration and sync logs | `IScimConfigurationRepository`: `GetAsync()`, `Add()`, `SaveChangesAsync()`; `IScimSyncLogRepository`: `GetRecentAsync()`, `Add()`, `SaveChangesAsync()` | Domain (ScimConfiguration, ScimSyncLog) | |
| 48 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IScimService.cs` | Interface for full SCIM 2.0 provisioning operations | Config: `GetConfigurationAsync()`, `EnableScimAsync()`, `DisableScimAsync()`, `RegenerateTokenAsync()`; Users: `CreateUserAsync()`, `UpdateUserAsync()`, `PatchUserAsync()`, `DeleteUserAsync()`, `GetUserAsync()`, `ListUsersAsync()`; Groups: similar CRUD | Application.DTOs (ScimDtos) | |
| 49 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IServiceAccountRepository.cs` | Repository interface for service account metadata | `GetByIdAsync()`, `GetAllAsync()`, `Add()`, `SaveChangesAsync()` | Domain (ServiceAccountMetadata, ServiceAccountMetadataId) | |
| 50 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/IServiceAccountService.cs` | Interface for OAuth2 service account lifecycle management | `CreateAsync()`, `ListAsync()`, `GetAsync()`, `RotateSecretAsync()`, `UpdateScopesAsync()`, `RevokeAsync()` | Application.DTOs, Domain (ServiceAccountMetadataId) | |
| 51 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/ISsoConfigurationRepository.cs` | Repository interface for SSO configuration | `GetAsync()`, `Add()`, `SaveChangesAsync()` | Domain (SsoConfiguration) | |
| 52 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/ISsoService.cs` | Interface for SSO configuration and integration management | `GetConfigurationAsync()`, `SaveSamlConfigurationAsync()`, `SaveOidcConfigurationAsync()`, `TestConnectionAsync()`, `ActivateAsync()`, `DisableAsync()`, `GetSamlServiceProviderMetadataAsync()`, `GetOidcCallbackInfoAsync()` | Application.DTOs (SsoConfigurationDto, etc.) | |
| 53 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Interfaces/ITokenService.cs` | Interface for OAuth2 token operations plus TokenResult record | `GetTokenAsync()`, `RefreshTokenAsync()`, `RevokeTokenAsync()`; defines `TokenResult` record | None | |

### Queries / GetApiScopes

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 54 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetApiScopes/GetApiScopesQuery.cs` | Query record for listing API scopes with optional category filter | Record with optional Category parameter | None | |
| 55 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetApiScopes/GetApiScopesHandler.cs` | Handles GetApiScopesQuery by querying IApiScopeRepository and mapping to DTOs | `Handle()` fetches scopes, maps to `ApiScopeDto` list | Application.Interfaces (IApiScopeRepository), Shared.Kernel (Result) | |

### Queries / GetServiceAccount

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 56 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetServiceAccount/GetServiceAccountQuery.cs` | Query record for getting a single service account | Record with ServiceAccountMetadataId | Domain (ServiceAccountMetadataId) | |
| 57 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetServiceAccount/GetServiceAccountHandler.cs` | Handles GetServiceAccountQuery by delegating to IServiceAccountService | `Handle()` calls `serviceAccountService.GetAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |

### Queries / GetServiceAccounts

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 58 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetServiceAccounts/GetServiceAccountsQuery.cs` | Query record for listing all service accounts | Empty record (no parameters) | None | |
| 59 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Queries/GetServiceAccounts/GetServiceAccountsHandler.cs` | Handles GetServiceAccountsQuery by delegating to IServiceAccountService | `Handle()` calls `serviceAccountService.ListAsync()` | Application.Interfaces (IServiceAccountService), Shared.Kernel (Result) | |

### Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 60 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Telemetry/IdentityModuleTelemetry.cs` | OpenTelemetry instrumentation for Identity module | `ActivitySource` for tracing, `SsoLoginsTotal` and `SsoFailuresTotal` counters | Shared.Kernel (Diagnostics) | |

### Settings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 61 | [ ] | `src/Modules/Identity/Wallow.Identity.Application/Settings/IdentitySettingKeys.cs` | Typed setting key definitions for the Identity module (timezone, locale, date format, theme) | Extends `SettingRegistryBase`; defines `SettingDefinition<string>` constants for identity.timezone, identity.locale, identity.date_format, identity.theme with defaults | Shared.Kernel (SettingRegistryBase, SettingDefinition) | |

---

## Infrastructure Layer

### Authorization

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 62 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs` | Middleware that authenticates requests via X-Api-Key header, falling through to JWT if absent | `InvokeAsync()` extracts header, validates via IApiKeyService, creates ClaimsIdentity with userId/tenantId/scopes | Application.Interfaces (IApiKeyService), Shared.Kernel (TenantContext) | |
| 63 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionAuthorizationHandler.cs` | ASP.NET authorization handler that checks for "permission" claims | `HandleRequirementAsync()` checks if user has the required permission claim | Microsoft.AspNetCore.Authorization | |
| 64 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionAuthorizationPolicyProvider.cs` | Dynamic policy provider that creates authorization policies for PermissionType values | `GetPolicyAsync()` creates PermissionRequirement-based policies; fallback requires authenticated user | Shared.Kernel (PermissionType) | |
| 65 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs` | Middleware that expands JWT roles/scopes into granular permission claims | `InvokeAsync()` detects service accounts (azp starts with "sa-") vs regular users; expands Keycloak realm_access roles via RolePermissionMapping; maps OAuth2 scopes to permissions | Shared.Kernel (PermissionType) | |
| 66 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionRequirement.cs` | IAuthorizationRequirement implementation holding a single permission string | Simple record-like class with `Permission` property | Microsoft.AspNetCore.Authorization | |
| 67 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionLookup.cs` | Implementation of IRolePermissionLookup that delegates to static RolePermissionMapping | `GetPermissions()` wraps `RolePermissionMapping.GetPermissions()` | Application.Interfaces (IRolePermissionLookup) | |
| 68 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionMapping.cs` | Static FrozenDictionary mapping Keycloak roles (admin/manager/user) to granular PermissionType permissions | Three role tiers: admin (all), manager (subset), user (basic); `GetPermissions()` returns union of all matched role permissions | Shared.Kernel (PermissionType) | |
| 69 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ScimAuthenticationMiddleware.cs` | Middleware authenticating SCIM /scim/v2/* requests via Bearer token with SHA256 hash comparison | `InvokeAsync()` bypasses tenant filters to find ScimConfiguration by token hash, sets tenant context; skips discovery endpoints | Infrastructure.Persistence (IdentityDbContext), Shared.Kernel (TenantContext) | |
| 70 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/ScimBearerAuthenticationHandler.cs` | ASP.NET AuthenticationHandler for "ScimBearer" scheme used by SCIM controller | `HandleAuthenticateAsync()` validates Bearer token against ScimConfiguration, creates ClaimsPrincipal with tenant claims | Infrastructure.Persistence (IdentityDbContext), Shared.Kernel (TenantContext) | |

### Data

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 71 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Data/ApiScopeSeeder.cs` | Seeds default API scopes (billing, identity, notifications, webhooks) to database | `SeedAsync()` is idempotent - only adds scopes not already present; `GetDefaultScopes()` yields ~11 default scopes | Infrastructure.Persistence (IdentityDbContext), Domain (ApiScope) | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 72 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/HttpResponseMessageExtensions.cs` | Extension method for Keycloak HTTP response error handling | `EnsureSuccessOrThrowAsync()` throws `ExternalServiceException` with status code and body on failure | Shared.Kernel (ExternalServiceException) | |
| 73 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs` | DI registration for all Identity infrastructure services | `AddIdentityInfrastructure()` registers Keycloak OIDC auth, authorization pipeline, persistence (IdentityDbContext), Keycloak admin client with resilience policies, all repositories and services | Keycloak.AuthServices, Shared.Infrastructure.Core (Resilience) | |
| 74 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityModuleExtensions.cs` | Top-level module registration and initialization | `AddIdentityModule()` composes Application + Infrastructure; `InitializeIdentityModuleAsync()` runs EF migrations in Development/Testing | Application.Extensions, Infrastructure.Persistence | |

### Configuration

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 75 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/KeycloakOptions.cs` | Options class for Keycloak connection settings | Properties: Realm, AuthorityUrl, AdminClientId, AdminClientSecret; section "Identity:Keycloak" | None | |
| 76 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/ScimConstants.cs` | SCIM protocol constants | `MaxPageSize = 100` | None | |

### Middleware

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 77 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Middleware/ServiceAccountTrackingMiddleware.cs` | Tracks service account API usage by updating LastUsedAt timestamp | `InvokeAsync()` fires-and-forget after successful responses from "sa-" prefixed clients; uses new DI scope to avoid disposed context | Infrastructure.Repositories (IServiceAccountUnfilteredRepository) | |

### MultiTenancy

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 78 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs` | Resolves tenant from JWT organization claim and populates ITenantContext | `InvokeAsync()` parses Keycloak 26+ JSON organization claim format `{"orgId":{"name":"orgName"}}`; supports admin X-Tenant-Id header override; emits OpenTelemetry metrics per tenant | Shared.Kernel (ITenantContextSetter, Diagnostics) | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 79 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Configurations/ApiScopeConfiguration.cs` | EF Core entity configuration for ApiScope table | Maps to "api_scopes" table; configures strongly-typed ID conversion, unique index on Code, index on Category | Domain (ApiScope, ApiScopeId) | |
| 80 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Configurations/ScimConfigurationConfiguration.cs` | EF Core entity configuration for ScimConfiguration table | Maps to "scim_configurations" table; TenantId conversion, unique index on TenantId | Domain (ScimConfiguration, ScimConfigurationId), Shared.Kernel (TenantId) | |
| 81 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Configurations/ScimSyncLogConfiguration.cs` | EF Core entity configuration for ScimSyncLog table | Maps to "scim_sync_logs" table; enum-to-string conversions for Operation/ResourceType; index on TenantId+Timestamp | Domain (ScimSyncLog, ScimSyncLogId) | |
| 82 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Configurations/ServiceAccountMetadataConfiguration.cs` | EF Core entity configuration for ServiceAccountMetadata table | Maps to "service_account_metadata" table; indexes on TenantId+Status and KeycloakClientId (unique) | Domain (ServiceAccountMetadata, ServiceAccountMetadataId) | |
| 83 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Configurations/SsoConfigurationConfiguration.cs` | EF Core entity configuration for SsoConfiguration table | Maps to "sso_configurations" table; enum-to-string for Protocol/Status/SamlNameIdFormat; unique index on TenantId; encrypted OidcClientSecret via EncryptedStringConverter | Domain (SsoConfiguration, SsoConfigurationId) | |

### Persistence / Converters

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 84 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Converters/EncryptedStringConverter.cs` | EF Core ValueConverter that encrypts/decrypts string values using IDataProtector | Constructor takes IDataProtector; Protect on write, Unprotect on read; handles nulls | Microsoft.AspNetCore.DataProtection | |

### Persistence / DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 85 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/IdentityDbContext.cs` | EF Core DbContext for Identity module with tenant query filters | Inherits `TenantAwareDbContext`; DbSets: ServiceAccountMetadata, ApiScopes, SsoConfigurations, ScimConfigurations, ScimSyncLogs; uses "identity" schema; applies EncryptedStringConverter to OidcClientSecret; NoTracking by default | Shared.Infrastructure.Core (TenantAwareDbContext), Microsoft.AspNetCore.DataProtection | |

### Persistence / Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 86 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260213182136_InitialCreate.cs` | Initial EF Core migration creating Identity schema tables | Creates api_scopes, scim_configurations, scim_sync_logs, service_account_metadata, sso_configurations tables with indexes | EF Core Migrations | |
| 87 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260213182136_InitialCreate.Designer.cs` | Auto-generated migration designer/snapshot for InitialCreate | EF Core auto-generated model snapshot | EF Core Migrations | |
| 88 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260305024743_SyncModelChanges.cs` | Migration syncing model changes after initial create | Schema adjustments for model sync | EF Core Migrations | |
| 89 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260305024743_SyncModelChanges.Designer.cs` | Auto-generated migration designer for SyncModelChanges | EF Core auto-generated model snapshot | EF Core Migrations | |
| 90 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260310211026_AddSettingsTables.cs` | Migration adding tenant_settings and module_settings tables to identity schema | Creates tenant_settings and module_settings tables with appropriate indexes | EF Core Migrations | |
| 91 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/20260310211026_AddSettingsTables.Designer.cs` | Auto-generated migration designer for AddSettingsTables | EF Core auto-generated model snapshot | EF Core Migrations | |
| 92 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs` | EF Core model snapshot representing current database state | Auto-generated snapshot used by migration tooling | EF Core Migrations | |

### Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 93 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ApiScopeRepository.cs` | EF Core implementation of IApiScopeRepository | `GetAllAsync()` with optional category filter, ordered by Category+Code; `GetByCodesAsync()` for batch lookup | Infrastructure.Persistence (IdentityDbContext), Application.Interfaces | |
| 94 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/IServiceAccountUnfilteredRepository.cs` | Internal interface for cross-tenant service account lookups | `GetByKeycloakClientIdAsync()` bypasses tenant filters; used by middleware before tenant context is established | Domain (ServiceAccountMetadata) | |
| 95 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ScimConfigurationRepository.cs` | EF Core implementation of IScimConfigurationRepository with compiled query | Uses `EF.CompileAsyncQuery` for optimized single-tenant SCIM config lookup; AsTracking for mutations | Infrastructure.Persistence (IdentityDbContext) | |
| 96 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ScimSyncLogRepository.cs` | EF Core implementation of IScimSyncLogRepository | `GetRecentAsync()` returns last N logs ordered by Timestamp descending | Infrastructure.Persistence (IdentityDbContext) | |
| 97 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs` | EF Core implementation of IServiceAccountRepository and IServiceAccountUnfilteredRepository | `GetByIdAsync()` excludes revoked; `GetByKeycloakClientIdAsync()` uses compiled query with IgnoreQueryFilters; `GetAllAsync()` excludes revoked, ordered by Name | Infrastructure.Persistence (IdentityDbContext) | |
| 98 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/SsoConfigurationRepository.cs` | EF Core implementation of ISsoConfigurationRepository | `GetAsync()` returns single SSO config per tenant with AsTracking | Infrastructure.Persistence (IdentityDbContext) | |

### Scim (Filter Engine)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 99 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Scim/ScimToken.cs` | Token types and token record for SCIM filter lexer | `TokenType` enum: Attr, Op, Logic, Lparen, Rparen, String, Bool; `ScimToken` record with Type, Value, Position | None | |
| 100 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Scim/ScimFilterLexer.cs` | Lexer that tokenizes SCIM 2.0 filter expressions | `Tokenize()` handles operators (eq, ne, co, sw, etc.), logical operators (and, or, not), parentheses, quoted strings, booleans | Application.Exceptions (ScimFilterException) | |
| 101 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Scim/ScimFilterParser.cs` | Recursive descent parser for SCIM filter AST with visitor pattern | Parses tokens into AST nodes: `ComparisonNode`, `LogicalNode`, `NotNode`, `PresenceNode`; defines `IScimFilterVisitor<T>` interface | Application.Exceptions (ScimFilterException) | |
| 102 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Scim/ScimToKeycloakTranslator.cs` | Translates SCIM filter AST into Keycloak admin API query parameters | `Translate()` uses lexer+parser+visitor; maps SCIM attributes (userName, email) to Keycloak query params; falls back to in-memory filtering for unsupported operators | Infrastructure.Scim (ScimFilterLexer, ScimFilterParser), Application.DTOs (ScimUser) | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 103 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakAdminService.cs` | Keycloak Admin API wrapper for user CRUD, role management, and activation | `CreateUserAsync()`, `GetUserByIdAsync()`, `GetUserByEmailAsync()`, `GetUsersAsync()`, `DeactivateUserAsync()`, `ActivateUserAsync()`, `AssignRoleAsync()`, `RemoveRoleAsync()`, `DeleteUserAsync()`; publishes integration events via Wolverine IMessageBus | Keycloak.AuthServices.Sdk, Shared.Contracts (Identity events), Wolverine | |
| 104 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakIdpService.cs` | Keycloak identity provider management for SSO SAML/OIDC configuration | `IdentityProviderExistsAsync()`, `EnableIdentityProviderAsync()`, `CreateSamlIdpAsync()`, `CreateOidcIdpAsync()`, `UpdateIdpAsync()`; handles certificate PEM formatting | Keycloak Admin HTTP client | |
| 105 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakOrganizationService.cs` | Keycloak Organizations API wrapper for org CRUD and membership | `CreateOrganizationAsync()`, `GetOrganizationByIdAsync()`, `GetOrganizationsAsync()`, `AddMemberAsync()`, `RemoveMemberAsync()`, `GetMembersAsync()`, `GetUserOrganizationsAsync()`; publishes OrganizationCreatedEvent via Wolverine | Shared.Contracts (Identity events), Wolverine | |
| 106 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakServiceAccountService.cs` | Keycloak client management for service account lifecycle | `CreateAsync()` creates confidential Keycloak client with client_credentials grant + local metadata; `RotateSecretAsync()`, `UpdateScopesAsync()`, `RevokeAsync()` manage client lifecycle; generates slugified clientId like "sa-{tenantPrefix}-{name}" | Keycloak Admin HTTP client, Application.Interfaces (IServiceAccountRepository) | |
| 107 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakSsoService.cs` | SSO configuration service managing SAML/OIDC identity providers in Keycloak | `SaveSamlConfigurationAsync()`, `SaveOidcConfigurationAsync()`, `TestConnectionAsync()`, `ActivateAsync()`, `DisableAsync()`, `GetSamlServiceProviderMetadataAsync()`; uses SsoClaimsSyncService and KeycloakIdpService; emits OTel metrics | Application.Interfaces (ISsoConfigurationRepository), Infrastructure.Services (KeycloakIdpService, SsoClaimsSyncService) | |
| 108 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakTokenService.cs` | Token service delegating to Keycloak's OpenID Connect token endpoint | `GetTokenAsync()` uses password grant, `RefreshTokenAsync()`, `RevokeTokenAsync()`; parses JSON responses into TokenResult records; masks email in logs | KeycloakOptions | |
| 109 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/RedisApiKeyService.cs` | Redis-backed API key management with SHA256 hashing | `CreateApiKeyAsync()` generates "sk_live_" prefixed keys, stores SHA256 hash in Redis; `ValidateApiKeyAsync()` looks up by hash; `ListApiKeysAsync()` retrieves metadata set; `RevokeApiKeyAsync()` removes key data | StackExchange.Redis | |
| 110 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/ScimGroupService.cs` | SCIM group provisioning via Keycloak Groups API | `CreateGroupAsync()`, `UpdateGroupAsync()`, `PatchGroupAsync()`, `DeleteGroupAsync()`, `GetGroupAsync()`, `ListGroupsAsync()`; maps SCIM group schema to Keycloak groups with scim_external_id attribute; logs sync operations | Keycloak Admin HTTP client, Application.Interfaces (IScimConfigurationRepository, IScimSyncLogRepository) | |
| 111 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/ScimService.cs` | Orchestrator for SCIM 2.0 provisioning (configuration + user/group delegation) | `EnableScimAsync()`, `DisableScimAsync()`, `RegenerateTokenAsync()` manage config; delegates user/group CRUD to ScimUserService/ScimGroupService; `GetSyncLogsAsync()` for audit | Application.Interfaces (IScimService), Infrastructure.Services (ScimUserService, ScimGroupService) | |
| 112 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/ScimUserService.cs` | SCIM user provisioning via Keycloak Users API | `CreateUserAsync()`, `UpdateUserAsync()`, `PatchUserAsync()`, `DeleteUserAsync()`, `GetUserAsync()`, `ListUsersAsync()`; maps SCIM user schema to Keycloak UserRepresentation; uses ScimToKeycloakTranslator for filter queries; handles auto-activation and org membership | Keycloak Admin HTTP client, Infrastructure.Scim (ScimToKeycloakTranslator) | |
| 113 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/SsoClaimsSyncService.cs` | Syncs SSO IdP group claims to Keycloak user roles | `SyncUserClaimsAsync()` reads user attributes from Keycloak, maps IdP groups to realm roles based on SSO config GroupsAttribute; skips if SyncGroupsAsRoles disabled | Application.Interfaces (ISsoConfigurationRepository), Keycloak Admin HTTP client | |
| 114 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/UserQueryService.cs` | Cached user lookup service implementing IUserQueryService from Shared.Contracts | `GetUserEmailAsync()` with HybridCache (60s TTL); `GetNewUsersCountAsync()` counts users created in date range via Keycloak API | Shared.Contracts (IUserQueryService), Microsoft.Extensions.Caching.Hybrid | |
| 115 | [ ] | `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/UserService.cs` | Adapter mapping Keycloak user DTOs to Shared.Contracts UserInfo | `GetUserByIdAsync()`, `GetUserByEmailAsync()` delegate to IKeycloakAdminService and map UserDto to UserInfo | Shared.Contracts (IUserService), Application.Interfaces (IKeycloakAdminService) | |

---

## Api Layer

### Contracts / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 116 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Enums/ApiSamlNameIdFormat.cs` | API-layer enum for SAML NameID format with explicit integer values | Mirrors domain SamlNameIdFormat: Email=0, Persistent=1, Transient=2, Unspecified=3 | None | |

### Contracts / Requests

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 117 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AddMemberRequest.cs` | Request to add a user to an organization | Record with UserId (Guid) | None | |
| 118 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/AssignRoleRequest.cs` | Request to assign a role to a user | Record with RoleName (string) | None | |
| 119 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/CreateApiKeyRequest.cs` | Request to create a new API key | Record with Name, optional Scopes, optional ExpiresAt | None | |
| 120 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/CreateOrganizationRequest.cs` | Request to create an organization | Record with Name, optional Domain | None | |
| 121 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/CreateServiceAccountRequest.cs` | Request to create a service account | Record with Name, optional Description, Scopes list | None | |
| 122 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/CreateUserRequest.cs` | Request to create a user in Keycloak | Record with Email, FirstName, LastName, optional Password | None | |
| 123 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/LogoutRequest.cs` | Request to revoke a refresh token (logout) | Record with RefreshToken | None | |
| 124 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/RefreshTokenRequest.cs` | Request to refresh an access token | Record with RefreshToken | None | |
| 125 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/SsoRequests.cs` | Request DTOs for SAML and OIDC SSO configuration | `ConfigureSamlSsoRequest` with IdP details and attribute mappings; `ConfigureOidcSsoRequest` with issuer/client details | Api.Contracts.Enums (ApiSamlNameIdFormat) | |
| 126 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/TokenRequest.cs` | Request for obtaining an access token via email/password | Record with Email, Password | None | |
| 127 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Requests/UpdateScopesRequest.cs` | Request to update service account scopes | Record with Scopes list | None | |

### Contracts / Responses

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 128 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/ApiKeyResponse.cs` | Response DTOs for API key operations: ApiKeyResponse (metadata) and ApiKeyCreatedResponse (includes full key) | Two records: listing metadata vs one-time creation response with full key | None | |
| 129 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/CreateOrganizationResponse.cs` | Response after creating an organization | Record with OrganizationId (Guid) | None | |
| 130 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/CurrentUserResponse.cs` | Response for /me endpoint with user info and permissions | Properties: Id, Email, FirstName, LastName, Roles, Permissions | None | |
| 131 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/SecretRotatedResponse.cs` | Response after rotating a service account secret with warning | Properties: NewClientSecret, RotatedAt, Warning ("Save this secret now") | None | |
| 132 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/ServiceAccountCreatedResponse.cs` | Response after creating a service account with one-time secret | Properties: Id, ClientId, ClientSecret, TokenEndpoint, Scopes, Warning | None | |
| 133 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/TokenResponse.cs` | OAuth2 token response with snake_case JSON property names | Record with AccessToken, RefreshToken, TokenType, ExpiresIn, RefreshExpiresIn, Scope | None | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 134 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Mappings/EnumMappings.cs` | Bidirectional mapping between Api and Domain SAML NameID format enums | `ToDomain()` and `ToApi()` extension methods with exhaustive switch expressions | Api.Contracts.Enums (ApiSamlNameIdFormat), Domain.Enums (SamlNameIdFormat) | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 135 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/ApiKeysController.cs` | API key management endpoints (CRUD) with permission-based authorization | `CreateApiKey()`, `ListApiKeys()`, `RevokeApiKey()`; uses `[HasPermission(PermissionType.ApiKeyManage)]`; validates scopes against ApiScopes.ValidScopes; returns one-time key on creation | Application.Interfaces (IApiKeyService), Shared.Kernel (ITenantContext, ICurrentUserService) | |
| 136 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/AuthController.cs` | Authentication token endpoints (login, refresh, logout) - proxies to Keycloak | `GetToken()`, `RefreshToken()`, `Logout()`; `[AllowAnonymous]` with rate limiting ("auth"); hides Keycloak config from clients | Application.Interfaces (ITokenService) | |
| 137 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/OrganizationsController.cs` | Organization CRUD and membership management endpoints | `Create()`, `GetAll()`, `GetById()`, `AddMember()`, `RemoveMember()`, `GetMembers()`; filters by current tenant; permission-gated | Application.Interfaces (IOrganizationService), Shared.Kernel (ITenantContext) | |
| 138 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/RolesController.cs` | Role listing and permission lookup endpoints via Keycloak API | `GetRoles()` fetches from Keycloak, filters system roles; `GetRolePermissions()` uses IRolePermissionLookup | Application.Interfaces (IRolePermissionLookup), IHttpClientFactory | |
| 139 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScimController.cs` | SCIM 2.0 endpoints (Users, Groups, ServiceProviderConfig, ResourceTypes) per RFC 7644 | Full SCIM CRUD: ListUsers/GetUser/CreateUser/UpdateUser/PatchUser/DeleteUser + Group equivalents; ScimBearer auth scheme; scim rate limiting; content type "application/scim+json" | Application.Interfaces (IScimService) | |
| 140 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/ScopesController.cs` | Lists available API scopes for service account assignment | Single `List()` endpoint with optional category filter; `[HasPermission(PermissionType.ScopeRead)]` | Application.Interfaces (IApiScopeRepository) | |
| 141 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/ServiceAccountsController.cs` | Service account CRUD endpoints (create, list, get, rotate secret, update scopes, revoke) | `Create()` returns one-time secret with warning; `RotateSecret()`, `UpdateScopes()`, `Revoke()`; permission-gated via ApiKeys permissions | Application.Interfaces (IServiceAccountService) | |
| 142 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/SsoController.cs` | SSO configuration management endpoints for tenant administrators | `GetConfiguration()`, `ConfigureSaml()`, `ConfigureOidc()`, `TestConnection()`, `Activate()`, `Disable()`, `GetSamlMetadata()`, `GetOidcCallback()`; maps API enums to domain via EnumMappings | Application.Interfaces (ISsoService), Api.Mappings (EnumMappings) | |
| 143 | [ ] | `src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs` | User management endpoints scoped to current tenant | `GetUsers()` with search/pagination via org members, `GetById()`, `GetMe()` (current user), `Create()`, `AssignRole()`, `RemoveRole()`, `Deactivate()`, `Activate()`, `Delete()`; org-scoped visibility | Application.Interfaces (IKeycloakAdminService, IOrganizationService), Shared.Kernel (ITenantContext) | |

---

## Test Files

### Wallow.Identity.Tests / Domain

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 144 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Domain/ApiScopeTests.cs` | Unit tests for ApiScope entity | Create factory, properties, validation | |
| 145 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Domain/ScimConfigurationTests.cs` | Unit tests for ScimConfiguration entity | Create, Enable, Disable, UpdateSettings, RegenerateToken, MarkSynced state transitions | |
| 146 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Domain/ScimSyncLogTests.cs` | Unit tests for ScimSyncLog entity | Create factory, domain event raising | |
| 147 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Domain/ServiceAccountMetadataTests.cs` | Unit tests for ServiceAccountMetadata entity | Create, Revoke, MarkUsed, UpdateScopes lifecycle | |
| 148 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Domain/SsoConfigurationTests.cs` | Unit tests for SsoConfiguration entity | State machine transitions (Draft->Testing->Active->Disabled), ConfigureSaml/Oidc, Activate event | |

### Wallow.Identity.Tests / Application / Commands

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 149 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Commands/CreateServiceAccountCommandTests.cs` | Unit tests for CreateServiceAccountCommand | Command record construction and properties | |
| 150 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Commands/RevokeServiceAccountCommandTests.cs` | Unit tests for RevokeServiceAccountCommand | Command record construction | |
| 151 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Commands/RotateServiceAccountSecretCommandTests.cs` | Unit tests for RotateServiceAccountSecretCommand | Command record construction | |
| 152 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Commands/UpdateServiceAccountScopesCommandTests.cs` | Unit tests for UpdateServiceAccountScopesCommand | Command record construction | |

### Wallow.Identity.Tests / Application / Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 153 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Extensions/ApplicationExtensionsTests.cs` | Unit tests for DI registration | Validates AddIdentityApplication registers validators | |

### Wallow.Identity.Tests / Application / Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 154 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Handlers/ScimSyncHandlerTests.cs` | Unit tests for SCIM sync handler logic | Handler delegation to SCIM service | |
| 155 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Handlers/ServiceAccountHandlerTests.cs` | Unit tests for service account command/query handlers | CreateServiceAccountHandler, RevokeServiceAccountHandler, etc. | |
| 156 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Handlers/SsoCommandHandlerTests.cs` | Unit tests for SSO-related handler logic | SSO configuration handlers | |

### Wallow.Identity.Tests / Application / Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 157 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Queries/GetApiScopesQueryTests.cs` | Unit tests for GetApiScopesHandler | Query handling with/without category filter | |
| 158 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Queries/GetServiceAccountQueryTests.cs` | Unit tests for GetServiceAccountHandler | Single service account query | |
| 159 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Queries/GetServiceAccountsQueryTests.cs` | Unit tests for GetServiceAccountsHandler | List service accounts query | |

### Wallow.Identity.Tests / Application / Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 160 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Validators/CreateServiceAccountValidatorTests.cs` | Unit tests for CreateServiceAccountValidator | Name required/maxlength, Description maxlength, Scopes not empty | |
| 161 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Application/Validators/UpdateServiceAccountScopesValidatorTests.cs` | Unit tests for UpdateServiceAccountScopesValidator | Scopes not empty validation | |

### Wallow.Identity.Tests / Api / Authorization

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 162 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Authorization/HasPermissionAttributeTests.cs` | Unit tests for HasPermission attribute | Attribute construction and policy name generation | |

### Wallow.Identity.Tests / Api / Contracts

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 163 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Contracts/RequestContractTests.cs` | Contract tests for API request records | Request record construction and serialization | |
| 164 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Contracts/ResponseContractTests.cs` | Contract tests for API response records | Response record construction and serialization | |

### Wallow.Identity.Tests / Api / Controllers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 165 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/ApiKeysControllerTests.cs` | Unit tests for ApiKeysController | Create, List, Revoke API key endpoints with mocked services | |
| 166 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/AuthControllerTests.cs` | Unit tests for AuthController | Token, Refresh, Logout endpoints with mocked ITokenService | |
| 167 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/OrganizationsControllerTests.cs` | Unit tests for OrganizationsController | CRUD and membership endpoints with tenant filtering | |
| 168 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/RolesControllerTests.cs` | Unit tests for RolesController | Role listing and permission lookup | |
| 169 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/ScimControllerTests.cs` | Unit tests for ScimController | SCIM user/group CRUD, error handling, content types | |
| 170 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/ScopesControllerTests.cs` | Unit tests for ScopesController | Scope listing with category filter | |
| 171 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/ServiceAccountsControllerTests.cs` | Unit tests for ServiceAccountsController | Service account CRUD endpoints | |
| 172 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/SsoControllerTests.cs` | Unit tests for SsoController | SSO SAML/OIDC configuration endpoints | |
| 173 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Controllers/UsersControllerTests.cs` | Unit tests for UsersController | User CRUD with tenant scoping | |

### Wallow.Identity.Tests / Api / Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 174 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Extensions/ResultExtensionsTests.cs` | Unit tests for Result-to-ActionResult mapping | Result pattern conversion to HTTP responses | |

### Wallow.Identity.Tests / Api / Mappings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 175 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Api/Mappings/EnumMappingsTests.cs` | Unit tests for enum mapping between Api and Domain layers | Bidirectional SamlNameIdFormat mapping | |

### Wallow.Identity.Tests / Authorization

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 176 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Authorization/ApiKeyPermissionExpansionTests.cs` | Tests for API key permission expansion in middleware | API key scope-to-permission mapping | |

### Wallow.Identity.Tests / Controllers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 177 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Controllers/ApiKeysControllerScopeValidationTests.cs` | Tests for API key scope validation logic | Scope validation against ApiScopes.ValidScopes | |

### Wallow.Identity.Tests / Infrastructure

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 178 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ApiKeyAuthenticationMiddlewareTests.cs` | Unit tests for ApiKeyAuthenticationMiddleware | X-Api-Key header extraction, validation, ClaimsIdentity creation, fallthrough to JWT | |
| 179 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/CurrentUserServiceTests.cs` | Unit tests for current user service | User context extraction from claims | |
| 180 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/HasPermissionAttributeTests.cs` | Unit tests for HasPermission attribute (infrastructure-level) | Attribute behavior and policy name | |
| 181 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakAdminServiceTests.cs` | Unit tests for KeycloakAdminService | User CRUD via mocked HTTP, event publishing | |
| 182 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakAdminServiceAdditionalTests.cs` | Additional edge case tests for KeycloakAdminService | Error handling, edge cases | |
| 183 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakAdminServiceGapTests.cs` | Gap coverage tests for KeycloakAdminService | Missing coverage scenarios | |
| 184 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakIdpServiceTests.cs` | Unit tests for KeycloakIdpService | IdP CRUD, SAML/OIDC creation, certificate handling | |
| 185 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakOrganizationServiceTests.cs` | Unit tests for KeycloakOrganizationService | Organization CRUD, membership, event publishing | |
| 186 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakOrganizationServiceAdditionalTests.cs` | Additional tests for KeycloakOrganizationService | Edge cases, error handling | |
| 187 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakOrganizationServiceGapTests.cs` | Gap coverage tests for KeycloakOrganizationService | Missing coverage scenarios | |
| 188 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakServiceAccountServiceTests.cs` | Unit tests for KeycloakServiceAccountService | Service account CRUD, secret rotation, scope updates | |
| 189 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakServiceAccountServiceAdditionalTests.cs` | Additional tests for KeycloakServiceAccountService | Edge cases | |
| 190 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakSsoServiceTests.cs` | Unit tests for KeycloakSsoService | SAML/OIDC config, activation, test connection | |
| 191 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakSsoServiceGapTests.cs` | Gap coverage tests for KeycloakSsoService | Missing coverage scenarios | |
| 192 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakTokenServiceTests.cs` | Unit tests for KeycloakTokenService | Token acquisition, refresh, revocation via mocked HTTP | |
| 193 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/KeycloakTokenServiceGapTests.cs` | Gap coverage tests for KeycloakTokenService | Missing coverage scenarios | |
| 194 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/PermissionAuthorizationHandlerTests.cs` | Unit tests for PermissionAuthorizationHandler | Permission claim checking, success/fail scenarios | |
| 195 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/PermissionAuthorizationPolicyProviderTests.cs` | Unit tests for PermissionAuthorizationPolicyProvider | Dynamic policy creation, fallback behavior | |
| 196 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/PermissionExpansionMiddlewareTests.cs` | Unit tests for PermissionExpansionMiddleware | Role expansion, service account scope mapping, API key handling | |
| 197 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/PermissionRequirementTests.cs` | Unit tests for PermissionRequirement | Requirement construction | |
| 198 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/RedisApiKeyServiceTests.cs` | Unit tests for RedisApiKeyService | Key generation, hashing, Redis storage/retrieval, revocation | |
| 199 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/RedisApiKeyServiceAdditionalTests.cs` | Additional tests for RedisApiKeyService | Edge cases, expiration | |
| 200 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/RolePermissionLookupTests.cs` | Unit tests for RolePermissionLookup | Delegation to RolePermissionMapping | |
| 201 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/RolePermissionMappingTests.cs` | Unit tests for RolePermissionMapping | Admin/manager/user role expansion, unknown roles | |
| 202 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimAuthenticationMiddlewareTests.cs` | Unit tests for ScimAuthenticationMiddleware | Token validation, tenant resolution, discovery endpoint bypass | |
| 203 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimFilterExceptionTests.cs` | Unit tests for ScimFilterException | Exception construction with position tracking | |
| 204 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimFilterLexerTests.cs` | Unit tests for ScimFilterLexer | Tokenization of SCIM filter expressions | |
| 205 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimFilterParserTests.cs` | Unit tests for ScimFilterParser | AST parsing of SCIM filters | |
| 206 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimGroupServiceTests.cs` | Unit tests for ScimGroupService | SCIM group CRUD via mocked Keycloak | |
| 207 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimServiceTests.cs` | Unit tests for ScimService orchestrator | Enable/disable SCIM, token regeneration, delegation | |
| 208 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimToKeycloakTranslatorTests.cs` | Unit tests for ScimToKeycloakTranslator | Filter translation to Keycloak query params | |
| 209 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimToKeycloakTranslatorAdditionalTests.cs` | Additional tests for ScimToKeycloakTranslator | Complex filter scenarios, in-memory fallback | |
| 210 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimUserServiceTests.cs` | Unit tests for ScimUserService | SCIM user CRUD via mocked Keycloak | |
| 211 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ScimUserServiceAdditionalTests.cs` | Additional tests for ScimUserService | Edge cases, auto-activation, org membership | |
| 212 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/ServiceAccountTrackingMiddlewareTests.cs` | Unit tests for ServiceAccountTrackingMiddleware | LastUsedAt tracking, fire-and-forget, error handling | |
| 213 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/SsoClaimsSyncServiceTests.cs` | Unit tests for SsoClaimsSyncService | Claims sync, skip scenarios, group-to-role mapping | |
| 214 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/SsoClaimsSyncServiceAdditionalTests.cs` | Additional tests for SsoClaimsSyncService | Edge cases | |
| 215 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/TenantResolutionMiddlewareTests.cs` | Unit tests for TenantResolutionMiddleware | JWT org claim parsing (JSON format), admin header override, anonymous requests | |
| 216 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/UserQueryServiceTests.cs` | Unit tests for UserQueryService | Cached user email lookup, new users count | |
| 217 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/UserQueryServiceAdditionalTests.cs` | Additional tests for UserQueryService | Cache behavior, error handling | |
| 218 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Infrastructure/UserServiceTests.cs` | Unit tests for UserService | UserDto to UserInfo mapping | |

### Wallow.Identity.Tests / Integration

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 219 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Integration/PostgresDatabaseCollection.cs` | xUnit collection definition for shared Postgres test database | Database fixture sharing across integration tests | |
| 220 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Integration/ServiceAccountRepositoryTests.cs` | Integration tests for ServiceAccountRepository against real Postgres | CRUD operations, tenant filtering, IgnoreQueryFilters | |
| 221 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/Integration/SsoConfigurationRepositoryTests.cs` | Integration tests for SsoConfigurationRepository against real Postgres | SSO config CRUD, tenant isolation | |

### Wallow.Identity.Tests / Root

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 222 | [ ] | `tests/Modules/Identity/Wallow.Identity.Tests/GlobalUsings.cs` | Global using directives for test project | Common test usings (xUnit, FluentAssertions, NSubstitute, etc.) | |

### Wallow.Identity.IntegrationTests / Root

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 223 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/GlobalUsings.cs` | Global using directives for integration test project | Common integration test usings | |
| 224 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/KeycloakIntegrationTestBase.cs` | Base class for Keycloak integration tests | Test setup with Keycloak container, realm config, token acquisition | |
| 225 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccountIntegrationTestBase.cs` | Base class for service account integration tests | Test setup with service account creation and cleanup | |

### Wallow.Identity.IntegrationTests / Fakes

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 226 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Fakes/FakeServiceAccountService.cs` | Fake IServiceAccountService for integration tests | In-memory service account operations without Keycloak | |

### Wallow.Identity.IntegrationTests / OAuth2

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 227 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/ServiceAccountFlowTests.cs` | Integration tests for OAuth2 client_credentials flow | End-to-end service account token acquisition | |
| 228 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/TokenAcquisitionTests.cs` | Integration tests for token acquisition | Password grant flow against Keycloak | |
| 229 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/OAuth2/TokenValidationTests.cs` | Integration tests for token validation | JWT validation, expiration, claims | |

### Wallow.Identity.IntegrationTests / Resilience

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 230 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Resilience/KeycloakResilienceTestFactory.cs` | Test factory for Keycloak resilience testing | WebApplicationFactory setup with resilience policies | |
| 231 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Resilience/HealthCheckResilienceTests.cs` | Integration tests for health check resilience | Health endpoint behavior during Keycloak outage | |
| 232 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Resilience/KeycloakCircuitBreakerTests.cs` | Integration tests for Keycloak circuit breaker pattern | Circuit breaker open/close behavior under failure | |

### Wallow.Identity.IntegrationTests / Scim

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 233 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Scim/MockScimIdpFixture.cs` | Test fixture for mock SCIM identity provider | Mock SCIM server setup for integration tests | |
| 234 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Scim/ScimProvisioningTests.cs` | Integration tests for SCIM provisioning flow | End-to-end user/group provisioning via SCIM endpoints | |

### Wallow.Identity.IntegrationTests / ServiceAccounts

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 235 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccounts/ApiScopesTests.cs` | Integration tests for API scopes endpoints | Scope listing via API | |
| 236 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccounts/CreateServiceAccountTests.cs` | Integration tests for service account creation | End-to-end creation with Keycloak | |
| 237 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccounts/ListServiceAccountsTests.cs` | Integration tests for listing service accounts | Listing with tenant scoping | |
| 238 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccounts/RevokeServiceAccountTests.cs` | Integration tests for service account revocation | Revocation flow with Keycloak client deletion | |
| 239 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/ServiceAccounts/RotateSecretTests.cs` | Integration tests for secret rotation | Secret rotation with Keycloak | |

### Wallow.Identity.IntegrationTests / Sso

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 240 | [ ] | `tests/Modules/Identity/Wallow.Identity.IntegrationTests/Sso/SsoConfigurationTests.cs` | Integration tests for SSO configuration flow | SAML/OIDC config, activation, test connection | |
