**status: completed**

# Local gateway verification for #246

Executed 2026-09-07 against the disposable independent `wallow-245-proof` Compose project.
This continues the storage evidence in `1115-observability-storage-evidence.md`.
Gateway source targets .NET 10; Docker builds with SDK 10.0.302 and runtime 10.0.11.
Alloy 1.19.2 is pinned to digest
`sha256:b8ec653c44235fbe910879145dac3597d66b0aaecf60bcbbe82580767771a839`.
Storage/query image digests remain pinned in Compose. Data root was `/tmp/wallow-245-data`.
The earlier disposable Loki 99% disk-threshold override remained in use because the host
filesystem was above Loki's default 90% threshold. The committed safeguard is unchanged.

## Public HTTP and backend proof

`verify-gateway-seed.py` provisioned two registrations through the authenticated control
API. Each sent forged headers, resource attributes, datapoint/span/log attributes and Faro
app metadata through the gateway. It submitted JSON and protobuf logs, traces and metrics,
plus Faro logs. `verify-gateway-query.py` then checked actual Loki, Tempo and Prometheus
results for the authenticated application/service/registration identities.

Commands from `docker/`:

```sh
evidence=$(docker run --rm -i --env-file observability/gateway.local \
  --network wallow-245-proof_storage python:3.13-alpine python - \
  < observability/verify-gateway-seed.py)
docker run --rm -i --network wallow-245-proof_storage python:3.13-alpine \
  python - "$evidence" < observability/verify-gateway-query.py
docker run --rm -i --env-file observability/collector.local \
  --network wallow-245-proof_storage python:3.13-alpine python - \
  < observability/verify-collector-boundaries.py
```

Both application query reports passed. Direct unauthenticated OTLP and Faro requests to
Alloy returned 401. An internal-credential-authenticated request containing only the
registration header incremented `otelcol_processor_filter_logs_filtered_total`; the filter
reported zero outgoing records. Missing any required trusted identity field now drops data.

The initial Faro proof exposed a real incompatibility: .NET's default JSON encoder escapes
`+` in timestamp offsets, while Faro's time parser does not decode those escapes. Direct
requests with `+00:00` returned 202; the equivalent `\u002B00:00` returned 400. The gateway
now uses a JSON encoder that preserves the offset on its private Faro forwarding path.
The committed seed fixture exercises an offset timestamp through the real receiver.

## Restart and Wallow teardown

A live producer fixture retained application credentials only in its process memory. While
it waited, the separate Wallow Compose project was stopped and the gateway recreated:

```sh
COMPOSE_PROJECT_NAME=wallow-245-isolation docker compose -p wallow-245-isolation \
  -f docker-compose.yml up -d valkey
# Start verify-gateway-seed.py --wait-for-restart in its own Python container.
COMPOSE_PROJECT_NAME=wallow-245-isolation docker compose -p wallow-245-isolation \
  -f docker-compose.yml down
OBSERVABILITY_DATA_DIR=/tmp/wallow-245-data docker compose \
  --project-name wallow-245-proof --env-file observability/.env \
  -f observability/compose.yml --profile ingestion up -d --force-recreate gateway
# Send continue to the waiting producer, then query with --after-restart.
```

The producer reported `PASS: existing credentials accepted after gateway recreation`.
Both backend queries with `--after-restart` passed, including the new log markers. This
uses an actual disposable Wallow infrastructure service/project; it does not claim the
full Wallow application was running. The gateway never connects to Wallow on ingestion.

Sanitized evidence from that lifecycle run:

```json
[
  {
    "registration": "1935c30e-abf5-492c-abe5-68469e8a1c71",
    "marker": "gateway-proof-a-122b29bff3954006b8c34247580b5222",
    "trace": "b05ab5be59c1452888fd8a835973fd6e",
    "proto_trace": "d501c7944c1e46f5a88896637534ae94",
    "application": "application-a",
    "server": "server-a",
    "browser": "browser-a",
    "timestamp": 1788796423790633721
  },
  {
    "registration": "4e35cc0e-73f5-4e82-9a06-c73d87c69267",
    "marker": "gateway-proof-b-8816fd2850bf402aa97bf21c7102323e",
    "trace": "4e92e46b448c4d1fb574d89368bd5eef",
    "proto_trace": "02887a06016c4cc2a615eec8d65c2a3e",
    "application": "application-b",
    "server": "server-b",
    "browser": "browser-b",
    "timestamp": 1788796423818061388
  }
]
```

## Behavior checks and review

Twelve HTTP tests use actual Kestrel listeners and SQLite files. They cover persisted
acknowledgement replay, conflict/stale revisions, deletion replay and resurrection denial
after restart, invalid revisions, concurrent retries, credential activation/revocation,
management/ingestion separation, request size and protocol rejection, and rotation at the
24-hour boundary after control restart. Retrying a rotation under a newer revision retains
the original deadline.

The rate-isolation regression test failed with the previous global-before-registration
ordering, with 11 passing and one failing test. Corrected ordering checks registration
allowance before spending global tokens. The focused run then passed all twelve tests.
Independent standards and spec review confirmed their reported issues were resolved.

`pnpm check` passed. `./scripts/run-tests.sh all` passed 5,610 tests including
Category=Integration. The final gateway image was rebuilt after the rate-limit fix.

This does not verify SDK/Wallow integration, deployed Pangolin trust, Debian storage quotas,
sustained load/capacity, publication, or the remaining #245 slices.
