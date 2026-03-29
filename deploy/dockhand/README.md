# Wallow — Dockhand Deployment (wallow.dev)

Deploy Wallow to a server running Dockhand. Pangolin handles TLS and routing.

---

## Prerequisites

- Server with Docker Engine 24+ and Docker Compose v2
- Dockhand configured to pull this repo
- Pangolin routing configured for `wallow.dev`, `api.wallow.dev`, `auth.wallow.dev`
- DNS A/CNAME records pointing all three domains to your server
- GitHub PAT with `read:packages` scope (for pulling from GHCR)

---

## 1. Authenticate with GHCR

On the server, log in to the GitHub Container Registry so Docker can pull the images:

```bash
echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

Create a PAT at: **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**
Required scope: `read:packages`

---

## 2. Generate Secrets

Run this once to generate all the secrets you need:

```bash
echo "POSTGRES_PASSWORD=$(openssl rand -base64 24)"
echo "VALKEY_PASSWORD=$(openssl rand -base64 24)"
echo "GARAGE_ACCESS_KEY=GK$(openssl rand -hex 12)"
echo "GARAGE_SECRET_KEY=$(openssl rand -hex 32)"
echo "Identity__SigningKey=$(openssl rand -base64 48)"
echo "OIDC_CLIENT_SECRET=$(openssl rand -base64 32)"
echo "AdminBootstrap__Password=$(openssl rand -base64 16)"
echo "GF_ADMIN_PASSWORD=$(openssl rand -base64 16)"
```

Copy the output — you'll paste these into `.env` in the next step.

---

## 3. Configure .env

```bash
cp deploy/dockhand/.env.example deploy/dockhand/.env
```

Edit `.env` and replace every `CHANGE_ME` value with the generated secrets.

**Critical items:**

| Variable | What to set |
|----------|-------------|
| `POSTGRES_PASSWORD` | Generated password |
| `VALKEY_PASSWORD` | Generated password |
| `GARAGE_ACCESS_KEY` | Generated key (starts with `GK`) |
| `GARAGE_SECRET_KEY` | Generated hex string |
| `Identity__SigningKey` | Generated base64 string |
| `SMTP_PASSWORD` | Your ZeptoMail password |
| `AdminBootstrap__Email` | Your admin email |
| `AdminBootstrap__Password` | Generated password (save this — you'll log in with it) |
| `OIDC_CLIENT_SECRET` | Generated secret |
| `PreRegisteredClients__Clients__0__Secret` | **Same value** as `OIDC_CLIENT_SECRET` |
| `PreRegisteredClients__Clients__0__SeedMembers__0` | **Same value** as `AdminBootstrap__Email` |

The OIDC client secret must match in both places — the API registers the client with `PreRegisteredClients__Clients__0__Secret`, and the Web app authenticates with `OIDC_CLIENT_SECRET`. If they differ, login will fail.

---

## 4. Configure Pangolin

Set up three routes in Pangolin:

| Domain | Target | Protocol |
|--------|--------|----------|
| `api.wallow.dev` | `localhost:8080` | HTTP |
| `auth.wallow.dev` | `localhost:8081` | HTTP |
| `wallow.dev` | `localhost:8082` | HTTP |

Enable WebSocket support on the `api.wallow.dev` route (needed for SignalR at `/hubs/realtime`).

---

## 5. Deploy

Point Dockhand at the compose file:

```
deploy/dockhand/docker-compose.yml
```

Or deploy manually:

```bash
cd deploy/dockhand
docker compose up -d
```

### Startup order (automatic via depends_on)

1. **Postgres** starts (empty — no init scripts needed)
2. **Postgres replica** streams base backup from primary
3. **Valkey** + **GarageHQ** start and become healthy
4. **wallow-migrations** runs EF Core bundles for all 10 modules, then exits
5. **wallow-api** starts (waits for migrations + valkey + garage + replica)
6. **wallow-auth** + **wallow-web** start (wait for API health check)

Watch it come up:

```bash
docker compose logs -f
```

First boot takes a couple minutes while migrations run and the replica syncs.

---

## 6. Verify

### Health check

```bash
curl https://api.wallow.dev/health
```

Expected: `Healthy`

### API info

```bash
curl https://api.wallow.dev/
```

### Test login flow

1. Go to `https://wallow.dev`
2. You'll be redirected to the login page at `auth.wallow.dev`
3. Log in with your `AdminBootstrap__Email` / `AdminBootstrap__Password`
4. You should land on the dashboard

### API docs

Visit `https://api.wallow.dev/scalar/v1` for interactive API documentation.

---

## Ongoing Operations

### Upgrade to a new release

```bash
cd deploy/dockhand

# Pull latest images and restart
docker compose pull && docker compose up -d
```

Or pin a specific version in `.env`:

```ini
APP_TAG=1.2.3
```

Then pull and restart.

### View logs

```bash
# All services
docker compose logs -f

# Just the API
docker compose logs -f wallow-api

# Just migrations (useful on first boot or after upgrade)
docker compose logs wallow-migrations
```

### Restart a service

```bash
docker compose restart wallow-api
docker compose restart wallow-auth
docker compose restart wallow-web
```

### Database backup

```bash
docker exec wallow-postgres pg_dump -U wallow wallow > backup_$(date +%Y%m%d).sql
```

### Database restore

```bash
docker exec -i wallow-postgres psql -U wallow wallow < backup_20260328.sql
```

### Enable observability (Grafana)

```bash
docker compose --profile observability up -d
```

Grafana UI at `http://YOUR_SERVER_IP:3001` (admin / your `GF_ADMIN_PASSWORD`).

### Add a new OIDC client

Add `PreRegisteredClients__Clients__1__*` variables to `.env`, then restart the API:

```bash
docker compose restart wallow-api
```

The API syncs clients from config on every startup.

---

## Troubleshooting

### Migrations failed

```bash
docker compose logs wallow-migrations
```

The output shows which module failed. Common causes:
- Postgres wasn't healthy yet (retry: `docker compose up -d`)
- Database user lacks permissions on the target schemas

### OIDC login redirect loop

- Verify `OIDC_CLIENT_SECRET` matches `PreRegisteredClients__Clients__0__Secret` in `.env`
- Verify Pangolin is passing the `Host` header correctly
- Check CORS: `Cors__AllowedOrigins__0` and `__1` must match your domains exactly

### Container won't start

```bash
docker compose logs wallow-api --tail 50
```

Common causes:
- Missing `Identity__SigningKey` → app crashes immediately
- Wrong database password → connection refused
- Port conflict → another service on 8080/8081/8082

### GarageHQ init fails

```bash
docker compose logs garage
```

If the bucket wasn't created, restart the container:

```bash
docker compose restart garage
```

### Reset everything (destroys all data)

```bash
cd deploy/dockhand
docker compose down -v
docker compose up -d
```

---

## File layout

```
deploy/dockhand/
├── docker-compose.yml          # Full production stack
├── .env.example                # Template — copy to .env and fill in secrets
└── README.md                   # This file

# Shared configs (referenced via relative paths):
docker/
├── init-replica.sh             # Replica streaming setup
├── garage/
│   ├── garage.toml             # GarageHQ S3 config
│   └── init-garage.sh          # Bucket + key init
└── alloy/
    └── config.alloy            # OTLP collector config
```

## Ports reference

| Service | Internal | Host Port | Domain |
|---------|----------|-----------|--------|
| Wallow API | 8080 | 8080 | api.wallow.dev |
| Wallow Auth | 8080 | 8081 | auth.wallow.dev |
| Wallow Web | 8080 | 8082 | wallow.dev |
| Postgres | 5432 | — | (internal only) |
| Postgres Replica | 5432 | — | (internal only) |
| Valkey | 6379 | — | (internal only) |
| GarageHQ | 3900 | — | (internal only) |
| Grafana | 3000 | 3001 | (optional) |
