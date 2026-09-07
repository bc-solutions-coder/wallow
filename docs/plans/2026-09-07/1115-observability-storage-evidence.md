**status: completed**

# Local storage verification for #244

Executed on 2026-09-07 with Docker Engine 28.5.1 on the local macOS host, using
project `wallow-245-proof` and disposable bind root `/tmp/wallow-245-data`.
This is not Debian deployment evidence.

Images are pinned by digest in `docker/observability/compose.yml`: Garage 2.2.0,
Loki 3.7.7, Tempo 2.10.8, Prometheus 3.14.0 and Grafana 13.2.1.
Python 3.13 Alpine ran the public HTTP probes inside the private storage network.

## Commands and results

From the repository root:

```sh
mkdir -p /tmp/wallow-245-data/{garage,loki,tempo,prometheus,grafana}
OBSERVABILITY_DATA_DIR=/tmp/wallow-245-data \
  python3 docker/observability/setup.py --project wallow-245-proof
```

All five containers started. Linux hosts require the ownership preparation documented in
the README; Docker Desktop's bind permission mapping is not proof of Debian ownership.
The host filesystem was 94% occupied with 55 GiB available. Loki correctly throttled WAL
writes at its default 90% threshold. The disposable proof used this uncommitted override:

```yaml
services:
  loki:
    command: ["-config.file=/etc/loki.yml", "-config.expand-env=true", "-ingester.wal-disk-full-threshold=0.99"]
```

The committed configuration retains the default disk safeguard. This small ingestion
exercise did not test disk capacity and did not remove unrelated Docker data.

```sh
evidence=$(docker run --rm -i --network wallow-245-proof_storage \
  python:3.13-alpine python - seed < docker/observability/verify.py)
OBSERVABILITY_DATA_DIR=/tmp/wallow-245-data docker compose \
  --project-name wallow-245-proof --env-file docker/observability/.env \
  -f docker/observability/compose.yml -f /tmp/wallow-245-proof.yml \
  --profile query up -d --force-recreate
docker run --rm -i --network wallow-245-proof_storage python:3.13-alpine \
  python - query --evidence "$evidence" < docker/observability/verify.py
```

Seed evidence:

```json
{"timestamp":1788794038073859964,"trace_id":"929a5cf19d5149f38131d8a6d1af4ad3","marker":"storage-proof-929a5cf19d5149f38131d8a6d1af4ad3","metric_time":1788794038.173}
```

Result:

```json
{"persisted_log":true,"object_store_trace":true,"historical_metric":true,"trace_id":"929a5cf19d5149f38131d8a6d1af4ad3"}
```

An earlier probe wrote `wallow-storage-proof-245` at `1788793829729230423`, trace
`24500000000000000000000000000001` with span name `storage-proof`, and queried
`up{job="prometheus"}` at `1788793829.762`. Garage bucket info showed two Loki objects
and four Tempo objects after flush. All three records remained queryable after recreation.
To exclude Loki's local WAL/cache as the source, the disposable Loki container was stopped,
its data directory moved to `loki-before-store-proof`, an empty directory created in its
place, and Loki force-recreated. The same log still returned. Query diagnostics showed
`ingester_chunk_downloaded=1`, `ingester_chunk_matches=0`, and no in-memory head bytes.
Tempo queries used `mode=blocks` and returned the exact stored span.

For Wallow project teardown, from `docker/`:

```sh
COMPOSE_PROJECT_NAME=wallow-245-isolation docker compose -p wallow-245-isolation \
  -f docker-compose.yml up -d valkey
COMPOSE_PROJECT_NAME=wallow-245-isolation docker compose -p wallow-245-isolation \
  -f docker-compose.yml down
```

The Wallow Valkey container and Wallow network were removed. The independent observability
containers remained running, and the persisted log/block-only trace/historical metric
query above passed afterward. This exercises actual Wallow Compose project isolation with
one infrastructure service; it does not claim the full Wallow application was running.

## Checks and review

- Compose validation with `--profile query config --quiet` passed.
- `pnpm lint:env`, `pnpm typecheck`, and `pnpm check` passed.
- Python byte compilation of setup and verification scripts passed.
- Independent standards review found no violations or substantiated smells.
- Independent spec review requested object-store and execution evidence, recorded above.

## Limits

Actual Debian mount readiness, enforced quotas, retention over days, sustained resource
usage, gateway/application integration, Pangolin SSO and package publication remain
unverified. Directory existence and Garage layout capacity are not quota enforcement.

A repeated setup run preserved byte-identical `credentials.local`, `loki.local`,
`tempo.local` and `grafana.local`; all four retained mode 600. No credential values
were printed by the comparison.
