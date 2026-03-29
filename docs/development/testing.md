# Testing Guide

This guide covers testing practices, patterns, and conventions for the Wallow platform. For specific topics, see the detailed guides:

- [E2E Testing](testing-e2e.md) -- browser-based end-to-end testing with Playwright
- [Testing with Docker](testing-docker.md) -- Docker Compose test stack and Testcontainers
- [CI/CD Pipeline](testing-ci.md) -- how tests run in GitHub Actions
- [Code Coverage](testing-coverage.md) -- coverage collection, exclusions, and thresholds

## Running Tests

Always use the test script, never bare `dotnet test`:

```bash
# Run all tests (excludes E2E)
./scripts/run-tests.sh

# Run a specific module
./scripts/run-tests.sh billing

# Run E2E tests
./scripts/run-tests.sh e2e
```

### Module Shorthands

`identity`, `billing`, `storage`, `notifications`, `messaging`, `announcements`, `inquiries`, `branding`, `apikeys`, `auth`, `auth-components`, `web`, `web-components`, `e2e`, `api`, `arch`, `shared`, `kernel`, `integration`

The script outputs structured per-assembly pass/fail counts and lists individual failed test names.

## Test Tiers

| Tier | Purpose | Infrastructure | Location |
|------|---------|---------------|----------|
| **Unit** | Individual components in isolation | None | `tests/Modules/{Module}/Wallow.{Module}.Tests/` |
| **Integration** | API endpoints with real databases | Docker (auto-managed via Testcontainers) | `tests/Wallow.Api.Tests/`, `tests/Modules/Identity/Wallow.Identity.IntegrationTests/` |
| **Architecture** | Layer dependencies and module isolation | None (reflection-based) | `tests/Wallow.Architecture.Tests/` |
| **E2E** | Complete user journeys in the browser | Docker Compose (full stack) | `tests/Wallow.E2E.Tests/` |

Additional test projects: `Wallow.Shared.Kernel.Tests`, `Wallow.Shared.Infrastructure.Tests`, `Wallow.Auth.Component.Tests`, `Wallow.Web.Component.Tests`, `Wallow.Auth.Tests`, `Wallow.Web.Tests`.

## Test Frameworks

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| FluentAssertions | Fluent assertions |
| NSubstitute | Mocking |
| Testcontainers | Docker-based integration testing |
| NetArchTest | Architecture rule validation |
| Bogus | Fake data generation |
| Microsoft.Playwright | Browser automation (E2E) |

## Test Project Structure

```
tests/
├── Wallow.Tests.Common/           # Shared test infrastructure
├── Wallow.Api.Tests/              # API integration tests
├── Wallow.Architecture.Tests/     # Architecture enforcement
├── Wallow.E2E.Tests/              # End-to-end browser tests
├── Wallow.Shared.Kernel.Tests/
├── Wallow.Shared.Infrastructure.Tests/
├── Wallow.Auth.Tests/
├── Wallow.Auth.Component.Tests/
├── Wallow.Web.Tests/
├── Wallow.Web.Component.Tests/
└── Modules/
    └── {Module}/
        └── Wallow.{Module}.Tests/
            ├── Domain/
            ├── Application/
            └── Infrastructure/
```

## Naming Convention

Use the pattern: `Method_Scenario_ExpectedResult`

```csharp
[Fact]
public async Task Handle_WithValidCommand_CreatesInvoice() { ... }

[Fact]
public async Task Handle_WithDuplicateNumber_ReturnsFailure() { ... }
```

## Unit Test Patterns

### Handler Tests

Mock dependencies with NSubstitute, test through the public `Handle` method, assert on the `Result` return value and verify repository/bus interactions.

### Validator Tests

Use FluentValidation's `TestValidate` extension to assert on specific property errors.

### Domain Entity Tests

Test entity behavior through factory methods and state transitions. Assert on domain events raised.

## Integration Tests

### WallowApiFactory

Extends `WebApplicationFactory<Program>` and manages Testcontainers for PostgreSQL and Valkey. Replaces authentication with `TestAuthHandler` and sets a fixed tenant context for tests.

### Collection Fixtures

Use `ICollectionFixture<WallowApiFactory>` (not `IClassFixture`) to share containers across test classes:

```csharp
[Collection(nameof(WallowTestCollection))]
public class InvoiceTests(WallowApiFactory factory) : WallowIntegrationTestBase(factory)
{
}
```

### Authentication

Tests use `TestAuthHandler` to bypass real OAuth2. Generate a test token via `JwtTokenHelper.GenerateToken(userId)`.

## Architecture Tests

NetArchTest enforces design rules at build time:

- Domain layer has no dependency on Application, Infrastructure, or EF Core
- No module references any other module directly (only via `Shared.Contracts`)
- All entities are sealed
- Modules are discovered dynamically by scanning for `Wallow.*.Domain.dll` -- no manual registration needed

## Best Practices

- Each test should be independent and not rely on other tests' state
- Use `IAsyncLifetime` for async setup/teardown
- Clear domain events after entity setup: `entity.ClearDomainEvents()`
- Test behavior through public interfaces, not internal state
- Use builders for complex entities, Bogus for random data, constants for shared IDs
- Keep container images aligned with `docker-compose.yml` (PostgreSQL `18-alpine`, Valkey `8-alpine`)
