# Testing with Docker

This guide covers the Docker infrastructure used for running tests locally and in CI.

## Test Compose Stack

`docker/docker-compose.test.yml` is a self-contained compose file separate from the development `docker-compose.yml`. It brings up the full stack — API plus both React apps — on ports distinct from the dev environment so both can run simultaneously. It is used for CI integration runs and for exercising the browser suites against containerized apps.

### Infrastructure Services

| Service | Image | Host Port | Purpose |
|---------|-------|-----------|---------|
| `postgres` | `postgres:18-alpine` | 5442 | Database (dev uses 5432) |
| `valkey` | `valkey/valkey:8-alpine` | 6389 | Cache (dev uses 6379) |
| `mailpit` | `axllent/mailpit:v1.22` | 8035 (UI), 1035 (SMTP) | Email capture |
| `garage` | `wallow-garage:v2.2.0-test` | 3910, 3913 | S3-compatible storage |

### Application Services

| Service | Image | Host Port | Purpose |
|---------|-------|-----------|---------|
| `wallow-api` | `wallow-api:test` | 5050 | API server |
| `wallow-auth` | `wallow-auth-react:test` | 5051 | Auth app (TanStack Start; same-origin reverse proxy to the API) |
| `wallow-web` | `wallow-web-react:test` | 5053 | Web app (TanStack Start dashboard + BFF) |

The two app services build from their own Dockerfiles with the repo root as build context
(`apps/wallow-auth/Dockerfile`, `apps/wallow-web/Dockerfile`) so the workspace `workspace:*`
dependencies resolve. No `dotnet publish` of a Blazor app is involved.

## Starting the Stack Manually

To start the test environment for manual exploration or repeated test runs:

```bash
# Build the API image
dotnet publish api/src/Wallow.Api/Wallow.Api.csproj -c Release /t:PublishContainer \
  -p:ContainerImageTag=test -p:ContainerRepository=wallow-api

# Build the app images and start everything (compose builds wallow-auth / wallow-web
# from their Dockerfiles)
docker compose -f docker/docker-compose.test.yml up -d --build

# Verify health
curl http://localhost:5050/health/ready
curl http://localhost:5051/health
curl http://localhost:5053/health
```

## Tearing Down

Stop the stack and remove its volumes when you are done:

```bash
docker compose -f docker/docker-compose.test.yml down -v
```

The browser E2E suites live in the React apps and run through Playwright (`pnpm --filter
./apps/wallow-auth test:e2e`); see [E2E Testing](testing-e2e.md). They can be pointed at this
stack by overriding the app's `E2E_BASE_URL` (for example `http://localhost:5051` for the auth
app).

## OIDC Configuration

The test compose file configures OIDC so browser-facing URLs use `localhost` while container-to-container communication uses Docker networking:

- **API:** `OpenIddict__Issuer` set to `http://localhost:5050` so tokens match the browser URL.
- **Auth:** The `wallow-auth` app is a same-origin reverse proxy; it reads `WALLOW_API_INTERNAL_URL: http://wallow-api:8080` to reach the API container.
- **Web:** The `wallow-web` BFF uses `OIDC_ISSUER: http://localhost:5050` for browser redirects and `OIDC_METADATA_URL: http://host.docker.internal:5050/.well-known/openid-configuration` for container-to-container OIDC discovery.

On Linux, add the hosts entry:

```bash
echo "127.0.0.1 host.docker.internal" | sudo tee -a /etc/hosts
```

## Integration Tests (Testcontainers)

Integration tests use Testcontainers to automatically manage PostgreSQL and Valkey containers. No manual Docker setup is needed -- `WallowApiFactory` handles container lifecycle via `IAsyncLifetime`.

Container images should match the test compose stack: `postgres:18-alpine` and `valkey/valkey:8-alpine`.
