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
