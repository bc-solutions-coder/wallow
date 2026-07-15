# Package Update & Warning Remediation — Plan

**Branch:** `chore/package-updates-security`
**Goal:** Eliminate all build-output warnings and bring every NuGet package up to date, without breaking the build or tests.

## Key finding

The build has **0 errors and 0 compiler/analyzer warnings**. All **1,204 "warnings" were NuGet
security advisories** (NU1902 moderate / NU1903 high / NU1904 critical) — the same ~10 vulnerable
packages counted once per referencing project. They surface as warnings (not errors) because
`Directory.Build.props` sets `WarningsNotAsErrors` for `NU1901-NU1904`.

So "fix all the warnings" == "update the vulnerable packages." A second, independent goal is the
~60 packages that are simply behind latest (`dotnet outdated`), including several risky majors.

## Vulnerable packages (source of the warnings)

| Severity | Package | Reaches it via | Fix |
|---|---|---|---|
| Critical | Microsoft.AspNetCore.DataProtection 10.0.2 | AspNetCore 10.0.2 (direct) | AspNetCore → 10.0.10 |
| Critical | Scriban.Signed 5.5.0 | WireMock.Net (test) | pin 7.2.5 |
| High | System.Security.Cryptography.Xml 10.0.2 | DataProtection | AspNetCore → 10.0.10 |
| High | MessagePack 2.5.187/192 | SignalR.StackExchangeRedis + StreamJsonRpc | pin 2.5.302 |
| High | Microsoft.OpenApi 2.0.0 | AspNetCore.OpenApi | pin 2.10.0 |
| High | SQLitePCLRaw.lib.e_sqlite3 2.1.11 | EF Core Sqlite (test) | pin 2.1.12 |
| Moderate | MailKit 4.15.1 | direct | → 4.17.0 |
| Moderate | OpenTelemetry.* 1.15.0 | direct + instrumentation | → 1.16.0 |

Transitive pins rely on `CentralPackageTransitivePinningEnabled=true` (already set in
`api/Directory.Build.props`). All version/pin changes live in `api/Directory.Packages.props` (CPM).

## Phases

**Phase 1 — Kill the vulnerabilities (the 1,204 warnings).** Security-only, low risk.
Bump `AspNetCoreVersion`/`EfCoreVersion`/`MicrosoftExtensionsVersion` to 10.0.10, Npgsql 10.0.3,
MailKit/MimeKit 4.17.0, OpenTelemetry 1.16.0; add the four transitive security pins.
Gate: rebuild → NU190x count = 0; full test suite green.

**Phase 2 — Safe minor/patch updates.** No major-version changes: Aspire 13.4.6, AWSSDK.S3,
Audit.EF 32.2, BlazorBlueprint, Dapper, JetBrains.Annotations, Identity.EFCore 10.0.10,
FeatureManagement 4.6, Test.Sdk 18.8, Playwright 1.61, OpenIddict 7.5, Scalar 2.16, Testcontainers
4.13, WireMock.Net 2.12, and the resilience family (Http.Resilience/Resilience/ServiceDiscovery/
Caching.Hybrid/TimeProvider.Testing) 10.4.0 → 10.8.0 **with** the Aspire bump and the
`AspirePackageVersionTests` guard updated deliberately. Gate: build + full suite.

**Phase 3 — Major bumps, one at a time (each its own commit + test run).**
WolverineFx 5.27 → 6.19 (highest blast radius — messaging core), Asp.Versioning 8 → 10,
StackExchange.Redis 2 → 3, NSubstitute 5 → 6, coverlet.collector 8 → 10. Fix code against tests
before moving on.

**Phase 4 — Analyzer upgrades (last, deliberately).** NetAnalyzers 10.0.104 → 10.0.302,
Meziantou 3.0.19 → 3.0.123. These will surface NEW code warnings — the one place this task creates
real code work. May require nudging the pinned Roslyn 5.0.0 versions. `TreatWarningsAsErrors=true`,
so every new warning must be fixed or explicitly suppressed with justification.

**Phase 5 — Verify & land.** Full `./scripts/run-tests.sh`, `./scripts/run-e2e.sh`,
`dotnet format Wallow.slnx`, clean build, conventional commits, push.

## Guardrails

- One concern per commit; conventional-commit messages (`fix(deps):`, `chore(deps):`, `feat(deps)!:`).
- Test gate after every phase; `Api.Tests` uses Testcontainers (Docker must be up).
- `AspirePackageVersionTests.MicrosoftExtensionsHttpResilience_ShouldRemainUnchanged` guards the
  resilience version — update it only in lockstep with a verified Aspire-compatible bump.
- Prefer same-major patched versions for transitive pins to avoid runtime binding breaks
  (e.g. MessagePack stays on the 2.x line at 2.5.302 for SignalR compatibility).
