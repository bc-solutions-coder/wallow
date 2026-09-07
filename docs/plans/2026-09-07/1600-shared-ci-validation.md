**status: active**

# Shared CI validation implementation plan

Issue: #280. The approved design remains on #212/#225/#227; the main-cache amendment on #227 controls conflicting historical cache policy.

## Outcome and boundaries

One CI entry point validates PR integration revisions and main push revisions. PRs receive no production environment or private network/cache credentials. Main JS validation uses the existing production environment for Turbo/Tailscale. Publication remains disabled. Actual cache authorization, fork/private-profile execution and merge-protection acceptance require live evidence; do not mark them complete from local fixtures.

## Implementation sequence

1. Add tested Python helpers in scripts/ci/validation.py for complete git-based routing, exact-job aggregate validation and run/attempt/checksum-bound artifact manifests. Test real temporary git histories and malformed/missing evidence with unittest, without source-text assertions.
2. Rewire .github/workflows/ci.yml to PR/main triggers with PR-only cancellation, the strict docs allowlist, all full-route correctness jobs and the stable CI / required aggregate. Replace bin/obj and image cache handoffs with explicitly named same-run artifacts verified before extraction/loading. Make missing unit/integration coverage fail.
3. Consolidate JS validation and route drift into a reusable js.yml with distinct environment-free PR/fork and main-production jobs sharing the same steps. Retain serialized tests, export/consumer checks and fork smoke. GitHub caches remain optional; main-only Turbo/Tailscale uses a cold fallback. Save exact validated package outputs.
4. Move docs and OpenAPI into the shared graph. Preserve docs contrast, package the validated site into the docs image, and emit downloadable patches for generated-file drift. Preserve the separate backend snapshot and generated-client comparisons.
5. Implement the selected portable/codeql security profiles, pinned scanner execution, explicit finding thresholds, report validation and scoped expiring exceptions. Preserve all-platform image scanning and resolved dependency checks. Keep reports independent of GitHub code-scanning uploads.
6. Run helper behavior tests, Actionlint, pnpm check and the .NET gate. Review spec compliance and code quality. Rebase onto current main and push a draft PR targeting main so the main-targeted PR trigger runs. Include the publication pause from the still-open #228 and record that dependency, then observe actual CI runs. Do not change main protections before the real aggregate check and negative acceptance scenarios are proven.

## Verification and handoff

Record exact commit/run links and any failures. Test routing for docs/full, rename/delete/malformed diffs; aggregate failures/cancellations/unexpected skips; manifest checksum/identity mismatch; scanner thresholds, exceptions and malformed reports. Run real full CI after pushing. Existing unit-one acceptance gaps remain with #228. Keep #280 open until its GitHub/private-fork/cache/ref/merge acceptance matrix is actually proven.

## Integration notes

Rebased onto main `7df0a560`. Main added independent telemetry publication to the retired package publisher; the publication pause takes precedence. Shared validation packs telemetry once and runs its isolated artifact consumer against that exact tarball. Unit three must restore telemetry alongside SDK/api-errors, including its browser tests and isolated artifact check.
