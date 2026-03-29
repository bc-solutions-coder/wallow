# CI/CD Test Pipeline

Tests run in GitHub Actions via `.github/workflows/ci.yml`. The pipeline runs four test jobs in parallel after the build job.

## Jobs

**`build`** -- Restores, builds, and checks code formatting.

**`unit-tests`** -- Runs all tests excluding `Category=Integration` and `Category=E2E`. Uploads coverage artifacts.

**`integration-tests`** -- Runs tests with `Category=Integration`. Uses GitHub Actions service containers for PostgreSQL and Valkey (not Testcontainers). Uploads coverage artifacts.

**`e2e`** -- Runs E2E tests against the full stack. Triggered on pushes to `main` and pull requests.

## E2E in CI

The E2E job follows this sequence:

1. **Build container images** using `dotnet publish /t:PublishContainer` with tag `test` for API, Auth, and Web.
2. **Configure host.docker.internal** (Linux: adds `127.0.0.1 host.docker.internal` to `/etc/hosts`).
3. **Start test environment** via `docker compose -f docker/docker-compose.test.yml up -d`.
4. **Build E2E tests and install Playwright** (runs in parallel with container startup).
5. **Wait for services** by polling health endpoints (up to 150 seconds).
6. **Run E2E tests** with `E2E_EXTERNAL_SERVICES=true`.
7. **Upload failure artifacts** on test failure (screenshots, traces, videos). Retained for 7 days.
8. **Tear down** the test environment.

## Post-Test Jobs

**`push-images`** -- After all tests pass on `main`, publishes container images to GHCR.

**`merge-coverage`** -- Merges unit and integration coverage reports, generates an HTML summary, and enforces a 90% line coverage threshold.

## Reading CI Results

- Test results appear in the GitHub Actions job logs.
- On E2E failure, download the `e2e-failure-artifacts` artifact for screenshots, page HTML, and traces.
- Coverage reports are available as the `coverage-report` artifact.
