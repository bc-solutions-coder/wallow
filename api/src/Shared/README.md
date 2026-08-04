# Shared Libraries

## Overview

Foundational libraries providing the building blocks for all Wallow modules. Each project's own
README is the authoritative description; this page is an index, not a second copy.

| Project | What it is |
|---------|------------|
| [`Wallow.Shared.Kernel`](Wallow.Shared.Kernel/README.md) | DDD primitives, multi-tenancy, `Result<T>`, custom fields, JWT claim helpers |
| [`Wallow.Shared.Contracts`](Wallow.Shared.Contracts/README.md) | The cross-module boundary: integration events, service interfaces |
| [`Wallow.Shared.Infrastructure`](Wallow.Shared.Infrastructure/README.md) | Settings framework and module coordination |
| [`Wallow.Shared.Infrastructure.Core`](Wallow.Shared.Infrastructure.Core/README.md) | Core middleware, caching, messaging, auditing, persistence |
| [`Wallow.Shared.Infrastructure.BackgroundJobs`](Wallow.Shared.Infrastructure.BackgroundJobs/README.md) | Hangfire job scheduling integration |
| [`Wallow.Shared.Infrastructure.Plugins`](Wallow.Shared.Infrastructure.Plugins/README.md) | Plugin system: discovery, isolated loading, lifecycle |
| [`Wallow.Shared.Api`](Wallow.Shared.Api/README.md) | Shared API utilities |

Module registration is assembled in `Wallow.Api/WallowModules.cs`.

## Dependency Rules

**Kernel**: Referenced by Domain, Application, and Infrastructure layers of all modules. Never references any module directly.

**Contracts**: Referenced by Application (for integration event DTOs) and Infrastructure (for event consumers). Never references any module, Kernel, or external packages.

**Cross-Module Communication**: Always via integration events through Wolverine. Never direct assembly references between modules. The canonical event catalogue is
[`Wallow.Shared.Contracts/README.md`](Wallow.Shared.Contracts/README.md).
