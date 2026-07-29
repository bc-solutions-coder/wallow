# CLAUDE.md

Wallow is a **fork-first base platform**: a .NET 10 modular monolith (multi-tenant, Clean
Architecture, DDD, CQRS, Wolverine messaging) plus a TypeScript BFF SDK for building
same-origin OIDC frontends. Teams fork this repo and extend it.

This repo is a **polyglot monorepo** with two toolchains:

- **`api/`** — the .NET backend (solution `api/Wallow.slnx`). See **`api/CLAUDE.md`** for
  backend architecture, modules, and commands.
- **`apps/` + `packages/`** — a pnpm workspace (TypeScript). See the JavaScript section below.

## Repository Layout

| Path                  | What it is                                                                                                                    |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `api/`                | .NET 10 solution (`Wallow.slnx`), central build/package props, `.editorconfig`, `stylecop.json`, `seed.json`, `branding.json` |
| `packages/sdk/`       | `@bc-solutions-coder/sdk` — TypeScript BFF auth SDK + generated OpenAPI client                                                |
| `packages/styles/`    | `@bc-solutions-coder/styles` — shared Tailwind v4 CSS entry + theme tokens emitted from `api/branding.json`                   |
| `packages/ui/`        | `@bc-solutions-coder/ui` — shared browser-only React component catalog (Base UI + CVA); see `packages/ui/CLAUDE.md`           |
| `packages/forms/`     | `@bc-solutions-coder/forms` — shared form-authoring layer (TanStack Form catalog + zod + RFC 7807 errors) bound to `@bc-solutions-coder/ui`; see `packages/forms/CLAUDE.md` |
| `packages/web-shell/` | `@bc-solutions-coder/web-shell` — shared browser-safe frontend runtime (the TanStack Query client factory)                    |
| `packages/testing/`   | `@bc-solutions-coder/testing` — shared vitest preset + browser-mode test utilities                                            |
| `apps/wallow-web/`    | TanStack Start + BFF OIDC reference frontend (dashboard) that consumes the SDK                                                |
| `apps/wallow-auth/`   | TanStack Start auth frontend (login/signup/MFA screens) on port 3002                                                          |
| `apps/examples/`      | Example apps (`minimal-app`)                                                                                                  |
| `docker/`             | Compose files for infra, production, and the e2e test stack                                                                   |
| `docs/`               | DocFX documentation site (`docfx.json` at root builds it)                                                                     |
| `scripts/`            | `run-tests.sh`, `e2e.sh` (backend-dependent E2E runner), docs/theme helpers                                                   |
| `docs/plans/`         | Session/design artifacts, local-only (gitignored) — NOT part of the docs site                                                 |

New plans MUST be written to `docs/plans/<YYYY-MM-DD>/<HHmm>-<name>.md` (date folder =
creation date, 24h HHmm prefix). Every plan starts with a `**status: active|completed|superseded**`
line; archive completed plans out of the repo (`~/Documents/wallow-plans-archive/`).

## JavaScript / TypeScript Monorepo

pnpm workspace (`pnpm-workspace.yaml` → `apps/*`, `apps/examples/*`, `packages/*`). Node **24** (`.nvmrc`),
pnpm **10.20.0** (`packageManager`). Formatter/linter is the **oxc** toolchain
(`oxfmt` + `oxlint`), not prettier/eslint. `@bc-solutions-coder` is scoped to GitHub
Packages (`.npmrc`, needs `NODE_AUTH_TOKEN`).

```bash
pnpm install                 # install workspace deps (--frozen-lockfile in CI)

pnpm backend                 # run the full .NET backend via Aspire AppHost
pnpm backend:infra           # docker compose up -d (infra only); :down to stop

pnpm --filter @bc-solutions-coder/sdk build   # build the SDK FIRST (apps typecheck against dist/)
pnpm build                   # pnpm -r build   (recursive across workspace)
pnpm test                    # pnpm -r test    (vitest per package)
pnpm typecheck               # pnpm -r typecheck
pnpm lint                    # oxlint apps packages --deny-warnings
pnpm format                  # oxfmt --write ...   (format:check verifies)
pnpm check:exports           # publint + @arethetypeswrong/cli over the built packages (needs dist/)
pnpm check                   # format:check + lint + typecheck + test + build + check:exports — the one-command quality gate
```

- **`packages/sdk`** — server-side **BFF** tunnel so the browser never holds a token, with
  four entries: browser (`.`), Node BFF (`./server`), pure reverse proxy
  (`./server/passthrough`), TanStack Query layer (`./query`). Its server handlers are
  web-standard `Request` → `Response` functions with no host-framework dependency. The
  OpenAPI client is **generated** from the committed snapshot `packages/sdk/openapi/v1.json`
  (CI fails on drift), and the SDK ships independently via `sdk-v*` tags. Full detail —
  entries, session/CSRF model, regen command, build/publish, test layout — lives in
  **`packages/sdk/CLAUDE.md`**; read it before touching the SDK.
- **`packages/forms`** — the one way a form is written: `useAppForm` (zod schema + generated
  `{operation}Mutation` + RFC 7807 error split) rendered through the `AppForm` shell and its
  pre-bound field catalog, with every `data-testid` DERIVED from the shell's `testIdPrefix`.
  It consumes `@bc-solutions-coder/ui`; `ui` must never import it. Guide:
  `docs/development/forms.md`; contributor detail: **`packages/forms/CLAUDE.md`**.
- **`apps/wallow-web`** — runnable TanStack Start reference frontend demonstrating the
  full same-origin BFF flow. `pnpm --filter @bc-solutions-coder/wallow-web dev` (SSR + BFF)
  or `... start` (`node .output/server/index.mjs`, the Nitro bundle the E2E container runs).
  Has a Dockerfile whose build context is the **repo root** (needs the whole workspace to
  resolve `workspace:*`). Every app hosts itself through TanStack Start server routes —
  there is no shared host runtime.

### Frontend state boundary

TanStack Query is the only store for backend data. Every key comes from the **generated**
per-operation artifacts in `@bc-solutions-coder/sdk/query` — `{operation}Options()` for a read,
`{operation}Mutation()` for a write, `{operation}QueryKey()` when you need the key alone; no
inline key literals, and never a hand-rolled factory. Those keys are flat
(`[{ _id, baseUrl, tags, ...args }]`) with no prefix to sweep by, so invalidation goes through
the curated `invalidations` predicates (`queriesWithTag`, `queriesForOperation`) from the same
entry. Zustand holds UI-only global state; it never stores API data.
See `docs/development/frontend-state.md`.

## Backend (summary — full detail in `api/CLAUDE.md`)

- Modules: **Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding**.
- Modules communicate only via Wolverine in-memory integration events through
  `Shared.Contracts` — never direct references. Each owns a separate Postgres schema.
- Clean Architecture per module: Domain → Application → Infrastructure → Api.
- The API is **headless**: the React apps (`apps/wallow-auth`, `apps/wallow-web`) are the only
  UIs. The Blazor `Wallow.Auth`/`Wallow.Web` apps are deleted (readable in git history).
- **`Wallow.AppHost`** is the .NET Aspire host that orchestrates everything (`pnpm backend`),
  including the React apps as Node resources.

Backend commands (run/seed/format/test) live in `api/CLAUDE.md` — use those, not from memory.

## Docker & Infrastructure

Run from `docker/` (copy `.env.example` → `.env` first; `GF_ADMIN_PASSWORD` is required).

```bash
cd docker && docker compose up -d                 # Postgres, Valkey, GarageHQ (S3), Mailpit, Grafana, Docs
cd docker && docker compose --profile clamav up -d # + ClamAV virus scanning
# Full production stack (copy .env.production.example → .env.production first):
cd docker && docker compose -f docker-compose.production.yml --env-file .env.production up --build
```

E2E tests are per-app `@playwright/test` suites (`apps/wallow-auth/e2e/`,
`apps/wallow-web/e2e/`); `./scripts/e2e.sh` is the one-command backend-dependent runner — see
`.claude/rules/E2E.md`. `docker-compose.test.yml` provides the containerised stack.

## Local Development

| Service         | URL                   | Notes                                    |
| --------------- | --------------------- | ---------------------------------------- |
| API             | http://localhost:5001 |                                          |
| Docs            | http://localhost:5004 | DocFX site                               |
| Web (TanStack)  | http://localhost:3000 | `apps/wallow-web`; override with `PORT`  |
| Auth (TanStack) | http://localhost:3002 | `apps/wallow-auth`; override with `PORT` |
| GarageHQ (S3)   | http://localhost:3900 | admin 3903; creds in `docker/.env`       |
| Mailpit         | http://localhost:8025 | SMTP 1025                                |
| Grafana         | http://localhost:3001 | otel-lgtm stack                          |

## Fork-First Configuration

- **`api/branding.json`** — canonical fork branding (name, icon, tagline, theme colors);
  `packages/styles` owns the canonical branding types and emits theme CSS from it. No
  source changes are needed to rebrand.
- `.gitattributes` marks `appsettings*.json`, `branding.json`, `docker/.env`,
  `docker/.env.example`, and `seed.json` as `merge=ours` so upstream merges preserve fork config.

## Versioning

Automated semver via [Conventional Commits](https://www.conventionalcommits.org/) +
[release-please](https://github.com/googleapis/release-please). See `docs/operations/versioning.md`.

**Commit format:** `<type>[optional scope][!]: <description>` (lowercase, imperative, no
trailing period, first line < 72 chars). Use the module name as scope when relevant
(e.g. `feat(inquiries): add form validation`).

| Prefix                                                               | Bump       |
| -------------------------------------------------------------------- | ---------- |
| `fix:`                                                               | Patch      |
| `feat:`                                                              | Minor      |
| `feat!:` / `BREAKING CHANGE:`                                        | Major      |
| `chore:` `refactor:` `docs:` `test:` `ci:` `style:` `perf:` `build:` | No release |

Merges to `main` update a **Release PR**; merging it tags `v*`, publishes images, and (for the
SDK) `sdk-v*` triggers a separate npm publish.

## Documentation

- **Fork guide:** `docs/getting-started/fork-guide.md`
- **Configuration:** `docs/getting-started/configuration.md`
- **Developer guide:** `docs/getting-started/developer-guide.md`
- **Frontend setup:** `docs/development/frontend-setup.md` · **Component library:** `docs/development/component-library.md` · **Forms:** `docs/development/forms.md`
- **Module creation:** `docs/architecture/module-creation.md`
- **BFF pattern / TS SDK:** `docs/integrations/bff-pattern.md`, `docs/integrations/typescript-sdk.md`
- **Deployment & CI/CD:** `docs/operations/deployment.md` · **Versioning:** `docs/operations/versioning.md`

Docs rules live in `docs/CLAUDE.md` (site content only; lowercase-kebab filenames; add new
guides to `docs/toc.yml`).

## Conventions & Rules

Detailed, enforced rules live in `.claude/rules/` — read the relevant file before touching its area:

- **`CONVENTIONS.md`** — C# coding rules; read before any backend change.
- **`TESTING.md`** — how to run backend and frontend tests; read before running or writing tests.
- **`E2E.md`** — Playwright suite layout and selectors; read before touching anything under `e2e/`.
- **`TEAMS.md`** — multi-agent session lifecycle; read when coordinating teammate agents.

## Agent Instructions

Uses **bd** (beads) for issue tracking.

```bash
bd ready                                    # Find available work
bd show <id>                                # View issue details
bd update <id> --status in_progress         # Claim work
bd close <id>                               # Complete work
```

### Memory discipline

`bd remember` is ONLY for timeless, repo-wide facts a fresh clone can't rediscover.
Bead-scoped findings go on the bead (`bd note <id>`). Never store dates, bead IDs,
plan references, or exact line numbers in a memory.

### Session Completion

Work is NOT complete until `git push` succeeds.

1. File issues for remaining work
2. Run quality gates (if code changed)
3. Close finished issues, update in-progress items
4. `git pull --rebase && bd dolt push && git push`
5. Verify `git status` shows "up to date with origin"
