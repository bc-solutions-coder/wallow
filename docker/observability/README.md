# Observability object storage

This independent Compose project provides Garage for the planned Loki and Tempo services.
It does not yet start Grafana, Loki, Tempo, or the telemetry gateway.

From the repository's `docker` directory:

```sh
python3 observability/setup.py
```

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

`down` preserves the project's Garage volumes. Wallow's Compose project does not own
these volumes or the storage network. `down --volumes` deletes the object store's data.

Garage is limited to 512 MiB RAM. The initial layout advertises 32 GB capacity; this is a
placement weight, **not a disk quota**. Named volumes currently use Docker's storage.
Before deploying to the Debian VM, place these volumes on the agreed dedicated telemetry
disk or enforce filesystem quotas. The 60 GB telemetry disk allocation is not provisioned
by this command.

This is a single-node store with replication factor one and no backups. It preserves data
across container recreation, but does not protect against VM or disk loss. Retention is
configured by Loki and Tempo when those services are added.
