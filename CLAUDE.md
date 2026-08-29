# CLAUDE.md

Wallow is a **fork-first base platform**: a .NET 10 modular monolith plus a TypeScript BFF SDK
for same-origin OIDC frontends. Teams fork this repo and extend it. Two toolchains: **`api/`**
(the .NET solution `api/Wallow.slnx` — see `api/CLAUDE.md`) and **`apps/` + `packages/`** (a
pnpm workspace — see `apps/CLAUDE.md` and each package's own `CLAUDE.md`). **Nested
`CLAUDE.md` files load only when you touch their directory** — keep directory-specific
knowledge there, not here.

## Deployment status — pre-release, no users

**Wallow has never been deployed anywhere except locally. No production environments, no
consumers, no data worth preserving.** Assume this until this section is removed or amended.

- Prefer the correct design over the compatible one; breaking changes to `main` are expected.
- No staged migrations, backfills, or dual-write windows — reshape the schema, replace the
  migration, re-seed. Local databases are disposable; `api/seed.json` is the only state that matters.
- API and contract changes are free: regenerate `packages/sdk/openapi/v1.json` and the SDK client.
- No feature flags or compatibility shims that only protect a rollout (flags for genuine
  product optionality are fine), and no effort on rollout edge cases.

Still required (correctness, not compatibility): quality gates (`pnpm check`,
`./scripts/run-tests.sh`), conventional commits, migrations that apply cleanly to a fresh database.

Plans go to `docs/plans/<YYYY-MM-DD>/<HHmm>-<name>.md`, start with a
`**status: active|completed|superseded**` line, and are **committed** — issues cite them by path.
Mark a finished plan `completed`/`superseded` in place; never archive one an open issue still cites.

## TypeScript monorepo

pnpm workspace (`apps/*`, `packages/*`). Node **24** (`.nvmrc`), pnpm via `packageManager`.
Formatter/linter is the **oxc** toolchain (`oxfmt` + `oxlint`), not prettier/eslint.
`@bc-solutions-coder` is scoped to GitHub Packages but `pnpm install` needs no token — every
scoped dep is `workspace:*`; a registry credential goes in `~/.npmrc`, never the committed `.npmrc`.

```bash
# build/test/typecheck/dev/format are standard; non-obvious scripts:
pnpm backend                 # full .NET backend via Aspire AppHost
pnpm backend:infra           # infra containers only; :down to stop
pnpm secrets:prod            # generate production secret values
pnpm lint:manifests          # sherif — workspace package.json hygiene (no ignores; keep it that way)
pnpm lint:env                # every ${VAR} a docker/*.yml interpolates must appear in its paired .env.example
pnpm lint:actions            # actionlint over workflows; NOT part of pnpm check (CI runs it separately)
pnpm check                   # the one-command quality gate
# `prepare` (= husky) runs on install; never invoke it by hand
```

- **Turbo owns `build`, `typecheck`, `test`, `dev`** (`turbo.jsonc`). The first three declare
  `dependsOn: ["^build"]` for the **hash**, not resolution — without it an edit under
  `packages/*/src` replays stale passes in dependents. Other lint/check scripts stay outside
  turbo. The optional remote cache (`TURBO_API`/`TURBO_TEAM`/`TURBO_TOKEN`) soft-fails — a
  warning, not a red run.
- **`test` runs at `--concurrency=1`, and that is load-bearing** — concurrent browser-mode
  suites starve each other's Vite dev server and die at module load. `check` and CI invoke
  `build typecheck` and `test` **separately**; do not fold them onto one line.
- Linting is **two passes over one partition** — `pnpm lint` (source) and `pnpm lint:tests`
  (specs/stories) cover every file exactly once; `pnpm check` runs both. Read
  `packages/lint/CLAUDE.md` before editing any `.oxlintrc.json`.

## Backend and local dev

.NET modular monolith, headless — the React apps are the only UIs. Commands and architecture:
`api/CLAUDE.md` — use it, not memory. Service URLs and ports: `README.md` Local Services table
and `docs/getting-started/developer-guide.md`; Compose commands: `docker/CLAUDE.md`.

**Fork branding** is `packages/styles/branding.json` — no source changes needed to rebrand.
`.gitattributes` protects fork-owned config on upstream merges (`docs/getting-started/fork-guide.md`).

## Versioning

Conventional Commits + release-please: `<type>[optional scope][!]: <description>` — lowercase,
imperative, no trailing period, first line < 72 chars; module name as scope when relevant. The
full type table lives only in `docs/operations/versioning.md` — do not restate it.

## Documentation

Docs rules: `docs/CLAUDE.md`; read it before adding a guide. **External library docs —
ref.tools, not memory**: look third-party APIs up before writing against them
(`mcp__ref-context__ref_search_documentation` / `mcp__ref-context__ref_read_url`), always when
a build/test error names a third-party type or option. Repo-internal questions go to local
docs and `.claude/rules/`, not ref.tools.

## Agent instructions

GitHub Issues via the `gh` CLI — conventions: `docs/agents/issue-tracker.md`; labels:
`docs/agents/triage-labels.md`. Historical `Wallow-xxxx` IDs refer to the retired beads
tracker; resolve them against `docs/agents/beads-archive/`.

- **Knowledge discipline:** timeless, repo-wide facts a fresh clone can't rediscover belong in
  the owning `CLAUDE.md` (or `CONTEXT.md`/ADR); issue-scoped findings go on the issue. Never
  store dates, issue numbers, plan references, or exact line numbers as if timeless.
- **Session completion — work is NOT complete until `git push` succeeds:** file issues for
  remaining work; run quality gates (if code changed); close finished issues, update
  in-progress items; `git pull --rebase && git push`; verify `git status` shows "up to date
  with origin".
- **Wayfinder:** `/wayfinder` maps and decision tickets are GitHub issues (`wayfinder:map`,
  `wayfinder:<type>`). Operations live in `docs/agents/issue-tracker.md` § Wayfinding
  operations — do not improvise them. Decisions go on tickets, never as `docs/plans/` files.
- **Triage:** the five canonical roles use their default names verbatim — `docs/agents/triage-labels.md`.
- **Domain docs:** one root `CONTEXT.md` + `docs/adr/`, created lazily by `/domain-modeling` — `docs/agents/domain.md`.
