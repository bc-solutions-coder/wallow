# Wallow Multi-Environment Deployment & Promotion Pipeline — Design

**status: active**

Design for Dev / Staging / Production environments on a Proxmox homelab, with an automated
build → dev → staging → prod promotion pipeline, Pangolin edge routing, fork-friendly
multi-domain support, and a scaling story that grows from one box to many without redesign.

Decisions made during brainstorming (2026-07-25):

| Decision | Choice |
|---|---|
| Orchestration | k3s (lightweight Kubernetes) + GitOps (Argo CD) |
| Topology | All environments on the Proxmox cluster |
| Cluster layout | 2 clusters: **nonprod** (dev + staging namespaces) and **prod** |
| Promotion model | merge→dev auto · release tag→staging auto · human approval→prod |
| Staging data | Sanitized production snapshot, refreshed on schedule |
| Data layer | In-cluster via operators (CloudNativePG, Valkey/Garage StatefulSets) |
| Secrets | External secrets manager (**Infisical**) + External Secrets Operator |
| VM layer | OpenTofu + cloud-init (Proxmox provider) |

---

## 1. Goals and non-goals

**Goals**

1. Three long-lived environments — Dev, Staging, Production — each independently
   reachable, independently configured, and rebuildable from git.
2. Fully automated flow: merged PR → running on Dev within minutes; released version →
   running on Staging with realistic data; tester sign-off → one approved action → Prod.
3. Production only ever runs an image tag that Staging ran first. Every promotion is a git
   commit (auditable, revertible).
4. Fork-friendly: another team stands this up on their own domain/servers by editing
   values files and pointing at their own secrets store. No `wallow.dev` hardcoded anywhere.
5. Keep the no-open-ports homelab posture (Pangolin + newt via VPS) while remaining
   deployable in "direct mode" (public 443 + Let's Encrypt) for forks without Pangolin.
6. Horizontal scaling of the API (expand/shrink with load) using platform primitives, not
   custom tooling. The Valkey SignalR backplane already makes the API multi-instance safe.

**Non-goals (now)**

- Microservice decomposition. The modular monolith deploys as one API image. The chart
  structure allows peeling a module into its own Deployment later, but nothing here
  requires it.
- Multi-region / geo-failover.
- Cluster autoscaling of *nodes* (adding VMs stays a one-command `tofu apply`).

---

## 2. Topology

```
                     Cloudflare DNS (wallow.dev)
     *.dev.wallow.dev    *.staging.wallow.dev    api|auth|app.wallow.dev
                               │  (A/AAAA → VPS)
                        ┌──────┴──────┐
                        │ VPS: Pangolin│   TLS termination + hostname routing
                        └──────┬──────┘
              WireGuard tunnels (newt, outbound-only from homelab)
      ┌────────────────────────┼──────────────────────────────┐
      │                 Proxmox cluster                        │
      │                                                        │
      │  ┌──────── k3s "nonprod" ────────┐   ┌── k3s "prod" ──┐│
      │  │ ns: wallow-dev                │   │ ns: wallow-prod ││
      │  │ ns: wallow-staging            │   │                 ││
      │  │ ns: argocd  (manages BOTH)    │   │ (no Argo here — ││
      │  │ ns: platform (ESO, CNPG,      │   │  registered as a ││
      │  │     metrics, LGTM, newt)      │   │  remote cluster) ││
      │  └───────────────────────────────┘   └─────────────────┘│
      │                                                        │
      │  Infisical (small VM/LXC, docker compose) — secrets    │
      │  OpenTofu state + cloud-init templates — VM provisioning│
      └────────────────────────────────────────────────────────┘
```

**Why two clusters, not one or three.** Prod is isolated from dev/staging experiments — a
botched CRD upgrade, a runaway dev workload, or a k3s version experiment in nonprod cannot
take prod down. Three clusters would triple control-plane overhead on homelab RAM for
little gain; staging still rehearses the *same chart, same operators, same manifests* that
prod runs, which is the honesty that matters.

**VM layout (initial sizing, adjustable in OpenTofu variables):**

| VM | Cluster | Role | Suggested size |
|---|---|---|---|
| `k3s-nonprod-1` | nonprod | server (control plane + worker) | 4 vCPU / 8–12 GB |
| `k3s-nonprod-2` | nonprod | agent (worker) — optional at start | 4 vCPU / 8 GB |
| `k3s-prod-1` | prod | server | 4 vCPU / 8 GB |
| `k3s-prod-2` | prod | agent — add when HA matters | 4 vCPU / 8 GB |
| `infisical` | — | secrets manager (compose) | 1 vCPU / 2 GB |

Single-server k3s per cluster is fine to start; the design does not change when servers
are added (k3s supports embedded-etcd HA by joining more servers).

**Why Infisical lives outside the clusters:** if a cluster is rebuilt from scratch, the
secrets needed to bootstrap it must not live inside it. A tiny compose-managed VM (backed
up like any other) breaks the circular dependency. Forks may substitute Vault, Doppler, or
cloud secret managers — the in-cluster interface is always the External Secrets Operator.

---

## 3. Repository layout — `deploy/` as the source of truth

The dangling `deploy/dockhand` reference in `docs/operations/deployment.md` is replaced by
a real `deploy/` tree. Everything an environment *is* lives in git; Argo CD makes the
clusters converge to it.

```
deploy/
├── chart/                        # Helm chart for the whole Wallow stack
│   ├── Chart.yaml
│   ├── values.yaml               # env-agnostic defaults
│   └── templates/
│       ├── api/                  # Deployment, Service, Ingress, HPA, PDB
│       ├── auth/                 # wallow-auth (TanStack Start BFF)
│       ├── web/                  # wallow-web (TanStack Start BFF)
│       ├── migrations-job.yaml   # Argo CD PreSync hook — EF Core bundles
│       ├── postgres-cluster.yaml # CloudNativePG Cluster (+ scheduled backup)
│       ├── valkey.yaml           # StatefulSet + Service
│       ├── garage.yaml           # StatefulSet + Service (S3)
│       ├── external-secrets.yaml # ExternalSecret resources per component
│       └── staging-refresh/      # sanitize Job + CronJob (staging only, flag-gated)
├── envs/
│   ├── dev/values.yaml           # tag: <sha, auto-bumped by CI>; hosts under dev.<baseDomain>
│   ├── staging/values.yaml       # tag: <X.Y.Z, bumped on release>; prod-shaped resources
│   └── prod/values.yaml          # tag: <X.Y.Z, bumped by promotion PR>; HPA on, replicas ≥2
├── argocd/
│   ├── projects.yaml             # AppProjects: wallow-dev / wallow-staging / wallow-prod
│   └── apps/                     # Application per env (app-of-apps root)
│       ├── dev.yaml              #   → deploy/chart + envs/dev/values.yaml, auto-sync
│       ├── staging.yaml          #   → deploy/chart + envs/staging/values.yaml, auto-sync
│       └── prod.yaml             #   → deploy/chart + envs/prod/values.yaml, auto-sync
│                                 #     (destination: prod cluster, registered remotely)
└── infra/
    ├── proxmox/                  # OpenTofu: VMs, cloud-init (k3s install), networks
    │   ├── main.tf  variables.tf clusters.tf
    │   └── cloud-init/           # templates: k3s server/agent, newt prereqs
    └── bootstrap/                # one-time per cluster: Argo CD install, ESO, CNPG,
                                  #   cluster registration, root app-of-apps
```

**Domain handling.** The chart exposes `global.baseDomain` plus per-component host
overrides. Environments derive hosts by convention:

| Env | API | Auth | Web |
|---|---|---|---|
| dev | `api.dev.<baseDomain>` | `auth.dev.<baseDomain>` | `app.dev.<baseDomain>` |
| staging | `api.staging.<baseDomain>` | `auth.staging.<baseDomain>` | `app.staging.<baseDomain>` |
| prod | `api.<baseDomain>` | `auth.<baseDomain>` | `app.<baseDomain>` |

`ServiceUrls__*`, `Cors__AllowedOrigins__*`, OIDC redirect URIs, and
`PreRegisteredClients__*` URLs are all templated from these hosts — a fork changes
`baseDomain: theirdomain.com` in three values files and every derived URL follows.
Additional custom domains (e.g. a fork serving two brands) are plain extra Ingress hosts
in values.

**Helm vs Kustomize:** Helm, because the stack needs real templating (derived URLs, count
of pre-registered clients, optional components like ClamAV/Grafana) and because
`envs/*/values.yaml` gives promotion a single obvious file to bump. Argo CD renders Helm
natively.

---

## 4. Cluster platform components

Installed once per cluster by `deploy/infra/bootstrap/` and thereafter managed by Argo CD
itself (app-of-apps):

| Component | Purpose | Notes |
|---|---|---|
| **k3s** | Kubernetes distribution | Bundled Traefik ingress + servicelb kept; flannel default |
| **Argo CD** | GitOps engine | Runs in nonprod only; prod registered as a remote cluster. UI at `argocd.dev.<baseDomain>` behind Pangolin |
| **External Secrets Operator** | Sync secrets from Infisical → k8s Secrets | One `ClusterSecretStore` per env pointing at the matching Infisical project (`wallow-dev` / `wallow-staging` / `wallow-prod`) |
| **CloudNativePG** | Postgres operator | Declarative clusters, streaming replicas (replaces the custom `postgres-replica` image), automated failover, `barmanObjectStore` backups to S3 |
| **metrics-server** | Resource metrics | Prerequisite for HPA |
| **Grafana otel-lgtm + Alloy** | Observability | One stack per cluster; apps point `OTEL_EXPORTER_OTLP_ENDPOINT` at the in-cluster Alloy (already env-driven) |
| **newt** | Pangolin tunnel | One Deployment per cluster; targets Traefik's ClusterIP |

**Migrations.** The existing `wallow-migrations` image runs as a Helm/Argo **PreSync hook
Job**: Argo will not roll the API/auth/web Deployments until the migration Job exits 0 —
the direct k8s translation of today's compose `depends_on` init container. Idempotent, so
re-syncs are safe.

**Secret inventory in Infisical (per env project):** Postgres app password,
`Identity__SigningKey`, Valkey password, Garage access/secret keys, SMTP credentials,
`AdminBootstrap__Password`, `OIDC_CLIENT_SECRET` + per-client secrets, external OAuth
provider secrets, Grafana admin. CI/CD never sees these — only ESO in-cluster does.
The one secret GitHub Actions holds is nothing: CI only pushes images and commits values
changes; it never talks to the clusters. (Argo pulls; nothing pushes into the clusters.)

**Backups (prod):** CNPG continuous WAL archiving + nightly base backups to an S3 bucket
**off the prod cluster** — the Garage instance on nonprod, or (recommended for real
protection) Cloudflare R2 / any external S3. Garage data volumes and Infisical get
Proxmox-level backup (PBS/vzdump). Staging's refresh job doubles as a monthly-or-better
restore drill.

---

## 5. Pipeline and promotion flow

The existing build half is kept as-is; the design adds the deploy half.

### What exists today (unchanged)

- `ci.yml` — PR gate: build, tests, lint.
- `deploy.yml` — on merge to main: builds multi-arch images, pushes `:nightly` + `:sha`
  to GHCR. (Rename candidate: `build-images.yml`, since Argo now owns "deploy".)
- `release-please` — Release PR; merging tags `vX.Y.Z`.
- `publish.yml` — on tag: promotes image tags to `:latest`, `:X.Y.Z`, `:X.Y`; Trivy scan.

### New flow

```
┌ PR merged to main ──────────────────────────────────────────────────────┐
│ deploy.yml pushes images :sha :nightly            (exists)              │
│ + new job: commit sha → deploy/envs/dev/values.yaml  [skip ci]          │
│ Argo CD auto-syncs wallow-dev (≤3 min)                                  │
│ + new workflow: dev-smoke.yml — waits for Argo health, runs Playwright  │
│   routes.spec (+ login.spec against seeded dev admin) against           │
│   https://app.dev.<domain>; failure → GitHub issue / notification       │
└──────────────────────────────────────────────────────────────────────────┘
┌ Release PR merged → tag vX.Y.Z ─────────────────────────────────────────┐
│ publish.yml promotes semver image tags            (exists)              │
│ + new job: commit X.Y.Z → deploy/envs/staging/values.yaml               │
│ Argo CD auto-syncs wallow-staging                                       │
│ Staging runs sanitized prod data → testing team validates               │
└──────────────────────────────────────────────────────────────────────────┘
┌ Testing sign-off ───────────────────────────────────────────────────────┐
│ promote-prod.yml (workflow_dispatch, input: version)                    │
│   1. verifies the version is what staging currently runs                │
│   2. opens PR bumping deploy/envs/prod/values.yaml                      │
│   3. PR gated by GitHub Environment "production" required reviewers     │
│ Approve + merge → Argo CD rolls prod (RollingUpdate, maxUnavailable: 0) │
│ PreSync migration Job runs first; PDBs keep ≥1 API pod serving          │
└──────────────────────────────────────────────────────────────────────────┘
```

**Guarantees**

- Prod can only run a semver tag, and `promote-prod.yml` refuses a version staging isn't
  currently running — "tested on staging" is enforced, not assumed.
- Every environment change is a commit touching one values file: `git log deploy/envs/prod`
  *is* the deployment history. **Rollback = revert the promotion commit** (images are
  immutable and stay in GHCR; CNPG PITR covers the rare migration-rollback case).
- Sign-off is a GitHub Environment approval — visible, assignable to the testing team,
  and recorded.

**Config-only changes** (values edits, replica counts, new env vars) follow the same path
with no new images: PR to `deploy/` → merge → Argo syncs. Argo CD's UI shows drift/diff
before and during sync.

### EF Core migration compatibility rule

Zero-downtime rolling updates mean old pods briefly run against the new schema. Adopt the
standard expand/contract discipline: migrations in release N must be backward-compatible
with app version N−1 (add columns nullable, don't drop/rename in the same release that
stops using them). Document in `api/CLAUDE.md`; enforce by review.

---

## 6. Edge & network — Pangolin now, portable always

**Homelab mode (yours):**

- Cloudflare DNS: `*.dev`, `*.staging`, and the apex/prod hosts → VPS IP.
- Pangolin terminates TLS on the VPS and routes by hostname to **newt sites**: nonprod
  hostnames → nonprod newt → nonprod Traefik; prod hostnames → prod newt → prod Traefik.
- Traefik routes by Host header to the right namespace's Services (the chart's Ingress
  resources). Nothing about namespaces/envs leaks to Pangolin — it only knows two tunnels.
- No inbound ports on the homelab. Grafana/Argo UIs exposed the same way, behind
  Pangolin auth (or IP allowlist).

**Direct mode (forks / other servers):** a values flag (`edge.mode: direct`) enables
cert-manager + Let's Encrypt ClusterIssuer annotations on the same Ingress resources.
Anyone can run this on a VPS with port 443 open and no Pangolin at all. The fork guide
documents both modes plus Cloudflare Tunnel as a third drop-in (also just "get traffic to
Traefik").

**Internal TLS:** Pangolin↔newt is WireGuard-encrypted; in-cluster traffic starts as
plain HTTP between Traefik and pods (standard for this size). mTLS/service mesh is
explicitly out of scope until there's a compliance reason.

---

## 7. Staging data — sanitized prod snapshots

A staging **refresh Job** (CronJob weekly + on-demand via `kubectl create job --from`),
flag-gated to the staging env only:

1. **Restore:** CNPG bootstraps a fresh `Cluster` from the latest prod barman backup
   (`bootstrap.recovery` from the shared S3 backup bucket). Staging gets a byte-real copy
   of prod's schema and data shape.
2. **Sanitize:** a Job runs SQL against the restored DB:
   - emails → `user-<stable-hash>@staging.invalid` (stable so relations survive refreshes)
   - password hashes → a single known staging test password
   - refresh tokens, sessions, API-key hashes, OIDC client secrets, webhook URLs → wiped
   - SMTP settings in DB (if any) neutralized; staging always points at Mailpit or a
     sandboxed SMTP so no real mail can ever leave staging
   - storage: Garage staging bucket gets prod object *metadata* references rewritten or a
     copied bucket, decided at implementation (files may be large; metadata-only is fine
     for most testing)
3. **Re-seed staging identities:** the API's existing idempotent startup sync
   (`AdminBootstrap__*`, `PreRegisteredClients__*` from staging's Infisical project)
   recreates staging's own admin and OIDC clients with staging redirect URIs.
4. **Swap:** the staging API is repointed at the refreshed cluster (blue/green DB swap via
   Service selector or connection-string secret update + rollout restart).

The sanitize SQL lives in `deploy/chart/templates/staging-refresh/` and is versioned with
the schema — a migration adding a PII column must extend the scrub script (review checklist
item).

---

## 8. Scaling story

| Horizon | Mechanism |
|---|---|
| Day 1 | Prod API `replicas: 2` behind its Service (in-cluster load balancing is free); PodDisruptionBudget keeps 1 serving during rollouts. SignalR already multi-instance via Valkey backplane. |
| Load-based | HPA on the API Deployment: CPU 70% target to start, min 2 / max N. Auth/web BFFs get the same treatment (they're stateless). |
| Read scaling | CNPG `instances: 2+` — streaming replicas with a read Service (`-ro`), replacing the hand-rolled replica image; app read/write split already exists in config. |
| More metal | `tofu apply` with `agent_count+1` → cloud-init joins the VM to the cluster; scheduler spreads pods. Works identically pointed at another Proxmox node, another homelab, or a cloud VPS running k3s agents. |
| Smarter signals | KEDA later, scaling on RPS or queue depth instead of CPU. |
| Someday | A hot module (e.g. Notifications) can ship as a second Deployment from the same image with a role flag/module filter — the monolith stays whole in code; the chart makes the split a values change. Full microservices remain a non-goal. |

---

## 9. Fork / new-domain checklist (what this design makes true)

Standing Wallow up on a new domain/server becomes:

1. Fork the repo (branding via `api/branding.json`, as today).
2. Provision: `cd deploy/infra/proxmox && tofu apply` (or any k3s anywhere in direct mode).
3. Stand up a secrets store (Infisical compose file provided) and fill the documented
   secret inventory per env.
4. Edit `deploy/envs/*/values.yaml`: `baseDomain`, registry path, edge mode.
5. Run `deploy/infra/bootstrap/` → Argo CD installs and converges everything.
6. Point DNS (or Pangolin resources) at the edge.

`docs/operations/deployment.md` is rewritten around this; the compose files in `docker/`
remain for local dev and the E2E stack only, and `docker-compose.production.yml` is
retired once prod runs on k3s.

---

## 10. Rollout roadmap (each phase independently useful)

| Phase | Deliverable | You gain |
|---|---|---|
| **1** | OpenTofu VMs + nonprod k3s + Argo CD + ESO/Infisical + Helm chart; **dev env live** with manual tag bumps | The platform, and a always-fresh dev URL |
| **2** | CI auto-bump to dev + `dev-smoke.yml` post-deploy Playwright gate | Merged PR → tested, running on dev, hands-free |
| **3** | Staging namespace + release-tag auto-bump + CNPG backups + sanitized refresh job | A truthful rehearsal env for the testing team |
| **4** | Prod cluster + `promote-prod.yml` + GitHub Environment approval; migrate prod off compose; retire `docker-compose.production.yml` | The full audited promotion pipeline |
| **5** | HPA + PDBs + per-cluster LGTM observability + alerting; docs rewrite | Expand/shrink under load; operational visibility |

Phases 1–2 are the learning-curve investment (k3s, Helm, Argo). Phases 3–5 are mostly
configuration on top of an already-working pattern.

## 11. Risks & mitigations

- **Kubernetes learning curve** — mitigated by phasing (dev-only first), Argo CD's visual
  UI, and the chart being the single thing to understand.
- **Homelab is a single failure domain for prod** — acknowledged; the design is portable
  by construction (k3s + direct mode), so prod can move to a VPS/cloud later with a
  `tofu`-provisioned k3s and the same git repo. Off-site DB backups (R2) protect data
  meanwhile.
- **Sanitize-script drift vs schema** — scrub script versioned next to the chart; PR
  checklist item when adding PII columns; staging refresh failure alerts.
- **Argo CD in nonprod manages prod** — nonprod outage stops *deployments*, never running
  prod workloads (Argo is not in the data path). Acceptable; revisit if it ever isn't.
