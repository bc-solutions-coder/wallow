# Independent observability storage and queries

This independent Compose project runs Garage, Loki, Tempo, Prometheus and Grafana.
The application gateway, collector and Pangolin integration are separate implementation slices.
All service ports remain private. Grafana anonymous access and account signup are disabled.

Provision and mount the telemetry disk on the Debian VM first. Choose a parent directory
(default `/srv/observability`) and create its service directories **after mounting the disk**:

```sh
sudo mkdir -p /srv/observability/{garage,loki,tempo,prometheus,grafana}
sudo chown 10001:10001 /srv/observability/{loki,tempo}
sudo chown 65534:65534 /srv/observability/prometheus
sudo chown 472:472 /srv/observability/grafana
```

From the repository's `docker` directory:

```sh
python3 observability/setup.py
```

To use another path, set `OBSERVABILITY_DATA_DIR=/your/mount/path` in
`observability/.env`, or pass it through the environment on the first run:

```sh
OBSERVABILITY_DATA_DIR=/your/mount/path python3 observability/setup.py
```

Persist the path in `observability/.env` for later setup and Compose commands. The shell
value overrides the file for that invocation. On first run setup creates `.env` with the
default path and a generated secret if it does not exist. An existing `.env` must retain
its valid `GARAGE_RPC_SECRET`; do not replace it with the empty example value.

Compose requires each service directory to exist. Garage runs as root; Loki and Tempo
use UID 10001, Prometheus 65534, and Grafana 472. It will not
create it implicitly. The path points to the Docker host, not a remote client machine.
This existence check is not a mount check: ensure the disk is mounted before Docker
starts, including after a VM reboot.

Requires Docker Compose v2 and Python 3. The setup command starts the pinned upstream
Garage image, initializes a single-node layout, and creates `observability-loki` and
`observability-tempo`. Each bucket has its own read/write key. Rerunning setup preserves
keys, objects, and the existing layout, and reapplies bucket permissions.

The command generates `observability/.env` and `observability/credentials.local` with
owner-only permissions. Both are ignored by Git. Credentials are written to the file,
never printed. Keep the RPC secret alongside the stored data when moving this deployment.
Do not use Wallow's Garage credentials here.

Loki and Tempo join this project's `storage` network and use
`http://garage:3900`, region `us-east-1`, and path-style S3 addressing. Setup writes separate `loki.local` and `tempo.local` environment files so each service
receives only its own credentials. `grafana.local` holds the generated recovery password;
setup preserves that password on subsequent runs. S3 and RPC have no published host
ports, and no web or admin HTTP service is enabled.

```sh
docker compose --env-file observability/.env -f observability/compose.yml ps
docker compose --env-file observability/.env -f observability/compose.yml down
python3 observability/setup.py
```

The default project name is `wallow-observability`. For a distinct test deployment, pass
`--project NAME` to setup and `--project-name NAME` to Compose. Run setup from a separate
checkout for each deployment so its local secrets remain independent.

Garage stores metadata and objects beneath `<OBSERVABILITY_DATA_DIR>/garage`.
Compose `down`, including `down --volumes`, does not delete bind-mounted data. Wallow's
Compose project does not own this directory or the storage network.

Changing the path does not move existing data. Stop Garage, copy its entire directory
(including metadata) to the prepared destination, retain the RPC secret, update `.env`,
and restart. Pointing at an empty directory initializes a new store.

Garage is limited to 512 MiB RAM. The initial layout advertises 32 GB capacity; this is a
placement weight, **not a disk quota**. Provision the agreed 60 GB telemetry disk or
filesystem quota manually; setting the path does not allocate space or enforce a quota.

This is a single-node store with replication factor one and no backups. It preserves data
across container recreation, but does not protect against VM or disk loss. Loki retains logs for 336 hours and Tempo traces for 168 hours. Prometheus retains
metrics for 30 days with an 8 GB retained-block limit. This excludes its WAL and head chunks.
Memory ceilings are Garage 512, Loki 1280, Tempo 1024, Prometheus 1024 and Grafana 512 MiB.
Each container rotates operational logs at 10 MB with three files.

Setup enables the `query` profile after provisioning Garage keys. Include `--profile query`
when manually starting or stopping the entire project. Generated configuration contains secrets;
do not print `docker compose config` or container environments into shared logs.

## Disposable storage verification

Use a separate checkout and an empty data directory. Never remove WAL or cache directories
in an established deployment for this exercise. Prepare service ownership as above and run
setup with `--project observability-proof`. Use a Python container on the private network;
no service port needs publishing. The Python image is a verification tool, not a service.

```sh
# Run from docker/. Set OBSERVABILITY_DATA_DIR to the disposable directory.
python3 observability/setup.py --project observability-proof
# Wait for Loki /ready, Tempo /ready and Prometheus /-/ready before seeding.
evidence=$(docker run --rm -i --network observability-proof_storage \
  python:3.13-alpine python - seed < observability/verify.py)
docker compose --project-name observability-proof --env-file observability/.env \
  -f observability/compose.yml --profile query up -d --force-recreate
docker run --rm -i --network observability-proof_storage python:3.13-alpine \
  python - query --evidence "$evidence" < observability/verify.py
```

`seed` writes a unique log and trace, records an existing self-scraped metric timestamp,
and requests Loki/Tempo flush. `query` retries for two minutes, checks the exact log and
trace marker, forces Tempo block-only lookup, and queries the historical metric timestamp.
Retain the seed JSON as sanitized evidence. A successful recreation query alone does not
prove Loki read Garage: its local WAL could still serve the record. After waiting for
Loki index upload, stop Loki in the **disposable project**, move that project's Loki data
directory aside, create a fresh directory with the same ownership, recreate Loki and repeat
`query`. Success with no local Loki state proves the configured object store supplied the log.

To test project isolation, start and tear down a separate disposable Wallow Compose project,
then repeat `query` against this project. Do not tear down another developer's running stack.
Record the exact Wallow Compose command and project name with the query output.

This proves local persistence and query behavior. It does not prove Debian mount readiness,
quotas, retention under sustained workload, deployed Pangolin trust, or application integration.

## Gateway and collector

Prepare two additional directories on the mounted disk:

```sh
sudo mkdir -p /srv/observability/{gateway,alloy}
sudo chown 1654:1654 /srv/observability/gateway
python3 observability/setup.py
docker compose --env-file observability/.env -f observability/compose.yml \
  --profile query --profile ingestion up -d --build
```

Alloy runs as root and the gateway as the .NET image's UID 1654. Setup generates
`gateway.local` with the management credential and `collector.local` with a separate
internal collector credential. Reruns retain both. Only gateway and Alloy receive the
collector credential. Neither management nor collector credentials belong in application
configuration. Setup still starts the storage/query profile by default; the ingestion
profile adds the gateway and Alloy. Include both profiles when stopping the entire project.

The gateway has two listeners, neither published on the host:

- `gateway:8081` accepts authenticated `PUT /control/v1/registrations/{registrationId}`.
- `gateway:8080` accepts authenticated `POST /faro`, `/v1/logs`, `/v1/traces` and `/v1/metrics`.

The control API takes a complete desired registration with `revision`, `clientId`,
`applicationId`, `browserService`, `serverService`, `environments`, `state`, `credentials`
and optional `rotation`. States are `Enabled`, `Disabled` or `Deleted`. Credential entries
contain only `id` and a 64-character hexadecimal SHA-256 `verifier`. Credential IDs may
contain ASCII letters, digits, hyphens and underscores, but no dots. Rotation identifies
`operationId`, `previousCredentialId` and `newCredentialId`, with both credential verifiers
included in the complete update. Application grouping is authorized by the management caller.

The response contains the committed revision, acknowledgement time and rotation deadline.
Identical retries return the original response. Conflicting/stale updates return 409,
and deletion permanently prevents resurrection. Rotation retires the previous credential
24 hours after its first acknowledgement; retrying an operation cannot extend that deadline.
Omitting a credential revokes it permanently. Disablement also revokes current credentials;
re-enabling requires a new credential ID and secret. Suspension should not send disablement.

Application servers authenticate with `Authorization: Bearer credentialId.secret`, set
`X-Wallow-Environment` to an authorized environment and optionally `X-Wallow-Release` to a
bounded release identifier. The gateway replaces identity headers and Faro app metadata.
It never forwards application credentials to Alloy. OTLP supports JSON and protobuf;
Faro supports JSON. Compression is rejected. Limits are 1 MiB per ingestion body,
64 KiB per control body, 16 concurrent forwards, 10 batches/second per registration with
burst 20, and 50 batches/second globally with burst 100. A body-read/export budget is five
seconds. Overload returns 429 and collector outages return 503.

SQLite registration state, credential verifiers, rotation deadlines and deletion records
live beneath the gateway data directory. Established ingestion uses only that local registry
and the independent collector/storage services. Registration UI, durable Wallow outbox,
SDK initialization and Pangolin routing are subsequent integration work.

### Gateway verification

In a disposable deployment, run these from `docker/`. The seed fixture provisions two
registrations through HTTP, forges identity in headers/resources/Faro metadata, and sends
JSON and protobuf logs, traces and metrics plus Faro logs. It prints only query identifiers.
Plaintext application credentials remain inside the fixture process.

```sh
evidence=$(docker run --rm -i --env-file observability/gateway.local \
  --network observability-proof_storage python:3.13-alpine python - \
  < observability/verify-gateway-seed.py)
docker run --rm -i --network observability-proof_storage python:3.13-alpine \
  python - "$evidence" < observability/verify-gateway-query.py
docker run --rm -i --env-file observability/collector.local \
  --network observability-proof_storage python:3.13-alpine python - \
  < observability/verify-collector-boundaries.py
```

The collector-boundary fixture checks 401 responses without the internal credential and
an increased dropped-record counter for an authenticated request missing trusted metadata.
The internal collector credential authenticates gateway-to-Alloy traffic in addition to
network isolation. Application credentials cannot access Alloy directly.

For restart/outage proof, mount `verify-gateway-seed.py` into the Python container and run
it with `--wait-for-restart`, keeping standard input open. After it prints its evidence,
recreate the gateway, or tear down the separate disposable Wallow project, then type
`continue`. The still-running producer sends new logs using its existing credentials.
Pass the first evidence line to `verify-gateway-query.py` with `--after-restart` to assert
those new logs are queryable. These are verification fixtures, not application setup tools.

Backend behavior checks:

```sh
# From the repository root:
./scripts/run-tests.sh api/tests/Wallow.TelemetryGateway.Tests
```

They exercise HTTP listeners and real SQLite files, including retry after restart,
conflicts, persisted deletion, revocation, rotation deadlines across restart, unsupported
protocols, oversized bodies, credential separation, and one registration exhausting its
rate allowance while another remains admitted.

## Connect Wallow registration

Configure these environment variables on the Wallow API process once for the deployment:

- `Telemetry__ControlEndpoint`: the private gateway control listener URL (port 8081).
- `Telemetry__ManagementSecret`: the management secret generated by `setup.py`, from the
  private `gateway.local` file. This is distinct from application ingestion credentials.
- `Telemetry__IngestionEndpoint`: the public HTTPS gateway base URL returned to registrants.

The API must be able to reach the control listener through a private route. Do not expose
that listener as the public ingestion endpoint. Wallow writes desired state and durable
outbox work in its existing database transaction, then provisions asynchronously. A
configured but unreachable gateway leaves new registrations Pending and retries them.
Missing deployment configuration shows Action required. Once acknowledged, registration
is Active and existing ingestion no longer depends on the Wallow process being available.

Registrants can opt in during client creation or enable observability from the existing
client ledger. Copy the separate server credential when revealed; read endpoints do not
return it again. The browser must never receive this credential.

Routine credential rotation is available after acknowledgement. The old credential
continues working for 24 hours from gateway activation; the ledger shows that deadline.
Another routine rotation is refused while that overlap is active. Revoke the credential
or disable observability to end all ingestion access instead. Both operations show
Pending revocation during a control-path outage and Disabled only after acknowledgement.
Re-enabling after acknowledgement returns a fresh one-time credential.

Deleting a client or its organization stores a versioned tombstone and delivers it through
the same durable path. The independent gateway refuses later resurrection of that
registration. Suspension leaves telemetry access intact. None of these operations deletes
historical logs, traces or metrics before their configured retention expires.
