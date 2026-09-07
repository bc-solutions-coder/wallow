**status: completed**

# Credential lifecycle evidence

Implements #250 within #245. Routine rotation persists both verifiers and an operation ID.
The gateway atomically activates the replacement and fixes the old key's 24-hour deadline;
Wallow records that acknowledgement without extending it. A second rotation during the
active overlap is refused. Revoke and disablement stop new ingestion only on acknowledgement.
Re-enabling issues a fresh secret. Client/organization deletion retains versioned tombstones;
suspension keeps diagnostics available.

Identity registration, later enablement and deletion share an organization row lock and
revalidate ownership inside the transaction. This prevents a late enablement from creating
orphaned access after deletion. Aggregate transitions and the existing outbox commit together.

Verification:

- Final Identity integration run: 283 passed. This covers original acknowledgement
  replay, exact expiry boundary after restart, repeated rotation refusal, suspension,
  pending revocation/recovery, re-enable/disable, organization tombstones, PostgreSQL
  lock-wait interleaving and gateway refusal after a delivered client-deletion tombstone.
- `pnpm check`: passed. Focused rendered controls: 11 passed, including one-time
  replacement copy/dismissal and immediate revoke.
- `./scripts/run-tests.sh all`: all integration, architecture and handler-codegen checks
  passed. Three Identity unit fixtures required storing their mocked organization for
  transaction-time ownership checks; fixed and reran `./scripts/run-tests.sh identity`:
  all 1,672 passed. The final parent verification will rerun the combined gate.
- `dotnet format api/Wallow.slnx --no-restore`: completed.
- Independent standards/spec reviews: all findings resolved; no remaining source blockers.

The local schema was reshaped rather than adding a compatibility migration, as required
for this pre-release repository. External deployment/publication remains unverified.
