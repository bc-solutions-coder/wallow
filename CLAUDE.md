# CLAUDE.md

Wallow is a **fork-first base platform**: a .NET 10 modular monolith (multi-tenant, Clean
Architecture, DDD, CQRS, Wolverine messaging) plus a TypeScript BFF SDK for building
same-origin OIDC frontends. Teams fork this repo and extend it.

This repo is a **polyglot monorepo** with two toolchains:

- **`api/`** — the .NET backend (solution `api/Wallow.slnx`). See **`api/CLAUDE.md`**.
- **`apps/` + `packages/`** — a pnpm workspace (TypeScript). See **`apps/CLAUDE.md`** and each
  package's own `CLAUDE.md`.

**Nested `CLAUDE.md` files load only when you touch their directory.** Keep directory-specific
knowledge in them, not here — this file is paid for by every session and every subagent.

## Deployment Status — pre-release, no users

**Wallow has never been deployed anywhere except locally. There are no production
environments, no consumers, and no data worth preserving.** Assume this is true until this
section is removed or amended.

Consequences for how work is done here:

- **Prefer the correct design over the compatible one.** Breaking changes to `main` are
  acceptable and expected.
- **Schema changes do not need staged migrations.** No expand/contract, no dual-write windows,
  no deprecation periods. Reshape the schema, replace the migration, re-seed. Local databases
  are disposable — `bd`-tracked data and `api/seed.json` are the only state that matters.
- **No backfills.** If a model change would strand data, drop and re-seed.
- **API and contract changes are free.** Regenerate `packages/sdk/openapi/v1.json` and the SDK
  client rather than versioning around a change.
- **No feature flags or compatibility shims** whose only purpose is protecting a rollout. Flags
  for genuine product optionality are still fine.
- **Do not spend effort on rollout edge cases** — release coordination, communicating permission
  changes, keeping old columns readable.

Still required, because these are correctness rather than compatibility: quality gates
(`pnpm check`, `./scripts/run-tests.sh`), conventional commits, and migrations that apply
cleanly to a fresh database.

## Repository Layout

| Path                | What it is                                                                                                                                                                  |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `api/`              | .NET 10 solution (`Wallow.slnx`), central build/package props, `.editorconfig`, `stylecop.json`, `seed.json`                                                                |
| `packages/sdk/`     | `@bc-solutions-coder/sdk` — TypeScript BFF auth SDK + generated OpenAPI client                                                                                              |
| `packages/styles/`  | `@bc-solutions-coder/styles` — shared Tailwind v4 CSS entry + theme tokens emitted from `packages/styles/branding.json`                                                                 |
| `packages/ui/`      | `@bc-solutions-coder/ui` — shared browser-only React component catalog (Base UI + CVA)                                                         |
| `packages/forms/`   | `@bc-solutions-coder/forms` — form-authoring layer (TanStack Form catalog + zod + RFC 7807 errors) bound to `@bc-solutions-coder/ui` |
| `packages/navigation/` | `@bc-solutions-coder/navigation` — the application shell: desktop rail, mobile drawer, and the `useNavStore` singleton |
| `packages/query/`   | `@bc-solutions-coder/query` — the shared TanStack Query facade: re-exports react-query plus `createQueryClient`                             |
| `packages/auth/`    | `@bc-solutions-coder/auth` — shared authn/authz layer (current-user query + hook, `beforeLoad` primer, role/permission helpers)                     |
| `packages/testing/` | `@bc-solutions-coder/testing` — shared vitest preset + browser-mode test utilities                                                                                          |
| `packages/config/` | `@bc-solutions-coder/config` — the Vite presets every workspace member builds with; never built, never published                            |
| `packages/lint/`   | `@bc-solutions-coder/lint` — Wallow's own oxlint JS-plugin rules (`wallow/*`), registered once at the repo root (which also enables `no-source-tests` repo-wide); nested configs inherit that registration and enable the rest per-tree. `packages/lint/CLAUDE.md` carries the config census and owns it |
| `packages/utils/`  | `@bc-solutions-coder/utils` — the bottom of the graph: pure functions, zero dependencies, subpath-only |
| `packages/env/`    | `@bc-solutions-coder/env` — deployment-derived addressing for Start apps, zero dependencies, subpath-only |
| `packages/logger/` | `@bc-solutions-coder/logger` — structured logging, both ends: browser core (`.`) and app-server ingest handler (`./server`) |
| `apps/wallow-web/`  | TanStack Start + BFF OIDC reference frontend (dashboard) that consumes the SDK                                                                                              |
| `apps/wallow-auth/` | TanStack Start auth frontend (login/signup/MFA screens) on port 3002                                                                                                        |
| `apps/minimal-app/` | Example app — the smallest wiring of the shared packages into a TanStack Start host                                                                                          |
| `docker/`           | Compose files for infra, production, and the e2e test stack                                                                                                                 |
| `docs/`             | DocFX documentation site (`docfx.json` at root builds it)                                                                                                                   |
| `scripts/`          | `run-tests.sh`, `e2e.sh` (backend-dependent E2E runner), docs/theme helpers                                                                                                 |
| `docs/plans/`       | Session/design artifacts — tracked in git, but NOT part of the docs site                                                                                                    |

New plans MUST be written to `docs/plans/<YYYY-MM-DD>/<HHmm>-<name>.md` (date folder =
creation date, 24h HHmm prefix). Every plan starts with a `**status: active|completed|superseded**`
line. Plans are **committed**, because beads cite them by path as the justification for the work
and a fresh clone must be able to read them — do not archive one out of the repo while an open
bead still points at it. Mark a finished plan `completed` or `superseded` in place instead.
`docfx.json` excludes `plans/**` from the site build, so a plan never ships as user-facing docs.

## JavaScript / TypeScript Monorepo

pnpm workspace (`pnpm-workspace.yaml` → `apps/*`, `packages/*`; every app is a direct child of
`apps/`, no grouping directories — turbo drops packages behind a negated glob). Node **24** (`.nvmrc`),
pnpm **11.24.0** (`packageManager`). Formatter/linter is the **oxc** toolchain
(`oxfmt` + `oxlint`), not prettier/eslint. `@bc-solutions-coder` is scoped to GitHub
Packages (`.npmrc`), but `pnpm install` here needs no token — every scoped dependency is
`workspace:*`. A registry credential belongs in `~/.npmrc` or `pnpm config set`, never in the
committed `.npmrc`, which pnpm refuses to expand env vars out of.

```bash
pnpm install                 # install workspace deps (--frozen-lockfile in CI)

pnpm backend                 # run the full .NET backend via Aspire AppHost
pnpm backend:infra           # docker compose up -d (infra only); pnpm backend:infra:down to stop
pnpm secrets:prod            # scripts/prod-secrets.sh — generate production secret values

pnpm build                   # turbo run build      (topological; no need to build the SDK first)
pnpm test                    # turbo run test       (vitest per package)
pnpm typecheck               # turbo run typecheck
pnpm dev                     # turbo run dev        (both apps)
pnpm lint                    # oxlint over SOURCE only (test/story files excluded); lint:fix autofixes
pnpm lint:tests              # scripts/lint-tests.sh — the excluded files, + the vitest plugin; lint:tests:fix autofixes
pnpm lint:manifests          # sherif — workspace package.json hygiene (no ignores; keep it that way)
pnpm lint:deps               # knip — unused files/exports/deps; knip.json ignores = generated code, lint fixtures, the fork-smoke scaffold, and the two check-exports.sh CLIs knip cannot trace through a shell script
pnpm lint:env                # scripts/check-env.sh — every ${VAR} a docker/*.yml interpolates must be documented in its paired .env.example (commented counts). Completeness, not requiredness; no Docker needed
pnpm format                  # oxfmt --write ...   (format:check verifies)
pnpm check:exports           # publint + @arethetypeswrong/cli over the built packages (needs dist/)
pnpm check                   # format:check + lint + lint:tests + lint:manifests + lint:deps + lint:env + build + typecheck + test + check:exports — the one-command quality gate

# `prepare` (= `husky`) is the twentieth script; pnpm runs it on install, never invoke it by hand
```

**Turbo owns `build`, `typecheck`, `test` and `dev`** (`turbo.jsonc`), with content-addressed
caching in `.turbo/` — a warm `pnpm check` is ~13 s against ~64 s before. The first three declare
`dependsOn: ["^build"]` for the **hash**, not for resolution: nothing resolves through `dist/`
in-repo, but a task's key folds in the keys of the tasks it depends on, so without `^build` an edit
under `packages/*/src` replays a stale pass in every dependent. `dev` declares no dependency — it
caches nothing and reads package source. Lint, format, manifests, deps and `check:exports` stay root invocations
outside turbo. Caching is **local only**; a self-hosted remote cache is filed, not built.

Linting runs as **two passes over one partition** — `pnpm lint` and `pnpm lint:tests` together
cover every file exactly once, and `pnpm check` runs both. Config detail lives in
`packages/lint/CLAUDE.md`; read it before editing any `.oxlintrc.json`.

## Backend (summary — full detail in `api/CLAUDE.md`)

- Modules: **Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding**.
- Modules communicate only via Wolverine in-memory integration events through
  `Shared.Contracts` — never direct references. Each owns a separate Postgres schema.
- Clean Architecture per module: Domain → Application → Infrastructure → Api.
- The API is **headless**: the React apps are the only UIs.
- **`Wallow.AppHost`** is the .NET Aspire host that orchestrates everything (`pnpm backend`).

Backend commands (run/seed/format/test) live in `api/CLAUDE.md` — use those, not from memory.

## Local Development

| Service         | URL                   | Notes                                      |
| --------------- | --------------------- | ------------------------------------------ |
| API             | http://localhost:5001 |                                            |
| Docs            | http://localhost:5004 | DocFX site; `./scripts/docs-serve.sh`      |
| Web (TanStack)  | http://localhost:3000 | `apps/wallow-web`; override with `PORT`    |
| Auth (TanStack) | http://localhost:3002 | `apps/wallow-auth`; override with `PORT`   |
| Minimal app     | http://localhost:3010 | `apps/minimal-app`; not started by `pnpm dev` |

Infra service ports (GarageHQ, Mailpit, Grafana) and Compose commands:
`docs/getting-started/developer-guide.md` and `docker/CLAUDE.md`. `README.md`'s Local Services
table is the front-door copy of these rows — change both together.

**Fork branding** is `packages/styles/branding.json` — no source changes are needed to rebrand.
`.gitattributes` protects fork-owned config on upstream merges; see
`docs/getting-started/fork-guide.md`.

## Versioning

Automated semver via [Conventional Commits](https://www.conventionalcommits.org/) +
[release-please](https://github.com/googleapis/release-please). See `docs/operations/versioning.md`.

**Commit format:** `<type>[optional scope][!]: <description>` (lowercase, imperative, no
trailing period, first line < 72 chars). Use the module name as scope when relevant
(e.g. `feat(inquiries): add form validation`). `feat:` is a minor bump, `fix:` a patch,
`feat!:`/`BREAKING CHANGE:` a major; every other type is non-releasing.
`docs/operations/versioning.md` carries the full type table — it is the only copy, so read it
there rather than restating it here or in `CONTRIBUTING.md`.

## Documentation

- **Fork guide:** `docs/getting-started/fork-guide.md` · **Configuration:** `docs/getting-started/configuration.md` · **Developer guide:** `docs/getting-started/developer-guide.md`
- **Frontend setup:** `docs/development/frontend-setup.md` · **Component library:** `docs/development/component-library.md` · **Forms:** `docs/development/forms.md` · **Logging:** `docs/development/logging.md` · **Frontend state:** `docs/development/frontend-state.md`
- **Module creation:** `docs/architecture/module-creation.md`
- **BFF pattern / TS SDK:** `docs/integrations/bff-pattern.md`, `docs/integrations/typescript-sdk.md`
- **Deployment & CI/CD:** `docs/operations/deployment.md` · **Versioning:** `docs/operations/versioning.md`

Docs rules live in `docs/CLAUDE.md`; read it before adding a guide.

### External library docs — use ref.tools, not memory

Wallow rides a lot of fast-moving third-party API surface (Wolverine, EF Core, .NET Aspire,
TanStack Start/Router/Query/Form, Base UI, Tailwind v4, zod, Vitest browser mode, Playwright, oxc,
DocFX, release-please). **Look the API up before writing against it** — do not answer or code from
recalled signatures.

- `mcp__ref-context__ref_search_documentation` — search official docs for a library/framework/API.
  Query with the library name plus the specific symbol or task.
- `mcp__ref-context__ref_read_url` — read a search hit (or any known doc URL) in full.

Prefer these over `WebSearch`/`WebFetch` for library documentation. Reach for ref.tools whenever
you're about to use an unfamiliar API, a config key, or a CLI flag — and *always* when a
build/test error names a third-party type or option. Repo-internal questions go to the local docs
and `.claude/rules/` files, not to ref.tools.

## Agent Instructions

Uses **bd** (beads) for issue tracking.

```bash
bd ready                                    # Find available work
bd show <id>                                # View issue details
bd update <id> --status in_progress         # Claim work
bd close <id>                               # Complete work
```

Beads sync over HTTPS through this same GitHub repo, on `refs/dolt/data` — **`git push` does not
carry them**. Setup, sync mechanics, and the husky/`bd hooks install` conflict are documented in
`docs/getting-started/developer-guide.md`.

### Memory discipline

`bd remember` is ONLY for timeless, repo-wide facts a fresh clone can't rediscover **and that no
`CLAUDE.md` already states**. Bead-scoped findings go on the bead (`bd note <id>`). Never store
dates, bead IDs, plan references, or exact line numbers in a memory.

### Session Completion

Work is NOT complete until `git push` succeeds.

1. File issues for remaining work
2. Run quality gates (if code changed)
3. Close finished issues, update in-progress items
4. `git pull --rebase && bd dolt push && git push` — `bd dolt push` is not optional and is not
   done for you by `git push`; it is the only thing that moves beads off this machine
5. Verify `git status` shows "up to date with origin". That says nothing about beads — confirm
   those separately with `git ls-remote origin refs/dolt/data` (the hash must change after a
   session that touched beads)
