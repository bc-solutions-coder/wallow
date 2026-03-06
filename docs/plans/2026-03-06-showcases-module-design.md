# Showcases Module Design

## Purpose

Admin-managed portfolio entries that display completed work on the platform website. Each entry links to a live demo, GitHub repository, or video. The admin role controls all write operations; read access is public.

## Domain Model

### Showcase (Aggregate Root)

| Field | Type | Rules |
|-------|------|-------|
| Id | ShowcaseId | Strongly-typed GUID |
| Title | string | Required, max 200 chars |
| Description | string | Required |
| Category | ShowcaseCategory | Required enum |
| DemoUrl | string? | At least one URL (demo, GitHub, or video) required |
| GitHubUrl | string? | See above |
| VideoUrl | string? | See above |
| ThumbnailUrl | string? | External URL (Cloudflare CDN recommended) |
| Tags | string[] | Simple strings, stored as PostgreSQL `text[]` |
| DisplayOrder | int | Defaults to 0; lower numbers display first |
| CreatedAt | DateTimeOffset | Set on creation |
| UpdatedAt | DateTimeOffset | Set on every update |

### ShowcaseCategory (Enum)

`WebApp`, `Api`, `Mobile`, `Library`, `Tool`

### Key Domain Rules

- Title is required.
- At least one of DemoUrl, GitHubUrl, or VideoUrl must be provided.
- Display order defaults to 0.
- Tags are value objects stored as a string array column — no join table.

## Scope

This module is **global** — not tenant-scoped. Showcases belong to the platform, not to individual tenants. No tenant filtering applies.

## Authorization

- **Read endpoints**: Anonymous. Portfolio content is public.
- **Write endpoints**: Require `ShowcasesManage` permission, granted to the `admin` role.

The `admin` role already receives all permissions via `PermissionType.All`. Adding `ShowcasesManage` to `PermissionType` makes it available automatically.

## API Endpoints

### Public (Anonymous)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/showcases` | List all showcases, ordered by display order. Filterable by category and tag. |
| GET | `/api/showcases/{id}` | Get a single showcase. |

### Admin (`ShowcasesManage` permission)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/showcases` | Create a showcase. |
| PUT | `/api/showcases/{id}` | Update a showcase. |
| DELETE | `/api/showcases/{id}` | Delete a showcase. |

No pagination initially. Portfolio lists are small.

## Project Structure

```
src/Modules/Showcases/
├── Foundry.Showcases.Domain/
│   ├── Entities/Showcase.cs
│   ├── Enums/ShowcaseCategory.cs
│   └── Repositories/IShowcaseRepository.cs
├── Foundry.Showcases.Application/
│   ├── Commands/ (Create, Update, Delete)
│   ├── Queries/ (GetShowcase, ListShowcases)
│   └── DTOs/ShowcaseResponse.cs
├── Foundry.Showcases.Infrastructure/
│   ├── Persistence/ShowcasesDbContext.cs
│   ├── Persistence/ShowcaseConfiguration.cs
│   ├── Repositories/ShowcaseRepository.cs
│   └── ShowcasesModule.cs
└── Foundry.Showcases.Api/
    └── Endpoints/ShowcaseEndpoints.cs
```

### Key Architectural Decisions

- Own PostgreSQL schema: `showcases`
- CQRS via Wolverine handlers
- Minimal API endpoints following existing Foundry patterns
- No tenant interceptor — global data
- Tags stored as PostgreSQL `text[]`, mapped via EF Core

## Testing

- **Domain unit tests**: Validation rules (title required, at least one URL).
- **Application tests**: Command/query handler behavior.
- **Integration tests**: EF Core persistence, endpoint auth (anonymous reads, admin-only writes).
- **Architecture tests**: Module follows Foundry conventions.

## Out of Scope

- Image upload (URLs only)
- Pagination
- Full-text search
- Draft/publishing workflow
- Drag-and-drop ordering API (just a sort integer)
