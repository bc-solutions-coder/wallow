# Testing Guide

This guide covers testing practices, patterns, and conventions for the Wallow platform. For specific topics, see the detailed guides:

- [E2E Testing](testing-e2e.md) -- browser-based end-to-end testing with Playwright
- [Testing with Docker](testing-docker.md) -- Docker Compose test stack and Testcontainers
- [CI/CD Pipeline](testing-ci.md) -- how tests run in GitHub Actions
- [Code Coverage](testing-coverage.md) -- coverage collection, exclusions, and thresholds

## Running Tests

Always use the test script for the .NET suites, never bare `dotnet test`:

```bash
# Run all tests (excludes Integration; E2E lives in the React apps)
./scripts/run-tests.sh

# Run a specific module
./scripts/run-tests.sh identity
```

E2E tests are browser suites that live with the frontend apps, not in the .NET solution. Run
them per app, for example:

```bash
pnpm --filter ./apps/wallow-auth test:e2e
```

See [E2E Testing](testing-e2e.md) for details.

### Module Shorthands

`identity`, `storage`, `notifications`, `announcements`, `inquiries`, `branding`, `apikeys`, `api`, `arch`, `shared`, `kernel`, `integration`

The script outputs structured per-assembly pass/fail counts and lists individual failed test names.

## Test Tiers

| Tier | Purpose | Infrastructure | Location |
|------|---------|---------------|----------|
| **Unit** | Individual components in isolation | None | `api/tests/Modules/{Module}/Wallow.{Module}.Tests/` |
| **Integration** | API endpoints with real databases | Docker (auto-managed via Testcontainers) | `api/tests/Wallow.Api.Tests/`, `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/` |
| **Architecture** | Layer dependencies and module isolation | None (reflection-based) | `api/tests/Wallow.Architecture.Tests/` |
| **E2E** | Complete user journeys in the browser | Running app + API | `apps/wallow-auth/e2e/`, `apps/wallow-web/e2e/` |

The E2E tier is Playwright (`@playwright/test`) and lives in the React apps, not the .NET
solution. Additional .NET test projects: `Wallow.Shared.Kernel.Tests`, `Wallow.Shared.Infrastructure.Tests`.

## Test Frameworks

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| AwesomeAssertions | Fluent assertions |
| NSubstitute | Mocking |
| Testcontainers | Docker-based integration testing |
| NetArchTest | Architecture rule validation |
| Bogus | Fake data generation |

E2E browser automation uses `@playwright/test` in the React apps (see [E2E Testing](testing-e2e.md)).

## Test Project Structure

```
api/tests/
├── Wallow.Tests.Common/           # Shared test infrastructure
├── Wallow.Api.Tests/              # API integration tests
├── Wallow.Architecture.Tests/     # Architecture enforcement
├── Wallow.Shared.Kernel.Tests/
├── Wallow.Shared.Infrastructure.Tests/
└── Modules/
    └── {Module}/
        └── Wallow.{Module}.Tests/
            ├── Domain/
            ├── Application/
            └── Infrastructure/
```

Browser E2E suites live with the frontend apps, e.g. `apps/wallow-auth/e2e/`.

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
