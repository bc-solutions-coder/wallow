# Test Architecture Audit Findings (2026-02-21)

## Audit Scope
7 parallel agents analyzed test projects across the active modules (Billing, Communications, Configuration, Identity, Storage). Reports in `tests/audit-*.md`.

## Critical Issues Found

### Naming Chaos (5 conventions across projects)
- Projects missing `Wallow.` prefix
- Some modules have mixed conventions internally
- Standard defined: `Wallow.{Module}.Tests` + `Wallow.{Module}.IntegrationTests`

### Shared Infrastructure Problems
- 6/8 builders are `internal` (dead code externally)
- Inline PostgreSQL container creations (should use shared fixture)
- WebApplicationFactory subclasses with copy-pasted code
- Some modules duplicate SetTestUser/SetAdminUser
- No shared DB-only integration test base
- No shared API integration test base

### Integration Test Issues
- Some modules use IClassFixture (3 containers per class) instead of ICollectionFixture
- Environment.SetEnvironmentVariable race condition risk
- Mixed cleanup strategies (RemoveRange vs TRUNCATE vs fresh tenant)

## What's Working Well
- Unit test patterns are mostly consistent (Method_Condition_Result naming)
- Central architecture tests cover all active modules comprehensively
- Testcontainers used consistently for infrastructure
- Good builder pattern in Tests.Common (just needs public visibility)
- Billing domain tests are gold standard for unit tests

## Implementation Plan
Epic: `wallow-aph` with implementation beads in progress.
Dependency chain: infra visibility -> base classes -> collection fixtures -> rename projects.
P1 (do first): shared infra visibility, DB base class, API base class.
P2 (then): GlobalUsings, rename projects, MessagingTestFixture refactor, collection fixtures.
P3 (last): remove redundant arch tests, add Wolverine convention tests, add WithCleanUp.

## CLAUDE.md Created
Comprehensive `tests/CLAUDE.md` with standards for naming, unit tests, integration tests, event sourcing, architecture tests.
