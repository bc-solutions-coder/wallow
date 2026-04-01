# Wallow.Tests.Common

Shared test infrastructure for Wallow integration tests.

## Components

### Fixtures

- **DatabaseFixture** (`Fixtures/DatabaseFixture.cs`): PostgreSQL 18 container via Testcontainers. Implements `IAsyncLifetime`.
- **RedisFixture** (`Fixtures/RedisFixture.cs`): Valkey 8 container via Testcontainers. Implements `IAsyncLifetime`.
- **PostgresContainerFixture** (`Fixtures/PostgresContainerFixture.cs`): Lower-level Postgres container fixture.

### Factories

- **WallowApiFactory** (`Factories/WallowApiFactory.cs`): Full API pipeline combining PostgreSQL and Valkey containers with `WebApplicationFactory<Program>`. Configures `TestAuthHandler`, replaces external service dependencies with fakes, and bootstraps an admin user for `SetupMiddleware`.

### Helpers

- **TestAuthHandler** (`Helpers/TestAuthHandler.cs`): Fake authentication handler. By default authenticates as admin. Supports customization via headers:
  - `X-Test-User-Id` and `X-Test-Roles`: set a custom user identity
  - `X-Test-Auth-Skip: true`: fail authentication (simulate unauthenticated request)
  - Also parses `test-token:{userId}:{roles}` format from Bearer tokens (used by SignalR tests)
- **JwtTokenHelper** (`Helpers/JwtTokenHelper.cs`): Generates and parses test tokens in `test-token:{userId}:{roles}` format for `TestAuthHandler`.
- **TestConstants** (`Helpers/TestConstants.cs`): Fixed GUIDs for `AdminUserId`, `TestOrgId`, and `TestTenantId`.
- **LoggerAssertionExtensions** (`Helpers/LoggerAssertionExtensions.cs`): Assertion helpers for logger verification.

### Bases

- **WallowIntegrationTestBase** (`Bases/WallowIntegrationTestBase.cs`): Base class for integration tests using `WallowApiFactory`.
- **DbContextIntegrationTestBase** (`Bases/DbContextIntegrationTestBase.cs`): Base class for tests that work directly with a `DbContext`.

### Fakes

- **FakeUserManagementService**: No-op replacement for `IUserManagementService`
- **FakeUserQueryService**: Returns zero counts for `IUserQueryService`
- **NoOpHybridCache**: No-op replacement for `HybridCache`

## Configuration

`WallowApiFactory` sets these configuration values at startup:

| Key | Source |
|-----|--------|
| `ConnectionStrings:DefaultConnection` | Testcontainers PostgreSQL |
| `ConnectionStrings:Redis` | Testcontainers Valkey (with `allowAdmin=true`) |
| `OpenIddict:SigningCertPath` | Ephemeral self-signed certificate |
| `OpenIddict:EncryptionCertPath` | Ephemeral self-signed certificate |
| `AdminBootstrap:Email` | `admin@wallow.test` |

## Running Tests

Always use the test script, never bare `dotnet test`:

```bash
# Run all tests
./scripts/run-tests.sh

# Run specific module tests
./scripts/run-tests.sh shared
```
