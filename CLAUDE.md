# CLAUDE.md

Wallow is a **fork-first base platform**: a .NET 10 modular monolith (multi-tenant, Clean
Architecture, DDD, CQRS, Wolverine messaging) plus a TypeScript BFF SDK for building
same-origin OIDC frontends. Teams fork this repo and extend it.

Two toolchains:

- **`api/`** — the .NET backend (solution `api/Wallow.slnx`). See **`api/CLAUDE.md`**.
- **`apps/` + `packages/`** — a pnpm workspace (TypeScript). See **`apps/CLAUDE.md`** and each
  package's own `CLAUDE.md`.

**Nested `CLAUDE.md` files load only when you touch their directory.** Keep directory-specific
knowledge in them, not here — this file is paid for by every session and every subagent.

## Deployment Status — pre-release, no users

**Wallow has never been deployed anywhere except locally. There are no production
environments, no consumers, and no data worth preserving.** Assume this until this section is
removed or amended.

- **Prefer the correct design over the compatible one.** Breaking changes to `main` are expected.
- **No staged migrations, backfills, or dual-write windows.** Reshape the schema, replace the
  migration, re-seed. Local databases are disposable — `api/seed.json` is the only state that
  matters.
- **API and contract changes are free.** Regenerate `packages/sdk/openapi/v1.json` and the SDK
  client rather than versioning around a change.
- **No feature flags or compatibility shims** whose only purpose is protecting a rollout; flags
  for genuine product optionality are fine.
- **Do not spend effort on rollout edge cases** (release coordination, deprecation periods,
  keeping old columns readable).

Still required, because they are correctness rather than compatibility: quality gates
(`pnpm check`, `./scripts/run-tests.sh`), conventional commits, and migrations that apply
cleanly to a fresh database.

## Repository Layout

| Path                   | What it is                                                                          |
| ---------------------- | ----------------------------------------------------------------------------------- |
| `api/`                 | .NET 10 solution, central build props, `.editorconfig`, `stylecop.json`, `seed.json` |
| `packages/sdk/`        | TypeScript BFF auth SDK + generated OpenAPI client                                  |
| `packages/styles/`     | Shared Tailwind v4 CSS entry + theme tokens from `branding.json`                    |
| `packages/ui/`         | Shared browser-only React component catalog (Base UI + CVA)                         |
| `packages/forms/`      | Form-authoring layer (TanStack Form + zod + RFC 7807 errors) bound to `ui`          |
| `packages/navigation/` | Application shell: desktop rail, mobile drawer, `useNavStore`                       |
| `packages/query/`      | Shared TanStack Query facade (re-exports react-query + `createQueryClient`)         |
| `packages/auth/`       | Shared authn/authz layer (current-user query/hook, `beforeLoad` primer, role helpers) |
| `packages/testing/`    | Shared vitest preset + browser-mode test utilities                                  |
| `packages/config/`     | Vite presets every workspace member builds with; never built, never published       |
| `packages/lint/`       | Wallow's own oxlint `wallow/*` rules; owns all lint config detail (`packages/lint/CLAUDE.md`) |
| `packages/utils/`      | Bottom of the graph: pure functions, zero dependencies, subpath-only                |
| `packages/env/`        | Deployment-derived addressing for Start apps, zero dependencies, subpath-only       |
| `packages/logger/`     | Structured logging: browser core (`.`) + app-server ingest handler (`./server`)     |
| `apps/wallow-web/`     | TanStack Start + BFF OIDC reference frontend (dashboard)                            |
| `apps/wallow-auth/`    | TanStack Start auth frontend (login/signup/MFA)                                     |
| `apps/minimal-app/`    | Smallest example wiring of the shared packages into a Start host                    |
| `docker/`              | Compose files for infra, production, and the e2e test stack                         |
| `docs/`                | DocFX documentation site (`docfx.json` at root builds it)                           |
| `scripts/`             | `run-tests.sh`, `e2e.sh`, docs/theme helpers                                        |
| `docs/plans/`          | Session/design artifacts — tracked in git, NOT part of the docs site                |

Plans go to `docs/plans/<YYYY-MM-DD>/<HHmm>-<name>.md` and start with a
`**status: active|completed|superseded**` line. Plans are **committed** — issues cite them by
path. Mark a finished plan `completed`/`superseded` in place; never archive one an open issue
still points at.

## JavaScript / TypeScript Monorepo

pnpm workspace (`apps/*`, `packages/*`; every app a direct child of `apps/`). Node **24**
(`.nvmrc`), pnpm via `packageManager`. Formatter/linter is the **oxc** toolchain
(`oxfmt` + `oxlint`), not prettier/eslint. `@bc-solutions-coder` is scoped to GitHub Packages,
but `pnpm install` needs no token — every scoped dependency is `workspace:*`. A registry
credential belongs in `~/.npmrc` or `pnpm config set`, never in the committed `.npmrc`.

```bash
pnpm install                 # install workspace deps (--frozen-lockfile in CI)

pnpm backend                 # run the full .NET backend via Aspire AppHost
pnpm backend:infra           # docker compose up -d (infra only); :down to stop
pnpm secrets:prod            # generate production secret values

pnpm build                   # turbo run build (topological)
pnpm test                    # turbo run test --concurrency=1 (see below)
pnpm typecheck               # turbo run typecheck
pnpm dev                     # turbo run dev (both apps)
pnpm lint                    # oxlint over SOURCE only; lint:fix autofixes
pnpm lint:tests              # the excluded test/story files, + vitest plugin; lint:tests:fix autofixes
pnpm lint:manifests          # sherif — workspace package.json hygiene (no ignores; keep it that way)
pnpm lint:deps               # knip — unused files/exports/deps
pnpm lint:env                # every ${VAR} a docker/*.yml interpolates must appear in its paired .env.example
pnpm lint:actions            # actionlint over workflows; NOT part of pnpm check (CI runs it separately)
pnpm format                  # oxfmt --write (format:check verifies)
pnpm check:exports           # publint + attw over built packages (needs dist/)
pnpm check                   # the one-command quality gate (all of the above except lint:actions)

# `prepare` (= husky) runs on install; never invoke it by hand
```

**Turbo owns `build`, `typecheck`, `test` and `dev`** (`turbo.jsonc`), cached in `.turbo/`.
The first three declare `dependsOn: ["^build"]` for the **hash**, not resolution — nothing
resolves through `dist/` in-repo, but without it an edit under `packages/*/src` replays stale
passes in dependents. Lint/format/manifests/deps/check:exports stay root invocations outside
turbo. An optional self-hosted remote cache activates only when `TURBO_API`/`TURBO_TEAM`/
`TURBO_TOKEN` are set; a failing remote is a warning, not a red run
(`docs/getting-started/developer-guide.md`).

**`test` runs at `--concurrency=1`, and that is load-bearing.** Concurrent browser-mode suites
starve each other's Vite dev server and die at module load (`Failed to fetch dynamically
imported module`, `Cannot connect to the iframe`) rather than at an assertion. Because turbo
applies concurrency to the whole run graph, `check` and CI invoke `build typecheck` and `test`
**separately** — do not fold them onto one line.

Linting is **two passes over one partition** — `pnpm lint` (source) and `pnpm lint:tests`
(specs/stories) cover every file exactly once; `pnpm check` runs both. Config detail lives in
`packages/lint/CLAUDE.md`; read it before editing any `.oxlintrc.json`.

## Backend (summary — full detail in `api/CLAUDE.md`)

- Modules: **Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding**.
- Modules communicate only via Wolverine in-memory integration events through
  `Shared.Contracts` — never direct references. Each owns a separate Postgres schema.
- Clean Architecture per module: Domain → Application → Infrastructure → Api.
- The API is **headless**: the React apps are the only UIs.
- **`Wallow.AppHost`** is the .NET Aspire host that orchestrates everything (`pnpm backend`).

Backend commands (run/seed/format/test) live in `api/CLAUDE.md` — use those, not memory.

## Local Development

| Service         | URL                   | Notes                                         |
| --------------- | --------------------- | --------------------------------------------- |
| API             | http://localhost:5001 |                                               |
| Docs            | http://localhost:5004 | DocFX site; `./scripts/docs-serve.sh`         |
| Web (TanStack)  | http://localhost:3000 | `apps/wallow-web`; override with `PORT`       |
| Auth (TanStack) | http://localhost:3002 | `apps/wallow-auth`; override with `PORT`      |
| Minimal app     | http://localhost:3010 | `apps/minimal-app`; not started by `pnpm dev` |

Infra ports and Compose commands: `docs/getting-started/developer-guide.md` and
`docker/CLAUDE.md`. `README.md`'s Local Services table mirrors these rows — change both
together.

**Fork branding** is `packages/styles/branding.json` — no source changes needed to rebrand.
`.gitattributes` protects fork-owned config on upstream merges (`docs/getting-started/fork-guide.md`).

## Versioning

Automated semver via Conventional Commits + release-please. **Commit format:**
`<type>[optional scope][!]: <description>` (lowercase, imperative, no trailing period, first
line < 72 chars); module name as scope when relevant. `feat:` minor, `fix:` patch,
`feat!:`/`BREAKING CHANGE:` major. The full type table lives only in
`docs/operations/versioning.md` — do not restate it.

## Documentation

- **Getting started:** `docs/getting-started/` (fork guide, configuration, developer guide)
- **Development:** `docs/development/` (frontend setup, component library, forms, logging, frontend state)
- **Architecture:** `docs/architecture/module-creation.md`
- **Integrations:** `docs/integrations/` (BFF pattern, TypeScript SDK)
- **Operations:** `docs/operations/` (deployment, versioning)

Docs rules live in `docs/CLAUDE.md`; read it before adding a guide.

### External library docs — use ref.tools, not memory

Wallow rides fast-moving third-party surface (Wolverine, EF Core, Aspire, TanStack, Base UI,
Tailwind v4, zod, Vitest browser mode, Playwright, oxc, DocFX, release-please). **Look APIs up
before writing against them**: `mcp__ref-context__ref_search_documentation` to search official
docs, `mcp__ref-context__ref_read_url` to read a hit. Prefer these over WebSearch/WebFetch for
library documentation, and *always* use them when a build/test error names a third-party type
or option. Repo-internal questions go to the local docs and `.claude/rules/`, not ref.tools.

## Agent Instructions

Uses **GitHub Issues** for issue tracking, via the `gh` CLI (conventions:
`docs/agents/issue-tracker.md`; labels: `docs/agents/triage-labels.md`).

```bash
gh issue list --state open                   # Find available work
gh issue view <number> --comments            # View issue details
gh issue edit <number> --add-assignee @me    # Claim work
gh issue close <number> --comment "..."      # Complete work
```

Historical `Wallow-xxxx` IDs in comments, commits, and plans refer to the retired beads
tracker; resolve them against the export in `docs/agents/beads-archive/`.

### Knowledge discipline

Timeless, repo-wide facts a fresh clone can't rediscover belong in the owning `CLAUDE.md` (or a
`CONTEXT.md`/ADR — see Domain docs below). Issue-scoped findings go on the issue as a comment.
Never store dates, issue numbers, plan references, or exact line numbers as if timeless.

### Session Completion

Work is NOT complete until `git push` succeeds.

1. File issues for remaining work
2. Run quality gates (if code changed)
3. Close finished issues, update in-progress items
4. `git pull --rebase && git push`
5. Verify `git status` shows "up to date with origin"

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (`bc-solutions-coder/wallow`), via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Wayfinder

`/wayfinder` maps and their decision tickets are GitHub issues (map labelled `wayfinder:map`,
tickets `wayfinder:<type>`; all five labels exist in the repo). The operations — map body,
sub-issue linking, blocking, frontier, claim, resolve — live in `docs/agents/issue-tracker.md`
under "Wayfinding operations"; do not improvise them. Wayfinder decisions are recorded on
tickets, never as `docs/plans/` files; an asset produced while resolving a ticket is committed
normally and linked from its ticket.

### Triage labels

The five canonical triage roles use their default names verbatim (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `CONTEXT.md` + `docs/adr/`, created lazily by `/domain-modeling`. See `docs/agents/domain.md`.
