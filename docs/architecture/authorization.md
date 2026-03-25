# Authorization Guide

Wallow uses role-based access control (RBAC) with permission expansion. ASP.NET Core Identity with OpenIddict manages authentication and assigns roles; the API expands roles into granular permissions at request time.

---

## How It Works

```
JWT with role claims
        │
        ▼
┌─────────────────────────────┐
│ PermissionExpansionMiddleware │
│ Reads roles from token       │
│ Expands to permission claims │
└─────────────┬───────────────┘
              ▼
┌─────────────────────────────┐
│ [HasPermission] attribute   │
│ Checks permission claims    │
└─────────────────────────────┘
```

1. User authenticates and receives a JWT containing roles (e.g., `admin`, `manager`, `user`)
2. `PermissionExpansionMiddleware` reads the roles and adds permission claims to the request
3. Controller actions decorated with `[HasPermission]` check for specific permissions

---

## Adding Permissions to Routes

### Step 1: Choose or Add a Permission

Permissions are defined in `PermissionType.cs`:

```
src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs
```

```csharp
public static class PermissionType
{
    public const string None = "None";

    // User management
    public const string UsersRead = "UsersRead";
    public const string UsersCreate = "UsersCreate";
    public const string UsersUpdate = "UsersUpdate";
    public const string UsersDelete = "UsersDelete";

    // Billing
    public const string BillingRead = "BillingRead";
    public const string BillingManage = "BillingManage";
    public const string InvoicesRead = "InvoicesRead";
    public const string InvoicesWrite = "InvoicesWrite";
    // ...
}
```

**Naming convention**: `{Domain}{Action}` — e.g., `BillingRead`, `InvoicesWrite`, `WebhooksManage`.

### Step 2: Map Permission to Roles

Edit `RolePermissionMapping.cs`:

```
src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/RolePermissionMapping.cs
```

```csharp
public static class RolePermissionMapping
{
    private static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        ["admin"] = PermissionType.All.ToArray(), // Admin gets everything

        ["manager"] = new[]
        {
            PermissionType.UsersRead,
            PermissionType.BillingRead,
            PermissionType.InvoicesRead,
            PermissionType.InvoicesWrite,
            // ...
        },

        ["user"] = new[]
        {
            PermissionType.BillingRead,  // Users can read billing
            // ...
        }
    };
}
```

### Step 3: Apply to Controller or Action

Add the `[HasPermission]` attribute:

```csharp
using Wallow.Identity.Api.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;

[ApiController]
[Route("api/billing/invoices")]
[Authorize]  // Requires authentication
public class InvoicesController : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionType.InvoicesRead)]  // Requires permission
    public async Task<IActionResult> GetAll()
    {
        // ...
    }

    [HttpPost]
    [HasPermission(PermissionType.InvoicesWrite)]
    public async Task<IActionResult> Create(CreateInvoiceRequest request)
    {
        // ...
    }
}
```

You can apply `[HasPermission]` at the controller level (all actions) or individual action level.

### Step 4: Add Project Reference (if needed)

If your module doesn't reference the Identity module, add the reference to access `HasPermissionAttribute`:

```xml
<!-- In your Module.Api.csproj -->
<ProjectReference Include="..\..\Identity\Wallow.Identity.Api\Wallow.Identity.Api.csproj" />
```

Or reference the Shared.Kernel project if you only need `PermissionType`:

```xml
<ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
```

---

## Adding New Roles

Roles are managed through ASP.NET Core Identity. To add a new role:

### Step 1: Define the Role

Add the role through the Identity module's role management API or seed it in the database migration.

### Step 2: Map Permissions to the Role

Add the role to `RolePermissionMapping.cs`:

```csharp
private static readonly Dictionary<string, string[]> RolePermissions = new()
{
    ["admin"] = PermissionType.All.ToArray(),

    ["billing-admin"] = new[]  // New role
    {
        PermissionType.BillingRead,
        PermissionType.BillingManage,
        PermissionType.InvoicesRead,
        PermissionType.InvoicesWrite,
        PermissionType.PaymentsRead,
        PermissionType.PaymentsWrite,
        PermissionType.SubscriptionsRead,
        PermissionType.SubscriptionsWrite,
    },

    ["manager"] = new[] { /* ... */ },
    ["user"] = new[] { /* ... */ },
};
```

### Step 3: Assign Role to Users

Assign roles to users via the Identity module's user management API using `IUserManagementService`.

---

## Service Account Permissions

Service accounts (machine-to-machine) use OAuth2 scopes instead of roles. The middleware maps scopes to permissions.

### Adding a New Scope Mapping

Edit `PermissionExpansionMiddleware.cs`:

```csharp
private static string? MapScopeToPermission(string scope)
{
    return scope switch
    {
        "billing.read" => PermissionType.BillingRead,
        "billing.manage" => PermissionType.BillingManage,
        "invoices.read" => PermissionType.InvoicesRead,
        "invoices.write" => PermissionType.InvoicesWrite,
        // ...
        _ => null
    };
}
```

**Scope naming convention**: `{domain}.{action}` — e.g., `invoices.read`, `billing.manage`.

---

## Quick Reference

### Files to Edit

| Task | File |
|------|------|
| Add permission | `Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` |
| Map permission to role | `Identity.Infrastructure/Authorization/RolePermissionMapping.cs` |
| Map scope to permission | `Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs` |
| Apply to route | Your controller with `[HasPermission(...)]` |

### Existing Roles

| Role | Description |
|------|-------------|
| `admin` | All permissions |
| `manager` | Team management, projects, tasks, billing read, API keys |
| `user` | Basic access — read tasks/projects, create tasks |

> **Note:** `PermissionType` is a static class with string constants (not a numeric enum). Permissions are grouped by domain area. The current active modules are: Identity, Billing, Storage, Notifications, Messaging, Announcements, and Inquiries.

---

## Multi-Tenancy Authorization

Wallow uses organization claims in JWTs for multi-tenancy. The `TenantResolutionMiddleware` extracts the tenant ID from the JWT and populates `ITenantContext`.

### How Tenant Resolution Works

```
JWT with organization claim
        │
        ▼
┌─────────────────────────────────────┐
│ TenantResolutionMiddleware          │
│ - Parses organization claim (JSON)  │
│ - Sets ITenantContext.TenantId      │
└─────────────────────────────────────┘
        │
        ▼
┌─────────────────────────────────────┐
│ EF Core Global Query Filters        │
│ - Automatically filter by TenantId  │
└─────────────────────────────────────┘
```

### Organization Claim Format

The identity provider uses a JSON format for the `organization` claim:

```json
{
  "550e8400-e29b-41d4-a716-446655440000": {
    "name": "Acme Corp"
  }
}
```

The middleware parses the GUID from the property name (not a value) and extracts the organization name from the nested object.

### Admin Tenant Override

Users with the `admin` role can switch tenant context using the `X-Tenant-Id` header:

```bash
curl -H "Authorization: Bearer $TOKEN" \
     -H "X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000" \
     http://localhost:5000/api/billing/invoices
```

This allows admins to view data across tenants for support scenarios.

### Accessing Tenant Context in Code

Inject `ITenantContext` to access the current tenant:

```csharp
public class InvoiceService
{
    private readonly ITenantContext _tenantContext;

    public InvoiceService(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<List<Invoice>> GetInvoicesAsync()
    {
        // TenantId is already set by middleware
        var tenantId = _tenantContext.TenantId;
        // EF Core global query filters handle filtering automatically
    }
}
```

---

## Testing Permissions

### Unit Test Example

```csharp
[Fact]
public async Task GetUsers_RequiresUsersRead_Returns403WhenMissing()
{
    // Arrange - user without UsersRead permission
    var client = _factory.CreateClientWithRole("user");

    // Act
    var response = await client.GetAsync("/api/identity/users");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

### Manual Testing

1. Get a token with the desired role
2. Call the API with the token
3. Verify 200 (allowed) or 403 (forbidden)

```bash
# Get token for user with 'manager' role
TOKEN=$(curl -s -X POST "http://localhost:5000/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{"email": "manager@example.com", "password": "password"}' | jq -r '.access_token')

# Call protected endpoint
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/identity/users
```

---

## Middleware Pipeline Order

The authorization middleware must be registered in the correct order in `Program.cs`:

```
1. UseAuthentication()         - OpenIddict JWT validation
2. TenantResolutionMiddleware  - Parses organization claim → ITenantContext
3. PermissionExpansionMiddleware - Expands roles → permission claims
4. UseAuthorization()          - Enforces [HasPermission] attributes
```

**Warning**: Reordering these middlewares will break authorization. For example, if `PermissionExpansionMiddleware` runs before authentication, there will be no user claims to expand.

---

## Troubleshooting

**403 Forbidden but user has the role**
- Check `RolePermissionMapping` includes the permission for that role
- Verify the role name matches exactly (case-sensitive)
- Check the JWT contains the role claim (decode at jwt.io)

**Permission not being checked**
- Ensure `[Authorize]` is on the controller (authentication required first)
- Verify `[HasPermission]` attribute is applied
- Check project references include `Wallow.Identity.Api`

**Service account getting 403**
- Verify the scope is in the token
- Check `MapScopeToPermission` includes the mapping
