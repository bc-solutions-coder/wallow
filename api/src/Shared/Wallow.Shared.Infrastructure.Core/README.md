# Wallow.Shared.Infrastructure.Core

Cross-cutting infrastructure every module builds on. Modules do not reference it directly — they
get it transitively through `Wallow.Shared.Infrastructure`. Its direct referencers are
`Wallow.Shared.Infrastructure`, `.BackgroundJobs`, `Wallow.MigrationService` and
`Wallow.Shared.Infrastructure.Tests`. Module READMEs should link here rather than restate what it
contains.

| Directory | What it holds |
|-----------|---------------|
| `Persistence/` | `TenantAwareDbContext`, `ReadDbContext`, `ReadDbContextFactory`, and shared EF Core plumbing |
| `Extensions/` | The DI entry points — `AddReadDbContext` (`ReadDbContextExtensions.cs`) and `AddTenantAwareScopedContext` (`TenantAwareDbContextExtensions.cs`) |
| `Middleware/` | Shared ASP.NET Core middleware |
| `Cache/` | `InstrumentedDistributedCache` — an OpenTelemetry decorator over `IDistributedCache` |
| `Messaging/` | Wolverine wiring shared across modules |
| `Auditing/` | Audit trail capture |
| `Resilience/` | Shared HTTP resilience policies |
| `Services/` | Supporting services |
| `Migrations/` | Generated EF migrations for the audit contexts (`AuditDbContext` and `AuthAudit/`) |

Sits below `Wallow.Shared.Infrastructure`, which aggregates it alongside `.BackgroundJobs` and
`.Plugins`. See [`../README.md`](../README.md) for the full shared-library index.
