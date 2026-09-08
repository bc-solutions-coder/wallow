**status: active**

# Simplify CI publishing implementation plan

Goal: deliver the user-approved practical CI and publishing scope, including docs deployment. This supersedes receipt, historical recovery, legacy provenance migration and exhaustive recovery acceptance requirements in earlier migration plans. Issue #283 records the scope decision.

## Target behavior

Keep required PR tests and security checks without production secrets. Keep main Turbo and Tailscale configuration in the main-only production environment. Release Please uses its production environment token only from protected main. Package, image and Pages jobs retain separate minimal permissions and environments.

Publishing accepts a successful CI run from this repository's protected main branch, resolves its exact commit and attempt, and consumes that run's matching artifacts. Preserve artifact integrity checks and immutable version conflicts. Publish main image commit tags and nightly; publish release versions and predictable stable tags. Serialize publication and prevent older retries from moving current tags backward using release versions and current main identity, without reconstructing historical job receipts. Pages deploys the validated site with the official Pages actions. A retry rechecks the registry and completes missing outputs; it never replaces conflicting immutable versions.

Remove custom origin, producer-selection, endorsement and alias receipt chains, historical recovery selection, Pages intent-history reconstruction and per-tag legacy provenance migrations. Existing release assets may remain as historical data, but are no longer prerequisites. Do not delete existing published versions or automatically overwrite conflicts. Cache-server restart tests and exhaustive historical recovery cases are outside this update.

## 1. Replace publishing orchestration

Files: `.github/workflows/publish.yml`, `.github/workflows/release-please.yml`, `.github/workflows/docs.yml`, `scripts/ci/publication*.py`, `.github/ci/publication.json`.

Trace retained artifact formats and registry transports before choosing reusable helpers. Reduce orchestration to successful-main authorization, Release Please, package publishing, image publishing and Pages. Use one explicit source/run selection shared by writers. Keep credentials out of artifact processing and lifecycle scripts where possible. Replace receipt-based release selection with actual release tag/commit and successful CI identity. Retain useful component/image catalog data. Remove receipt recorder jobs and their permissions rather than leaving disabled jobs.

Verify selection rejects PR/fork/failed/wrong-commit runs, registry retries skip identical bytes, conflicting immutable versions fail, and older releases cannot roll current tags backward. Use behavior tests for these small boundaries, not raw workflow-source assertions.

## 2. Remove unused recovery and receipt code

Files: `.github/workflows/ci.yml`, `.github/actions/`, reusable JS/security/CodeQL workflows, `scripts/ci/recovery_request.py`, `scripts/ci/publication_recovery*.py`, `scripts/ci/publication_release_origin.py`, `scripts/ci/publication_release_receipts.py`, `scripts/ci/release_evidence.py`, associated tests and migration configuration.

Remove historical recovery dispatch inputs and source plumbing while preserving normal PR/main checkout isolation. Delete unused registration, receipt, recovery and legacy alias helpers after caller migration. Remove corresponding tests and scanner exceptions. Verify remaining imports and workflow references resolve. Retain generic artifact integrity and registry code only where the replacement actually calls it.

## 3. Document and review one coherent replacement

Files: `docs/operations/ci-publication.md`, affected active migration plans, `.github/ci/security-exceptions.json` and issue #283.

Rewrite operations around secrets, ordinary publishing, tags, retry and conflicts. Mark superseded plans accurately without erasing historical evidence. Run actionlint, applicable security policy checks and retained behavior tests. Review the complete replacement diff and submit one implementation PR through full required CI. No additional receipt diagnostic PR is required.

## 4. Finite live acceptance

- A normal PR passes required checks without production secrets or remote Turbo credentials.
- Its merge passes main validation with remote Turbo enabled; retain the already-observed genuine remote-hit evidence unless a cache change requires revalidation.
- Release Please produces the expected release PR/release from protected main.
- Intended packages publish and install; images publish and can be pulled by version/commit and intended stable tags.
- Pages deploys the validated site and returns the expected content.
- One normal rerun safely completes or skips existing identical outputs. An isolated conflicting-version behavior check proves rejection.
- The operating guide and issue checklist match the deployed workflows.

These outcomes define completion. Historical receipts, expired-artifact recovery, cache-server restart tests and a broad failure-scenario matrix are not release blockers.
