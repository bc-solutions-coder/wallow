# Package Update & Warning Remediation — Implementation Log

Living record of what was actually changed, verified, and committed. Updated as each phase lands.

**Branch:** `chore/package-updates-security` (off `refactor/api-restructure`)
**Baseline:** build clean (0 errors) but **1,204 NuGet vulnerability warnings**; ~60 packages behind latest.

---

## Phase 1 — Security patches ✅ COMPLETE

**Commit:** `fix(deps): patch vulnerable direct and transitive packages`

Changes in `api/Directory.Packages.props`:

| Package / variable | From | To | Why |
|---|---|---|---|
| `AspNetCoreVersion` | 10.0.2 | 10.0.10 | Critical DataProtection GHSA-9mv3-2cwr-p262 + Xml high |
| `EfCoreVersion` | 10.0.5 | 10.0.10 | family alignment / patches |
| `MicrosoftExtensionsVersion` | 10.0.5 | 10.0.10 | Caching.StackExchangeRedis etc. |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | 10.0.3 | patches |
| MailKit / MimeKit | 4.15.1 | 4.17.0 | moderate advisory |
| OpenTelemetry.* (stable) | 1.15.x | 1.16.0 | OTLP + Api moderate advisories |
| OpenTelemetry.Instrumentation.* (prerelease) | 1.15.0-beta.1 | 1.16.0-beta.1 / -rc.1 | same line |
| **Transitive pin** MessagePack | (2.5.187) | 2.5.302 | GHSA-hv8m-jj95-wg3x (v2 fix line) |
| **Transitive pin** Microsoft.OpenApi | (2.0.0) | 2.10.0 | GHSA-v5pm-xwqc-g5wc (fixed 2.7.5+) |
| **Transitive pin** Scriban.Signed | (5.5.0) | 7.2.5 | GHSA-5wr9-m6jw-xx44 (fixed 7.0.0) |
| **Transitive pin** SQLitePCLRaw.lib.e_sqlite3 | (2.1.11) | 2.1.12 | GHSA-2m69-gcr7-jv3q |

**Verification:**
- `dotnet build api/Wallow.slnx` → 0 warnings, 0 errors (was 1,204 warnings).
- `dotnet list package --vulnerable --include-transitive` → **0** vulnerable packages.
- Full suite `./scripts/run-tests.sh` → all green (~5,570 tests) after reverting an unrelated
  opportunistic bump (see note).

**Note / course-correction:** initially also bumped the resilience family
(Http.Resilience/Resilience/ServiceDiscovery/Caching.Hybrid/TimeProvider.Testing) 10.4.0 → 10.8.0.
That tripped `AspirePackageVersionTests.MicrosoftExtensionsHttpResilience_ShouldRemainUnchanged`,
a deliberate guard coupling resilience to Aspire. Those bumps are **not security-related**, so they
were reverted out of Phase 1 and deferred to Phase 2 (handled with the Aspire bump + guard update).
The restore needed `--force` to regenerate stale `project.assets.json` before pins took effect.

**Caveat:** a pre-existing unstaged `package.json` change (adds `backend*` npm scripts) was present
in the working tree and is **not** from this work — left unstaged.

---

## Phase 2 — Safe minor/patch updates ✅ COMPLETE

Discovered during Phase 2: Aspire.Hosting.Redis 13.4.6 requires `StackExchange.Redis >= 2.13.1`,
which triggered an NU1109 downgrade error against our 2.12.4. Resolved by bumping to the latest
v2 (**2.13.17**) — stays on the v2 line; the 2 → 3 major stays in Phase 3.

Also: WireMock.Net 2.12.0 now pulls `Scriban.Signed 7.2.5` natively, so the Phase 1 Scriban
transitive pin is redundant and is removed in Phase 2.

Applied bumps (no major version change):

- Aspire.Hosting(.AppHost/.PostgreSQL/.Redis) 13.2.1 → 13.4.6
- Resilience family 10.4.0 → 10.8.0 + update `AspirePackageVersionTests` guard (verify with Aspire)
- AWSSDK.S3 4.0.20.2 → 4.0.101.1
- Audit.EntityFramework.Core 32.0.0 → 32.2.0
- BlazorBlueprint.Components 3.9.3 → 3.14.0, BlazorBlueprint.Icons.Lucide 2.0.0 → 2.0.2
- Dapper 2.1.72 → 2.1.79
- JetBrains.Annotations 2025.2.4 → 2026.2.0
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.0 → 10.0.10
- Microsoft.FeatureManagement.AspNetCore 4.4.0 → 4.6.0
- Microsoft.NET.Test.Sdk 18.3.0 → 18.8.1
- Microsoft.Playwright 1.58.0 → 1.61.0
- OpenIddict.AspNetCore/EntityFrameworkCore 7.4.0 → 7.5.0
- Scalar.AspNetCore 2.13.11 → 2.16.12
- Testcontainers(.PostgreSql/.Redis) 4.11.0 → 4.13.0
- WireMock.Net 2.0.0 → 2.12.0 (may make the Scriban.Signed pin redundant — re-check)

**Verification:** build 0 warnings / 0 errors, `--vulnerable` scan clean, full suite green
(exit 0, 0 failures). Committed scoped to only `Directory.Packages.props` +
`AspirePackageVersionTests.cs` (`chore(deps): update packages to latest minor/patch versions`).

**⚠️ Working-tree caveat:** the repo has **concurrent JS/SDK work** in flight (tsup→vite
migration, `pnpm-lock.yaml`, `package.json` backend scripts, CLAUDE.md restructure). An initial
Phase 2 commit accidentally swept some of it in; recovered via `git reset --soft` + a path-scoped
re-commit. **All remaining commits must be path-scoped** (`git commit -m ... -- <my paths>`).

---

## Phase 3 — Major bumps ✅ COMPLETE (one deferral)

One major per commit, each with its own test gate. Landed:

| Package | From → To | Commit | Verification |
|---|---|---|---|
| coverlet.collector | 8.0.1 → 10.0.1 | `chore(deps): bump coverlet.collector to 10.0.1` | build clean (pure collector) |
| StackExchange.Redis | 2.13.17 → 3.0.17 | `chore(deps)!: upgrade StackExchange.Redis to 3.0.17` | Redis-backed suites green: identity 1582, apikeys 194, api 296, inquiries 207 |
| Asp.Versioning.Mvc(.ApiExplorer) | 8.1.1 → 10.0.0 | `chore(deps)!: upgrade Asp.Versioning to 10.0.0` | api host tests green (296); versioned endpoints + OpenAPI intact |
| WolverineFx (all 4) | 5.27.0 → 6.19.0 | `chore(deps)!: upgrade WolverineFx to 6.19.0` | **full unit suite green (4995/4995)**; `--vulnerable` clean |

**Wolverine 6 notes:**
- New `ICommandBus.StreamAsync<TResponse>` overloads (2) implemented as `NotSupportedException`
  stubs on `Wallow.SeederService/NullMessageBus.cs` (seeder never streams).
- Wolverine 6 dragged a transitive **Newtonsoft.Json 11.0.1** (GHSA-5crp-9r3c-p9vr, high) into
  non-Hangfire projects. Added a transitive security pin at **13.0.4** (floor set by
  Aspire.Hosting.AppHost + WireMock.Net.OpenApiParser, which require >= 13.0.4). Vulnerability
  scan back to zero.

**⚠️ Deferred — NSubstitute 5.3.0 → 6.0.0 (NOT applied):** NSubstitute 6 tightened nullable
annotations; under `TreatWarningsAsErrors` + `Nullable=enable` this produced **762 build errors**
across test files (388 CS8625, 368 CS8602, 6 CS8604) at NSubstitute call sites — a per-call-site
API/annotation change, not a cheap systemic fix. NSubstitute 5.3.0 is **not vulnerable and not
EOL**, so the upgrade carries zero security/warning benefit against high test-code churn. Reverted
to 5.3.0; coverlet 10 kept. **Backlog bead filed: `Wallow-b48r` (P4)** — revisit opportunistically.

## Phase 4 — Analyzer upgrades ✅ COMPLETE

**Commit:** `chore(deps): upgrade NetAnalyzers and Meziantou analyzers`

- Microsoft.CodeAnalysis.NetAnalyzers 10.0.104 → **10.0.302**
- Meziantou.Analyzer 3.0.19 → **3.0.123**
- Roslynator (4.15.0) and StyleCop (1.2.0-beta.556) already at latest — no change.
- Roslyn pin stayed at 5.0.0 (no bump needed).

**Verification:** `dotnet clean` + full rebuild → **0 warnings, 0 errors**. The new analyzers
surfaced NO new diagnostics. (A `--no-incremental` rebuild throws MSB3030 on
`appsettings.Staging.json` / static-web-assets copy — a known build-system quirk of that flag,
not an analyzer diagnostic and not caused by this change; the normal clean build is green.)

## Phase 5 — Verify & land 🔄 IN PROGRESS
Full unit suite already green at Wolverine 6 (4995/4995). Remaining: final `--vulnerable` scan,
`dotnet format` check, E2E (pre-existing baseline: 65/76, 11 known-flaky failures — see bead
`e2e-baseline-11-preexisting-failures`), commit plan/impl docs. Ephemeral branch — do NOT push
unless the user explicitly asks.
