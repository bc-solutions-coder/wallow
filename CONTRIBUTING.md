# Contributing to Wallow

Thank you for your interest in contributing to Wallow! This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) and Docker Compose
- [Node 24](https://nodejs.org/) (see `.nvmrc`) and pnpm 11.24.0 (see `packageManager` in
  `package.json`)
- A code editor (Visual Studio, Rider, or VS Code)

### Local Setup

1. Fork and clone the repository
2. Install workspace dependencies:
   ```bash
   pnpm install
   ```
3. Start infrastructure services:
   ```bash
   pnpm backend:infra
   ```
4. Run the backend (the Aspire AppHost orchestrates the API, both React apps, migrations, and the seeder):
   ```bash
   pnpm backend
   ```
5. Run the quality gates to verify your setup:
   ```bash
   ./scripts/run-tests.sh          # backend
   pnpm check                      # frontend
   ```

See the [Developer Guide](docs/getting-started/developer-guide.md) for detailed setup instructions and service URLs.

### Agent and repo conventions

Working rules that apply to every change live in [`CLAUDE.md`](CLAUDE.md) and `.claude/rules/`
(testing, E2E, coding conventions, and team-agent lifecycle). Issue tracking uses
[beads](https://github.com/steveyegge/beads) rather than a separate tracker for in-flight work —
`bd ready` lists available work.

## How to Contribute

### Reporting Bugs

- Search [existing issues](../../issues) to avoid duplicates
- Open a new issue using the **Bug Report** template
- Include steps to reproduce, expected vs actual behavior, and your environment details

### Suggesting Features

- Open a new issue using the **Feature Request** template
- Describe the problem you're solving and your proposed approach
- Be open to discussion about alternative solutions

### Submitting Code

1. **Fork** the repository and create a branch from `main`
2. **Write tests** for any new functionality or bug fixes
3. **Follow the architecture** - see [Architecture](#architecture) below
4. **Use Conventional Commits** for your commit messages (see [Commit Messages](#commit-messages))
5. **Open a Pull Request** using the PR template

## Architecture

Wallow is a modular monolith following Clean Architecture and DDD principles. Before contributing, understand these rules:

- **Modules:** Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding
- **Layer order:** Domain → Application → Infrastructure → Api
- Domain has no external dependencies; Application depends only on Domain
- Modules communicate via Wolverine in-memory events, never direct project references
- Cross-module contracts go in `Shared.Contracts` only
- Each module owns its own database schema
- Use EF Core for writes, Dapper for complex reads

The frontend half is a pnpm workspace: `apps/wallow-web`, `apps/wallow-auth`, and `apps/minimal-app`
(the smallest wiring of the shared packages), built on the `packages/*` libraries. See
[`apps/CLAUDE.md`](apps/CLAUDE.md) and [Frontend Setup](docs/development/frontend-setup.md).

For adding new modules, see `docs/architecture/module-creation.md`.

## Commit Messages

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope][!]: <description>
```

`feat` is a minor bump, `fix` a patch, and `!`/`BREAKING CHANGE:` a major; every other type is
non-releasing. The complete type table lives in
[Versioning](docs/operations/versioning.md) — it is the only copy, so read it there.

**Examples:**
```
feat(inquiries): add form validation
fix(identity): resolve null reference in tenant resolver
test(storage): add upload service unit tests
docs: update contributing guide
```

Add `!` after the type for breaking changes: `feat!: redesign authentication API`

## Code Style

- **C#:** use explicit types instead of `var` (this rule is C#-only; TypeScript uses inference
  normally). Full C# conventions are in `api/CLAUDE.md`.
- **TypeScript:** the toolchain is oxc (`oxfmt` + `oxlint`), not prettier/eslint — `pnpm format`
  and `pnpm lint` are the entry points.
- Follow existing patterns within each module
- Keep domain logic free of infrastructure concerns
- Write unit tests for domain and application layers

## Pull Request Process

1. Ensure both quality gates pass: `./scripts/run-tests.sh all` (backend — `all`, not a bare run,
   because a bare run filters out every `Category=Integration` test) and `pnpm check` (frontend)
2. Update documentation if you changed public APIs or behavior
3. Fill out the PR template completely
4. Request review from a maintainer
5. Address any feedback promptly

## Questions?

If you have questions about contributing, open a [Discussion](../../discussions) or reach out at BC@bcordes.dev.

## License

By contributing to Wallow, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
