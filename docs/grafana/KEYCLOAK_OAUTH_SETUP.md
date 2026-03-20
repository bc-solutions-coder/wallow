# Grafana Keycloak OAuth Setup

This guide covers configuring Grafana to use Keycloak as an OAuth provider for single sign-on (SSO), including embedded dashboard support for multi-tenant product UIs.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Keycloak Client Configuration](#2-keycloak-client-configuration)
3. [Grafana Configuration](#3-grafana-configuration)
4. [Environment Variables](#4-environment-variables)
5. [Role Mapping](#5-role-mapping)
6. [Embedding Dashboards](#6-embedding-dashboards)
7. [Multi-Tenancy Configuration](#7-multi-tenancy-configuration)
8. [Docker Compose Setup](#8-docker-compose-setup)
9. [Kubernetes Setup](#9-kubernetes-setup)
10. [Troubleshooting](#10-troubleshooting)
11. [Security Best Practices](#11-security-best-practices)

---

## 1. Prerequisites

Before configuring Grafana with Keycloak OAuth, ensure you have:

- **Grafana 10.x or later** (earlier versions have limited OAuth support)
- **Keycloak 26.x or later** with the `wallow` realm configured
- **Network connectivity** between Grafana and Keycloak
- **DNS/hostname** configured for both services (for production)

### Wallow Infrastructure

If you're using Wallow's Docker Compose setup, Keycloak is already configured:

| Service | Local URL | Description |
|---------|-----------|-------------|
| Keycloak | http://localhost:8080 | Identity provider |
| Grafana | http://localhost:3000 | Dashboards and observability |
| Realm | wallow | Pre-configured realm with Organizations enabled |

---

## 2. Keycloak Client Configuration

### Step 1: Access Keycloak Admin Console

1. Navigate to http://localhost:8080 (or your Keycloak URL)
2. Login with admin credentials (default: `admin` / `admin` for local development)
3. Select the `wallow` realm from the dropdown

### Step 2: Create the Grafana Client

1. Navigate to **Clients** in the left sidebar
2. Click **Create client**
3. Configure the client:

| Field | Value |
|-------|-------|
| Client type | OpenID Connect |
| Client ID | `grafana` |
| Name | Grafana Dashboard |
| Description | OAuth client for Grafana SSO |

4. Click **Next**

### Step 3: Configure Client Authentication

1. Enable **Client authentication** (makes this a confidential client)
2. Enable **Standard flow** (Authorization Code flow)
3. Disable **Direct access grants** (not needed for OAuth)
4. Click **Next**

### Step 4: Configure Redirect URIs

Add the following redirect URIs:

```
# Local development
http://localhost:3000/login/generic_oauth

# Production (replace with your domain)
https://grafana.yourplatform.com/login/generic_oauth
```

Configure Web Origins for CORS:

```
# Local development
http://localhost:3000

# Production
https://grafana.yourplatform.com
```

5. Click **Save**

### Step 5: Get Client Secret

1. Navigate to the **Credentials** tab
2. Copy the **Client secret** value
3. Store this securely - you'll need it for Grafana configuration

### Step 6: Configure Client Scopes

1. Navigate to the **Client scopes** tab
2. Verify these scopes are assigned:
   - `openid` (Default)
   - `profile` (Default)
   - `email` (Default)

### Step 7: Add Role Mapper (Required for RBAC)

Create a mapper to include realm roles in the token:

1. Navigate to **Client scopes** > **grafana-dedicated** (auto-created scope)
2. Click **Add mapper** > **By configuration**
3. Select **User Realm Role**
4. Configure:

| Field | Value |
|-------|-------|
| Name | realm-roles |
| Mapper Type | User Realm Role |
| Token Claim Name | realm_access.roles |
| Claim JSON Type | JSON |
| Add to ID token | ON |
| Add to access token | ON |
| Add to userinfo | ON |

5. Click **Save**

### Step 8: (Optional) Add Organization Mapper for Multi-Tenancy

If using Keycloak Organizations for multi-tenancy:

1. Navigate to **Client scopes** > **grafana-dedicated**
2. Click **Add mapper** > **By configuration**
3. Select **User Attribute**
4. Configure:

| Field | Value |
|-------|-------|
| Name | organization |
| User Attribute | organization |
| Token Claim Name | organization |
| Add to ID token | ON |
| Add to access token | ON |

---

## 3. Grafana Configuration

### grafana.ini - Complete Configuration

Add the following to your `grafana.ini` file:

```ini
#################################### Server ####################################
[server]
# The full public-facing URL where Grafana will be accessed
root_url = %(protocol)s://%(domain)s:%(http_port)s/

#################################### Generic OAuth #############################
[auth.generic_oauth]
enabled = true
name = Keycloak
allow_sign_up = true
auto_login = false

# Client credentials (use environment variables in production)
client_id = grafana
client_secret = ${GRAFANA_OAUTH_SECRET}

# OAuth scopes to request
scopes = openid profile email

# Keycloak OIDC endpoints
# Replace auth.yourplatform.com with your Keycloak hostname
auth_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/auth
token_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/token
api_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/userinfo

# Role mapping using JMESPath expression
# Maps Keycloak realm roles to Grafana roles
role_attribute_path = contains(realm_access.roles[*], 'admin') && 'Admin' || contains(realm_access.roles[*], 'manager') && 'Editor' || 'Viewer'

# Strict role mapping - users without mapped roles cannot login
role_attribute_strict = false

# Allow users to change their email (synced from Keycloak)
allow_assign_grafana_admin = true

# User attribute mappings
email_attribute_name = email
login_attribute_path = preferred_username
name_attribute_path = name

# Token settings
use_pkce = true
use_refresh_token = true

#################################### JWT Authentication ########################
# For embedding dashboards in your product UI
[auth.jwt]
enabled = true
header_name = X-JWT-Assertion
email_claim = email
username_claim = preferred_username
jwk_set_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/certs

# Role mapping from JWT claims
role_attribute_path = contains(realm_access.roles[*], 'admin') && 'Admin' || contains(realm_access.roles[*], 'manager') && 'Editor' || 'Viewer'

# Cache JWK keys for performance
jwk_set_cache_ttl = 60m

#################################### Security ##################################
[security]
# Required for embedding dashboards in iframes
allow_embedding = true

# Cookie settings for cross-origin embedding
cookie_samesite = none
cookie_secure = true

# Disable Angular deprecation UI warnings
angular_support_enabled = false

#################################### Users #####################################
[users]
# Allow users to sign up via OAuth
allow_sign_up = true

# Auto-assign new users to an organization
auto_assign_org = true
auto_assign_org_id = 1
auto_assign_org_role = Viewer

#################################### Anonymous Auth ############################
[auth.anonymous]
# Disable anonymous access for security
enabled = false

#################################### Basic Auth ################################
[auth.basic]
# Keep basic auth enabled for API access and initial setup
enabled = true
```

### Local Development Configuration

For local development with Keycloak at `localhost:8080`:

```ini
[auth.generic_oauth]
enabled = true
name = Keycloak
allow_sign_up = true
client_id = grafana
client_secret = ${GRAFANA_OAUTH_SECRET}
scopes = openid profile email
auth_url = http://localhost:8080/realms/wallow/protocol/openid-connect/auth
token_url = http://localhost:8080/realms/wallow/protocol/openid-connect/token
api_url = http://localhost:8080/realms/wallow/protocol/openid-connect/userinfo
role_attribute_path = contains(realm_access.roles[*], 'admin') && 'Admin' || 'Viewer'

[auth.jwt]
enabled = true
header_name = X-JWT-Assertion
email_claim = email
username_claim = preferred_username
jwk_set_url = http://localhost:8080/realms/wallow/protocol/openid-connect/certs

[security]
allow_embedding = true
```

---

## 4. Environment Variables

Configure Grafana using environment variables for portability:

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `GRAFANA_OAUTH_SECRET` | Client secret from Keycloak | `AbCdEf123...` |
| `GF_SERVER_ROOT_URL` | Public URL of Grafana | `https://grafana.yourplatform.com` |

### Optional Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `GF_AUTH_GENERIC_OAUTH_ENABLED` | Enable OAuth | `true` |
| `GF_AUTH_GENERIC_OAUTH_NAME` | OAuth provider name | `Keycloak` |
| `GF_AUTH_GENERIC_OAUTH_CLIENT_ID` | Keycloak client ID | `grafana` |
| `GF_AUTH_GENERIC_OAUTH_SCOPES` | OAuth scopes | `openid profile email` |
| `GF_AUTH_GENERIC_OAUTH_AUTH_URL` | Authorization endpoint | (see config) |
| `GF_AUTH_GENERIC_OAUTH_TOKEN_URL` | Token endpoint | (see config) |
| `GF_AUTH_GENERIC_OAUTH_API_URL` | Userinfo endpoint | (see config) |
| `GF_SECURITY_ALLOW_EMBEDDING` | Allow iframe embedding | `true` |
| `GF_AUTH_JWT_ENABLED` | Enable JWT auth | `true` |
| `GF_AUTH_JWT_JWK_SET_URL` | JWKS endpoint | (see config) |

### Example .env File

```bash
# Grafana OAuth Configuration
GRAFANA_OAUTH_SECRET=your-client-secret-from-keycloak

# Grafana Server
GF_SERVER_ROOT_URL=https://grafana.yourplatform.com

# Keycloak URLs (adjust for your environment)
KEYCLOAK_URL=https://auth.yourplatform.com
KEYCLOAK_REALM=wallow

# Derived OAuth URLs (used in grafana.ini or as env vars)
GF_AUTH_GENERIC_OAUTH_AUTH_URL=${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/auth
GF_AUTH_GENERIC_OAUTH_TOKEN_URL=${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token
GF_AUTH_GENERIC_OAUTH_API_URL=${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/userinfo
GF_AUTH_JWT_JWK_SET_URL=${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/certs

# Security settings for embedding
GF_SECURITY_ALLOW_EMBEDDING=true
GF_SECURITY_COOKIE_SAMESITE=none
GF_SECURITY_COOKIE_SECURE=true
```

---

## 5. Role Mapping

### Keycloak to Grafana Role Mapping

Grafana uses JMESPath expressions to map Keycloak roles to Grafana roles:

| Keycloak Role | Grafana Role | Permissions |
|---------------|--------------|-------------|
| `admin` | Admin | Full access, manage users, datasources, dashboards |
| `manager` | Editor | Create/edit dashboards, explore data |
| (default) | Viewer | View dashboards only |

### JMESPath Expression Explained

```
role_attribute_path = contains(realm_access.roles[*], 'admin') && 'Admin' || contains(realm_access.roles[*], 'manager') && 'Editor' || 'Viewer'
```

This expression:
1. Checks if user has `admin` realm role -> assigns `Admin`
2. Otherwise, checks if user has `manager` realm role -> assigns `Editor`
3. Otherwise, assigns `Viewer` (default)

### Custom Role Mapping Examples

**Map organization-specific roles:**

```
role_attribute_path = contains(groups[*], '/grafana-admins') && 'Admin' || contains(groups[*], '/grafana-editors') && 'Editor' || 'Viewer'
```

**Use client roles instead of realm roles:**

```
role_attribute_path = contains(resource_access.grafana.roles[*], 'admin') && 'Admin' || 'Viewer'
```

**Strict role requirement (users must have explicit role):**

```ini
role_attribute_path = contains(realm_access.roles[*], 'grafana-admin') && 'Admin' || contains(realm_access.roles[*], 'grafana-editor') && 'Editor' || contains(realm_access.roles[*], 'grafana-viewer') && 'Viewer'
role_attribute_strict = true
```

### Assigning Roles in Keycloak

1. Navigate to **Users** in Keycloak
2. Select the user
3. Go to **Role mapping** tab
4. Click **Assign role**
5. Select the appropriate realm role (`admin`, `manager`, or `user`)

---

## 6. Embedding Dashboards

### Embedding in Your Product UI

Grafana dashboards can be embedded in your product UI using iframes with JWT authentication:

```html
<iframe
  src="https://grafana.yourplatform.com/d/usage-dashboard?var-tenant_id=${currentTenant}&kiosk=tv"
  width="100%"
  height="600"
  frameborder="0"
></iframe>
```

### URL Parameters for Embedding

| Parameter | Description | Example |
|-----------|-------------|---------|
| `kiosk` | Kiosk mode (hides menu) | `tv` (hides all), `1` (hides side menu) |
| `var-<name>` | Dashboard variable | `var-tenant_id=abc123` |
| `from` | Time range start | `now-7d` |
| `to` | Time range end | `now` |
| `refresh` | Auto-refresh interval | `5s`, `1m`, `5m` |
| `theme` | Dashboard theme | `light`, `dark` |

### Passing JWT Token for Authentication

When embedding, pass the user's JWT token via the `X-JWT-Assertion` header:

**Backend Proxy Approach (Recommended):**

```typescript
// Your backend proxies requests to Grafana with JWT
app.get('/api/grafana/*', async (req, res) => {
  const userJwt = req.headers.authorization?.replace('Bearer ', '');

  const response = await fetch(`https://grafana.yourplatform.com${req.path}`, {
    headers: {
      'X-JWT-Assertion': userJwt,
      ...req.headers
    }
  });

  res.status(response.status).send(await response.text());
});
```

**Frontend with Server-Side Rendering:**

```typescript
// Pass JWT in iframe src via signed URL
function getGrafanaEmbedUrl(dashboardId: string, tenantId: string) {
  const token = getAccessToken();
  // Use a signed URL or session cookie approach
  return `/api/grafana/d/${dashboardId}?var-tenant_id=${tenantId}&kiosk=tv`;
}
```

### Content Security Policy (CSP)

Update your product's CSP to allow Grafana embedding:

```
Content-Security-Policy: frame-src 'self' https://grafana.yourplatform.com;
```

---

## 7. Multi-Tenancy Configuration

### Dashboard Variable: tenant_id

All multi-tenant dashboards should use a `$tenant_id` variable for data isolation.

**Creating the Variable:**

1. Open the dashboard in Grafana
2. Click **Dashboard settings** (gear icon)
3. Navigate to **Variables**
4. Click **Add variable**
5. Configure:

| Field | Value |
|-------|-------|
| Name | tenant_id |
| Type | Query |
| Data source | PostgreSQL |
| Query | `SELECT DISTINCT tenant_id FROM billing.invoices WHERE $__timeFilter(created_at)` |
| Refresh | On time range change |

**Using the Variable in Queries:**

```sql
-- PostgreSQL query with tenant filtering
SELECT status, COUNT(*) as count, SUM(total_amount) as total
FROM billing.invoices
WHERE tenant_id = '$tenant_id'
  AND created_at BETWEEN $__timeFrom() AND $__timeTo()
GROUP BY status
```

```promql
# Prometheus query with tenant filtering
sum(rate(http_server_request_duration_seconds_count{tenant_id="$tenant_id"}[5m])) by (endpoint)
```

### Injecting Tenant from JWT

When using JWT authentication, extract `tenant_id` from the token:

**Grafana Configuration:**

```ini
[auth.jwt]
# ... other settings ...

# Map organization claim to Grafana variable
# Note: This requires custom variable provisioning
```

**Embedding with Tenant:**

```typescript
// Extract tenant from JWT and pass to dashboard
function embedDashboard(dashboardUid: string) {
  const token = getAccessToken();
  const decoded = jwtDecode(token);
  const tenantId = decoded.organization; // From Keycloak org claim

  const url = new URL(`/d/${dashboardUid}`, GRAFANA_URL);
  url.searchParams.set('var-tenant_id', tenantId);
  url.searchParams.set('kiosk', 'tv');

  return url.toString();
}
```

### Tenant-Scoped Data Sources

For complete isolation, configure tenant-specific data sources:

1. Use Grafana's data source provisioning with tenant-specific connection strings
2. Implement row-level security in PostgreSQL
3. Use Prometheus multi-tenancy features (cortex, mimir)

---

## 8. Docker Compose Setup

### Development Configuration

Add Grafana with Keycloak OAuth to your `docker-compose.yml`:

```yaml
services:
  grafana:
    image: grafana/grafana:11.4.0
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-grafana
    environment:
      # Admin password (change in production)
      GF_SECURITY_ADMIN_PASSWORD: admin

      # Server configuration
      GF_SERVER_ROOT_URL: http://localhost:3000

      # OAuth configuration
      GF_AUTH_GENERIC_OAUTH_ENABLED: "true"
      GF_AUTH_GENERIC_OAUTH_NAME: Keycloak
      GF_AUTH_GENERIC_OAUTH_ALLOW_SIGN_UP: "true"
      GF_AUTH_GENERIC_OAUTH_CLIENT_ID: grafana
      GF_AUTH_GENERIC_OAUTH_CLIENT_SECRET: ${GRAFANA_OAUTH_SECRET}
      GF_AUTH_GENERIC_OAUTH_SCOPES: openid profile email
      GF_AUTH_GENERIC_OAUTH_AUTH_URL: http://keycloak:8080/realms/wallow/protocol/openid-connect/auth
      GF_AUTH_GENERIC_OAUTH_TOKEN_URL: http://keycloak:8080/realms/wallow/protocol/openid-connect/token
      GF_AUTH_GENERIC_OAUTH_API_URL: http://keycloak:8080/realms/wallow/protocol/openid-connect/userinfo
      GF_AUTH_GENERIC_OAUTH_ROLE_ATTRIBUTE_PATH: "contains(realm_access.roles[*], 'admin') && 'Admin' || 'Viewer'"

      # JWT configuration for embedding
      GF_AUTH_JWT_ENABLED: "true"
      GF_AUTH_JWT_HEADER_NAME: X-JWT-Assertion
      GF_AUTH_JWT_EMAIL_CLAIM: email
      GF_AUTH_JWT_USERNAME_CLAIM: preferred_username
      GF_AUTH_JWT_JWK_SET_URL: http://keycloak:8080/realms/wallow/protocol/openid-connect/certs

      # Embedding configuration
      GF_SECURITY_ALLOW_EMBEDDING: "true"
      GF_SECURITY_COOKIE_SAMESITE: none
      GF_SECURITY_COOKIE_SECURE: "false"  # Set to true in production with HTTPS

    volumes:
      - grafana_data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ./grafana/dashboards:/var/lib/grafana/dashboards:ro
    ports:
      - "3000:3000"
    depends_on:
      keycloak:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:3000/api/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

volumes:
  grafana_data:
```

### Production Configuration

For production, update URLs and security settings:

```yaml
services:
  grafana:
    image: grafana/grafana:11.4.0
    environment:
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD}
      GF_SERVER_ROOT_URL: https://grafana.yourplatform.com

      # Production OAuth URLs
      GF_AUTH_GENERIC_OAUTH_AUTH_URL: https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/auth
      GF_AUTH_GENERIC_OAUTH_TOKEN_URL: https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/token
      GF_AUTH_GENERIC_OAUTH_API_URL: https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/userinfo
      GF_AUTH_JWT_JWK_SET_URL: https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/certs

      # Production security
      GF_SECURITY_COOKIE_SECURE: "true"
      GF_SERVER_PROTOCOL: http  # Terminated by reverse proxy

      # Disable sign-up after initial setup
      GF_USERS_ALLOW_SIGN_UP: "false"
```

---

## 9. Kubernetes Setup

### ConfigMap for Grafana

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-config
  namespace: wallow
data:
  grafana.ini: |
    [server]
    root_url = https://grafana.yourplatform.com

    [auth.generic_oauth]
    enabled = true
    name = Keycloak
    allow_sign_up = true
    client_id = grafana
    scopes = openid profile email
    auth_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/auth
    token_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/token
    api_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/userinfo
    role_attribute_path = contains(realm_access.roles[*], 'admin') && 'Admin' || 'Viewer'

    [auth.jwt]
    enabled = true
    header_name = X-JWT-Assertion
    email_claim = email
    username_claim = preferred_username
    jwk_set_url = https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/certs

    [security]
    allow_embedding = true
    cookie_samesite = none
    cookie_secure = true
```

### Secret for OAuth Credentials

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: grafana-secrets
  namespace: wallow
type: Opaque
stringData:
  admin-password: "your-admin-password"
  oauth-client-secret: "your-keycloak-client-secret"
```

### Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: grafana
  namespace: wallow
spec:
  replicas: 1
  selector:
    matchLabels:
      app: grafana
  template:
    metadata:
      labels:
        app: grafana
    spec:
      containers:
        - name: grafana
          image: grafana/grafana:11.4.0
          ports:
            - containerPort: 3000
          env:
            - name: GF_SECURITY_ADMIN_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: grafana-secrets
                  key: admin-password
            - name: GF_AUTH_GENERIC_OAUTH_CLIENT_SECRET
              valueFrom:
                secretKeyRef:
                  name: grafana-secrets
                  key: oauth-client-secret
          volumeMounts:
            - name: config
              mountPath: /etc/grafana/grafana.ini
              subPath: grafana.ini
            - name: data
              mountPath: /var/lib/grafana
          livenessProbe:
            httpGet:
              path: /api/health
              port: 3000
            initialDelaySeconds: 60
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /api/health
              port: 3000
            initialDelaySeconds: 5
            periodSeconds: 5
      volumes:
        - name: config
          configMap:
            name: grafana-config
        - name: data
          persistentVolumeClaim:
            claimName: grafana-pvc
```

### Service and Ingress

```yaml
apiVersion: v1
kind: Service
metadata:
  name: grafana
  namespace: wallow
spec:
  selector:
    app: grafana
  ports:
    - port: 80
      targetPort: 3000
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: grafana
  namespace: wallow
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - grafana.yourplatform.com
      secretName: grafana-tls
  rules:
    - host: grafana.yourplatform.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: grafana
                port:
                  number: 80
```

---

## 10. Troubleshooting

### Common Issues and Solutions

#### OAuth Login Redirects to Error Page

**Symptom:** Clicking "Sign in with Keycloak" redirects to an error page.

**Solutions:**

1. **Check Redirect URI:** Ensure `http://localhost:3000/login/generic_oauth` is in Keycloak's "Valid Redirect URIs"

2. **Verify Client Secret:** Confirm the secret matches in both Keycloak and Grafana configuration

3. **Check Keycloak Logs:**
   ```bash
   docker logs wallow-keycloak 2>&1 | grep -i error
   ```

4. **Test OAuth Endpoints:**
   ```bash
   # Test token endpoint directly
   curl -X POST http://localhost:8080/realms/wallow/protocol/openid-connect/token \
     -d "client_id=grafana" \
     -d "client_secret=YOUR_SECRET" \
     -d "grant_type=client_credentials"
   ```

#### "User not found" After Successful OAuth

**Symptom:** OAuth succeeds but user cannot access Grafana.

**Solutions:**

1. **Enable allow_sign_up:**
   ```ini
   [auth.generic_oauth]
   allow_sign_up = true
   ```

2. **Check role_attribute_strict:**
   ```ini
   role_attribute_strict = false  # Allow users without explicit roles
   ```

#### JWT Authentication Not Working

**Symptom:** Embedded dashboards show login page instead of content.

**Solutions:**

1. **Verify JWK Set URL is accessible from Grafana:**
   ```bash
   docker exec wallow-grafana curl -s http://keycloak:8080/realms/wallow/protocol/openid-connect/certs
   ```

2. **Check JWT header is being passed:**
   ```bash
   curl -H "X-JWT-Assertion: YOUR_JWT_TOKEN" http://localhost:3000/api/dashboards/home
   ```

3. **Verify JWT claims match configuration:**
   ```bash
   # Decode JWT and check claims
   echo "YOUR_JWT_TOKEN" | cut -d. -f2 | base64 -d | jq .
   ```

#### Embedded Dashboards Show "Refused to Connect"

**Symptom:** Browser blocks iframe embedding.

**Solutions:**

1. **Enable embedding in Grafana:**
   ```ini
   [security]
   allow_embedding = true
   cookie_samesite = none
   cookie_secure = true  # Required for SameSite=None
   ```

2. **Check parent page CSP:**
   ```html
   <meta http-equiv="Content-Security-Policy" content="frame-src 'self' https://grafana.yourplatform.com">
   ```

3. **Verify HTTPS in production:** `SameSite=None` requires `Secure` cookies, which require HTTPS.

#### Role Mapping Not Working

**Symptom:** Users have wrong permissions in Grafana.

**Solutions:**

1. **Debug role claims:** Add logging to see what roles are received:
   ```bash
   # Check Grafana logs for OAuth debug info
   docker logs wallow-grafana 2>&1 | grep -i "oauth\|role"
   ```

2. **Test JMESPath expression:**
   ```bash
   # Use jp CLI to test JMESPath
   echo '{"realm_access":{"roles":["admin","user"]}}' | jp "contains(realm_access.roles[*], 'admin')"
   ```

3. **Verify mapper configuration:** Ensure the realm roles mapper is added to the `grafana-dedicated` client scope and configured to add claims to both ID token and access token.

### Diagnostic Commands

```bash
# Check Grafana configuration
docker exec wallow-grafana cat /etc/grafana/grafana.ini | grep -A 20 "auth.generic_oauth"

# Check Grafana logs
docker logs wallow-grafana --tail 100

# Test Keycloak OIDC discovery
curl http://localhost:8080/realms/wallow/.well-known/openid-configuration | jq .

# Verify Keycloak client exists
docker exec wallow-keycloak /opt/keycloak/bin/kcadm.sh get clients -r wallow --fields clientId

# Check Grafana health
curl http://localhost:3000/api/health

# Test OAuth flow manually
# 1. Get authorization URL
open "http://localhost:8080/realms/wallow/protocol/openid-connect/auth?client_id=grafana&response_type=code&scope=openid%20profile%20email&redirect_uri=http://localhost:3000/login/generic_oauth"
```

---

## 11. Security Best Practices

### Credentials Management

- **Never commit secrets** to version control
- **Use environment variables** or secrets management (Vault, AWS Secrets Manager)
- **Rotate client secrets** periodically
- **Use strong admin passwords** for Grafana and Keycloak

### OAuth Security

- **Enable PKCE** for additional security:
  ```ini
  [auth.generic_oauth]
  use_pkce = true
  ```

- **Disable direct access grants** in Keycloak client settings

- **Use short token lifetimes** in Keycloak (5-15 minutes for access tokens)

- **Enable refresh token rotation** in Keycloak

### Network Security

- **Use HTTPS everywhere** in production
- **Restrict network access** between Grafana and Keycloak
- **Use internal DNS** for service-to-service communication:
  ```yaml
  # In Docker Compose or Kubernetes, use service names
  auth_url = http://keycloak:8080/...  # Internal
  # Not public URLs for backend communication
  ```

### Embedding Security

- **Validate tenant_id server-side** before passing to Grafana
- **Use short-lived JWT tokens** for embedded dashboards
- **Implement proper CSP headers** on the parent application
- **Consider read-only dashboard permissions** for embedded views

### Monitoring and Auditing

- **Enable Grafana audit logging:**
  ```ini
  [log]
  mode = console file
  level = info

  [log.file]
  log_rotate = true
  max_days = 7
  ```

- **Monitor authentication events** in Keycloak

- **Set up alerts** for failed login attempts

### Least Privilege

- **Use Viewer role by default** for new users
- **Limit Admin access** to platform operators only
- **Create specific roles** for different team needs:
  ```
  # Example role hierarchy
  admin        -> Full platform access
  manager      -> Edit dashboards, manage data sources
  team-lead    -> Edit team dashboards
  developer    -> View all, edit own dashboards
  viewer       -> View dashboards only
  ```

---

## Quick Reference

### URLs

| Service | Local | Production |
|---------|-------|------------|
| Grafana | http://localhost:3000 | https://grafana.yourplatform.com |
| Keycloak | http://localhost:8080 | https://auth.yourplatform.com |
| OIDC Discovery | http://localhost:8080/realms/wallow/.well-known/openid-configuration | https://auth.yourplatform.com/realms/wallow/.well-known/openid-configuration |
| JWKS | http://localhost:8080/realms/wallow/protocol/openid-connect/certs | https://auth.yourplatform.com/realms/wallow/protocol/openid-connect/certs |

### Key Configuration

| Setting | Value |
|---------|-------|
| Keycloak Realm | `wallow` |
| Grafana Client ID | `grafana` |
| OAuth Scopes | `openid profile email` |
| JWT Header | `X-JWT-Assertion` |
| Role Claim Path | `realm_access.roles` |

### Test Commands

```bash
# Verify OAuth configuration
curl http://localhost:3000/api/login/generic_oauth/settings

# Test JWT authentication
curl -H "X-JWT-Assertion: $TOKEN" http://localhost:3000/api/user

# Get OIDC configuration
curl http://localhost:8080/realms/wallow/.well-known/openid-configuration
```
