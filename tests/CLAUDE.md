# Tests

- Contains all test projects: unit tests, integration tests, architecture enforcement tests, and shared test infrastructure.

## Project Naming Standard

```
tests/Modules/{Module}/
  Foundry.{Module}.Tests/              -> Unit + infrastructure tests (combined)
    Domain/                            -> Entity, value object, domain event tests
    Application/                       -> Command/query handler, validator tests
    Infrastructure/                    -> Repository, Dapper query tests
  Foundry.{Module}.IntegrationTests/   -> Full-pipeline integration tests (if needed)

tests/
  Foundry.Tests.Common/               -> Shared infrastructure (factories, fixtures, builders, helpers, fakes)
  Foundry.Api.Tests/                   -> API-level integration tests (health checks, SignalR, middleware)
  Foundry.Architecture.Tests/          -> Cross-cutting architecture enforcement (NetArchTest)
  Foundry.Messaging.IntegrationTests/  -> End-to-end messaging tests (Wolverine + RabbitMQ)
```

## Unit Test Conventions

- **Class naming**: `{Entity}{Operation}Tests` for domain, `{HandlerName}Tests` for application
- **Method naming**: `{Method}_{Condition}_{ExpectedResult}` pattern (e.g., `Create_WithValidData_ReturnsInvoiceInDraftStatus`)
- **Validator tests**: `Should_Have_Error_When_{Condition}()` / `Should_Not_Have_Error_When_{Condition}()`
- **AAA**: Implicit (no comments), separate sections with blank lines
- **Mocking**: Create NSubstitute mocks in constructor, instantiate handler once
- **Test data**: Use builders for entities needing state setup; direct factory methods for simple entities
- **Builders** are in `Foundry.Tests.Common/Builders/`, must be `public`, and clear domain events after building

## Integration Test Conventions

### Approach Selection

| Need | Use | Containers |
|------|-----|------------|
| Full API pipeline (HTTP, auth, middleware) | `FoundryApiFactory` via collection fixture | 3 (Postgres + RabbitMQ + Redis) |
| Database-only (repository, Dapper) | `DatabaseFixture` via collection fixture | 1 (Postgres) |
| Real Keycloak (OAuth2 flows) | `KeycloakTestFixture` | 4 (Keycloak + Postgres + RabbitMQ + Redis) |

### ALWAYS use collection fixtures for container sharing

Never use `IClassFixture<FoundryApiFactory>` directly -- it creates 3 new containers per test class. Use `ICollectionFixture`:

```csharp
[CollectionDefinition(nameof(MyModuleTestCollection))]
public class MyModuleTestCollection : ICollectionFixture<FoundryApiFactory> { }

[Collection(nameof(MyModuleTestCollection))]
[Trait("Category", "Integration")]
public class MyControllerTests : MyModuleIntegrationTestBase
{
    public MyControllerTests(FoundryApiFactory factory) : base(factory) { }
}
```

### Integration Test Base Classes

Each module should have a base class handling HttpClient setup, scoped services, and cleanup via `IAsyncLifetime`. Clean DB state in `InitializeAsync()` (before each test, not after).

### Authentication in Tests

- Default: `TestAuthHandler` makes all requests admin. Set `Authorization: Bearer test-token`.
- Custom user: Set `X-Test-User-Id` and `X-Test-Roles` headers.
- Skip auth: Set `X-Test-Auth-Skip: true` header.
- Real Keycloak: Only for Identity module OAuth2 tests. Use `KeycloakTestFixture`.

### Test Isolation

- **API tests:** Clean DB state in `InitializeAsync()`
- **Repository tests:** Use `TenantId.New()` per test class for natural tenant isolation
- **Redis tests:** `FLUSHDB` in `InitializeAsync()`

### Container Images

Must match `docker-compose.yml` exactly:

| Service | Image |
|---------|-------|
| PostgreSQL | `postgres:18-alpine` |
| RabbitMQ | `rabbitmq:4.2-management-alpine` |
| Valkey/Redis | `valkey/valkey:8-alpine` |
| Keycloak | `quay.io/keycloak/keycloak:26.0` |

### Trait Tagging

All integration tests MUST be tagged: `[Trait("Category", "Integration")]`

## Architecture Test Conventions

Central architecture tests in `Foundry.Architecture.Tests/` cover all modules. Do NOT create redundant module-level tests.

When adding a new module:
1. Add to `TestConstants.AllModules` array
2. Add to `_tenantAwareModules` in `MultiTenancyArchitectureTests` if module has tenant-scoped entities

Architecture tests enforce: Clean Architecture layers, module isolation (only `Shared.Contracts`), CQRS naming conventions, API conventions, multi-tenancy (`ITenantScoped`, `ITenantContext`), and module registration.

## Shared Test Infrastructure (`Foundry.Tests.Common`)

### Fixtures

| Fixture | Purpose | Container |
|---------|---------|-----------|
| `DatabaseFixture` | Standalone Postgres for repo tests | PostgreSQL |
| `RabbitMqFixture` | Standalone RabbitMQ | RabbitMQ |
| `RedisFixture` | Standalone Valkey/Redis | Valkey |
| `KeycloakFixture` | Keycloak with realm import | Keycloak |

### Factories

| Factory | Purpose |
|---------|---------|
| `FoundryApiFactory` | Full API pipeline with 3 containers + TestAuthHandler + fake services |

### Builders (all MUST be `public`)

`InvoiceBuilder` -- fluent API with state transitions, clears domain events after building. All builders must be `public`.

### Fakes

| Fake | Replaces |
|------|----------|
| `FakeUserManagementService` | `IUserManagementService` -- no-op Keycloak admin |
| `FakeInvoiceQueryService` | `IInvoiceQueryService` -- returns empty/zero |
| `FakeMeteringQueryService` | `IMeteringQueryService` -- returns null quotas |
| `FakeUserQueryService` | `IUserQueryService` -- returns zero counts |

### Helpers

| Helper | Purpose |
|--------|---------|
| `TestConstants` | Fixed GUIDs: `AdminUserId`, `TestOrgId`, `TestTenantId` |
| `TestAuthHandler` | Fake auth handler (default admin, header-customizable) |
| `JwtTokenHelper` | Generate/parse `test-token:{userId}:{roles}` format |
| `HttpClientExtensions` | `.WithAuth(userId, roles)` extension on HttpClient |
| `QueryPerformanceExtensions` | Performance assertion helpers for queries |

## Constraints

- Do not use real Keycloak in tests unless testing OAuth2 flows. Use `TestAuthHandler`.
- Do not hardcode connection strings. Testcontainers provide dynamic strings.
- Architecture tests must pass on every build. Fix violations, don't delete tests.
- Always use `postgres:18-alpine`. Never override to a different Postgres version.
- Integration tests needing the full API pipeline use `FoundryApiFactory`. Unit tests must not.
- All classes in `Foundry.Tests.Common` consumed by other test projects MUST be `public`.
- All shared fixtures MUST set `.WithCleanUp(true)` for Ryuk-based container cleanup.
