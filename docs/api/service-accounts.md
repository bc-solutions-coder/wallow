# Service Accounts API Documentation

## Overview

Service accounts provide programmatic access to the Wallow API for server-to-server integrations. Unlike user authentication (which requires browser-based login), service accounts use OAuth2 client credentials flow designed for automated systems, background jobs, and integrations.

### When to Use Service Accounts

- Integrating Wallow with external systems (CRM, ERP, analytics)
- Building custom dashboards or reporting tools
- Automating administrative tasks
- Creating webhooks that call back to your API
- Scheduled jobs that need to read/write data

### Service Accounts vs User Tokens

| Aspect | User Token | Service Account |
|--------|-----------|-----------------|
| Authentication | Browser login (OIDC) | Client credentials (OAuth2) |
| Lifespan | Session-based (hours) | Short-lived (5-15 min) |
| Scope | User permissions | Explicit scopes |
| Use case | Interactive applications | Server-to-server |
| Credential type | Password + 2FA | Client ID + Secret |

---

## Getting Started

### Step 1: Create a Service Account

Log into your Wallow tenant and navigate to **Settings > API Management > Service Accounts**.

Click **Create Service Account** and provide:
- **Name**: Descriptive identifier (e.g., "Production Backend", "Analytics Pipeline")
- **Description**: Optional notes about the integration
- **Scopes**: Select the API permissions this account needs

After creation, you'll receive:
- `client_id`: Your service account identifier
- `client_secret`: Secret credential (shown only once - save it securely!)
- `token_endpoint`: OAuth2 token endpoint URL

**IMPORTANT**: The client secret is shown only once. Store it securely (environment variables, secret manager). You cannot retrieve it later.

### Step 2: Test Your Credentials

Use the provided credentials to request an access token:

```bash
curl -X POST https://api.yourplatform.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=your-client-id" \
  -d "client_secret=your-client-secret" \
  -d "grant_type=client_credentials"
```

Response:
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expires_in": 300,
  "token_type": "Bearer",
  "scope": "invoices.read invoices.write"
}
```

### Step 3: Call the API

Use the access token in the `Authorization` header:

```bash
curl -X GET https://api.yourplatform.com/api/invoices \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## Authentication Flow

The OAuth2 client credentials flow involves two steps:

```
┌─────────────────┐                      ┌──────────────┐
│   Your Server   │                      │  Wallow API  │
└────────┬────────┘                      └──────┬───────┘
         │                                      │
         │  1. POST /connect/token              │
         │     client_id=xxx                    │
         │     client_secret=yyy                │
         │     grant_type=client_credentials    │
         │─────────────────────────────────────►│
         │                                      │
         │  2. { access_token: "eyJ...",        │
         │       expires_in: 300 }              │
         │◄─────────────────────────────────────│
         │                                      │
         │  3. GET /api/invoices                │
         │     Authorization: Bearer eyJ...     │
         │─────────────────────────────────────►│
         │                                      │
         │  4. { invoices: [...] }              │
         │◄─────────────────────────────────────│
         │                                      │
         │  (Token expires after 5 min)         │
         │  (Repeat step 1 to get new token)    │
```

**Key Points:**
- Tokens expire in 5-15 minutes (check `expires_in` field)
- Cache tokens and refresh before expiry
- Each token request counts toward rate limits
- Never embed secrets in client-side code

---

## Code Examples

### Python (requests)

#### Basic Usage

```python
import requests
import os

# Load credentials from environment variables
CLIENT_ID = os.getenv("WALLOW_CLIENT_ID")
CLIENT_SECRET = os.getenv("WALLOW_CLIENT_SECRET")
TOKEN_URL = "https://api.yourplatform.com/connect/token"
API_BASE = "https://api.yourplatform.com"

# Step 1: Get access token
token_response = requests.post(
    TOKEN_URL,
    data={
        "client_id": CLIENT_ID,
        "client_secret": CLIENT_SECRET,
        "grant_type": "client_credentials"
    }
)
token_response.raise_for_status()
access_token = token_response.json()["access_token"]

# Step 2: Call API
headers = {"Authorization": f"Bearer {access_token}"}

# Get invoices
invoices = requests.get(f"{API_BASE}/api/invoices", headers=headers).json()

# Create invoice
new_invoice = requests.post(
    f"{API_BASE}/api/invoices",
    headers=headers,
    json={
        "customerId": "cust_123",
        "items": [
            {"description": "Professional Services", "amount": 1500.00}
        ]
    }
).json()
```

#### Production-Ready with Token Caching

```python
import time
import requests
from threading import Lock
from typing import Optional

class WallowClient:
    """Production-ready Wallow API client with automatic token refresh."""

    def __init__(self, client_id: str, client_secret: str, token_url: str, api_base: str):
        self.client_id = client_id
        self.client_secret = client_secret
        self.token_url = token_url
        self.api_base = api_base
        self._token: Optional[str] = None
        self._expires_at: float = 0
        self._lock = Lock()

    def _get_token(self) -> str:
        """Get current access token, refreshing if needed."""
        with self._lock:
            # Refresh 30 seconds before expiry for safety margin
            if time.time() >= self._expires_at - 30:
                response = requests.post(
                    self.token_url,
                    data={
                        "client_id": self.client_id,
                        "client_secret": self.client_secret,
                        "grant_type": "client_credentials"
                    }
                )
                response.raise_for_status()
                data = response.json()
                self._token = data["access_token"]
                self._expires_at = time.time() + data["expires_in"]

            return self._token

    def _request(self, method: str, path: str, **kwargs):
        """Make authenticated API request."""
        headers = kwargs.pop("headers", {})
        headers["Authorization"] = f"Bearer {self._get_token()}"

        response = requests.request(
            method,
            f"{self.api_base}{path}",
            headers=headers,
            **kwargs
        )
        response.raise_for_status()
        return response.json()

    def get(self, path: str, **kwargs):
        return self._request("GET", path, **kwargs)

    def post(self, path: str, **kwargs):
        return self._request("POST", path, **kwargs)

    def put(self, path: str, **kwargs):
        return self._request("PUT", path, **kwargs)

    def delete(self, path: str, **kwargs):
        return self._request("DELETE", path, **kwargs)

# Usage
client = WallowClient(
    client_id=os.getenv("WALLOW_CLIENT_ID"),
    client_secret=os.getenv("WALLOW_CLIENT_SECRET"),
    token_url="https://api.yourplatform.com/connect/token",
    api_base="https://api.yourplatform.com"
)

# Make requests (token refresh is automatic)
invoices = client.get("/api/invoices")
customer = client.post("/api/customers", json={"name": "Acme Corp"})
```

---

### C# / .NET

#### Basic Usage

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class WallowApiExample
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task Main()
    {
        var clientId = Environment.GetEnvironmentVariable("WALLOW_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("WALLOW_CLIENT_SECRET");
        var tokenUrl = "https://api.yourplatform.com/connect/token";
        var apiBase = "https://api.yourplatform.com";

        // Step 1: Get access token
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials"
        });

        var tokenResponse = await httpClient.PostAsync(tokenUrl, tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        var accessToken = tokenData.AccessToken;

        // Step 2: Call API
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // Get invoices
        var invoices = await httpClient.GetFromJsonAsync<List<Invoice>>($"{apiBase}/api/invoices");

        // Create invoice
        var newInvoice = new CreateInvoiceRequest
        {
            CustomerId = "cust_123",
            Items = new[]
            {
                new InvoiceItem { Description = "Professional Services", Amount = 1500.00m }
            }
        };

        var createResponse = await httpClient.PostAsJsonAsync($"{apiBase}/api/invoices", newInvoice);
        var created = await createResponse.Content.ReadFromJsonAsync<Invoice>();
    }
}

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType
);
```

#### Production-Ready with Token Caching

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

public class WallowClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tokenUrl;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTime _tokenExpiresAt;

    public WallowClient(string clientId, string clientSecret, string tokenUrl, string apiBase)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tokenUrl = tokenUrl;
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBase) };
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            // Refresh 30 seconds before expiry
            if (DateTime.UtcNow >= _tokenExpiresAt.AddSeconds(-30))
            {
                var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["grant_type"] = "client_credentials"
                });

                var response = await _httpClient.PostAsync(_tokenUrl, tokenRequest, ct);
                response.EnsureSuccessStatusCode();

                var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
                _accessToken = tokenData.AccessToken;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
            }

            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, path, ct);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken ct = default)
    {
        var request = await CreateRequestAsync(HttpMethod.Post, path, ct);
        request.Content = JsonContent.Create(body);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _tokenLock?.Dispose();
    }
}

// Usage
var client = new WallowClient(
    clientId: Environment.GetEnvironmentVariable("WALLOW_CLIENT_ID")!,
    clientSecret: Environment.GetEnvironmentVariable("WALLOW_CLIENT_SECRET")!,
    tokenUrl: "https://api.yourplatform.com/connect/token",
    apiBase: "https://api.yourplatform.com"
);

// Make requests (token refresh is automatic)
var invoices = await client.GetAsync<List<Invoice>>("/api/invoices");
var customer = await client.PostAsync<CreateCustomerRequest, Customer>(
    "/api/customers",
    new CreateCustomerRequest { Name = "Acme Corp" }
);
```

---

### JavaScript / Node.js

#### Basic Usage (ES Modules)

```javascript
import axios from 'axios';

const CLIENT_ID = process.env.WALLOW_CLIENT_ID;
const CLIENT_SECRET = process.env.WALLOW_CLIENT_SECRET;
const TOKEN_URL = 'https://api.yourplatform.com/connect/token';
const API_BASE = 'https://api.yourplatform.com';

// Step 1: Get access token
const tokenResponse = await axios.post(
  TOKEN_URL,
  new URLSearchParams({
    client_id: CLIENT_ID,
    client_secret: CLIENT_SECRET,
    grant_type: 'client_credentials'
  })
);

const accessToken = tokenResponse.data.access_token;

// Step 2: Call API
const headers = { Authorization: `Bearer ${accessToken}` };

// Get invoices
const invoices = await axios.get(`${API_BASE}/api/invoices`, { headers });
console.log(invoices.data);

// Create invoice
const newInvoice = await axios.post(
  `${API_BASE}/api/invoices`,
  {
    customerId: 'cust_123',
    items: [
      { description: 'Professional Services', amount: 1500.00 }
    ]
  },
  { headers }
);
console.log(newInvoice.data);
```

#### Production-Ready with Token Caching

```javascript
import axios from 'axios';

class WallowClient {
  constructor(clientId, clientSecret, tokenUrl, apiBase) {
    this.clientId = clientId;
    this.clientSecret = clientSecret;
    this.tokenUrl = tokenUrl;
    this.apiBase = apiBase;
    this.accessToken = null;
    this.tokenExpiresAt = 0;
    this.tokenRefreshPromise = null;
  }

  async getAccessToken() {
    const now = Date.now() / 1000;

    // Refresh 30 seconds before expiry
    if (now >= this.tokenExpiresAt - 30) {
      // Prevent multiple simultaneous refresh requests
      if (!this.tokenRefreshPromise) {
        this.tokenRefreshPromise = this._refreshToken();
      }
      await this.tokenRefreshPromise;
      this.tokenRefreshPromise = null;
    }

    return this.accessToken;
  }

  async _refreshToken() {
    const response = await axios.post(
      this.tokenUrl,
      new URLSearchParams({
        client_id: this.clientId,
        client_secret: this.clientSecret,
        grant_type: 'client_credentials'
      })
    );

    this.accessToken = response.data.access_token;
    this.tokenExpiresAt = Date.now() / 1000 + response.data.expires_in;
  }

  async request(method, path, options = {}) {
    const token = await this.getAccessToken();

    const config = {
      method,
      url: `${this.apiBase}${path}`,
      headers: {
        ...options.headers,
        Authorization: `Bearer ${token}`
      },
      ...options
    };

    const response = await axios(config);
    return response.data;
  }

  async get(path, options = {}) {
    return this.request('GET', path, options);
  }

  async post(path, data, options = {}) {
    return this.request('POST', path, { ...options, data });
  }

  async put(path, data, options = {}) {
    return this.request('PUT', path, { ...options, data });
  }

  async delete(path, options = {}) {
    return this.request('DELETE', path, options);
  }
}

// Usage
const client = new WallowClient(
  process.env.WALLOW_CLIENT_ID,
  process.env.WALLOW_CLIENT_SECRET,
  'https://api.yourplatform.com/connect/token',
  'https://api.yourplatform.com'
);

// Make requests (token refresh is automatic)
const invoices = await client.get('/api/invoices');
const customer = await client.post('/api/customers', { name: 'Acme Corp' });
```

---

## API Reference

### Service Account Management Endpoints

All service account management endpoints require user authentication (not service account authentication).

#### Create Service Account

```http
POST /api/v1/identity/service-accounts
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "name": "Production Backend",
  "description": "Main production server integration",
  "scopes": ["invoices.read", "invoices.write", "payments.read"]
}
```

**Response** (201 Created):
```json
{
  "id": "sa_abc123",
  "clientId": "sa-tenant12-production-backend",
  "clientSecret": "xK9mN2pL8qR5sT7vW3yZ1aB4cD6eF8gH0iJ2kL5mN7oP9qR1sT3uV5wX7yZ9",
  "tokenEndpoint": "https://api.yourplatform.com/connect/token",
  "scopes": ["invoices.read", "invoices.write", "payments.read"],
  "createdAt": "2024-02-06T10:00:00Z",
  "warning": "Save this secret now. It will not be shown again."
}
```

#### List Service Accounts

```http
GET /api/v1/identity/service-accounts
Authorization: Bearer <user-token>
```

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "sa_abc123",
      "clientId": "sa-tenant12-production-backend",
      "name": "Production Backend",
      "description": "Main production server integration",
      "status": "Active",
      "scopes": ["invoices.read", "invoices.write", "payments.read"],
      "createdAt": "2024-02-01T10:00:00Z",
      "lastUsedAt": "2024-02-06T14:23:00Z"
    },
    {
      "id": "sa_def456",
      "clientId": "sa-tenant12-analytics-pipeline",
      "name": "Analytics Pipeline",
      "description": "Data warehouse sync job",
      "status": "Active",
      "scopes": ["orders.read", "customers.read"],
      "createdAt": "2024-01-15T08:30:00Z",
      "lastUsedAt": "2024-02-06T14:00:00Z"
    }
  ],
  "total": 2
}
```

#### Get Service Account Details

```http
GET /api/v1/identity/service-accounts/{id}
Authorization: Bearer <user-token>
```

**Response** (200 OK):
```json
{
  "id": "sa_abc123",
  "clientId": "sa-tenant12-production-backend",
  "name": "Production Backend",
  "description": "Main production server integration",
  "status": "Active",
  "scopes": ["invoices.read", "invoices.write", "payments.read"],
  "createdAt": "2024-02-01T10:00:00Z",
  "lastUsedAt": "2024-02-06T14:23:00Z"
}
```

#### Update Service Account Scopes

```http
PUT /api/v1/identity/service-accounts/{id}/scopes
Authorization: Bearer <user-token>
Content-Type: application/json

{
  "scopes": ["invoices.read", "invoices.write", "payments.read", "subscriptions.read"]
}
```

**Response** (200 OK):
```json
{
  "id": "sa_abc123",
  "scopes": ["invoices.read", "invoices.write", "payments.read", "subscriptions.read"],
  "updatedAt": "2024-02-06T15:30:00Z"
}
```

#### Rotate Service Account Secret

```http
POST /api/v1/identity/service-accounts/{id}/rotate-secret
Authorization: Bearer <user-token>
```

**Response** (200 OK):
```json
{
  "clientSecret": "aB1cD2eF3gH4iJ5kL6mN7oP8qR9sT0uV1wX2yZ3aB4cD5eF6gH7iJ8kL9",
  "rotatedAt": "2024-02-06T16:00:00Z",
  "warning": "The old secret is now invalid. Update your application immediately."
}
```

#### Delete Service Account

```http
DELETE /api/v1/identity/service-accounts/{id}
Authorization: Bearer <user-token>
```

**Response** (204 No Content)

---

#### Get Available Scopes

```http
GET /api/v1/identity/scopes
Authorization: Bearer <user-token>
```

**Query Parameters:**
- `category` (optional): Filter by category (e.g., "Billing", "Identity")

**Response** (200 OK):
```json
{
  "items": [
    {
      "code": "invoices.read",
      "displayName": "Read Invoices",
      "category": "Billing",
      "description": "View invoice data",
      "isDefault": true
    },
    {
      "code": "invoices.write",
      "displayName": "Create/Update Invoices",
      "category": "Billing",
      "description": "Create and modify invoices",
      "isDefault": false
    },
    {
      "code": "payments.read",
      "displayName": "Read Payments",
      "category": "Billing",
      "description": "View payment transactions",
      "isDefault": false
    }
  ]
}
```

---

## Available Scopes

### Billing

| Scope | Description | Default |
|-------|-------------|---------|
| `invoices.read` | View invoice data | Yes |
| `invoices.write` | Create and modify invoices | No |
| `payments.read` | View payment transactions | No |
| `payments.write` | Process payments | No |
| `subscriptions.read` | View subscription data | No |
| `subscriptions.write` | Manage subscriptions | No |

### Identity

| Scope | Description | Default |
|-------|-------------|---------|
| `users.read` | View user data | No |
| `users.write` | Create and modify users | No |

### Notifications

| Scope | Description | Default |
|-------|-------------|---------|
| `notifications.read` | View notifications | No |
| `notifications.write` | Send notifications | No |

### Announcements

| Scope | Description | Default |
|-------|-------------|---------|
| `announcements.read` | View announcements | No |
| `announcements.write` | Manage announcements | No |

### Storage

| Scope | Description | Default |
|-------|-------------|---------|
| `storage.read` | View and download files | Yes |
| `storage.write` | Upload and manage files | No |

### Configuration

| Scope | Description | Default |
|-------|-------------|---------|
| `configuration.read` | View feature flags and settings | No |
| `configuration.write` | Manage feature flags and settings | No |

### Platform

| Scope | Description | Default |
|-------|-------------|---------|
| `webhooks.manage` | Manage webhook subscriptions | No |

---

## Best Practices

### 1. Never Commit Secrets

Store credentials in environment variables or a secret manager (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault).

**Bad:**
```python
# config.py
CLIENT_SECRET = "xK9mN2pL8qR5sT7vW3yZ..."  # DO NOT DO THIS
```

**Good:**
```python
# config.py
import os
CLIENT_SECRET = os.getenv("WALLOW_CLIENT_SECRET")
```

**Better:**
```python
# Using AWS Secrets Manager
import boto3
import json

def get_secret():
    client = boto3.client('secretsmanager')
    response = client.get_secret_value(SecretId='wallow/api-credentials')
    return json.loads(response['SecretString'])

credentials = get_secret()
```

### 2. Cache Access Tokens

Don't request a new token for every API call. Tokens are valid for 5-15 minutes.

**Bad:**
```python
for invoice in invoices_to_create:
    token = get_token()  # Unnecessary token request every loop
    create_invoice(invoice, token)
```

**Good:**
```python
token = get_token()
for invoice in invoices_to_create:
    create_invoice(invoice, token)
```

**Best:**
```python
client = WallowClient(...)  # Handles token caching internally
for invoice in invoices_to_create:
    client.post("/api/invoices", invoice)
```

### 3. Refresh Tokens Before Expiry

Check the `expires_in` field and refresh 30-60 seconds early to avoid mid-request expiration.

```python
if time.time() >= token_expires_at - 30:
    token = refresh_token()
```

### 4. Use Minimum Necessary Scopes

Only request scopes your application actually needs. This limits damage if credentials are compromised.

**Bad:**
```json
{
  "scopes": ["*"]  // All scopes - too permissive
}
```

**Good:**
```json
{
  "scopes": ["invoices.read"]  // Only what you need
}
```

### 5. Rotate Secrets Regularly

Rotate service account secrets every 90 days or when:
- An employee with access leaves
- A security incident occurs
- You suspect credential exposure

```bash
# Automate rotation
curl -X POST https://api.yourplatform.com/api/v1/identity/service-accounts/{id}/rotate-secret \
  -H "Authorization: Bearer $USER_TOKEN"
```

### 6. Monitor Usage

Check the `lastUsedAt` field to detect:
- Unused service accounts (can be deleted)
- Unexpected usage patterns (potential compromise)

```bash
# Get service accounts not used in 30+ days
GET /api/service-accounts?unused-since=30d
```

### 7. Handle Token Expiration Gracefully

Implement retry logic for 401 Unauthorized responses.

```python
def make_request(url, headers, retry=True):
    response = requests.get(url, headers=headers)

    if response.status_code == 401 and retry:
        # Token expired, refresh and retry once
        headers["Authorization"] = f"Bearer {get_fresh_token()}"
        return make_request(url, headers, retry=False)

    return response
```

### 8. Use HTTPS Only

Never send credentials over unencrypted HTTP. All Wallow endpoints use HTTPS.

### 9. Implement Rate Limiting

Respect API rate limits to avoid service disruption.

```python
import time

def rate_limited_request(client, path, max_per_second=10):
    time.sleep(1 / max_per_second)
    return client.get(path)
```

### 10. Log Securely

Never log access tokens or client secrets.

**Bad:**
```python
logger.info(f"Token: {access_token}")  # DO NOT DO THIS
```

**Good:**
```python
logger.info("Access token acquired")
logger.debug(f"Token expires at {expires_at}")
```

---

## Error Handling

### Common Error Responses

#### 401 Unauthorized - Invalid Token

```json
{
  "error": "invalid_token",
  "error_description": "Token is expired"
}
```

**Solution**: Refresh your access token and retry.

#### 401 Unauthorized - Invalid Credentials

```json
{
  "error": "invalid_client",
  "error_description": "Invalid client credentials"
}
```

**Solution**: Verify your `client_id` and `client_secret`. If you rotated the secret, update your application.

#### 403 Forbidden - Insufficient Scopes

```json
{
  "error": "insufficient_scope",
  "error_description": "The request requires higher privileges than provided by the access token",
  "required_scopes": ["invoices.write"]
}
```

**Solution**: Update your service account scopes via the portal or API.

#### 429 Too Many Requests

```json
{
  "error": "rate_limit_exceeded",
  "retry_after": 60
}
```

**Solution**: Implement exponential backoff and respect the `retry_after` header.

### Example Error Handling (Python)

```python
import time
import requests

def safe_api_call(client, method, path, max_retries=3, **kwargs):
    for attempt in range(max_retries):
        try:
            response = getattr(client, method)(path, **kwargs)
            return response

        except requests.HTTPError as e:
            if e.response.status_code == 401:
                # Token expired, client will auto-refresh on next call
                continue

            elif e.response.status_code == 429:
                # Rate limited
                retry_after = int(e.response.headers.get('Retry-After', 60))
                print(f"Rate limited. Waiting {retry_after} seconds...")
                time.sleep(retry_after)
                continue

            elif e.response.status_code == 403:
                # Insufficient permissions
                error_data = e.response.json()
                print(f"Missing scopes: {error_data.get('required_scopes')}")
                raise

            else:
                raise

    raise Exception(f"Max retries exceeded for {method} {path}")

# Usage
invoices = safe_api_call(client, 'get', '/api/invoices')
```

---

## Troubleshooting

### Issue: "invalid_client" Error

**Symptoms:**
```json
{"error": "invalid_client", "error_description": "Invalid client credentials"}
```

**Causes:**
1. Wrong `client_id` or `client_secret`
2. Service account was deleted or revoked
3. Secret was rotated and application not updated

**Solutions:**
1. Verify credentials in your portal
2. Check service account status (Active vs Revoked)
3. If secret was rotated, update your environment variables

---

### Issue: Token Refresh Loop

**Symptoms:**
- Application requests a new token every API call
- High latency
- Rate limiting errors

**Cause:**
- Not caching tokens
- Token expiry logic incorrect

**Solution:**
Use the production-ready client examples above with proper token caching.

---

### Issue: "insufficient_scope" Error

**Symptoms:**
```json
{"error": "insufficient_scope", "required_scopes": ["invoices.write"]}
```

**Cause:**
Service account doesn't have required scope.

**Solution:**
Update scopes via portal or API:
```bash
curl -X PUT https://api.yourplatform.com/api/v1/identity/service-accounts/{id}/scopes \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"scopes": ["invoices.read", "invoices.write"]}'
```

---

### Issue: Requests Fail After Secret Rotation

**Symptoms:**
- API calls return 401 after rotating secret
- Old secret no longer works

**Cause:**
Secret rotation immediately invalidates the old secret.

**Solution:**
Update application configuration immediately after rotation:
```bash
# 1. Rotate secret
NEW_SECRET=$(curl -X POST .../rotate-secret | jq -r '.clientSecret')

# 2. Update environment variable
export WALLOW_CLIENT_SECRET=$NEW_SECRET

# 3. Restart application
systemctl restart myapp
```

For zero-downtime rotation:
1. Create a second service account
2. Update application to use new account
3. Verify new account works
4. Delete old account

---

### Issue: CORS Errors

**Symptoms:**
```
Access to fetch at 'https://api.yourplatform.com/...' from origin 'http://localhost:3000'
has been blocked by CORS policy
```

**Cause:**
Service accounts are designed for server-to-server communication. Client-side JavaScript cannot safely use client secrets.

**Solution:**
- For browser applications, use user authentication (OIDC flow)
- For backend integrations, use service accounts from server-side code
- Never embed client secrets in frontend code (they'll be visible in browser)

---

## Frequently Asked Questions

### Can I use service accounts from mobile apps?

No. Service accounts require storing a client secret, which cannot be kept secure in mobile apps (users can decompile and extract it). Use user authentication (OAuth2 Authorization Code with PKCE) for mobile apps.

### How many service accounts can I create?

Most tenants can create up to 10 service accounts. Contact support to increase this limit.

### Can service accounts perform admin actions?

Service accounts respect the same permission system as users. Admin actions require admin-level scopes, which must be explicitly granted.

### What happens if my secret is compromised?

Immediately rotate the secret via the portal or API. The old secret becomes invalid instantly. All tokens issued with the old secret will expire within 5-15 minutes.

### Can I use service accounts for webhook callbacks?

Yes, but it's not recommended. Webhooks should authenticate using HMAC signatures. Service accounts are designed for your code calling our API, not vice versa.

### Do service account requests count toward my API limits?

Yes. All API requests (user or service account) count toward your tenant's rate limits and usage metering.

---

## Next Steps

- [Portal Documentation](./portal.md) - Manage service accounts via UI

- [API Changelog](./changelog.md) - Stay updated on API changes
- [Support](https://support.yourplatform.com) - Get help

---

**Last Updated:** 2026-03-13
**API Version:** v1
