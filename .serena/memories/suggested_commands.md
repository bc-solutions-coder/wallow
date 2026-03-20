# Suggested Commands for Wallow

## Infrastructure

```bash
# Start infrastructure (Postgres, RabbitMQ, Mailpit)
docker compose up -d

# Reset infrastructure completely
docker compose down -v
docker compose up -d

# Check running containers
docker compose ps
```

## Build & Run

```bash
# Build solution
dotnet build

# Run the API
dotnet run --project src/Wallow.Api

# Or run everything including API in Docker
docker compose --profile all up -d
```

## Testing

```bash
# Run all tests
dotnet test

# Run specific module tests (example: Billing)
dotnet test tests/Modules/Billing/Modules.Billing.Tests

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests (requires Docker)
dotnet test --filter "Category=Integration"
```

## EF Core Migrations

```bash
# Add migration (example for Billing)
dotnet ef migrations add MigrationName \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext

# Apply migrations
dotnet ef database update \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext
```

## Local URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | - |
| API Docs (Scalar) | http://localhost:5000/scalar/v1 | - |
| RabbitMQ | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | - |
| PostgreSQL | localhost:5432 | See `docker/.env` |

## CI/CD & Releases

```bash
# Create a version tag and trigger Docker build
git tag v0.1.0
git push origin v0.1.0

# Pull image on deployment server
docker pull ghcr.io/bc-solutions-coder/wallow:0.1.0
```

Images are published to GitHub Container Registry on version tags.

## Utility Commands (macOS/Darwin)

```bash
# Git operations
git status
git diff
git log --oneline -10
git branch -a

# File search
find . -name "*.cs" -type f
find . -path "*/bin" -prune -o -name "*.cs" -print

# Text search in code
grep -r "pattern" --include="*.cs" .
```
