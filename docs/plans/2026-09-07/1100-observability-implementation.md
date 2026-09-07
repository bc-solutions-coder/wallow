**status: active**

# Observability implementation

Implement GitHub spec #245 and its approved slices on the current branch. The resolved
Wayfinder decisions and #240 already approve design and public testing seams.

1. Completed — #244: preserve Garage commits; add pinned storage/query services in
   `docker/observability`, automatic private configuration and repeatable setup. Exercise
   real ingestion, flush, recreation and object-store reads with disposable data. Record
   image digests and query evidence. Validate Compose and environment documentation.
2. Completed — #246: standalone .NET gateway, SQLite registry and control/ingestion HTTP boundaries.
   Test ordering, replay, persistence, authentication, attribution and limits through HTTP.
3. Completed — #247: installable telemetry package with explicit Node initialization, bounded export
   and bundled maintained logger. Verify a packed external Node consumer.
4. Completed — #248: transactional registration desired state and outbox, retries and one-time reveal.
   Exercise existing organization-client integration and rendered registration seams.
5. Completed — #249: browser entry and same-origin session-aware relay, sanitization and owned-route
   propagation. Exercise external browser consumers and login/context transitions.
6. Completed — #250: acknowledged rotation, revocation and deletion with persisted deadlines. Test
   the public gateway boundary with a controllable clock and restart.
7. #251: correlated browser/Node/API failure and Grafana investigation views. Query actual
   logs, spans and metrics, including forged identity and sensitive-marker attempts.
8. #252: operator-only Pangolin access. Separate local proxy proof from actual deployment.
9. #253: disposable overload, outages, persistence and capacity exercises; record dropped
   data counters and keep mount/quota deployment claims explicit.
10. #254: release configuration, packed artifact and exact published consumer verification.

Use behavior-focused red/green tests at the approved seams. Run relevant focused checks
and typechecking during implementation, then `pnpm check` and `./scripts/run-tests.sh`.
Review standards and spec independently with the code-review skill before handoff.
Commit coherent slices. Do not claim publication or Debian verification without evidence.
