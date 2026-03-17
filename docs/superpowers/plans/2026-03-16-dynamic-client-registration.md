# Dynamic Client Registration — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable external apps to self-register as OAuth2 service account clients via Keycloak DCR (RFC 7591), with lazy metadata sync in Foundry.

**Architecture:** Keycloak handles client registration via its OIDC DCR endpoint. A realm-default audience scope ensures all clients get `aud: foundry-api`. A post-import shell script configures DCR policies (trusted hosts for dev, Initial Access Tokens for prod). Foundry's `ServiceAccountTrackingMiddleware` lazily syncs DCR-registered clients to local metadata using `TenantId.Platform` sentinel.

**Tech Stack:** Keycloak 26 (DCR, Admin REST API component model), .NET 10, EF Core, FluentValidation, NSubstitute, Docker Compose

**Spec:** `docs/superpowers/specs/2026-03-16-dynamic-client-registration-design.md`

---

## Chunk 1: Keycloak Realm Configuration

### Task 1: Promote audience mapper to realm default client scope

**Files:**
- Modify: `docker/keycloak/realm-export.json`

- [ ] **Step 1.1: Add `foundry-api-audience` client scope**

Add to the `clientScopes` array (after the `tenant-context` scope):

```json
{
  "name": "foundry-api-audience",
  "description": "Adds foundry-api audience claim to access tokens",
  "protocol": "openid-connect",
  "attributes": {
    "include.in.token.scope": "false",
    "display.on.consent.screen": "false"
  },
  "protocolMappers": [
    {
      "name": "foundry-api-audience",
      "protocol": "openid-connect",
      "protocolMapper": "oidc-audience-mapper",
      "consentRequired": false,
      "config": {
        "included.client.audience": "foundry-api",
        "id.token.claim": "false",
        "access.token.claim": "true"
      }
    }
  ]
}
```

- [ ] **Step 1.2: Add `defaultDefaultClientScopes` at realm root**

After `"registrationAllowed": true,` add:
```json
"defaultDefaultClientScopes": ["foundry-api-audience"],
```

- [ ] **Step 1.3: Remove per-client `foundry-api-audience` protocol mappers**

Remove the `foundry-api-audience` protocol mapper block from all three clients:
- `foundry-api` client `protocolMappers` array
- `sa-foundry-api` client `protocolMappers` array
- `foundry-spa` client `protocolMappers` array

After removal:
- `foundry-api` keeps only: `realm roles` mapper
- `sa-foundry-api` keeps only: `tenant_id` mapper
- `foundry-spa` keeps only: `realm roles` mapper

- [ ] **Step 1.4: Validate JSON**

```bash
python3 -c "import json; json.load(open('docker/keycloak/realm-export.json')); print('Valid JSON')"
```

- [ ] **Step 1.5: Verify audience inheritance after realm re-import**

```bash
cd docker && docker compose down keycloak && docker compose up -d keycloak
# Wait for healthy
sleep 30
# Test sa-foundry-api token still has aud: foundry-api
TOKEN=$(curl -s -X POST http://localhost:8080/realms/foundry/protocol/openid-connect/token \
  -d "grant_type=client_credentials&client_id=sa-foundry-api&client_secret=foundry-api-secret" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
echo $TOKEN | cut -d. -f2 | python3 -c "import sys,json,base64; p=sys.stdin.read().strip(); p+='='*(4-len(p)%4); d=json.loads(base64.urlsafe_b64decode(p)); print('aud:', d.get('aud'))"
# Expected: aud: foundry-api
```

- [ ] **Step 1.6: Commit**

```bash
git add docker/keycloak/realm-export.json
git commit -m "feat(keycloak): promote foundry-api audience mapper to realm default client scope

Adds foundry-api-audience as a realm default client scope so all clients
(including DCR-registered) automatically receive the aud: foundry-api claim.
Removes duplicate per-client audience mappers (atomic change)."
```

---

### Task 2: Create DCR policy setup script

**Files:**
- Create: `docker/keycloak/configure-dcr.sh`

- [ ] **Step 2.1: Write the script**

```bash
#!/bin/bash
set -euo pipefail

# Configure Keycloak DCR policies for the foundry realm.
# Must be run after Keycloak has fully started and imported the realm.
#
# Environment variables (all have defaults matching docker-compose.yml):
#   KEYCLOAK_URL              - Keycloak base URL (default: http://localhost:8080)
#   KEYCLOAK_ADMIN            - Admin username (default: admin)
#   KEYCLOAK_ADMIN_PASSWORD   - Admin password (default: admin)
#   FOUNDRY_REALM             - Realm name (default: foundry)

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
KEYCLOAK_ADMIN="${KEYCLOAK_ADMIN:-admin}"
KEYCLOAK_ADMIN_PASSWORD="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
FOUNDRY_REALM="${FOUNDRY_REALM:-foundry}"

echo "Waiting for Keycloak realm '${FOUNDRY_REALM}' to be ready at ${KEYCLOAK_URL}..."
until curl -sf "${KEYCLOAK_URL}/realms/${FOUNDRY_REALM}/.well-known/openid-configuration" > /dev/null 2>&1; do
  echo "  Realm not ready yet, retrying in 5s..."
  sleep 5
done
echo "Keycloak realm is ready."

# Obtain admin access token
echo "Obtaining admin token..."
ADMIN_TOKEN=$(curl -sf -X POST \
  "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" \
  -d "username=${KEYCLOAK_ADMIN}" \
  -d "password=${KEYCLOAK_ADMIN_PASSWORD}" \
  | jq -r '.access_token')

if [ -z "$ADMIN_TOKEN" ] || [ "$ADMIN_TOKEN" = "null" ]; then
  echo "ERROR: Failed to obtain admin token."
  exit 1
fi
echo "Admin token obtained."

# Get realm's internal UUID (needed as parentId for component model)
REALM_ID=$(curl -sf \
  "${KEYCLOAK_URL}/admin/realms/${FOUNDRY_REALM}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  | jq -r '.id')

if [ -z "$REALM_ID" ] || [ "$REALM_ID" = "null" ]; then
  echo "ERROR: Could not fetch realm ID."
  exit 1
fi
echo "Realm ID: ${REALM_ID}"

# Trusted Hosts policy (idempotent)
EXISTING=$(curl -sf \
  "${KEYCLOAK_URL}/admin/realms/${FOUNDRY_REALM}/components?type=org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  | jq -r '[.[] | select(.providerId == "trusted-hosts")] | length')

if [ "$EXISTING" -gt "0" ]; then
  echo "Trusted Hosts policy already configured — skipping."
else
  echo "Adding Trusted Hosts DCR policy (localhost/127.0.0.1)..."
  HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    "${KEYCLOAK_URL}/admin/realms/${FOUNDRY_REALM}/components" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"Trusted Hosts\",
      \"providerId\": \"trusted-hosts\",
      \"providerType\": \"org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy\",
      \"parentId\": \"${REALM_ID}\",
      \"config\": {
        \"trusted-hosts\": [\"localhost\", \"127.0.0.1\"],
        \"host-sending-registration-request-must-match\": [\"true\"]
      }
    }")
  if [ "$HTTP_STATUS" = "201" ]; then
    echo "Trusted Hosts policy created."
  else
    echo "WARNING: Trusted Hosts policy returned HTTP ${HTTP_STATUS} (may need Keycloak 26 API verification)."
  fi
fi

# Max Clients policy (idempotent)
EXISTING_MAX=$(curl -sf \
  "${KEYCLOAK_URL}/admin/realms/${FOUNDRY_REALM}/components?type=org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  | jq -r '[.[] | select(.providerId == "max-clients")] | length')

if [ "$EXISTING_MAX" -gt "0" ]; then
  echo "Max Clients policy already configured — skipping."
else
  echo "Adding Max Clients DCR policy (limit: 100)..."
  HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    "${KEYCLOAK_URL}/admin/realms/${FOUNDRY_REALM}/components" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{
      \"name\": \"Max Clients\",
      \"providerId\": \"max-clients\",
      \"providerType\": \"org.keycloak.services.clientregistration.policy.ClientRegistrationPolicy\",
      \"parentId\": \"${REALM_ID}\",
      \"config\": {
        \"max-clients\": [\"100\"]
      }
    }")
  if [ "$HTTP_STATUS" = "201" ]; then
    echo "Max Clients policy created."
  else
    echo "WARNING: Max Clients policy returned HTTP ${HTTP_STATUS}."
  fi
fi

echo ""
echo "DCR configuration complete."
```

- [ ] **Step 2.2: Make executable**

```bash
chmod +x docker/keycloak/configure-dcr.sh
```

- [ ] **Step 2.3: Commit**

```bash
git add docker/keycloak/configure-dcr.sh
git commit -m "feat(keycloak): add configure-dcr.sh for post-import DCR policy setup

Adds trusted hosts policy (localhost/127.0.0.1) and max clients policy (100)
via Keycloak component model API. Script is idempotent and safe to re-run."
```

---

### Task 3: Docker Compose integration for DCR setup

**Files:**
- Modify: `docker/docker-compose.yml`

- [ ] **Step 3.1: Add `keycloak-setup` one-shot service**

After the `keycloak` service block, add:

```yaml
  # ============================================
  # KEYCLOAK POST-IMPORT SETUP
  # ============================================
  keycloak-setup:
    image: alpine:3.21
    container_name: ${COMPOSE_PROJECT_NAME:-foundry}-keycloak-setup
    environment:
      KEYCLOAK_URL: http://keycloak:8080
      KEYCLOAK_ADMIN: ${KEYCLOAK_ADMIN:-admin}
      KEYCLOAK_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD:-admin}
      FOUNDRY_REALM: foundry
    volumes:
      - ./keycloak/configure-dcr.sh:/configure-dcr.sh:ro
    entrypoint: ["/bin/sh", "-c", "apk add --no-cache curl jq > /dev/null 2>&1 && sh /configure-dcr.sh"]
    depends_on:
      keycloak:
        condition: service_healthy
    networks:
      - foundry
    restart: "no"
```

- [ ] **Step 3.2: Verify it runs**

```bash
cd docker && docker compose up -d
docker compose logs keycloak-setup
# Expected: "DCR configuration complete."
docker compose ps keycloak-setup
# Expected: exited (0)
```

- [ ] **Step 3.3: Commit**

```bash
git add docker/docker-compose.yml
git commit -m "feat(docker): run configure-dcr.sh as one-shot service after keycloak starts"
```

---

## Chunk 2: Foundry Backend Changes

### Task 4: Add TenantId.Platform sentinel

**Files:**
- Modify: `src/Shared/Foundry.Shared.Kernel/Identity/TenantId.cs`
- Test: `tests/Foundry.Shared.Kernel.Tests/Identity/StronglyTypedIdTests.cs`

- [ ] **Step 4.1: Write the failing tests**

Add to `TenantIdTests` class in `tests/Foundry.Shared.Kernel.Tests/Identity/StronglyTypedIdTests.cs`:

```csharp
[Fact]
public void Platform_HasExpectedSentinelValue()
{
    Guid expected = Guid.Parse("00000000-0000-0000-0000-000000000001");
    TenantId.Platform.Value.Should().Be(expected);
}

[Fact]
public void Platform_IsNotEmpty()
{
    TenantId.Platform.Value.Should().NotBe(Guid.Empty);
}

[Fact]
public void Platform_IsDistinctFrom_Default()
{
    TenantId defaultId = default;
    TenantId.Platform.Should().NotBe(defaultId);
}

[Fact]
public void Create_WithPlatformGuid_EqualsPlatform()
{
    Guid platformGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    TenantId id = TenantId.Create(platformGuid);
    id.Should().Be(TenantId.Platform);
}
```

- [ ] **Step 4.2: Run tests to verify they fail**

```bash
dotnet test tests/Foundry.Shared.Kernel.Tests --filter "TenantIdTests" -v n
```
Expected: FAIL — `TenantId` has no `Platform` member.

- [ ] **Step 4.3: Add Platform sentinel to TenantId**

In `src/Shared/Foundry.Shared.Kernel/Identity/TenantId.cs`:

```csharp
namespace Foundry.Shared.Kernel.Identity;

public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>
{
    /// <summary>
    /// Sentinel value for the platform itself (not a tenant).
    /// Used for tenant-agnostic entities such as DCR-registered service accounts.
    /// </summary>
    public static readonly TenantId Platform = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public static TenantId Create(Guid value) => new(value);
    public static TenantId New() => new(Guid.NewGuid());
}
```

- [ ] **Step 4.4: Run tests to verify they pass**

```bash
dotnet test tests/Foundry.Shared.Kernel.Tests --filter "TenantIdTests" -v n
```
Expected: ALL PASS

- [ ] **Step 4.5: Commit**

```bash
git add src/Shared/Foundry.Shared.Kernel/Identity/TenantId.cs tests/Foundry.Shared.Kernel.Tests/Identity/StronglyTypedIdTests.cs
git commit -m "feat(shared-kernel): add TenantId.Platform sentinel for DCR service accounts"
```

---

### Task 5: Lazy metadata sync in ServiceAccountTrackingMiddleware

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Repositories/IServiceAccountUnfilteredRepository.cs`
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs`
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Middleware/ServiceAccountTrackingMiddleware.cs`
- Test: `tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/ServiceAccountTrackingMiddlewareTests.cs`

- [ ] **Step 5.1: Write the failing tests**

Add to existing `ServiceAccountTrackingMiddlewareTests.cs`:

```csharp
[Fact]
public async Task InvokeAsync_UnknownSaClient_CreatesMetadataWithPlatformTenant()
{
    _repository.GetByKeycloakClientIdAsync("sa-new-app", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<ServiceAccountMetadata?>(null));

    ServiceAccountMetadata? capturedMetadata = null;
    _repository.When(r => r.Add(Arg.Any<ServiceAccountMetadata>()))
        .Do(call => capturedMetadata = call.Arg<ServiceAccountMetadata>());

    DefaultHttpContext context = CreateHttpContextWithScopes("sa-new-app", 200, "inquiries.read inquiries.write");
    ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

    await middleware.InvokeAsync(context);
    await WaitForReceivedCallAsync(
        () => _repository.Received().Add(Arg.Any<ServiceAccountMetadata>()));

    capturedMetadata.Should().NotBeNull();
    capturedMetadata!.KeycloakClientId.Should().Be("sa-new-app");
    capturedMetadata.TenantId.Should().Be(TenantId.Platform);
    capturedMetadata.Scopes.Should().Contain("inquiries.read");
    capturedMetadata.Scopes.Should().Contain("inquiries.write");
}

[Fact]
public async Task InvokeAsync_UnknownSaClient_NoScopes_CreatesWithEmptyScopes()
{
    _repository.GetByKeycloakClientIdAsync("sa-no-scopes", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<ServiceAccountMetadata?>(null));

    ServiceAccountMetadata? capturedMetadata = null;
    _repository.When(r => r.Add(Arg.Any<ServiceAccountMetadata>()))
        .Do(call => capturedMetadata = call.Arg<ServiceAccountMetadata>());

    DefaultHttpContext context = CreateHttpContext("sa-no-scopes", 200);
    ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

    await middleware.InvokeAsync(context);
    await WaitForReceivedCallAsync(
        () => _repository.Received().Add(Arg.Any<ServiceAccountMetadata>()));

    capturedMetadata!.Scopes.Should().BeEmpty();
}

[Fact]
public async Task InvokeAsync_KnownSaClient_DoesNotCreateDuplicate()
{
    ServiceAccountMetadata existing = ServiceAccountMetadata.Create(
        TenantId.Platform, "sa-existing", "Existing", null, [], Guid.Empty, TimeProvider.System);
    _repository.GetByKeycloakClientIdAsync("sa-existing", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<ServiceAccountMetadata?>(existing));

    DefaultHttpContext context = CreateHttpContext("sa-existing", 200);
    ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

    await middleware.InvokeAsync(context);
    await WaitForReceivedCallAsync(
        () => _repository.Received().GetByKeycloakClientIdAsync("sa-existing", Arg.Any<CancellationToken>()));

    _repository.DidNotReceive().Add(Arg.Any<ServiceAccountMetadata>());
}

[Fact]
public async Task InvokeAsync_LazySyncFails_DoesNotBlockRequest()
{
    _repository.GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<ServiceAccountMetadata?>(null));
    _repository.When(r => r.Add(Arg.Any<ServiceAccountMetadata>()))
        .Do(_ => throw new InvalidOperationException("DB write failed"));

    DefaultHttpContext context = CreateHttpContext("sa-broken", 200);
    ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

    // Should not throw
    await middleware.InvokeAsync(context);
}
```

Add helper:
```csharp
private DefaultHttpContext CreateHttpContextWithScopes(string clientId, int statusCode, string scopes)
{
    DefaultHttpContext context = new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("azp", clientId),
            new Claim("scope", scopes)
        ]))
    };
    context.Response.StatusCode = statusCode;
    context.RequestServices = BuildServiceProvider();
    return context;
}
```

- [ ] **Step 5.2: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "ServiceAccountTrackingMiddlewareTests" -v n
```

- [ ] **Step 5.3: Add `Add` method to `IServiceAccountUnfilteredRepository`**

```csharp
void Add(ServiceAccountMetadata entity);
```

- [ ] **Step 5.4: Implement `Add` in `ServiceAccountRepository`**

Add explicit interface implementation:
```csharp
void IServiceAccountUnfilteredRepository.Add(ServiceAccountMetadata entity)
{
    context.ServiceAccountMetadata.Add(entity);
}
```

- [ ] **Step 5.5: Add lazy sync logic to `ServiceAccountTrackingMiddleware`**

In the existing `else` branch (when `metadata` is null), add:

```csharp
else
{
    // Lazy sync: DCR-registered client not yet known
    string? scopeClaim = context.User.FindFirst("scope")?.Value;
    IEnumerable<string> scopes = string.IsNullOrEmpty(scopeClaim)
        ? []
        : scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    ServiceAccountMetadata newMetadata = ServiceAccountMetadata.Create(
        TenantId.Platform,
        clientId,
        clientId,
        null,
        scopes,
        Guid.Empty,
        timeProvider);

    repository.Add(newMetadata);
    await repository.SaveChangesAsync();
    LogLazySyncCreated(clientId);
}
```

Add log message:
```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Lazily created ServiceAccountMetadata for DCR-registered client {ClientId}")]
private partial void LogLazySyncCreated(string clientId);
```

- [ ] **Step 5.6: Run tests to verify they pass**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "ServiceAccountTrackingMiddlewareTests" -v n
```
Expected: ALL PASS

- [ ] **Step 5.7: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Repositories/IServiceAccountUnfilteredRepository.cs \
        src/Modules/Identity/Foundry.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs \
        src/Modules/Identity/Foundry.Identity.Infrastructure/Middleware/ServiceAccountTrackingMiddleware.cs \
        tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/ServiceAccountTrackingMiddlewareTests.cs
git commit -m "feat(identity): add lazy DCR metadata sync to ServiceAccountTrackingMiddleware

When an unknown sa-* client hits the API (registered via Keycloak DCR),
automatically creates a ServiceAccountMetadata record with TenantId.Platform.
Fire-and-forget — sync failures are logged but don't block the request."
```

---

## Chunk 3: Integration Tests and Documentation

### Task 6: DCR flow integration tests

**Files:**
- Create: `tests/Modules/Identity/Foundry.Identity.IntegrationTests/OAuth2/DcrFlowTests.cs`
- Modify: `tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs` (add `CreateServiceAccountClientAsync`)

- [ ] **Step 6.1: Add `CreateServiceAccountClientAsync` helper to KeycloakFixture**

```csharp
public async Task<(string ClientSecret, string KeycloakClientId)> CreateServiceAccountClientAsync(string clientId)
{
    string adminToken = await GetAdminTokenAsync();
    using HttpClient http = new();
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

    await http.PostAsJsonAsync($"{BaseUrl}/admin/realms/{RealmName}/clients", new
    {
        clientId, enabled = true, serviceAccountsEnabled = true,
        standardFlowEnabled = false, directAccessGrantsEnabled = false,
        clientAuthenticatorType = "client-secret"
    });

    using HttpResponseMessage listResponse = await http.GetAsync($"{BaseUrl}/admin/realms/{RealmName}/clients?clientId={clientId}");
    System.Text.Json.JsonDocument clientList = await System.Text.Json.JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
    string keycloakId = clientList.RootElement[0].GetProperty("id").GetString()!;

    using HttpResponseMessage secretResponse = await http.GetAsync($"{BaseUrl}/admin/realms/{RealmName}/clients/{keycloakId}/client-secret");
    System.Text.Json.JsonDocument secretDoc = await System.Text.Json.JsonDocument.ParseAsync(await secretResponse.Content.ReadAsStreamAsync());
    string secret = secretDoc.RootElement.GetProperty("value").GetString()!;

    return (secret, keycloakId);
}
```

- [ ] **Step 6.2: Write DcrFlowTests**

Test cases: token acquisition for DCR client, audience claim verification, wrong-prefix 403.

- [ ] **Step 6.3: Run integration tests**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.IntegrationTests --filter "DcrFlow" -v n
```

- [ ] **Step 6.4: Commit**

```bash
git add tests/Modules/Identity/Foundry.Identity.IntegrationTests/OAuth2/DcrFlowTests.cs \
        tests/Foundry.Tests.Common/Fixtures/KeycloakFixture.cs
git commit -m "test(identity): add DCR flow integration tests"
```

---

### Task 7: App integration guide

**Files:**
- Create: `docs/DCR_INTEGRATION_GUIDE.md`

- [ ] **Step 7.1: Write the guide**

Covers: registration (dev vs prod), `sa-` prefix convention, credential storage, token acquisition, API usage, scope assignment, troubleshooting table.

- [ ] **Step 7.2: Commit**

```bash
git add docs/DCR_INTEGRATION_GUIDE.md
git commit -m "docs: add DCR integration guide for frontend developers"
```

---

### Task 8: Verify pre-configured sa-foundry-api scopes

**Files:**
- Modify: `tests/Modules/Identity/Foundry.Identity.IntegrationTests/OAuth2/TokenAcquisitionTests.cs`

- [ ] **Step 8.1: Add scope verification tests**

```csharp
[Fact]
public async Task SaFoundryApi_Token_ContainsInquiriesWriteScope()
{
    string token = await GetServiceAccountTokenAsync("sa-foundry-api", "foundry-api-secret");
    JwtSecurityTokenHandler handler = new();
    JwtSecurityToken jwt = handler.ReadJwtToken(token);
    string? scopeClaim = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
    scopeClaim.Should().Contain("inquiries.write");
    scopeClaim.Should().Contain("inquiries.read");
}

[Fact]
public async Task SaFoundryApi_Token_ContainsFoundryApiAudience()
{
    string token = await GetServiceAccountTokenAsync("sa-foundry-api", "foundry-api-secret");
    JwtSecurityTokenHandler handler = new();
    JwtSecurityToken jwt = handler.ReadJwtToken(token);
    jwt.Audiences.Should().Contain("foundry-api");
}
```

- [ ] **Step 8.2: Run and commit**

```bash
dotnet test tests/Modules/Identity/Foundry.Identity.IntegrationTests --filter "TokenAcquisitionTests" -v n
git add tests/Modules/Identity/Foundry.Identity.IntegrationTests/OAuth2/TokenAcquisitionTests.cs
git commit -m "test(identity): verify sa-foundry-api pre-configured scopes and audience"
```

---

## Task Dependencies

```
Task 1 (realm audience scope) ──────────► Task 3 (docker compose)
Task 2 (configure-dcr.sh) ──────────────► Task 3 (docker compose)
Task 4 (TenantId.Platform) ─────────────► Task 5 (lazy sync middleware)
Task 5 (lazy sync middleware) ───────────► Task 6 (integration tests)
Task 1 + Task 3 ─────────────────────────► Task 6 (integration tests)
Task 1 ──────────────────────────────────► Task 8 (scope verification)
Task 7 (docs) has no dependencies
```

Tasks 1, 2, 4, and 7 can run in parallel.
Tasks 3 depends on 1 + 2.
Task 5 depends on 4.
Tasks 6 and 8 depend on 1 + 3 + 5.
