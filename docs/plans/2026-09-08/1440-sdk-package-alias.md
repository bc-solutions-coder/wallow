**status: superseded**

Superseded by the user-approved practical publishing scope in `1700-simplify-publishing.md` and issue #283, comment 5590163766. Historical acceptance below is no longer a completion requirement.

# SDK package alias migration

Implementation follows the exact, expiring package alias migration mechanism already reviewed in #298. Decision and observed registry evidence belong to issue #283.

1. Bind SDK `latest` 2.0.0 to authenticated 3.0.0 in `.github/ci/legacy-package-aliases.json`. Release 383734408 resolves to commit 97159f6f97ece1b055f57974508169804c82a9f0; release 384544952 resolves to c078b1aa2b7c311c6d2c7b267dcec5501e0e0018. GitHub compare confirms the former is an ancestor, 36 commits behind. Pin immutable package receipt asset 550336780 and its exact digest. Preserve the existing expiry and all normal registry byte, scan, dependency and observed-tag checks.
2. Run publication tests and policy validation. Review and merge through required PR CI. The exact-file DevSkim exception covers public Git identifiers and expires with this migration.
3. Dispatch original producer 34200366770/1 for SDK release 384544952. Verify successful alias writer and recorder evidence for `latest`, `major-3`, and `minor-3.0`, exact package bytes, and the unchanged immutable receipt. Retry identically.
4. Remove the consumed entry and its scanner exception after successful cutover and retry. Verify the ordinary path after retirement before marking this plan completed.

Preflight: automatic publish 34234816983 reported SDK `latest` target 2.0.0 with zero matching durable receipts. A read-only invocation of the existing migration authorizer passed against live GitHub release identities and ancestry. Configuration SHA-256: 4d156b188520acdc080c175c857983bb02e41573f76641d7d8808ae82fe83cf6. All 280 publication tests passed on the starting revision.
