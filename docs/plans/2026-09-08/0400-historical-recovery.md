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

## Initial verification

The isolated `ci/historical-recovery` worktree starts at `be0f9a3e`. Request authorization, distinct schema-2 source/controller identity, current scanner controls, and sealed recovery registration are implemented and independently reviewed. The registration checkpoint is `c6c38578`.

The workflow integration preserves current helper files, security exceptions, Actionlint configuration, and the OpenAPI action before historical checkout. CI contract tests run against the current controller first. Recovery selects local JS execution and the full validation route. Four Git-backed snapshot tests cover historical checkout, modified control files, controller mismatch, and tracked symlink rejection.

The combined helper suite passes 276 tests. Actionlint, immutable action policy, and whitespace checks pass. Zizmor 1.30.0 reports 27 findings with no blocking findings under existing policy; no new exception was added. Independent review resolved historical OpenAPI action loading, historical Actionlint configuration, and historical CI contract test selection.

These checks establish local implementation evidence only. Completed-run recovery authorization, recovery receipts, explicit publication selection, and hosted positive/negative acceptance remain pending. Schema-2 registration alone does not authorize publication.
