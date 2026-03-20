# Identity Module Integration Tests

Integration tests for the Identity module using ASP.NET Core `WebApplicationFactory` with Testcontainers.

## Test Infrastructure

- **Base Class**: `IdentityIntegrationTestBase` and `ServiceAccountIntegrationTestBase` provide common setup
- **Test Containers**: Uses `WallowApiFactory` which spins up PostgreSQL, RabbitMQ, and Valkey via Testcontainers
- **Authentication**: Uses `TestAuthHandler` for simplified testing with configurable claims
- **Identity Seeding**: `IdentityFixture` seeds test users via ASP.NET Core Identity and OAuth2 clients via OpenIddict
- **Tenant Context**: Fixed test tenant via `TestConstants.TestTenantId`

## Test Categories

### Service Account Tests (ServiceAccounts/)
Tests the complete service account lifecycle using `FakeServiceAccountService`.

### SCIM Tests (Scim/)
Tests SCIM provisioning using WireMock to simulate IdP Admin API.

### SSO Tests (Sso/)
Tests SSO configuration using WireMock to simulate IdP Admin API.

### OAuth2 Tests (OAuth2/)
Tests OAuth2 token flows via OpenIddict:
- Token acquisition via client credentials
- Token validation against protected endpoints
- Service account flow end-to-end

### Resilience Tests (Resilience/)
Tests HTTP client resilience policies using WireMock.

## Running

```bash
dotnet build tests/Modules/Identity/Wallow.Identity.IntegrationTests
dotnet test tests/Modules/Identity/Wallow.Identity.IntegrationTests
dotnet test tests/Modules/Identity/Wallow.Identity.IntegrationTests --filter FullyQualifiedName~OAuth2
```
