**status: superseded**

Superseded by the user-approved practical publishing scope in `1700-simplify-publishing.md` and issue #283, comment 5590163766. Historical acceptance below is no longer a completion requirement.

# Legacy package alias implementation plan

Goal: migrate the observed API Errors `latest` tag from 1.0.0 to the authenticated 2.0.0 release through the existing alias pipeline.

Design and decision evidence: issue #283, comment 5584019579. This plan records implementation tasks; the ticket owns the decision. Python standard library helpers and GitHub Actions remain the implementation stack.

## 1. Bind the exact migration

Create `.github/ci/legacy-package-aliases.json` and `scripts/ci/publication_legacy_package_aliases.py`. Bind the repository, package, alias, old version and release identity, destination identity, destination receipt pointer, and expiry. Validate the old release against GitHub and its ancestry to the destination. An entry grants permission to replace the observed old tag; it does not create a publication receipt for the old package.

Use existing release-identity helpers rather than a second tag parser. Fail closed on malformed configuration, expiry, duplicate entries, wrong repository, wrong destination, changed receipt, changed old release identity, or divergent ancestry. Return a migration identity only for the exact transition; otherwise preserve ordinary alias behavior.

Add behavior tests in `scripts/ci/test_publication_legacy_package_aliases.py`. Run the tests before implementing the helper to verify the missing behavior, then implement and rerun them.

## 2. Integrate preparation and promotion

Modify `scripts/ci/publication_alias_registry.py` and `scripts/ci/publication_alias_pipeline.py`. Consult migration authority only when an existing package alias has no authenticated predecessor. Keep ambiguous receipt matches rejected. Verify destination registry bytes normally and produce an `advance` entry carrying explicit migration evidence.

Load and revalidate the migration in both preparation and promotion. Preserve fresh scans, package dependency readiness, exact plan equality, and the immediate pre-write observed-tag comparison. Include migration evidence in the existing sealed plan and append-only alias progress. Ensure controller policy hashing covers the migration configuration. Already-migrated tags take the normal identical path, including after migration entry retirement.

Extend behavior tests in `scripts/ci/test_publication_alias_pipeline.py` for exact migration, changed observed tag, wrong destination, and no write on rejection. Verify finalization retains the additional evidence without weakening its exact comparison.

## 3. Validate and ship

Run `python3 -m unittest discover -s scripts/ci -p 'test_publication*.py'` and `git diff --check`. Inspect workflow/policy implications and run applicable CI policy checks. Review the complete diff, push a normal PR, and wait for required CI before merging the tested head.

Run the original API Errors producer 34200366770/1 against release 384544937 on main. Verify latest, major-2, and minor-2.0 resolve to exact 2.0.0 bytes and alias receipts belong to successful writer/recorder jobs. Retry identically and confirm no replacement of the immutable package receipt. Inspect automatic publication run 34216195801 for SDK/telemetry outcomes as well.

## 4. Retire the migration

Hosted cutover 34223437387 passed on controller 8e4308dd23784aa8966e3d2777dc4a562d06bd3a. Release receipt asset 550447037 records verified `latest`, `major-2`, and `minor-2.0` outcomes for exact API Errors 2.0.0 bytes. The original immutable package receipt remains unchanged. Evidence: issue #283, comment 5585444013.

This cleanup removes the consumed configuration and its exact-file DevSkim exception. Identical retry 34228552152 is still pending; merge the cleanup only after successful retry proof. Keep this plan active until hosted normal-path validation passes without the migration configuration.

Remove the consumed migration entry after hosted cutover and retry proof, retaining historical evidence in the ticket. Keep unknown aliases rejected. Mark this plan completed only after the migrated aliases pass normal validation without the entry. Continue the broader image, Pages, recovery, and cache acceptance work tracked by #283.
