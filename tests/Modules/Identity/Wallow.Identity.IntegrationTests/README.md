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

### SCIM Tests (`Scim/`)

Tests SCIM provisioning using WireMock to simulate IdP Admin API.

### SSO Tests (`Sso/`)

Tests SSO configuration using WireMock to simulate IdP Admin API.

### OAuth2 Tests (`OAuth2/`)

Tests OAuth2 token flows via OpenIddict:
- Token acquisition via client credentials
- Token validation against protected endpoints
- Service account flow end-to-end

### Resilience Tests (`Resilience/`)

Tests HTTP client resilience policies using WireMock.

## Running

```bash
# Run all identity integration tests
./scripts/run-tests.sh integration

# Run all identity tests (unit + integration)
./scripts/run-tests.sh identity
```
