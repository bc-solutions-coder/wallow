# Identity Module Integration Tests

Integration tests for the Identity module using `WebApplicationFactory` with Testcontainers.

## Test Infrastructure

- **IdentityIntegrationTestBase**: Base class extending `WallowIntegrationTestBase` for standard identity tests.
- **ServiceAccountIntegrationTestBase**: Base class extending `WallowIntegrationTestBase` for service account tests using `ServiceAccountTestFactory`.
- **IdentityFixture**: Seeds test users via ASP.NET Core Identity and OAuth2 clients via OpenIddict.
- **Test Containers**: `WallowApiFactory` spins up PostgreSQL and Valkey via Testcontainers.
- **Authentication**: `TestAuthHandler` provides configurable claims via headers.
- **Tenant Context**: Fixed test tenant via `TestConstants.TestTenantId`.

## Test Categories

### Service Account Tests (`ServiceAccounts/`)

Tests the complete service account lifecycle using `FakeServiceAccountService`.

### OAuth2 Tests (`OAuth2/`)

Tests OAuth2 token flows via OpenIddict:
- Token acquisition via client credentials
- Token validation against protected endpoints
- Service account flow end-to-end

### Resilience Tests (`Resilience/`)

Tests HTTP client resilience policies using WireMock.

### Other directories

`Apps/`, `Invitations/`, `Memberships/`, `Mfa/`, `Organizations/`, `Settings/` and `Users/` each
cover the corresponding controller and service surface. `Fakes/` holds test doubles, not tests.

## Running

```bash
# This suite, plus every other Category=Integration test in the solution.
./scripts/run-tests.sh integration

# The same, alongside the fast suites.
./scripts/run-tests.sh all
```

> [!IMPORTANT]
> `./scripts/run-tests.sh identity` does **not** run this suite. That argument resolves to
> `Wallow.Identity.Tests` only, and — because the argument is neither `integration` nor `all` — the
> script also appends `--filter "Category!=E2E&Category!=Integration"`, so it skips
> `[Trait("Category", "Integration")]` tests wherever they live. `integration` and `all` are the
> only two arguments that do not add that exclusion; both select by category across
> `api/Wallow.slnx`, so they run this project **and** the integration tests in
> `Wallow.Api.Tests`, `Wallow.Storage.Tests`, `Wallow.Announcements.Tests`,
> `Wallow.Inquiries.Tests`, `Wallow.Identity.Tests` and `Wallow.Shared.Infrastructure.Tests`.
>
> These tests need Docker running (Testcontainers PostgreSQL and Valkey).
