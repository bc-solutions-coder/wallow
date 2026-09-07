**status: completed**

# Push device ownership and self-test sending

Implement the approved brief on GitHub issue #222. Review baseline: `5c6aada6`.

The public HTTP API and actual developer-application token flow are the agreed test seams. Use disposable PostgreSQL/Valkey fixtures, production organization context, ordinary members, and a controlled push provider. Keep actual browser delivery in #221.

1. Reproduce same-organization cross-user removal, then carry the caller identity through removal and return 404 for another owner's device.
2. Reproduce registration retries, then make registration atomic under the existing organization/token uniqueness constraint. Same-owner retries converge; active ownership conflicts return 409; removal permits re-registration. Removal must also check ownership when it writes, to prevent a concurrent ownership change from invalidating an earlier check.
3. Reproduce public arbitrary-recipient sending, then remove the recipient selector and derive the self-test recipient from authentication. Preserve internal delivery and preferences.
4. Verify missing authentication/organization, isolation, concurrent retries, and controlled delivery. Regenerate OpenAPI/SDK and document the contract.
5. Run repository quality gates, review Standards and Spec independently, fix findings, commit, and push the current branch.

Registration uses one parameterized PostgreSQL upsert through EF Core, with organization scoping explicit because raw SQL does not apply EF filters. A conditional update permits only the same owner/platform or an inactive registration. Use the affected-row count to distinguish an ownership conflict. Conditional removal uses the same owner predicate at write time.


## Delivery correction

Controlled delivery exposed a background-context timing problem: the worker database context
was created before organization restoration. The queued command now carries the organization,
and the push-message repository refreshes its database context from the restored scoped
organization before reading. Query filters remain active; repository read methods do not take
an organization argument.

The worker loads the current registration and checks that it is active and owned by the
message recipient. A gated delivery test reproduces removal and ownership handover while a
message is queued and verifies that the worker skips the obsolete registration.

## Verification

- Ownership, registration retries, self-test recipient, and queued handover each reproduced
  before their corresponding fix.
- `pnpm check` passed, including generated-artifact consistency, builds, typechecks, tests,
  export checks, and the external SDK consumer.
- The full backend run included integration tests: 5,540 passed and one architecture assertion
  failed. The assertion rejected a tenant argument on a repository read; the implementation
  was corrected without relaxing the architecture test.
- Final focused backend verification passed all 983 tests: all architecture and Notifications
  tests, nine push HTTP integration cases, and handler compilation checks. Three existing
  Notifications tests remained skipped.
- `dotnet format api/Wallow.slnx --no-restore` and diff whitespace checks passed.
- Independent Standards and Spec reviews have no remaining findings.

The public request contract and generated SDK no longer expose recipient selection. Browser
Web Push protocol delivery remains outside this issue.
