# Phase 16: Infrastructure & Configuration

**Scope:** `docker/`, `.github/`, `deploy/`, and root configuration files
**Status:** Not Started
**Files:** 93 config files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Root Configuration Files

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 1 | [ ] | `Foundry.sln` | Visual Studio solution file defining all projects, their GUIDs, and build configurations (Debug/Release). | All project references and solution folders |
| 2 | [ ] | `Directory.Build.props` | MSBuild props applied to all projects. Sets .NET 10, C# latest, nullable, implicit usings, TreatWarningsAsErrors, code analysis, deterministic builds, central package management, and InternalsVisibleTo rules. | `TargetFramework=net10.0`, `Version=0.2.0` (release-please managed), `EnforceCodeStyleInBuild=true` |
| 3 | [ ] | `Directory.Packages.props` | Central package version management (CPM). Defines all NuGet package versions in one place. Groups: ASP.NET Core, EF Core, health checks, messaging, identity, storage, observability, testing. | Version variables: `MicrosoftExtensionsVersion`, `EfCoreVersion`, `AspNetCoreVersion` |
| 4 | [ ] | `Directory.Build.targets` | MSBuild targets that add code analyzers (Microsoft.CodeAnalysis.NetAnalyzers, StyleCop, Meziantou, Roslynator) to all non-test projects as development-only dependencies. | Analyzer packages with `PrivateAssets=all` |
| 5 | [ ] | `global.json` | Pins .NET SDK version to `10.0.103` with `latestPatch` roll-forward policy. | SDK version pinning |
| 6 | [ ] | `stylecop.json` | StyleCop Analyzers configuration: XML header disabled, documentation rules (document interfaces + exposed elements, skip internals/privates), require newline at EOF, system usings first. | `documentInternalElements: false`, `topLevelTypes: 1` |
| 7 | [ ] | `.editorconfig` | EditorConfig defining code style: UTF-8, spaces, 4-space indent for C#, 2-space for XML project files, plus extensive .NET/C# coding conventions and analyzer severity overrides. | Indent styles, naming conventions, analyzer rules |
| 8 | [ ] | `qodana.yaml` | JetBrains Qodana static analysis config. Uses `cdnet` linter on `Foundry.sln` with `failThreshold: 0` (any issue fails). | Zero-tolerance static analysis |
| 9 | [ ] | `release-please-config.json` | Release-please configuration for automated semver releases. Simple release type, updates `Version` in `Directory.Build.props` via XPath, generates `CHANGELOG.md`. | XPath: `//Project/PropertyGroup/Version` |
| 10 | [ ] | `Dockerfile` | Multi-stage Docker build: restore (with layer caching via `--parents`), build, publish, final image based on `mcr.microsoft.com/dotnet/aspnet:10.0`. Includes health check. | Pinned base images with SHA digests, `HEALTHCHECK` on `/healthz` |
| 11 | [ ] | `.dockerignore` | Excludes IDE files, build artifacts (`bin/`, `obj/`), Docker files, secrets, and documentation from Docker build context. | Standard .NET Docker exclusions |

## Docker Infrastructure (`docker/`)

### Core Docker Compose Files

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 12 | [ ] | `docker/docker-compose.yml` | Base infrastructure services: PostgreSQL 18, RabbitMQ 4.2, Valkey 8, Keycloak 26, Mailpit. Defines networks, volumes, and health checks. | Service definitions, port mappings (5432, 5672, 15672, 6379, 8080, 8025), health checks |
| 13 | [ ] | `docker/docker-compose.dev.yml` | Development overlay adding Grafana, Alloy (telemetry collector), and Prometheus for local observability stack. | Grafana at :3000, dashboard provisioning, Alloy config |
| 14 | [ ] | `docker/docker-compose.prod.yml` | Production overlay with resource limits, restart policies, and production-tuned settings for all services. | CPU/memory limits, `restart: always`, production volumes |
| 15 | [ ] | `docker/docker-compose.staging.yml` | Staging overlay with moderate resource limits and staging-specific configuration. | Intermediate resource limits |
| 16 | [ ] | `docker/docker-compose.rabbitmq.yml` | RabbitMQ federation overlay for multi-region message replication. | Federation upstream/policy configuration |
| 17 | [ ] | `docker/docker-compose.multi-region.yml` | Multi-region infrastructure overlay defining region-aware service configurations. | Region-specific service settings |

### Environment & Scripts

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 18 | [ ] | `docker/.env` | Actual environment variables for local development (PostgreSQL, RabbitMQ, Keycloak credentials). Git-ignored in production. | Connection credentials |
| 19 | [ ] | `docker/.env.example` | Template environment file with placeholder values. Documents required variables: `POSTGRES_USER/PASSWORD`, `RABBITMQ_USER/PASSWORD`, `KEYCLOAK_ADMIN/PASSWORD`. | `CHANGE_ME` placeholders |
| 20 | [ ] | `docker/dev-up.sh` | Shell script to start the development Docker infrastructure. | `docker compose up -d` wrapper |
| 21 | [ ] | `docker/dev-down.sh` | Shell script to stop the development Docker infrastructure. | `docker compose down` wrapper |
| 22 | [ ] | `docker/dev-logs.sh` | Shell script to tail logs from development Docker services. | `docker compose logs -f` wrapper |

### Database Initialization

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 23 | [ ] | `docker/init-db.sql` | Creates separate PostgreSQL schemas for each module: `identity`, `billing`, `communications`, `storage`. Runs on first container initialization. | `CREATE SCHEMA IF NOT EXISTS` for each module |
| 24 | [ ] | `docker/init-keycloak-db.sql` | Creates a dedicated `keycloak_db` database and `keycloak_user` role within the shared PostgreSQL instance. Grants privileges on the public schema. | Separate DB for Keycloak with dedicated credentials |

### Keycloak

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 25 | [ ] | `docker/keycloak/realm-export.json` | Keycloak realm configuration export for the `foundry` realm. Defines clients, roles, identity providers, and authentication flows. | Realm settings, client configurations, role mappings |

### RabbitMQ

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 26 | [ ] | `docker/rabbitmq/rabbitmq.conf` | RabbitMQ server configuration file. | Queue, exchange, and connection settings |
| 27 | [ ] | `docker/rabbitmq/definitions-federation.json` | RabbitMQ definitions for federation (multi-region message replication). Defines upstreams and federation policies. | Federation upstream URIs and policies |

### Grafana Dashboards

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 28 | [ ] | `docker/grafana/dashboards/aspnetcore-otel.json` | Grafana dashboard for ASP.NET Core OpenTelemetry metrics (request rates, latencies, error rates). | PromQL queries for ASP.NET Core metrics |
| 29 | [ ] | `docker/grafana/dashboards/billing-dashboard.json` | Grafana dashboard for Billing module metrics (invoice counts, revenue, payment status). | Billing-specific PromQL queries |
| 30 | [ ] | `docker/grafana/dashboards/dotnet-runtime.json` | Grafana dashboard for .NET runtime metrics (GC, thread pool, memory). | Runtime instrumentation metrics |
| 31 | [ ] | `docker/grafana/dashboards/messaging-dashboard.json` | Grafana dashboard for Wolverine/RabbitMQ messaging metrics (publish rates, consume rates, queue depths). | Messaging pipeline metrics |
| 32 | [ ] | `docker/grafana/dashboards/module-overview.json` | Grafana dashboard providing a cross-module overview of all Foundry modules. | Aggregated module-level metrics |
| 33 | [ ] | `docker/grafana/dashboards/multi-region-overview.json` | Grafana dashboard for multi-region deployment monitoring. | Region-aware metrics and replication status |
| 34 | [ ] | `docker/grafana/dashboards/sales-dashboard.json` | Grafana dashboard for sales/revenue metrics. | Business-level PromQL queries |
| 35 | [ ] | `docker/grafana/dashboards/slo-monitoring.json` | Grafana dashboard for SLO (Service Level Objective) monitoring with error budgets. | SLI/SLO calculations and burn rate alerts |

### Grafana Provisioning

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 36 | [ ] | `docker/grafana/provisioning/dashboards/dashboards.yml` | Grafana provisioning config that auto-loads dashboard JSON files from the dashboards directory. | Dashboard provider path configuration |
| 37 | [ ] | `docker/grafana/provisioning/alerting/alerting.yml` | Grafana alerting rules for infrastructure health (DB, RabbitMQ, Redis connectivity). | Alert conditions and notification channels |
| 38 | [ ] | `docker/grafana/provisioning/alerting/slo-alerts.yml` | Grafana alerting rules for SLO violations and error budget consumption. | SLO-based alert thresholds |

### Alloy (Telemetry)

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 39 | [ ] | `docker/alloy/config.alloy` | Grafana Alloy (formerly Agent) configuration for collecting and forwarding OpenTelemetry data (traces, metrics, logs). | OTLP receiver, Prometheus remote write, Loki push |

### Documentation

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 40 | [ ] | `docker/PRODUCTION-NOTES.md` | Production deployment notes and recommendations for Docker-based deployments. | Security hardening, scaling guidance |

## GitHub CI/CD (`.github/`)

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 41 | [ ] | `.github/workflows/ci.yml` | Main CI pipeline: restore, build, test on push/PR to main. Uses .NET 10 SDK. | `dotnet restore`, `dotnet build`, `dotnet test` steps |
| 42 | [ ] | `.github/workflows/publish.yml` | Docker image build and push to GitHub Container Registry (GHCR). Triggered by version tags (`v*`) or manual dispatch. Tags with semver, latest, and git SHA. | `docker/build-push-action`, GHCR authentication |
| 43 | [ ] | `.github/workflows/release-please.yml` | Release-please automation: maintains a release PR with changelog and version bump on each push to main. Merging creates a GitHub Release and tag. | `contents: write`, `pull-requests: write` permissions |
| 44 | [ ] | `.github/workflows/security.yml` | CodeQL security analysis on push to main/expansion, PRs to main, and weekly schedule (Monday 6 AM UTC). | CodeQL initialization and analysis |
| 45 | [ ] | `.github/dependabot.yml` | Dependabot configuration: weekly updates for GitHub Actions and NuGet packages. Groups Microsoft packages, EF Core, and test packages. | Weekly schedule, grouped PRs |

## Deployment (`deploy/`)

### Deployment Scripts

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 46 | [ ] | `deploy/.env.example` | Template environment file for deployment targets. | Deployment-specific variable placeholders |
| 47 | [ ] | `deploy/bootstrap.sh` | Initial deployment bootstrap script for first-time infrastructure setup. | Server provisioning and initial configuration |
| 48 | [ ] | `deploy/deploy.sh` | Main deployment script for rolling out new versions. | Deployment orchestration |
| 49 | [ ] | `deploy/init-db.sql` | Database initialization SQL for deployment environments. | Schema creation for deployment targets |

### Deployment Docker Compose

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 50 | [ ] | `deploy/docker-compose.base.yml` | Base Docker Compose for deployments, defines the Foundry API service. | API service definition, network configuration |
| 51 | [ ] | `deploy/docker-compose.dev.yml` | Development deployment overlay. | Dev-specific overrides |
| 52 | [ ] | `deploy/docker-compose.prod.yml` | Production deployment overlay with resource limits and production config. | Production resource constraints |
| 53 | [ ] | `deploy/docker-compose.staging.yml` | Staging deployment overlay. | Staging-specific settings |

### DNS Configuration

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 54 | [ ] | `deploy/dns/cloudflare-config.template.yaml` | Cloudflare DNS configuration template for Foundry domains. | DNS record templates, proxy settings |
| 55 | [ ] | `deploy/dns/route53-config.template.yaml` | AWS Route53 DNS configuration template for Foundry domains. | Hosted zone and record set templates |

### Helm Chart (`deploy/helm/foundry/`)

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 56 | [ ] | `deploy/helm/foundry/Chart.yaml` | Helm chart metadata: name, version, description, dependencies. | Chart version, app version |
| 57 | [ ] | `deploy/helm/foundry/.helmignore` | Files to ignore when packaging the Helm chart. | Standard Helm ignores |
| 58 | [ ] | `deploy/helm/foundry/values.yaml` | Default Helm values: image, replicas, resources, ingress, service config. | Base configuration for all environments |
| 59 | [ ] | `deploy/helm/foundry/values-dev.yaml` | Development environment Helm value overrides. | Low resource limits, debug settings |
| 60 | [ ] | `deploy/helm/foundry/values-staging.yaml` | Staging environment Helm value overrides. | Moderate resources, staging URLs |
| 61 | [ ] | `deploy/helm/foundry/values-prod.yaml` | Production environment Helm value overrides. | High availability, production resources |
| 62 | [ ] | `deploy/helm/foundry/values-us-east.yaml` | US-East region-specific Helm value overrides. | Region endpoint, replica count |
| 63 | [ ] | `deploy/helm/foundry/values-eu-west.yaml` | EU-West region-specific Helm value overrides. | Region endpoint, replica count |

### Helm Templates

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 64 | [ ] | `deploy/helm/foundry/templates/_helpers.tpl` | Helm template helpers: labels, selectors, fullname generation. | Reusable template functions |
| 65 | [ ] | `deploy/helm/foundry/templates/NOTES.txt` | Post-install notes displayed after `helm install`. | Usage instructions |
| 66 | [ ] | `deploy/helm/foundry/templates/deployment.yaml` | Kubernetes Deployment manifest template for the Foundry API. | Container spec, probes, env vars, volumes |
| 67 | [ ] | `deploy/helm/foundry/templates/service.yaml` | Kubernetes Service manifest template. | ClusterIP service, port mappings |
| 68 | [ ] | `deploy/helm/foundry/templates/ingress.yaml` | Kubernetes Ingress manifest template with TLS support. | Host rules, TLS configuration, annotations |
| 69 | [ ] | `deploy/helm/foundry/templates/configmap.yaml` | Kubernetes ConfigMap template for application configuration. | `appsettings.json` overrides |
| 70 | [ ] | `deploy/helm/foundry/templates/secret.yaml` | Kubernetes Secret template for sensitive configuration (connection strings, API keys). | Base64-encoded secrets |
| 71 | [ ] | `deploy/helm/foundry/templates/serviceaccount.yaml` | Kubernetes ServiceAccount template. | RBAC service account |
| 72 | [ ] | `deploy/helm/foundry/templates/hpa.yaml` | Kubernetes HorizontalPodAutoscaler template for auto-scaling based on CPU/memory. | Min/max replicas, target utilization |
| 73 | [ ] | `deploy/helm/foundry/templates/pdb.yaml` | Kubernetes PodDisruptionBudget template for availability during maintenance. | `minAvailable` or `maxUnavailable` |
| 74 | [ ] | `deploy/helm/foundry/templates/pvc.yaml` | Kubernetes PersistentVolumeClaim template for storage. | Storage class, access modes, capacity |

### Kustomize (`deploy/kustomize/`)

| # | Status | File | Purpose | Key Config | Your Notes |
|---|--------|------|---------|------------|------------|
| 75 | [ ] | `deploy/kustomize/base/kustomization.yaml` | Kustomize base configuration listing all base resources. | Resource list |
| 76 | [ ] | `deploy/kustomize/base/deployment.yaml` | Base Kubernetes Deployment manifest. | Container spec, health probes |
| 77 | [ ] | `deploy/kustomize/base/service.yaml` | Base Kubernetes Service manifest. | Port mappings |
| 78 | [ ] | `deploy/kustomize/base/ingress.yaml` | Base Kubernetes Ingress manifest. | Host and path rules |
| 79 | [ ] | `deploy/kustomize/base/configmap.yaml` | Base Kubernetes ConfigMap. | Application configuration |
| 80 | [ ] | `deploy/kustomize/base/secret.yaml` | Base Kubernetes Secret. | Sensitive configuration |
| 81 | [ ] | `deploy/kustomize/overlays/dev/kustomization.yaml` | Dev overlay Kustomization referencing base and patches. | Patch list, namespace |
| 82 | [ ] | `deploy/kustomize/overlays/dev/patch-configmap.yaml` | Dev-specific ConfigMap patches. | Dev configuration overrides |
| 83 | [ ] | `deploy/kustomize/overlays/dev/patch-deployment.yaml` | Dev-specific Deployment patches (lower resources, debug settings). | Resource limits, replica count |
| 84 | [ ] | `deploy/kustomize/overlays/staging/kustomization.yaml` | Staging overlay Kustomization. | Staging patches |
| 85 | [ ] | `deploy/kustomize/overlays/staging/patch-deployment.yaml` | Staging-specific Deployment patches. | Moderate resource limits |
| 86 | [ ] | `deploy/kustomize/overlays/prod/kustomization.yaml` | Production overlay Kustomization. | Production patches |
| 87 | [ ] | `deploy/kustomize/overlays/prod/patch-deployment.yaml` | Production-specific Deployment patches (high replicas, strict resources). | Production resource limits |
| 88 | [ ] | `deploy/kustomize/overlays/us-east/kustomization.yaml` | US-East region overlay Kustomization. | Region-specific patches |
| 89 | [ ] | `deploy/kustomize/overlays/us-east/patch-deployment.yaml` | US-East region Deployment patches. | Region endpoint configuration |
| 90 | [ ] | `deploy/kustomize/overlays/us-east/region-config.yaml` | US-East region ConfigMap with region-specific settings. | Region endpoints, feature flags |
| 91 | [ ] | `deploy/kustomize/overlays/eu-west/kustomization.yaml` | EU-West region overlay Kustomization. | Region-specific patches |
| 92 | [ ] | `deploy/kustomize/overlays/eu-west/patch-deployment.yaml` | EU-West region Deployment patches. | Region endpoint configuration |
| 93 | [ ] | `deploy/kustomize/overlays/eu-west/region-config.yaml` | EU-West region ConfigMap with region-specific settings. | Region endpoints, data residency |
