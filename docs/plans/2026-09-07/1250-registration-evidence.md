**status: completed**

# Registration provisioning evidence

Implements #248 within parent #245. Registration opt-in and later enablement store a
separate random 256-bit credential verifier and versioned desired state in Identity.
The existing transaction enrolls durable outbox work; plaintext appears only in the
one-time response. The worker reconciles through the gateway's private HTTP control
listener, retries transient failures and refreshes acknowledged registrations every
five minutes. Optimistic concurrency discards stale delivery results.

Missing operator configuration shows Action required with an explanation. Read APIs
contain status and revisions, never the credential or verifier. The organization
ledger refreshes Pending and Action required states; the one-time dialog clears its
secret on dismissal.

Verification:

- Focused rendered registration and API seam tests: 24 passed, including opt-in,
  later enablement, copy/dismissal and both automatic status transitions.
- `pnpm check`: passed, including generated artifacts, lint, builds, typechecking,
  workspace tests, export checks and the isolated SDK consumer.
- `dotnet format api/Wallow.slnx --no-restore`: completed.
- `./scripts/run-tests.sh api/tests/Modules/Identity/Wallow.Identity.IntegrationTests all`:
  279 passed, zero failed. Real gateway outage recovery, authenticated ingestion,
  verifier-only persistence, duplicate desired-state replay and duplicate enablement
  all passed. Duplicate enablement uses the existing 422 business-rule contract.
- Independent standards/spec reviews: earlier response-buffer and status-refresh
  findings resolved; no remaining source blockers in #248.

This slice does not claim lifecycle rotation/deletion (#250), package publication,
Pangolin deployment or Debian mount/quota verification.
