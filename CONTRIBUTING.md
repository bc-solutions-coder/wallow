# Contributing to Wallow

Thank you for your interest in contributing to Wallow! This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) and Docker Compose
- A code editor (Visual Studio, Rider, or VS Code)

### Local Setup

1. Fork and clone the repository
2. Start infrastructure services:
   ```bash
   cd docker && docker compose up -d
   ```
3. Run the API:
   ```bash
   dotnet run --project src/Wallow.Api
   ```
4. Run all tests to verify your setup:
   ```bash
   dotnet test
   ```

See the [Developer Guide](docs/DEVELOPER_GUIDE.md) for detailed setup instructions and service URLs.

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

- **Modules:** Identity, Storage, Communications, Billing, Notifications
- **Layer order:** Domain → Application → Infrastructure → Api
- Domain has no external dependencies; Application depends only on Domain
- Modules communicate via RabbitMQ events, never direct project references
- Cross-module contracts go in `Shared.Contracts` only
- Each module owns its own database schema
- Use EF Core for writes, Dapper for complex reads

For adding new modules, see `docs/claude/module-creation.md`.

## Commit Messages

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope][!]: <description>
```

| Type | Purpose | Version Impact |
|------|---------|---------------|
| `feat` | New feature | Minor |
| `fix` | Bug fix | Patch |
| `docs` | Documentation only | None |
| `test` | Adding or fixing tests | None |
| `refactor` | Code change that neither fixes a bug nor adds a feature | None |
| `chore` | Maintenance tasks | None |
| `ci` | CI/CD changes | None |
| `perf` | Performance improvement | None |

**Examples:**
```
feat(billing): add invoice PDF export
fix(identity): resolve null reference in tenant resolver
test(storage): add upload service unit tests
docs: update contributing guide
```

Add `!` after the type for breaking changes: `feat!: redesign authentication API`

## Code Style

- Use explicit types instead of `var`
- Follow existing patterns within each module
- Keep domain logic free of infrastructure concerns
- Write unit tests for domain and application layers

## Pull Request Process

1. Ensure all tests pass: `dotnet test`
2. Update documentation if you changed public APIs or behavior
3. Fill out the PR template completely
4. Request review from a maintainer
5. Address any feedback promptly

## Questions?

If you have questions about contributing, open a [Discussion](../../discussions) or reach out at BC@bcordes.dev.

## License

By contributing to Wallow, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
