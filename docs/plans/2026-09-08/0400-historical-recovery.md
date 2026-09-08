**status: active**

# Recover exact historical release artifacts

Issue #283 requires credential-free recovery when a selected release producer's artifacts expire. Ordinary publication remains pinned to its existing producer and must never rebuild under publication credentials.

## Required outcome

An explicit CI dispatch on protected `main` names an exact release ID and source commit. The current validation graph executes against that release source, with current control helpers and security policy. All application execution remains outside production environments, Tailscale, and private Turbo. A new successful producer registration records both the historical source commit and current workflow controller commit without changing GitHub's own run identity.

Recovery preserves existing release origins and selection receipts. A separate append-only recovery receipt binds those original asset IDs and digests to the new producer and its artifacts. Publication must explicitly select that recovery. Existing registry versions must still match the original authenticated immutable output bytes. A matching source commit is not proof of reproducible output.

## Implementation sequence

1. Add a recovery request boundary that verifies exact dispatch, repository, workflow, run, attempt, current main controller ancestry, release identity, original origin, and historical source ancestry. It emits data for validation only, never publication authority.
2. Thread an explicit source and controller identity through the current CI graph. Recovery always selects the full route. Source checkout and current control checkout remain separate. Current scanner rules, exception policy, artifact helpers, registration, and aggregate logic come from the protected controller. Historical build inputs remain the selected source.
3. Extend sealed artifact identity and registration with a recovery schema. Normal push and PR schema remains unchanged. API run and artifact `head_sha` remain the controller commit; sealed payload source is the historical commit. Never override `GITHUB_SHA`.
4. Add a distinct recovery-producer authorization path. Require the actual successful CI workflow, required Actions check, complete full-route job evidence, and exact sealed recovery request and registration. Normal push authorization remains unchanged.
5. Add a narrow recovery-receipt finalizer after validation. Reauthenticate the exact original release origin and selection, verify the new successful producer, and append immutable recovery evidence. Preserve failed-finalizer evidence through the existing endorsement pattern without overwriting old assets.
6. Add explicit recovery selection to publication dispatch and shared release-candidate authorization. Automatic and ordinary retries continue to use the original selected producer. All immutable registry conflicts continue to fail before promotion.
7. Verify behavior and actual hosted recovery with no production environment access. Exercise wrong source/tag, PR/foreign workflow, incomplete validation, altered seals, expired ordinary inputs, explicit successful recovery, and changed rebuilt bytes. Document exact retry syntax only after it is implemented.

## Constraints

- Recovery cannot manufacture a missing Release Please origin. Missing origin evidence requires a separately reviewed migration decision.
- Historical source that cannot pass the current validation graph fails. Do not patch that source or skip checks inside recovery.
- Existing completed durable publication receipts remain readable without historical Actions artifacts. Recovery is needed for missing unfinished work, not for every old release.
- No new registry credential, production token, privileged build, or blanket bypass is introduced.
- Keep mutable alias decisions separate from recovering immutable outputs.

## Verification checkpoint

The recovery branch is based on main `a65e967a`. Current-controller checkout, full secretless historical validation, schema-2 registration, completed-run authorization, durable recovery recording, and explicit publication selection are implemented. Package and image writers retain recovery lineage without replacing original origin or selection receipts. Alias preparation can use explicitly recovered package dependency inputs while preserving immutable published outputs as authority.

Independent reviews cover the request and registration boundaries, completed-run authorization, receipt retention and endorsements, preparation and writer forwarding, durable package/image lineage, alias verification, and workflow routing. The combined helper suite passes 367 tests at the alias pipeline checkpoint `35b5259f`. Actionlint passes for the recovery Publish routing. Zizmor 1.30.0 reports four findings in that workflow with zero unexcepted blockers after independent review renewed its exact dangerous-trigger fingerprint. The exception retains its original owner, tracking issue and expiration.

The operator guide contains the three dispatches: historical CI validation, recovery receipt recording, and publication using separate original and recovery producer inputs. PR #293 merged the flow to main at `89043153`; successful hosted recovery acceptance remains pending. Recovery publication excludes main/nightly images, Pages and Release Please.

A separate main-based PR #292 addresses legacy release enumeration outside main ancestry, discovered by real Release Please run 34188348962. That run created release PR #291; its required CI run 34190178773 succeeded. PR #292 merged at `f02bbe13`. Hosted Publish runs 34192088792 and 34193082102 succeeded; the former recorded 17 pending-origin and seven unsupported-ancestry releases without an error. These runs do not prove publication of a new release.

Remaining acceptance includes the actual current-controller historical checkout and full CI, recording and explicitly publishing recovered artifacts, negative source/tag/workflow/receipt cases, unchanged-byte retries and immutable conflicts, and genuine package/image/alias publication readback. Local tests do not establish hosted completion. Schema-2 registration or a recovery receipt alone does not authorize publication.

## Hosted acceptance checkpoint

Recovery rejection checks passed on the merged controller: run 34193297084 rejected a mismatched release source, 34193388359 rejected missing authenticated origin evidence, and 34193522268 rejected a non-main dispatch. Each failed before application validation or production jobs could execute. These expected failures establish refusal behavior only.

Main CI run 34193191714 exposed a telemetry deletion concurrency failure. PR #294 addresses it with row locking and state refresh. Its deterministic regression failed before the fix; all 284 Identity integration tests passed locally after the fix. Hosted CI run 34196298415 passed for commit `96f963a0`, including the original failing deletion test and the deterministic concurrency regression. PR #294 merged at `2ed078d5`. Main validation and a fresh Release Please update remain required before release acceptance continues.
