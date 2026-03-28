# Testing with Docker

This guide covers the Docker infrastructure used for running tests locally and in CI.

## Test Compose Stack

E2E tests use `docker/docker-compose.test.yml`, a self-contained compose file separate from the development `docker-compose.yml`. All services use different ports to allow both environments to run simultaneously.

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
| `wallow-auth` | `wallow-auth:test` | 5051 | Auth Blazor app |
| `wallow-web` | `wallow-web:test` | 5053 | Web Blazor app |

## Starting the Stack Manually

To start the test environment for manual exploration or repeated test runs:

```bash
# Build container images
dotnet publish src/Wallow.Api/Wallow.Api.csproj -c Release /t:PublishContainer \
  -p:ContainerImageTag=test -p:ContainerRepository=wallow-api

dotnet publish src/Wallow.Auth/Wallow.Auth.csproj -c Release /t:PublishContainer \
  -p:ContainerImageTag=test -p:ContainerRepository=wallow-auth

dotnet publish src/Wallow.Web/Wallow.Web.csproj -c Release /t:PublishContainer \
  -p:ContainerImageTag=test -p:ContainerRepository=wallow-web

# Start
docker compose -f docker/docker-compose.test.yml up -d

# Verify health
curl http://localhost:5050/health/ready
curl http://localhost:5051/health
curl http://localhost:5053/health
```

## Automatic Container Management

When running `./scripts/run-tests.sh e2e`, the `DockerComposeFixture` automatically:

1. Detects whether services are already running (checks the API health endpoint).
2. If not running, starts via `docker compose -f docker/docker-compose.test.yml up -d --build`.
3. Waits for all health checks to pass.
4. After tests complete, tears down with `docker compose down -v`.

Set `E2E_EXTERNAL_SERVICES=true` to skip automatic management (when you started them manually or in CI).

## OIDC Configuration

The test compose file configures OIDC so browser-facing URLs use `localhost` while container-to-container communication uses Docker networking:

- **API:** `OpenIddict__Issuer` set to `http://localhost:5050` so tokens match the browser URL.
- **Auth:** Uses `host.docker.internal:5050` for server-to-server API calls.
- **Web:** `Oidc__Authority: http://localhost:5050` for browser redirects, `Oidc__MetadataAddress: http://host.docker.internal:5050/.well-known/openid-configuration` for container-to-container OIDC discovery.

On Linux, add the hosts entry:

```bash
echo "127.0.0.1 host.docker.internal" | sudo tee -a /etc/hosts
```

## Integration Tests (Testcontainers)

Integration tests use Testcontainers to automatically manage PostgreSQL and Valkey containers. No manual Docker setup is needed -- `WallowApiFactory` handles container lifecycle via `IAsyncLifetime`.

Container images should match the test compose stack: `postgres:18-alpine` and `valkey/valkey:8-alpine`.
