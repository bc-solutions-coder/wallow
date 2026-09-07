# Observability object storage

This independent Compose project provides Garage for the planned Loki and Tempo services.
It does not yet start Grafana, Loki, Tempo, or the telemetry gateway.

Provision and mount the telemetry disk on the Debian VM first. Choose a parent directory
(default `/srv/observability`) and create its Garage directory **after mounting the disk**:

```sh
sudo mkdir -p /srv/observability/garage
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

Compose requires the `<OBSERVABILITY_DATA_DIR>/garage` directory to exist. It will not
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

Future Loki and Tempo services join this project's `storage` network and use
`http://garage:3900`, region `us-east-1`, and path-style S3 addressing. Configure them with
only their own credentials from `credentials.local`. S3 and RPC have no published host
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
across container recreation, but does not protect against VM or disk loss. Retention is
configured by Loki and Tempo when those services are added.
