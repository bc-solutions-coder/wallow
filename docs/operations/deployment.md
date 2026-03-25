# Wallow Deployment Guide

This guide covers deploying Wallow to a Hetzner server with automated CI/CD and connecting client applications to the API.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Prerequisites](#2-prerequisites)
3. [Server Setup](#3-server-setup)
4. [GitHub Configuration](#4-github-configuration)
5. [First Deployment](#5-first-deployment)
6. [Connecting Client Applications](#6-connecting-client-applications)
7. [Operations](#7-operations)
8. [Troubleshooting](#8-troubleshooting)
9. [Kubernetes Deployment](#9-kubernetes-deployment)

---

## 1. Overview

### Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  GitHub                                                         │
│  ┌─────────────┐    ┌─────────────────────────────────────────┐│
│  │ Push v* tag │───>│ Build Docker Image → Push to GHCR      ││
│  │             │    │ Create GitHub Release                  ││
│  └─────────────┘    └─────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Hetzner Server                                                 │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Docker Compose                                             ││
│  │  ┌───────────┐  ┌──────────┐  ┌──────────┐               ││
│  │  │ Wallow   │  │ Postgres │  │ GarageHQ │               ││
│  │  │ API       │  │          │  │ (S3)     │               ││
│  │  │ :8080     │  │ :5432    │  │ :3900    │               ││
│  │  └───────────┘  └──────────┘  └──────────┘               ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                  │
│  ┌───────────────────────────┴─────────────────────────────────┐│
│  │  Caddy/Nginx Reverse Proxy (HTTPS)                          ││
│  │  api.yourdomain.com → localhost:8080                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### CI/CD Triggers

| Trigger | Action | Docker Tags |
|---------|--------|-------------|
| PR to `main` | CI: build + test | *(none)* |
| Push version tag (`v*`) | Publish: Docker image + GitHub Release | `<version>`, `<major>.<minor>`, `latest`, `<sha>` |

---

## 2. Prerequisites

### On Your Local Machine

- Git with SSH key configured for GitHub
- SSH client

### On Hetzner

- Ubuntu 22.04+ or Debian 12+ server
- Root or sudo access
- Docker and Docker Compose installed
- Ports 80, 443 open (for HTTPS)

### GitHub Repository

- Access to repository settings (to configure secrets)
- GitHub Container Registry enabled (automatic for public repos)

---

## 3. Server Setup

### 3.1 Initial Server Configuration

SSH into your Hetzner server as root:

```bash
ssh root@YOUR_SERVER_IP
```

#### Install Docker

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh

# Start and enable Docker
systemctl enable docker
systemctl start docker
```

#### Create Deploy User

```bash
# Create user
useradd -m -s /bin/bash deploy
usermod -aG docker deploy

# Set up SSH directory
mkdir -p /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
touch /home/deploy/.ssh/authorized_keys
chmod 600 /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh
```

### 3.2 Generate Deploy SSH Key

On your **local machine**:

```bash
# Generate a dedicated deploy key
ssh-keygen -t ed25519 -C "github-wallow-deploy" -f ~/.ssh/wallow_deploy -N ""

# Display the public key (copy this)
cat ~/.ssh/wallow_deploy.pub

# Display the private key (you'll need this for GitHub secrets)
cat ~/.ssh/wallow_deploy
```

Add the **public key** to the server:

```bash
# On your local machine
ssh root@YOUR_SERVER_IP "echo 'YOUR_PUBLIC_KEY_CONTENT' >> /home/deploy/.ssh/authorized_keys"
```

Test the connection:

```bash
ssh -i ~/.ssh/wallow_deploy deploy@YOUR_SERVER_IP
```

### 3.3 Create Directory Structure

```bash
# As root on the server
mkdir -p /opt/wallow/{dev,staging,prod}
mkdir -p /opt/wallow/scripts
chown -R deploy:deploy /opt/wallow
```

### 3.4 Copy Deployment Files

From your local machine (in the repository root):

```bash
# Copy the deploy script
scp deploy/deploy.sh deploy@YOUR_SERVER_IP:/opt/wallow/scripts/

# Make it executable
ssh deploy@YOUR_SERVER_IP "chmod +x /opt/wallow/scripts/deploy.sh"

# Copy compose files and templates for each environment
for ENV in dev staging prod; do
  scp deploy/docker-compose.base.yml deploy@YOUR_SERVER_IP:/opt/wallow/$ENV/
  scp deploy/docker-compose.$ENV.yml deploy@YOUR_SERVER_IP:/opt/wallow/$ENV/
  scp deploy/init-db.sql deploy@YOUR_SERVER_IP:/opt/wallow/$ENV/
  scp deploy/.env.example deploy@YOUR_SERVER_IP:/opt/wallow/$ENV/.env
done
```

### 3.5 Configure Environment Variables

SSH as the deploy user and configure each environment:

```bash
ssh deploy@YOUR_SERVER_IP
```

Edit production config:

```bash
nano /opt/wallow/prod/.env
```

Required changes:

```ini
# Environment identification
COMPOSE_PROJECT_NAME=wallow-prod
ASPNETCORE_ENVIRONMENT=Production

# Docker image (update to your GitHub username/org)
APP_IMAGE=ghcr.io/YOUR_GITHUB_USER/wallow
APP_TAG=latest

# Database (use strong passwords!)
POSTGRES_USER=wallow
POSTGRES_PASSWORD=<generate-strong-password>
POSTGRES_DB=wallow

# GarageHQ (S3-compatible object storage)
GARAGE_KEY_NAME=wallow-prod
GARAGE_ACCESS_KEY=<generate-strong-key>
GARAGE_SECRET_KEY=<generate-strong-secret>
GARAGE_BUCKET=wallow-files
```

Generate strong passwords:

```bash
# Generate random passwords
openssl rand -base64 32
```

Repeat for dev and staging environments with appropriate values.

#### CORS Allowed Origins

The `Cors.AllowedOrigins` array is empty by default in Production and Staging settings. You **must** configure allowed origins via environment variables before deployment, otherwise cross-origin requests will be rejected:

```ini
Cors__AllowedOrigins__0=https://app.yourdomain.com
Cors__AllowedOrigins__1=https://admin.yourdomain.com
```

Add one variable per origin, incrementing the index.

### 3.6 Authenticate Docker with GHCR

The server needs to pull images from GitHub Container Registry:

```bash
# Create a GitHub Personal Access Token with read:packages scope
# Go to: GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)

# Login to GHCR
echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

### 3.7 Set Up Reverse Proxy (Caddy)

Install Caddy for automatic HTTPS:

```bash
# As root
apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt update
apt install caddy
```

Configure Caddy:

```bash
nano /etc/caddy/Caddyfile
```

```caddyfile
# Production API
api.yourdomain.com {
    reverse_proxy localhost:8080
}

# Staging API (port 8082 per deploy.sh)
api-staging.yourdomain.com {
    reverse_proxy localhost:8082
}
```

Restart Caddy:

```bash
systemctl restart caddy
```

---

## 4. GitHub Configuration

### 4.1 Create Environments

Go to your repository: **Settings → Environments**

Create three environments:
- `production`
- `staging` (optional)
- `development`

### 4.2 Add Secrets to Each Environment

For each environment, add these secrets:

| Secret Name | Value |
|-------------|-------|
| `DEPLOY_HOST` | Your Hetzner server IP or hostname |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Contents of `~/.ssh/wallow_deploy` (the **private** key) |

### 4.3 Verify Workflow Files

The repository includes these workflows in `.github/workflows/`:

- `ci.yml` - Runs on PRs to `main` branch (build + test)
- `publish.yml` - Builds Docker image and creates GitHub Release on version tag push (`v*`)

---

## 5. First Deployment

### 5.1 Start Infrastructure Manually (First Time)

SSH to the server and start infrastructure services:

```bash
ssh deploy@YOUR_SERVER_IP
cd /opt/wallow/prod

# Start infrastructure first
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d postgres garage

# Wait for them to be healthy
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml ps
```

### 5.2 Trigger Deployment

For production, create and push a version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

### 5.3 Monitor Deployment

Watch the GitHub Actions workflow:
- Go to **Actions** tab in your repository
- Click on the running workflow

On the server, watch logs:

```bash
ssh deploy@YOUR_SERVER_IP
docker logs -f wallow-prod-app
```

### 5.4 Verify Deployment

```bash
# Health check
curl https://api.yourdomain.com/health

# API info
curl https://api.yourdomain.com/
```

Expected response:

```json
{
  "name": "Wallow API",
  "version": "1.0.0",
  "environment": "Production",
  "documentation": "/scalar/v1",
  "health": "/health",
  "backgroundJobs": "/hangfire"
}
```

---

## 6. Connecting Client Applications

### 6.1 Authentication Flow

Wallow uses OpenIddict for authentication. Client apps authenticate via OpenID Connect:

```
┌─────────────┐     ┌─────────────┐
│  Client App │────>│ Wallow API │
│  (SPA/Mobile)│     │ (OpenIddict)│
└─────────────┘     └─────────────┘
      │                    │
      │  1. Request token  │
      │  (password/code)   │
      │                    │
      │  2. Receive JWT    │
      │<───────────────────│
      │                    │
      │  3. API requests   │
      │  with Bearer token │
      │───────────────────>│
```

### 6.2 Authentication Options

Wallow supports two authentication methods:

| Method | Use Case | Header |
|--------|----------|--------|
| **JWT Token** | Mobile/SPA apps, user sessions | `Authorization: Bearer <token>` |
| **API Key** | Backend services, M2M, integrations | `X-Api-Key: sk_live_...` |

### 6.3 Getting a Token (Simplified)

Use the token proxy endpoint:

```bash
curl -X POST https://api.yourdomain.com/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email": "user@example.com", "password": "password123"}'
```

Response:

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refresh_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 300
}
```

Refresh the token before it expires:

```bash
curl -X POST https://api.yourdomain.com/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "eyJhbGciOiJIUzI1..."}'
```

### 6.4 Using API Keys (For Backend Services)

API keys are ideal for service-to-service authentication. They don't expire (unless you set an expiry) and don't require token refresh.

**Create an API key** (requires authentication first):

```bash
curl -X POST https://api.yourdomain.com/api/auth/keys \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Production Backend", "scopes": ["billing:read"]}'
```

Response (save this key - it's only shown once!):

```json
{
  "keyId": "abc123",
  "apiKey": "sk_live_xK8j2mN9pL4qR7sT...",
  "prefix": "sk_live_xK8j2mN9",
  "name": "Production Backend"
}
```

**Use the API key:**

```bash
curl -H "X-Api-Key: sk_live_xK8j2mN9pL4qR7sT..." \
  https://api.yourdomain.com/api/billing/invoices
```

**List your API keys:**

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  https://api.yourdomain.com/api/auth/keys
```

**Revoke an API key:**

```bash
curl -X DELETE https://api.yourdomain.com/api/auth/keys/abc123 \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### 6.5 Making API Requests

With JWT token:

```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  https://api.yourdomain.com/api/billing/invoices
```

With API key:

```bash
curl -H "X-Api-Key: sk_live_..." \
  https://api.yourdomain.com/api/billing/invoices
```

### 6.6 Frontend SPA Integration

For browser-based apps, you can use the simple token proxy:

```typescript
// Simple approach - use the token proxy
async function login(email: string, password: string) {
  const response = await fetch('https://api.yourdomain.com/api/auth/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password })
  });

  const { access_token, refresh_token } = await response.json();
  localStorage.setItem('token', access_token);
  localStorage.setItem('refresh_token', refresh_token);
}

// Use token for API calls
async function fetchInvoices() {
  const token = localStorage.getItem('token');
  return fetch('https://api.yourdomain.com/api/billing/invoices', {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    }
  });
}

// Refresh token before expiry
async function refreshToken() {
  const refresh_token = localStorage.getItem('refresh_token');
  const response = await fetch('https://api.yourdomain.com/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: refresh_token })
  });

  const { access_token } = await response.json();
  localStorage.setItem('token', access_token);
}
```

### 6.7 Mobile App Integration

For mobile apps, use the simple token proxy:

```swift
// iOS example - Simple token request
func login(email: String, password: String) async throws -> TokenResponse {
    var request = URLRequest(url: URL(string: "https://api.yourdomain.com/api/auth/token")!)
    request.httpMethod = "POST"
    request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    request.httpBody = try JSONEncoder().encode(["email": email, "password": password])

    let (data, _) = try await URLSession.shared.data(for: request)
    return try JSONDecoder().decode(TokenResponse.self, from: data)
}
```

```kotlin
// Android example - Simple token request
suspend fun login(email: String, password: String): TokenResponse {
    val response = httpClient.post("https://api.yourdomain.com/api/auth/token") {
        contentType(ContentType.Application.Json)
        setBody(mapOf("email" to email, "password" to password))
    }
    return response.body()
}
```

### 6.8 API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /` | API info and version |
| `GET /health` | Full health check |
| `GET /health/ready` | Readiness probe |
| `GET /health/live` | Liveness probe |
| `GET /scalar/v1` | Interactive API documentation |
| `GET /hangfire` | Background jobs dashboard |

#### Module Endpoints

| Module | Base Path | Example Endpoints |
|--------|-----------|-------------------|
| Identity | `/api/identity` | `/users`, `/organizations`, `/roles` |
| Billing | `/api/billing` | `/invoices`, `/payments` |
| Communications | `/api/communications` | `/notifications`, `/announcements` |
| Storage | `/api/storage` | `/files` |

### 6.9 Real-time Updates (SignalR)

Connect to the SignalR hub for real-time notifications:

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://api.yourdomain.com/hubs/realtime', {
    accessTokenFactory: () => getAccessToken()
  })
  .withAutomaticReconnect()
  .build();

// Subscribe to notifications
connection.on('ReceiveNotification', (notification) => {
  console.log('New notification:', notification);
});

await connection.start();
```

### 6.10 Multi-Tenancy

Each user belongs to an organization (tenant). The API automatically scopes data to the user's organization based on the JWT claims.

For admin users who need to access other tenants:

```bash
curl -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "X-Tenant-Id: OTHER_TENANT_ID" \
  https://api.yourdomain.com/api/billing/invoices
```

---

## 7. Operations

### 7.1 Viewing Logs

```bash
# Application logs
docker logs wallow-prod-app -f --tail 100

# All services
cd /opt/wallow/prod
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml logs -f
```

### 7.2 Manual Deployment

```bash
ssh deploy@YOUR_SERVER_IP
bash /opt/wallow/scripts/deploy.sh prod v1.2.3
```

### 7.3 Rollback

The deploy script automatically rolls back on health check failure. For manual rollback:

```bash
ssh deploy@YOUR_SERVER_IP
cd /opt/wallow/prod

# Edit .env to set previous tag
nano .env  # Change APP_TAG=v1.2.2

# Redeploy
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml pull app
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d --no-deps app
```

### 7.4 Database Backups

```bash
# Backup
docker exec wallow-prod-postgres pg_dump -U wallow wallow > backup.sql

# Restore
docker exec -i wallow-prod-postgres psql -U wallow wallow < backup.sql
```

### 7.5 Scaling

For horizontal scaling, consider:

1. **Database**: Use managed PostgreSQL (Hetzner Cloud, AWS RDS)
2. **Load Balancer**: Add Hetzner Load Balancer in front of multiple app instances
3. **Object Storage**: Use managed S3 (AWS, Cloudflare R2) or self-hosted GarageHQ cluster

---

## 8. Troubleshooting

### Deployment Fails - SSH Connection

```
Error: ssh: connect to host ... port 22: Connection refused
```

**Fix**: Verify server IP is correct in GitHub secrets. Check firewall allows port 22.

### Deployment Fails - Docker Pull

```
Error: denied: installation not allowed to access repository
```

**Fix**: On the server, re-authenticate with GHCR:

```bash
echo "YOUR_GITHUB_PAT" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

### App Fails Health Check

```
Health check failed after 12 attempts. Rolling back...
```

**Fix**: Check application logs:

```bash
docker logs wallow-prod-app --tail 200
```

Common causes:
- Database connection string incorrect
- Missing environment variables

### Database Connection Issues

```bash
# Test database connectivity
docker exec wallow-prod-postgres pg_isready -U wallow

# Check connection string in .env
grep POSTGRES /opt/wallow/prod/.env
```

### GarageHQ Issues

```bash
# Check GarageHQ status
docker exec wallow-prod-garage garage status

# List buckets
docker exec wallow-prod-garage garage bucket list

# List keys
docker exec wallow-prod-garage garage key list
```

### Reset Everything

```bash
cd /opt/wallow/prod
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml down -v
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml up -d
```

**Warning**: This deletes all data!

---

## Quick Reference

### URLs (Production Example)

| Service | URL |
|---------|-----|
| API | https://api.yourdomain.com |
| API Docs | https://api.yourdomain.com/scalar/v1 |
| Health Check | https://api.yourdomain.com/health |

### Deploy Commands

```bash
# Deploy to production
git tag v1.0.0 && git push origin v1.0.0

# Manual deploy
ssh deploy@SERVER "bash /opt/wallow/scripts/deploy.sh prod v1.0.0"
```

### Server Commands

```bash
# View logs
docker logs wallow-prod-app -f

# Restart app
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml restart app

# Check status
docker compose -f docker-compose.base.yml -f docker-compose.prod.yml ps
```

---

## 9. Kubernetes Deployment

Wallow includes Helm charts and Kustomize overlays for deploying to Kubernetes clusters.

### 9.1 Prerequisites

- [Helm 3](https://helm.sh/docs/intro/install/) installed
- `kubectl` configured with cluster access
- A Kubernetes cluster (1.26+)
- External PostgreSQL, Valkey, and S3-compatible storage instances (or managed services)
- Container image pushed to GHCR (the CI/CD pipeline handles this)

### 9.2 Deploying with Helm

Helm charts are located in `deploy/helm/wallow/`. Each environment has a values override file.

```bash
# Development
helm install wallow ./deploy/helm/wallow -f deploy/helm/wallow/values-dev.yaml -n wallow-dev --create-namespace

# Staging
helm install wallow ./deploy/helm/wallow -f deploy/helm/wallow/values-staging.yaml -n wallow-staging --create-namespace

# Production
helm install wallow ./deploy/helm/wallow -f deploy/helm/wallow/values-prod.yaml -n wallow-prod --create-namespace

# Upgrade an existing release
helm upgrade wallow ./deploy/helm/wallow -f deploy/helm/wallow/values-prod.yaml -n wallow-prod
```

### 9.3 Deploying with Kustomize

Kustomize overlays are in `deploy/kustomize/overlays/`. Each overlay patches the base manifests for its environment.

```bash
# Development
kubectl apply -k deploy/kustomize/overlays/dev

# Staging
kubectl apply -k deploy/kustomize/overlays/staging

# Production
kubectl apply -k deploy/kustomize/overlays/prod
```

### 9.4 Configuring Infrastructure Connection Strings

External infrastructure (PostgreSQL, RabbitMQ, Valkey) is configured in the `secrets` section of the values file. For staging/production, edit the appropriate values file:

```yaml
# In values-staging.yaml or values-prod.yaml
secrets:
  ConnectionStrings__DefaultConnection: "Host=db.example.com;Port=5432;Database=wallow;Username=wallow;Password=STRONG_PASSWORD"
  ConnectionStrings__Redis: "cache.example.com:6379"
  Storage__S3__Endpoint: "http://garage.example.com:3900"
  Storage__S3__AccessKey: "STRONG_ACCESS_KEY"
  Storage__S3__SecretKey: "STRONG_SECRET_KEY"
```

For Kustomize deployments, update the `secret.yaml` in the base or overlay directory.

> **Tip**: For production, use Kubernetes `ExternalSecret` or a secrets manager (Vault, AWS Secrets Manager) instead of plain values files.

### 9.5 Enabling TLS via Ingress

TLS is controlled by the ingress settings. Enable it in your values file:

```yaml
ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
  host: api.yourdomain.com
  tls:
    enabled: true
    secretName: wallow-tls
```

This requires [cert-manager](https://cert-manager.io/) installed in the cluster for automatic certificate provisioning. Alternatively, provide a pre-existing TLS secret.

### 9.6 Scaling

The Helm chart includes a Horizontal Pod Autoscaler (HPA) that scales based on CPU utilization. Enable it in your values file:

```yaml
autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 10
  targetCPUUtilizationPercentage: 80
```

When `autoscaling.enabled` is `true`, the `replicaCount` field is ignored. A Pod Disruption Budget (PDB) is also available to ensure availability during node maintenance:

```yaml
podDisruptionBudget:
  enabled: true
  minAvailable: 1
```

### 9.7 Health Check Endpoints

Kubernetes probes are pre-configured in the Helm chart and Kustomize base:

| Probe | Endpoint | Purpose |
|-------|----------|---------|
| Startup | `GET /health` | Allows slow startup (30 retries x 5s) before liveness kicks in |
| Readiness | `GET /health/ready` | Removes pod from service if not ready to serve traffic |
| Liveness | `GET /health/live` | Restarts pod if it becomes unresponsive |

You can also hit these endpoints manually for debugging:

```bash
kubectl exec -it deploy/wallow -n wallow-prod -- curl -s localhost:8080/health
```
