# Testing Guide

This guide covers the .NET backend suites, the frontend Vitest suites, code coverage, the
containerised Docker test stack, and how everything runs in CI. Browser end-to-end testing has
its own page: [E2E Testing](testing-e2e.md).

## Backend Tests

### Always use the test script

Run the .NET suites through `./scripts/run-tests.sh`, never bare `dotnet test`:

```bash
./scripts/run-tests.sh              # the fast suites in api/Wallow.slnx; integration EXCLUDED
./scripts/run-tests.sh integration  # ONLY Category=Integration, solution-wide (needs Docker)
./scripts/run-tests.sh all          # both, in one run (needs Docker)
./scripts/run-tests.sh identity     # one module
```

The script logs each assembly to a TRX file (`--logger "trx;LogFilePrefix=results"` into a
temp directory), parses those TRX files, and prints structured per-assembly pass/fail/skip
counts plus the names of individual failed tests. It exits with the underlying `dotnet test`
exit code.

**Why bare `dotnet test` is banned:** the script passes
`--settings api/tests/coverage.runsettings` automatically. That runsettings file excludes
generated code — EF Core migrations, `LoggerMessage.g.cs`, and friends — which would otherwise
be counted as uncovered lines and deflate every coverage number. Coverage exclusions live
**only** in `api/tests/coverage.runsettings`; never duplicate them into a project file or a
CI command line.

### Integration tests are opt-in, and the script says so

Every invocation except `integration` and `all` appends
`--filter "Category!=E2E&Category!=Integration"`, because that tier needs live infrastructure
(Testcontainers, hence Docker). Those runs print `SCOPE: fast suites only` beside their totals and
close with an `INTEGRATION TESTS DID NOT RUN` banner naming the two commands that do run them —
a green total from a bare run is **not** evidence that the integration guards passed.

`integration` and `all` select by **category across `api/Wallow.slnx`**, not by project, because
integration tests live in seven assemblies. `Wallow.Api.Tests` is the one that matters most:
`HandlerCodegenTests` compiles every discovered Wolverine handler and is the only guard that a
handler dependency the codegen cannot inline-construct fails in the suite rather than in a
dead-letter queue.

A run that executes zero tests reports `RESULT: FAIL` and exits nonzero — a selector that matches
nothing is a broken selector, not a pass.

### Module shorthands

`identity`, `storage`, `notifications`, `announcements`, `inquiries`, `branding`, `apikeys`,
`api`, `arch` (or `architecture`), `seeder`, `migrations`, `shared`, `kernel`, `integration`,
`all`. Shorthands are matched case-insensitively.

A **second** argument — `integration` or `all` — narrows that tier to whatever the first argument
selected, so you can iterate on one assembly's integration tests without running the other six:

```bash
./scripts/run-tests.sh api integration        # just Wallow.Api.Tests' Category=Integration tests
./scripts/run-tests.sh storage integration    # just Storage's Testcontainers suites
./scripts/run-tests.sh api/tests/Wallow.Api.Tests all   # a path works as the first argument too
```

Any other second argument is rejected with exit code 2 rather than silently ignored.

Anything the script does not recognise as a shorthand is passed through as a project path, so
`./scripts/run-tests.sh api/tests/Wallow.Api.Tests` works too.

## Test Tiers

| Tier | Purpose | Infrastructure | Location |
|------|---------|---------------|----------|
| **Unit** | Individual components in isolation | None | `api/tests/Modules/{Module}/Wallow.{Module}.Tests/` |
| **Integration** | API endpoints against real databases | Docker (auto-managed via Testcontainers) | `api/tests/Wallow.Api.Tests/`, `api/tests/Modules/Identity/Wallow.Identity.IntegrationTests/` |
| **Architecture** | Layer dependencies and module isolation | None (reflection-based) | `api/tests/Wallow.Architecture.Tests/` |
| **Component (frontend)** | One React component in a real browser | Headless Chromium | `apps/*/src/**/*.test.tsx` |
| **E2E** | Complete user journeys in the browser | Running app + API | `apps/wallow-auth/e2e/`, `apps/wallow-web/e2e/` |

The E2E tier is Playwright (`@playwright/test`) and lives in the React apps, not in the .NET
solution. Additional .NET test projects: `Wallow.Shared.Kernel.Tests`,
`Wallow.Shared.Infrastructure.Tests`, `Wallow.AppHost.Tests`, `Wallow.MigrationService.Tests`,
`Wallow.SeederService.Tests`, and the `Benchmarks/` projects.

## Test Frameworks

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| AwesomeAssertions | Fluent assertions |
| NSubstitute | Mocking |
| Testcontainers | Docker-based integration testing |
| NetArchTest | Architecture rule validation |
| Bogus | Fake data generation |

## Test Project Structure

```
api/tests/
├── coverage.runsettings           # Coverage config — the single source of exclusions
├── Directory.Build.props
├── Benchmarks/                    # BenchmarkDotNet projects
├── Wallow.Tests.Common/           # Shared test infrastructure
├── Wallow.Api.Tests/              # API integration tests
├── Wallow.AppHost.Tests/          # Aspire host wiring
├── Wallow.Architecture.Tests/     # Architecture enforcement
├── Wallow.MigrationService.Tests/
├── Wallow.SeederService.Tests/
├── Wallow.Shared.Kernel.Tests/
├── Wallow.Shared.Infrastructure.Tests/
└── Modules/
    └── {Module}/
        └── Wallow.{Module}.Tests/
            ├── Domain/
            ├── Application/
            └── Infrastructure/
```

## Naming Convention

Use the pattern `Method_Scenario_ExpectedResult`:

```csharp
[Fact]
public async Task Handle_WithValidCommand_CreatesInvoice() { ... }

[Fact]
public async Task Handle_WithDuplicateNumber_ReturnsFailure() { ... }
```

## Unit Test Patterns

### Handler tests

Mock dependencies with NSubstitute, test through the public `Handle` method, assert on the
`Result` return value, and verify repository/bus interactions.

### Validator tests

Use FluentValidation's `TestValidate` extension to assert on specific property errors.

### Domain entity tests

Test entity behaviour through factory methods and state transitions. Assert on the domain
events raised.

## Integration Tests

### WallowApiFactory

Extends `WebApplicationFactory<Program>` and manages Testcontainers for PostgreSQL and Valkey.
It replaces authentication with `TestAuthHandler` and sets a fixed tenant context. No manual
Docker setup is needed — the factory drives the container lifecycle via `IAsyncLifetime`.
Keep the container images aligned with the compose stacks: `postgres:18-alpine` and
`valkey/valkey:8-alpine`.

### Collection fixtures

Use `ICollectionFixture<WallowApiFactory>` (not `IClassFixture`) so containers are shared
across test classes:

```csharp
[Collection(nameof(WallowTestCollection))]
public class InvoiceTests(WallowApiFactory factory) : WallowIntegrationTestBase(factory)
{
}
```

### Authentication

Tests use `TestAuthHandler` to bypass real OAuth2. Generate a test token with
`JwtTokenHelper.GenerateToken(userId)`.

## Architecture Tests

NetArchTest enforces the design rules on every run:

- The Domain layer takes no dependency on Application, Infrastructure, or EF Core.
- No module references another module directly — only via `Shared.Contracts`.
- All entities are sealed.
- Modules are discovered dynamically by scanning for `Wallow.*.Domain.dll`, so no manual
  registration is needed.

## Frontend Tests

`pnpm test` (which is `turbo run test`, i.e. `vitest run` per package, topologically ordered and
cached in `.turbo/`) drives the frontend suites. Vitest runs a **two-project split**, configured by the shared `createVitestProjects`
preset in `packages/testing` and wired up by each app's `vitest.config.ts`:

| Project | Includes | Runtime |
|---------|----------|---------|
| **node** | `src/**/*.test.ts`, plus every `src/**/*.ssr.test.tsx` | Node |
| **browser** | `src/**/*.test.tsx` minus the `*.ssr.test.tsx` specs | Headless Chromium |

The browser project uses the Vitest `playwright()` factory provider from
`@vitest/browser-playwright` with `headless: true` and a single `chromium` instance;
`vitest-browser-react` supplies `render` and the locator API, and assertions come from
`@vitest/expect` locator matchers.

**jsdom, happy-dom, and jest are banned in this repo.** Anything that touches the DOM —
rendering a component, reading layout, focus, or computed styles — runs in a real browser.
Do not add a `// @vitest-environment jsdom` pragma or a jsdom/happy-dom dependency; either one
regresses the suite off real-browser fidelity.

The `*.ssr.test.tsx` suffix exists for specs that render through `react-dom/server`
(`renderToString`) or assert a route's `beforeLoad` redirect, and never mount a live DOM.
Routing those into Chromium buys nothing and costs real per-test browser overhead. It is a
naming convention rather than a per-app list precisely so a new SSR spec lands on the node
project the moment it is created — the config needs no edit. (`createVitestProjects` still
accepts an explicit `nodeTsxSpecs` array, which replaces the convention; no package in this
repo uses it.)

Playwright E2E specs are deliberately kept out of Vitest: the Vitest `include` globs are
scoped to `src/**`, while Playwright specs live only in `e2e/` (or wallow-web's
`e2e-cross-app/`).

## Code Coverage

Coverage is collected automatically by `./scripts/run-tests.sh` using
`api/tests/coverage.runsettings`. The format is Cobertura, the include filter is
`[Wallow.*]*`, test assemblies are excluded (`IncludeTestAssembly=false`), and SourceLink is
enabled.

### Exclusions

Excluded by assembly/type filter:

- EF Core migrations (`*.Migrations.*`)
- `Program` and `Startup` classes
- Module registration extensions (`*.Extensions.*Module*`) and `*WallowModules`
- Assembly info (`*AssemblyInfo`)
- Test and benchmark assemblies (`Wallow.Tests.Common`, `Wallow.Benchmarks`, `*.Tests`,
  `*.IntegrationTests`)
- `System.Runtime.CompilerServices.*`

Excluded by file:

- `**/Migrations/**/*.cs`
- Generated sources: `**/Logging.g.cs`, `**/LoggerMessage.g.cs`, `**/RegexGenerator.g.cs`,
  `**/*.generated.cs`
- Design-time and factory classes: `**/DesignTimeTenantContext.cs`, `**/*DbContextFactory.cs`

Excluded by attribute: `CompilerGeneratedAttribute`, `ExcludeFromCodeCoverageAttribute`.

### Viewing coverage locally

```bash
# Run tests — coverage is collected automatically
./scripts/run-tests.sh

# Install the report generator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate an HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

open coverage-report/index.html
```

## Docker Test Stack

`docker/docker-compose.test.yml` is a self-contained compose file, separate from the
development `docker-compose.yml`. It brings up infrastructure plus the API and both React apps
on ports distinct from the dev environment, so both stacks can run at once.

> **Drive it with `./scripts/e2e.sh`, not by hand.** A bare
> `docker compose -f docker/docker-compose.test.yml up` fails: the `wallow-migrations`,
> `wallow-seeder`, and `wallow-api` services declare an `image:` with **no `build:` block**, so
> they expect prebuilt `:test` images that a plain `up` never produces. `scripts/e2e.sh`
> publishes those images (via `dotnet publish /t:PublishContainer`) before bringing the stack
> up. See [E2E Testing](testing-e2e.md).

### Infrastructure services

| Service | Image | Host port | Purpose |
|---------|-------|-----------|---------|
| `postgres` | `postgres:18-alpine` | 5442 | Database (dev uses 5432) |
| `valkey` | `valkey/valkey:8-alpine` | 6389 | Cache (dev uses 6379) |
| `mailpit` | `axllent/mailpit:v1.22` | 8035 (UI), 1035 (SMTP) | Email capture — the passwordless and reset-password E2E specs read mail back over its HTTP API |
| `garage` | `wallow-garage:test` | 3910, 3913 | S3-compatible storage. Built from `docker/images/garage` — the same image the dev stack builds, just tagged `:test` instead of `:v2.2.0`, with test-only credentials and bucket passed as env. |

### Migration and seed services

| Service | Image | Purpose |
|---------|-------|---------|
| `wallow-migrations` | `wallow-migrations:test` | Applies EF migrations, then exits. Gates on `postgres` being healthy. |
| `wallow-seeder` | `wallow-seeder:test` | Seeds roles, scopes, the admin, and OIDC clients from `api/seed.json`. Gates on `wallow-migrations` completing successfully, and overrides the `wallow-web-client` redirect URIs for the test port (`http://localhost:5053/bff/callback`). |

### Application services

| Service | Image | Host port | Purpose |
|---------|-------|-----------|---------|
| `wallow-api` | `wallow-api:test` | 5050 | API server |
| `wallow-auth` | `wallow-auth-react:test` | 5051 | Auth app (TanStack Start; a pure same-origin reverse proxy to the API) |
| `wallow-web` | `wallow-web-react:test` | 5053 | Web app (TanStack Start dashboard + BFF) |
| `bff-example` | `wallow-bff-example:test` | 3003 | SDK BFF reference host, authenticating as the `bcordes-bff` client. **Not started by CI** — the CI job brings up only `wallow-auth` and its transitive dependencies. |

The three Node services build from `apps/wallow-auth/Dockerfile` and `apps/wallow-web/Dockerfile`
with the **repo root** as build context, so the `workspace:*` dependencies resolve. The image
tags are deliberately distinct from the deleted Blazor apps' tags so a stale image can never be
silently reused.

### OIDC configuration

The test compose file splits browser-facing URLs from container-to-container ones:

- **API:** `OpenIddict__Issuer` is `http://localhost:5050`, so tokens match the URL the browser
  sees.
- **Auth:** `wallow-auth` is a pure reverse proxy holding no session. It reads exactly three env
  vars — `PORT`, `HOST`, and `WALLOW_API_INTERNAL_URL` (`http://wallow-api:8080`).
- **Web:** the `wallow-web` BFF uses `OIDC_ISSUER: http://localhost:5050` for browser redirects
  and `OIDC_METADATA_URL: http://host.docker.internal:5050/.well-known/openid-configuration` for
  container-side discovery. Sessions live in Valkey so logout truly revokes.

On Linux, add the hosts entry that `host.docker.internal` needs:

```bash
echo "127.0.0.1 host.docker.internal" | sudo tee -a /etc/hosts
```

## CI

Tests run in GitHub Actions via `.github/workflows/ci.yml`. The workflow triggers on
**pull requests targeting `main` only** — there is no `push` trigger and no image-publishing
job in this workflow.

| Job | Depends on | What it does |
|-----|-----------|--------------|
| `build` | — | Restores, builds `api/Wallow.slnx` in Release, and runs `dotnet format --verify-no-changes`. Caches the build output for the downstream jobs. |
| `unit-tests` | `build` | `dotnet test --filter "Category!=Integration&Category!=E2E"` with `--settings api/tests/coverage.runsettings`. Uploads the `coverage-unit` artifact. |
| `integration-tests` | `build` | `dotnet test --filter "Category=Integration"`. PostgreSQL comes from a GitHub Actions service container; Valkey is started with a `docker run` step and polled until it answers `PING`. Uploads the `coverage-integration` artifact. |
| `cross-tenant-tests` | `build` | `dotnet test --filter "Category=CrossTenant"` against the same Postgres service container and `docker run` Valkey. This is the tenant-isolation gate; it does not upload coverage. |
| `docker-images-app` | `build` | Publishes the API, migration, and seeder container images plus the `wallow-auth-react` / `wallow-web-react` Docker builds, for both `linux-x64` and `linux-arm64`, then caches them as a tarball. |
| `docker-images-infra` | `build` | Builds the `garage` image via `docker compose -f docker/docker-compose.test.yml build garage` and the Postgres replica image, then caches them. |
| `e2e-tests` | `docker-images-app`, `docker-images-infra` | Loads the cached images, installs Chromium, and runs `./scripts/e2e.sh` with `E2E_SKIP_IMAGE_BUILD=1`, `E2E_UP_SERVICE=wallow-auth`, `E2E_BASE_URL=http://localhost:5051`. That one script runs all three Playwright suites — wallow-auth, wallow-web, and the cross-app suite (both its first-party and external-origin specs). Uploads the `playwright-report-wallow-auth` and `playwright-report-wallow-web` artifacts. |
| `fork-smoke` | — | Runs `./scripts/fork-smoke.sh` outside the checkout: packs `packages/sdk` and `packages/styles` and builds a scratch app against the tarballs, proving an out-of-workspace consumer can install them. |
| `merge-coverage` | `unit-tests`, `integration-tests` | Merges the two coverage artifacts with ReportGenerator, enforces the coverage threshold, and uploads the `coverage-report` artifact. |

**The frontend gate is a different workflow.** `.github/workflows/js.yml` has a single `build` job
that runs `pnpm lint`, `lint:tests`, `lint:manifests`, `lint:deps`, `lint:env`, `format:check`,
`turbo run build typecheck test`, and `check:exports` — the same set `pnpm check` runs locally.
`ci.yml` does not run any frontend unit tests.

Because `docker-images-app` prebuilds and caches the `:test` images, the `e2e-tests` job sets
`E2E_SKIP_IMAGE_BUILD=1` and loads them instead of rebuilding. That knob suppresses **both** halves
of the runner's image work — the `dotnet publish` of the API/migration/seeder images and compose's
`--build` of the services with a build block — which is why a local run, where it is unset, always
builds against the current tree. `bff-example` is the one image no job caches, so compose builds it
either way. Setting `E2E_BASE_URL` makes Playwright drive the containerised `wallow-auth` app on
`:5051` directly rather than booting a local dev server.

### Coverage threshold

`merge-coverage` extracts the merged `line-rate` from `Cobertura.xml` and fails the job — and
the pipeline — if line coverage is below **90%**. If neither test job produced a coverage
artifact, the job emits a warning and skips reporting rather than failing.

### Reading CI results

- Test results appear in the GitHub Actions job logs.
- On E2E failure, download the `playwright-report-wallow-auth` artifact for the HTML report,
  traces, and screenshots (retained 5 days).
- The merged coverage report is the `coverage-report` artifact (retained 30 days).

## Best Practices

- Keep each test independent; never rely on another test's state.
- Use `IAsyncLifetime` for async setup and teardown.
- Clear domain events after entity setup: `entity.ClearDomainEvents()`.
- Test behaviour through public interfaces, not internal state.
- Use builders for complex entities, Bogus for random data, and constants for shared IDs.
- Keep Testcontainers images aligned with the compose files (`postgres:18-alpine`,
  `valkey/valkey:8-alpine`).
