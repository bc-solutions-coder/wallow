# Wallow Troubleshooting Guide

This guide helps you diagnose and resolve common issues when developing with Wallow. It covers infrastructure, authentication, database, messaging, testing, and build problems.

---

## Table of Contents

1. [Infrastructure Issues](#1-infrastructure-issues)
2. [Authentication Issues](#2-authentication-issues)
3. [Database Issues](#3-database-issues)
4. [Messaging Issues](#4-messaging-issues)
5. [Test Failures](#5-test-failures)
6. [Build Issues](#6-build-issues)
7. [Debugging Tips](#7-debugging-tips)

---

## 1. Infrastructure Issues

### Docker Containers Not Starting

#### Symptom
```
docker compose up -d
# Containers exit immediately or show "Restarting" status
```

#### Diagnosis
```bash
# Check container status
cd docker && docker compose ps

# View logs for specific container
docker compose logs postgres
docker compose logs rabbitmq
```

#### Common Causes and Solutions

**Port already in use:**
```
Error: bind: address already in use
```
```bash
# Find process using the port (e.g., 5432)
lsof -i :5432

# Kill the process or stop the conflicting service
kill -9 <PID>

# Or change the port in docker-compose.yml
```

**Volume permission issues:**
```bash
# Reset volumes (WARNING: deletes all data)
cd docker && docker compose down -v
docker compose up -d
```

**Out of disk space:**
```bash
# Check Docker disk usage
docker system df

# Clean up unused resources
docker system prune -a --volumes
```

**Environment file missing:**
```bash
# Create .env file from example
cp docker/.env.example docker/.env
```

### PostgreSQL Connection Failures

#### Symptom
```
Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:5432
  ---> System.Net.Sockets.SocketException: Connection refused
```

#### Diagnosis
```bash
# Check if PostgreSQL container is running
docker compose ps postgres

# Test connectivity
docker exec wallow-postgres pg_isready -U wallow

# Check logs
docker compose logs postgres
```

#### Solutions

**Container not running:**
```bash
cd docker && docker compose up -d postgres
```

**Wrong connection string:**
Check `appsettings.Development.json` or environment variables:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow"
  }
}
```

**Database not initialized:**
```bash
# Recreate with init scripts
cd docker && docker compose down -v
docker compose up -d postgres
```

**PostgreSQL not accepting connections:**
```
FATAL: no pg_hba.conf entry for host
```
Check `docker/init-db.sql` and ensure the database user has proper permissions.

### RabbitMQ Connection Issues

#### Symptom
```
RabbitMQ.Client.Exceptions.BrokerUnreachableException: None of the specified endpoints were reachable
```

#### Diagnosis
```bash
# Check RabbitMQ container
docker compose ps rabbitmq

# Test management UI
curl -u guest:guest http://localhost:15672/api/overview

# Check logs
docker compose logs rabbitmq
```

#### Solutions

**Container not healthy:**
```bash
# Wait for RabbitMQ to fully start (can take 30+ seconds)
docker compose logs -f rabbitmq

# Look for: "Server startup complete"
```

**Wrong connection string:**
```json
{
  "ConnectionStrings": {
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  }
}
```

**Virtual host not found:**
```
RabbitMQ.Client.Exceptions.OperationInterruptedException: AMQP close-reason: NOT_FOUND - no vhost '/'
```
The default vhost `/` should exist. If using a custom vhost, create it:
```bash
docker exec wallow-rabbitmq rabbitmqctl add_vhost wallow
docker exec wallow-rabbitmq rabbitmqctl set_permissions -p wallow guest ".*" ".*" ".*"
```

### Valkey/Redis Connection Problems

#### Symptom
```
StackExchange.Redis.RedisConnectionException: It was not possible to connect to the redis server(s)
```

#### Diagnosis
```bash
# Check Valkey container
docker compose ps valkey

# Test connectivity
docker exec wallow-valkey valkey-cli ping
# Should return: PONG

# Check logs
docker compose logs valkey
```

#### Solutions

**Container not running:**
```bash
cd docker && docker compose up -d valkey
```

**Wrong connection string:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

**Memory limit exceeded:**
```bash
# Check memory usage
docker exec wallow-valkey valkey-cli info memory

# Clear cache if needed
docker exec wallow-valkey valkey-cli FLUSHALL
```

**TLS/SSL configuration issues:**
For production with TLS:
```
"Redis": "localhost:6379,ssl=true,abortConnect=false"
```

---

## 2. Authentication Issues

### JWT Validation Failures

#### Symptom
```
Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException: IDX10500: Signature validation failed
```
or
```
401 Unauthorized
WWW-Authenticate: Bearer error="invalid_token"
```

#### Diagnosis
```bash
# Check that the API is running and healthy
curl http://localhost:5000/health/ready
```

#### Solutions

**Wrong authentication configuration:**
Check `appsettings.json` for correct OpenIddict settings.

**Clock skew between server and client:**
```
IDX10222: Lifetime validation failed. The token is expired.
```
Ensure system clocks are synchronized. JWT has a 5-minute tolerance by default.

### Token Expiration Problems

#### Symptom
```
Token expired at [timestamp]
```

#### Solutions

**Get a fresh token:**
```bash
curl -s -X POST http://localhost:5000/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email": "admin@wallow.dev", "password": "Admin123!"}'
```

**Use refresh token:**
```bash
curl -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "YOUR_REFRESH_TOKEN"}'
```

### Missing Claims/Permissions

#### Symptom
```
403 Forbidden
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "User does not have required permission: Billing.InvoicesCreate"
}
```

#### Diagnosis
Decode your JWT token at https://jwt.io and check:
- `realm_access.roles` - Should contain role names
- `organization` or `org_id` - Should contain tenant ID

#### Solutions

**User missing role:**
Assign the required roles via the Identity module's user management API.

**Permission not mapped to role:**
Check `PermissionExpansionMiddleware` and role-to-permission mappings in:
`src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs`

**Organization claim missing:**
Ensure user belongs to an organization via the Identity module's organization management API.

### Tenant Resolution Failures

#### Symptom
```
Wallow.Shared.Kernel.MultiTenancy.TenantNotResolvedException: Could not resolve tenant from request
```

#### Diagnosis
Check if `ITenantContext.IsResolved` is `false` in your handler.

#### Solutions

**Missing organization claim:**
The `TenantResolutionMiddleware` reads tenant from JWT `organization` claim or `X-Tenant-Id` header.

Ensure your JWT contains:
```json
{
  "organization": "00000000-0000-0000-0000-000000000001"
}
```

**Using Dapper without tenant filter:**
When using Dapper directly, you must filter by tenant manually:
```csharp
var sql = "SELECT * FROM billing.invoices WHERE tenant_id = @TenantId";
await connection.QueryAsync<Invoice>(sql, new { TenantId = _tenantContext.TenantId.Value });
```

**Test environment:**
In tests, `WallowApiFactory` registers a fixed tenant context. If you need a different tenant, use the test headers:
```csharp
client.DefaultRequestHeaders.Add("X-Tenant-Id", "your-tenant-guid");
```

---

## 3. Database Issues

### Migration Conflicts

#### Symptom
```
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes
  ---> Npgsql.PostgresException: 42P01: relation "billing.invoices" does not exist
```

#### Diagnosis
```bash
# Check migration status
dotnet ef migrations list \
  --project src/Modules/Billing/Wallow.Billing.Infrastructure \
  --startup-project src/Wallow.Api \
  --context BillingDbContext
```

#### Solutions

**Apply pending migrations:**
```bash
dotnet ef database update \
  --project src/Modules/Billing/Wallow.Billing.Infrastructure \
  --startup-project src/Wallow.Api \
  --context BillingDbContext
```

**Migration history mismatch:**
```bash
# Reset database (WARNING: deletes all data)
cd docker && docker compose down -v
docker compose up -d postgres
dotnet run --project src/Wallow.Api
```

**Conflicting migration:**
```
The migration '20260215_AddNewField' has already been applied to the database
```
```bash
# Remove the conflicting migration
dotnet ef migrations remove \
  --project src/Modules/Billing/Wallow.Billing.Infrastructure \
  --startup-project src/Wallow.Api \
  --context BillingDbContext
```

### EF Core Tracking Issues

#### Symptom
```
System.InvalidOperationException: The instance of entity type 'Invoice' cannot be tracked because another instance with the same key value is already being tracked
```

#### Solutions

**Use AsNoTracking for read-only queries:**
```csharp
var invoices = await _context.Invoices
    .AsNoTracking()
    .Where(i => i.Status == InvoiceStatus.Pending)
    .ToListAsync();
```

**Detach existing entity:**
```csharp
var existingEntry = _context.Entry(existingInvoice);
existingEntry.State = EntityState.Detached;
```

**Use new DbContext scope:**
```csharp
using var scope = _serviceProvider.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
// Now you have a fresh tracking context
```

### Connection Pool Exhaustion

#### Symptom
```
Npgsql.NpgsqlException: The connection pool has been exhausted
  ---> System.InvalidOperationException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.
```

#### Diagnosis
```sql
-- Check active connections
SELECT count(*) FROM pg_stat_activity WHERE datname = 'wallow';

-- See connection details
SELECT pid, usename, application_name, state, query_start
FROM pg_stat_activity
WHERE datname = 'wallow'
ORDER BY query_start DESC;
```

#### Solutions

**Increase pool size:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;...;Maximum Pool Size=100;Connection Idle Lifetime=300"
  }
}
```

**Dispose connections properly:**
```csharp
// Use 'using' or 'await using' for DbContext
await using var context = await _contextFactory.CreateDbContextAsync();
```

**Close long-running connections:**
```sql
-- Terminate idle connections older than 10 minutes
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'wallow'
  AND state = 'idle'
  AND query_start < now() - interval '10 minutes';
```

---

## 4. Messaging Issues

### Messages Not Being Delivered

#### Symptom
- Events published but handlers never execute
- No errors in logs

#### Diagnosis
```bash
# Check RabbitMQ management UI
open http://localhost:15672
# Login: see docker/.env for credentials

# Look for:
# - Queues tab: messages should be 0 if consumed
# - Exchanges tab: check bindings exist
```

#### Solutions

**Handler not discovered:**
Wolverine discovers handlers in `Wallow.*` assemblies. Ensure:
1. Handler class is `public static`
2. Method is named `Handle` or `HandleAsync`
3. Handler is in a `Wallow.*` assembly

```csharp
// Correct handler pattern
public static class MyEventHandler
{
    public static async Task HandleAsync(MyEvent @event, IMyService service, CancellationToken ct)
    {
        // Handle event
    }
}
```

**Missing queue binding:**
Check `Program.cs` for routing configuration:
```csharp
opts.PublishMessage<MyEvent>().ToRabbitExchange("my-events");
opts.ListenToRabbitQueue("my-inbox");
```

**RabbitMQ not connected:**
```bash
# Verify connection
docker exec wallow-rabbitmq rabbitmqctl list_connections
```

### Dead Letter Queue Buildup

#### Symptom
Messages accumulating in error queues.

#### Diagnosis
```bash
# Check dead letter queue in RabbitMQ UI
# Or via CLI:
docker exec wallow-rabbitmq rabbitmqctl list_queues name messages | grep -i error
```

#### Solutions

**View failed message details:**
In RabbitMQ Management UI:
1. Go to Queues
2. Select the error queue (e.g., `wolverine.errors`)
3. Click "Get Message(s)" to see payload and headers

**Reprocess dead letters:**
```sql
-- Check Wolverine incoming_envelopes table
SELECT * FROM wolverine.wolverine_incoming_envelopes WHERE status = 'error';
```

**Fix the handler and replay:**
1. Fix the bug in your handler
2. Deploy the fix
3. Use RabbitMQ UI to move messages from DLQ back to original queue

**Clear dead letter queue:**
```bash
docker exec wallow-rabbitmq rabbitmqctl purge_queue wolverine.errors
```

### Handler Not Being Discovered

#### Symptom
```
Wolverine.Runtime.UnknownMessageTypeException: Unknown message type 'MyEvent'
```

#### Diagnosis
Check Wolverine's discovered handlers at startup in logs.

#### Solutions

**Ensure handler follows conventions:**
```csharp
// Must be public static class
public static class MyEventHandler
{
    // Method must be Handle or HandleAsync
    // First parameter must be the message type
    public static async Task HandleAsync(MyEvent @event, ILogger<MyEvent> logger)
    {
        // ...
    }
}
```

**Assembly not included in discovery:**
In `Program.cs`, handlers are discovered from `Wallow.*` assemblies:
```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Wallow.") == true))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

If your handler is in a different namespace/assembly, add it explicitly.

### Outbox Not Processing

#### Symptom
Messages stuck in `wolverine.wolverine_outgoing_envelopes` table.

#### Diagnosis
```sql
-- Check outbox status
SELECT status, count(*)
FROM wolverine.wolverine_outgoing_envelopes
GROUP BY status;

-- View stuck messages
SELECT * FROM wolverine.wolverine_outgoing_envelopes
WHERE status = 'scheduled'
ORDER BY scheduled_time;
```

#### Solutions

**Wolverine agent not running:**
The Wolverine durability agent processes the outbox. Ensure it's enabled:
```csharp
opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
```

**Database transaction not committed:**
Messages are only sent when the transaction commits:
```csharp
// Ensure SaveChanges is called
await _context.SaveChangesAsync();
// Outbox messages are now ready for sending
```

**Agent polling interval:**
By default, Wolverine polls every 5 seconds. For debugging:
```csharp
opts.Durability.PollingInterval = TimeSpan.FromSeconds(1);
```

---

## 5. Test Failures

### Testcontainers Not Starting

#### Symptom
```
Docker.DotNet.DockerApiException: Docker API responded with status code=InternalServerError
```
or
```
Testcontainers.Containers.ContainerStartException: The container did not start in time
```

#### Diagnosis
```bash
# Verify Docker is running
docker info

# Check Docker resources
docker system info | grep -E "CPUs|Memory"
```

#### Solutions

**Docker not running:**
Start Docker Desktop or Docker daemon.

**Insufficient resources:**
In Docker Desktop settings, increase:
- Memory: At least 4GB
- CPUs: At least 2

**Port conflicts:**
```bash
# Find conflicting ports
lsof -i :5432
lsof -i :5672
lsof -i :6379
```

**Container image not found:**
```bash
# Pull images manually
docker pull postgres:18-alpine
docker pull rabbitmq:4.2-management-alpine
docker pull valkey/valkey:8-alpine
```

**Timeout too short:**
```csharp
// Increase wait time in test fixture
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilPortIsAvailable(5432)
        .WithTimeout(TimeSpan.FromMinutes(2)))
    .Build();
```

### Parallel Test Conflicts

#### Symptom
Tests pass individually but fail when run together:
```
System.InvalidOperationException: Database is in use by another process
```

#### Solutions

**Use test collection to run sequentially:**
```csharp
[Collection("Database")]
public class InvoiceTests : IClassFixture<WallowApiFactory>
{
    // Tests in same collection run sequentially
}
```

**Isolate test data:**
```csharp
// Use unique identifiers per test
var invoiceId = Guid.NewGuid();
var tenantId = Guid.NewGuid();
```

**Disable parallel execution:**
In `xunit.runner.json`:
```json
{
  "parallelizeTestCollections": false
}
```

### SignalR Test Issues

#### Symptom
```
System.IO.IOException: The server returned status code '401' when status code '101' was expected
```

#### Solutions

**Include auth token in connection:**
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl($"{_factory.Server.BaseAddress}hubs/realtime", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(_token);
        options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
    })
    .Build();
```

**Use TestAuthHandler headers:**
```csharp
// The TestAuthHandler reads auth from query parameters too
var url = $"{baseUrl}hubs/realtime?access_token={token}";
```

**Wait for connection:**
```csharp
await connection.StartAsync();
// Give SignalR time to establish connection
await Task.Delay(500);
```

### Integration Test Authentication

#### Symptom
Tests return 401 even with TestAuthHandler.

#### Solutions

**Ensure test scheme is used:**
`WallowApiFactory` configures a "Test" authentication scheme:
```csharp
services.AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
```

**Set required headers:**
```csharp
client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
client.DefaultRequestHeaders.Add("X-Test-Roles", "admin");
```

**Skip auth for specific tests:**
```csharp
client.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");
```

---

## 6. Build Issues

### Package Restore Failures

#### Symptom
```
error NU1101: Unable to find package Wallow.Billing.Domain
```

#### Solutions

**Restore from solution root:**
```bash
cd /path/to/Wallow
dotnet restore
```

**Clear NuGet cache:**
```bash
dotnet nuget locals all --clear
dotnet restore
```

**Check package sources:**
```bash
dotnet nuget list source
# Ensure nuget.org is present
```

**Verify network connectivity:**
```bash
curl https://api.nuget.org/v3/index.json
```

### Project Reference Problems

#### Symptom
```
error CS0246: The type or namespace name 'InvoiceDto' could not be found
```

#### Solutions

**Check project references:**
```bash
# View project references
dotnet list src/Modules/Billing/Wallow.Billing.Api/Wallow.Billing.Api.csproj reference
```

**Add missing reference:**
```bash
dotnet add src/Modules/Billing/Wallow.Billing.Api/Wallow.Billing.Api.csproj \
  reference src/Modules/Billing/Wallow.Billing.Application/Wallow.Billing.Application.csproj
```

**Clean and rebuild:**
```bash
dotnet clean
dotnet build
```

### Assembly Conflicts

#### Symptom
```
System.IO.FileLoadException: Could not load file or assembly 'Newtonsoft.Json, Version=13.0.0.0'
```

#### Solutions

**Check for version conflicts:**
All package versions are centrally managed in `Directory.Packages.props`:
```bash
grep -r "Newtonsoft.Json" Directory.Packages.props
```

**Enable binding redirects:**
In the project file:
```xml
<PropertyGroup>
  <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
</PropertyGroup>
```

**Clear build artifacts:**
```bash
dotnet clean
rm -rf */bin */obj
dotnet build
```

---

## 7. Debugging Tips

### Enabling Detailed Logging

**Serilog configuration:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Wolverine": "Debug"
      }
    }
  }
}
```

**EF Core query logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Wolverine message logging:**
Already configured in `Program.cs`:
```csharp
opts.ConfigureMessageLogging(); // Logs message execution
```

### RabbitMQ Management Console Usage

**Access:**
```
URL: http://localhost:15672
Username: guest
Password: guest
```

**Key tabs:**
- **Overview**: Connection count, message rates
- **Queues**: Message backlog, consumer count
- **Exchanges**: Bindings between exchanges and queues
- **Connections**: Active connections from applications

**Useful actions:**
- **Purge queue**: Clear all messages (for testing)
- **Get messages**: View message payload without consuming
- **Move messages**: Transfer from error queue to original

### Checking Outbox Table

**View pending outbox messages:**
```sql
SELECT
    id,
    message_type,
    destination,
    status,
    scheduled_time,
    attempts
FROM wolverine.wolverine_outgoing_envelopes
ORDER BY scheduled_time DESC
LIMIT 50;
```

**View incoming (inbox) messages:**
```sql
SELECT
    id,
    message_type,
    status,
    received_at
FROM wolverine.wolverine_incoming_envelopes
ORDER BY received_at DESC
LIMIT 50;
```

**Clear stuck messages:**
```sql
-- WARNING: This may lose messages
DELETE FROM wolverine.wolverine_outgoing_envelopes WHERE status = 'error';
```

### Quick Diagnostic Commands

**Check all services:**
```bash
cd docker && docker compose ps
docker compose logs --tail=50
```

**Test database connectivity:**
```bash
docker exec wallow-postgres psql -U wallow -d wallow -c "SELECT 1"
```

**Test RabbitMQ connectivity:**
```bash
docker exec wallow-rabbitmq rabbitmq-diagnostics check_running
```

**Test Redis/Valkey connectivity:**
```bash
docker exec wallow-valkey valkey-cli ping
```

**View application logs:**
```bash
# If running via dotnet run
# Logs output to console

# If running in Docker
docker logs wallow-api --tail=100 -f
```

**Reset everything:**
```bash
cd docker && docker compose down -v && docker compose up -d
```

---

## Quick Reference: Error Code Mapping

| HTTP Status | Exception Type | Meaning |
|------------|----------------|---------|
| 400 | `ValidationException` | Invalid request data |
| 401 | `SecurityTokenException` | Authentication failed |
| 403 | `UnauthorizedAccessException` | Missing permission |
| 404 | `EntityNotFoundException` | Resource not found |
| 409 | `DbUpdateConcurrencyException` | Optimistic concurrency conflict |
| 422 | `BusinessRuleException` | Business rule violation |
| 500 | Unhandled exception | Server error |

---

## Getting Help

If you cannot resolve an issue:

1. **Check existing documentation:**
   - `docs/getting-started/developer-guide.md` - Development setup
   - `docs/operations/deployment.md` - Production deployment

2. **Search the codebase:**
   ```bash
   grep -r "error message" src/
   ```

3. **Check recent commits:**
   ```bash
   git log --oneline -20
   ```

4. **Run architecture tests:**
   ```bash
   dotnet test tests/Wallow.Architecture.Tests
   ```
