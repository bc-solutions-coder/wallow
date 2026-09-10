**status: completed**

# Reduce Actions artifact storage

The user approved shorter retention and fewer unnecessary transfers. Implement the retention change first without combining untrusted preparation with credentialed publication.

## Implementation

1. Retain the .NET same-run build handoff for one day in `.github/workflows/ci.yml`.
2. Retain publication candidates in `ci.yml` and `js.yml`, and prepared payloads in `publish.yml`, for three days. Preserve small reports and plans.
3. Document the shortened approval/retry window in `docs/operations/ci-publication.md`.
4. Run actionlint, the existing CI Python suite, and diff checks. Commit and push the isolated branch.

## Scope and follow-up

Do not delete existing artifacts: pending publications and historical retries can still reference them. Retention edits affect future uploads. Do not remove the preparation-to-publisher transfer: fresh jobs keep build code away from publication credentials. A future Garage/Tailscale design should use isolated runners, restricted network access, short-lived object access, separate PR/trusted cache namespaces, and automatic expiry. Storage alone does not guarantee faster builds; measure transfer and cache-hit timings.

GitHub audit found `first_time_contributors` fork approval and branch-restricted environments without required reviewers. Recommend all-external-contributor approval and a separately protected environment for private infrastructure. These settings are not changed by this storage patch. Approval permits code execution; it is not a credential-exfiltration sandbox.

## Verification

Actionlint and the immutable action policy pass. All 163 existing CI Python tests pass before and after the change. `git diff --check` passes. No source tests were added. GitHub execution of the changed workflows remains to be verified after integration.
