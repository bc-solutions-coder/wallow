# Database Scaling Guide

This guide covers scaling PostgreSQL for Wallow deployments using connection pooling with PgBouncer and read replicas for horizontal read scaling.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Understanding the Problem](#2-understanding-the-problem)
3. [Part 1: Connection Pooling with PgBouncer](#3-part-1-connection-pooling-with-pgbouncer)
4. [Part 2: Read Replicas](#4-part-2-read-replicas)
5. [Part 3: Combined Setup (PgBouncer + Replicas)](#5-part-3-combined-setup-pgbouncer--replicas)
6. [Monitoring and Alerting](#6-monitoring-and-alerting)
7. [Troubleshooting](#7-troubleshooting)
8. [Performance Tuning](#8-performance-tuning)

---

## 1. Overview

### Why the Database is Often the Bottleneck

In distributed systems, the database frequently becomes the first scaling bottleneck because:

1. **Stateful Nature**: Unlike stateless application servers that can be horizontally scaled trivially, databases maintain state and require careful coordination
2. **Connection Overhead**: Each PostgreSQL connection consumes approximately 5-10MB of memory for session state, prepared statements, and work memory
3. **Lock Contention**: Write operations require locks that can create contention under high concurrency
4. **I/O Bound Operations**: Complex queries may be limited by disk I/O rather than CPU or network

### PostgreSQL's Connection Limits

PostgreSQL's default `max_connections` is typically set between 100-200 connections. Each connection:

- Spawns a backend process (not a lightweight thread)
- Allocates memory for sorting, hashing, and query execution (`work_mem`)
- Maintains session-level caches and prepared statements
- Consumes file descriptors

Beyond a certain threshold (often 200-500 connections depending on hardware), PostgreSQL performance degrades significantly as the OS spends more time context-switching between processes than doing actual work.

### Read vs Write Scaling

| Scaling Type | Strategy | Use Case |
|--------------|----------|----------|
| **Connection Scaling** | PgBouncer | Many application instances, connection exhaustion |
| **Read Scaling** | Read Replicas | Read-heavy workloads, reporting, geographic distribution |
| **Write Scaling** | Sharding, Partitioning | Extremely high write throughput (beyond this guide's scope) |

Wallow's architecture naturally supports read/write splitting:
- **EF Core** handles writes (commands) to the primary database
- **Dapper** can be configured for complex read queries against replicas
- **Background jobs** (Hangfire) use the primary for job state management

---

## 2. Understanding the Problem

### The Connection Math Problem

Consider a typical production scenario:

```
PostgreSQL max_connections = 200

Application Configuration:
- 4 application instances (for high availability)
- 50 connections per EF Core pool (default)
- 20 connections per Dapper pool
- Background jobs (Hangfire): 10 connections
- Wolverine outbox: 10 connections

Calculation:
  4 instances x (50 EF + 20 Dapper + 10 Hangfire + 10 Wolverine)
= 4 x 90
= 360 connections needed

Result: 360 > 200 = Connection refused errors
```

### Why This Becomes Critical at Scale

1. **Horizontal Scaling Multiplier**: Each new application instance multiplies connection usage
2. **Kubernetes Autoscaling**: Pod autoscalers can spin up instances faster than you can increase `max_connections`
3. **Connection Storms**: Application restarts or deployments can cause all instances to reconnect simultaneously
4. **Memory Exhaustion**: If you simply increase `max_connections` to 1000, PostgreSQL will require 5-10GB just for connection overhead

### The Solution: External Connection Pooling

Instead of each application managing its own pool to the database, we introduce PgBouncer as an intermediary:

```
Before (Direct):
┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│ App 1   │     │ App 2   │     │ App 3   │     │ App 4   │
│ 90 conn │     │ 90 conn │     │ 90 conn │     │ 90 conn │
└────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
     │               │               │               │
     └───────────────┴───────┬───────┴───────────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   PostgreSQL    │
                    │  (200 max conn) │
                    │   ❌ EXCEEDED   │
                    └─────────────────┘

After (PgBouncer):
┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│ App 1   │     │ App 2   │     │ App 3   │     │ App 4   │
│ 90 conn │     │ 90 conn │     │ 90 conn │     │ 90 conn │
└────┬────┘     └────┬────┘     └────┬────┘     └────┬────┘
     │               │               │               │
     └───────────────┴───────┬───────┴───────────────┘
                             │ 360 client connections
                             ▼
                    ┌─────────────────┐
                    │   PgBouncer     │
                    │  (lightweight)  │
                    └────────┬────────┘
                             │ 50 server connections
                             ▼
                    ┌─────────────────┐
                    │   PostgreSQL    │
                    │  (200 max conn) │
                    │   ✅ OK         │
                    └─────────────────┘
```

---

## 3. Part 1: Connection Pooling with PgBouncer

### 3.1 What is PgBouncer

PgBouncer is a lightweight, single-threaded connection pooler for PostgreSQL. It sits between your application and PostgreSQL, multiplexing thousands of client connections to a smaller pool of actual database connections.

Key characteristics:
- **Extremely lightweight**: Uses ~2KB per connection (vs 5-10MB for PostgreSQL)
- **High throughput**: Single process can handle 10,000+ connections
- **Protocol-level proxy**: Speaks PostgreSQL wire protocol natively
- **Zero application changes**: Applications connect to PgBouncer as if it were PostgreSQL

### Pooling Modes Explained

| Mode | Description | Use Case | Wallow Compatibility |
|------|-------------|----------|----------------------|
| **Session** | Connection held for entire client session | Legacy apps needing session-level state | Full compatibility |
| **Transaction** | Connection returned after each transaction | Most web applications | **Recommended** |
| **Statement** | Connection returned after each statement | Read-only workloads | Limited (no multi-statement transactions) |

**Transaction pooling** is recommended for Wallow because:
- EF Core and Dapper both work well with transaction-level pooling
- Wolverine's outbox pattern commits within transactions
- Wolverine's outbox pattern commits within transactions

### 3.2 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  Application Layer                                               │
├─────────┬─────────┬─────────┬─────────┬─────────┬───────────────┤
│ Wallow │ Wallow │ Wallow │ Wallow │ Hangfire│ Other         │
│ API #1  │ API #2  │ API #3  │ API #4  │ Worker  │ Services      │
│         │         │         │         │         │               │
│ EF Core │ EF Core │ EF Core │ EF Core │ EF Core │ EF Core       │
│ Dapper  │ Dapper  │ Dapper  │ Dapper  │         │ Dapper        │
│ Wolvnr  │ Marten  │ Wolvnr  │ Marten  │         │               │
└────┬────┴────┬────┴────┬────┴────┬────┴────┬────┴───────┬───────┘
     │         │         │         │         │            │
     │         │         │         │         │            │
     └─────────┴─────────┴────┬────┴─────────┴────────────┘
                              │
                              │ Client Connections
                              │ (potentially thousands)
                              │ Port 6432
                              ▼
              ┌───────────────────────────────┐
              │         PgBouncer             │
              │                               │
              │  • Pool size: 50-100          │
              │  • Mode: transaction          │
              │  • Lightweight multiplexing   │
              └───────────────┬───────────────┘
                              │
                              │ Server Connections
                              │ (limited pool)
                              │ Port 5432
                              ▼
              ┌───────────────────────────────┐
              │        PostgreSQL             │
              │                               │
              │  • max_connections: 200       │
              │  • Reserved: ~50 for admin    │
              │  • Available: ~150            │
              └───────────────────────────────┘
```

### 3.3 Installation and Setup

#### Docker Setup

Add PgBouncer to your `docker-compose.yml`:

```yaml
services:
  # ... existing postgres service ...

  pgbouncer:
    image: bitnami/pgbouncer:1.23.0
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-pgbouncer
    environment:
      # Basic configuration
      PGBOUNCER_DATABASE: ${POSTGRES_DB}
      PGBOUNCER_PORT: 6432
      PGBOUNCER_BIND_ADDRESS: 0.0.0.0

      # Authentication
      POSTGRESQL_HOST: postgres
      POSTGRESQL_PORT: 5432
      POSTGRESQL_USERNAME: ${POSTGRES_USER}
      POSTGRESQL_PASSWORD: ${POSTGRES_PASSWORD}

      # Pool configuration
      PGBOUNCER_POOL_MODE: transaction
      PGBOUNCER_DEFAULT_POOL_SIZE: 50
      PGBOUNCER_MIN_POOL_SIZE: 10
      PGBOUNCER_RESERVE_POOL_SIZE: 10
      PGBOUNCER_MAX_CLIENT_CONN: 1000
      PGBOUNCER_MAX_DB_CONNECTIONS: 100

      # Timeouts
      PGBOUNCER_SERVER_IDLE_TIMEOUT: 600
      PGBOUNCER_CLIENT_IDLE_TIMEOUT: 0
      PGBOUNCER_QUERY_TIMEOUT: 0

      # Logging
      PGBOUNCER_LOG_CONNECTIONS: 1
      PGBOUNCER_LOG_DISCONNECTIONS: 1
      PGBOUNCER_LOG_POOLER_ERRORS: 1

      # Stats access
      PGBOUNCER_STATS_USERS: ${POSTGRES_USER}
      PGBOUNCER_ADMIN_USERS: ${POSTGRES_USER}
    ports:
      - "6432:6432"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "pg_isready", "-h", "localhost", "-p", "6432", "-U", "${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped
```

#### Manual PgBouncer Configuration

For production deployments or more control, use a custom `pgbouncer.ini`:

```ini
;; pgbouncer.ini - Production Configuration for Wallow
;; Place this file at: /etc/pgbouncer/pgbouncer.ini

;;; Database Configuration ;;;
[databases]
; Format: dbname = host=... port=... dbname=... user=... password=...
; Application database - connects to PostgreSQL primary
wallow = host=postgres port=5432 dbname=wallow

; You can define multiple databases pointing to different servers
; wallow_readonly = host=postgres-replica port=5432 dbname=wallow

; Keycloak database (if needed)
keycloak_db = host=postgres port=5432 dbname=keycloak_db

; Wildcard fallback - any database name not listed above
; * = host=postgres port=5432

;;; PgBouncer Settings ;;;
[pgbouncer]
; Network settings
listen_addr = 0.0.0.0
listen_port = 6432

; Authentication file (see userlist.txt below)
auth_file = /etc/pgbouncer/userlist.txt

; Authentication type:
; - md5: Password hashed with MD5 (legacy, but widely supported)
; - scram-sha-256: Modern, more secure (requires PostgreSQL 10+)
auth_type = scram-sha-256

; Query for loading user passwords from database (alternative to auth_file)
; auth_query = SELECT usename, passwd FROM pg_shadow WHERE usename=$1

;;; Pool Mode ;;;
; session    - Connection held for entire client session
; transaction - Connection returned after each transaction (RECOMMENDED)
; statement  - Connection returned after each statement (limited use)
pool_mode = transaction

;;; Pool Sizing ;;;
; Maximum number of client connections allowed
max_client_conn = 1000

; Default pool size per user/database pair
default_pool_size = 50

; Minimum pool size maintained even when idle
min_pool_size = 10

; Extra connections allowed when pool is exhausted
reserve_pool_size = 10

; Timeout for reserve pool connections to be used (seconds)
reserve_pool_timeout = 5

; Maximum connections per database (across all pools)
max_db_connections = 100

; Maximum connections per user (across all pools)
max_user_connections = 100

;;; Timeouts ;;;
; Server connection idle timeout (seconds)
; Connections idle longer than this are closed
server_idle_timeout = 600

; Time to wait for a connection from pool (seconds)
; 0 = wait indefinitely
query_wait_timeout = 120

; Client idle timeout (seconds)
; 0 = disabled (don't close idle clients)
client_idle_timeout = 0

; Maximum time queries can run (seconds)
; 0 = disabled
query_timeout = 0

; How long to wait when connecting to server
server_connect_timeout = 15

; How long to wait for login to complete
server_login_retry = 15

; Close server connection after this many seconds
; Useful for avoiding stuck connections
server_lifetime = 3600

;;; Connection Handling ;;;
; Close server connection if client disconnects in transaction
; "on" can lead to server-side transaction leaks if set incorrectly
cancel_wait_timeout = 10

; Application name to set on server connections
application_name_add_host = 1

;;; TLS Configuration (for production) ;;;
; Enable TLS for client connections
; client_tls_sslmode = prefer
; client_tls_key_file = /etc/pgbouncer/pgbouncer.key
; client_tls_cert_file = /etc/pgbouncer/pgbouncer.crt

; TLS for server connections (to PostgreSQL)
; server_tls_sslmode = prefer
; server_tls_ca_file = /etc/pgbouncer/root.crt

;;; Logging ;;;
; Log file location (or use syslog)
logfile = /var/log/pgbouncer/pgbouncer.log

; Log level: debug, info, notice, warning, error
log_level = info

; What to log
log_connections = 1
log_disconnections = 1
log_pooler_errors = 1

; Prefix for log lines
; %d = database, %u = user, %p = PID, %a = application_name
log_prefix = %t [%p] <%d,%u>

;;; Administrative Access ;;;
; Users allowed to connect to admin database (pgbouncer virtual db)
admin_users = postgres,wallow

; Users allowed to run SHOW commands
stats_users = postgres,wallow,monitoring

;;; Unix Socket (optional) ;;;
; unix_socket_dir = /var/run/pgbouncer
; unix_socket_mode = 0777
```

#### userlist.txt Configuration

PgBouncer needs to know how to authenticate users. Create `userlist.txt`:

```txt
;; userlist.txt - PgBouncer Authentication File
;; Format: "username" "password_hash"
;;
;; Password hash formats:
;; - Plain text (NOT recommended): "user" "plainpassword"
;; - MD5: "user" "md5<32-char-hash>"
;; - SCRAM-SHA-256: "user" "SCRAM-SHA-256$<iterations>:<salt>$<stored-key>:<server-key>"

;; To generate MD5 hash:
;; echo -n "passwordusername" | md5sum
;; Then prefix with "md5"

;; To get SCRAM hash from PostgreSQL:
;; SELECT rolname, rolpassword FROM pg_authid WHERE rolname = 'wallow';

;; Example entries:
"wallow" "SCRAM-SHA-256$4096:abc123...$StoredKey:ServerKey"
"readonly" "SCRAM-SHA-256$4096:def456...$StoredKey:ServerKey"

;; For development only - plain text (NEVER use in production):
; "wallow" "your-password-here"
```

**Generating password hashes:**

```bash
# Get SCRAM-SHA-256 hash from PostgreSQL (recommended)
docker exec wallow-postgres psql -U postgres -c \
  "SELECT rolname, rolpassword FROM pg_authid WHERE rolname = 'wallow';"

# Or generate MD5 hash manually
echo -n "your-password-here""wallow" | md5sum | cut -d' ' -f1 | sed 's/^/md5/'
```

### 3.4 Configuring Wallow to Use PgBouncer

#### Connection String Changes

Update your connection strings to point to PgBouncer instead of PostgreSQL directly:

```json
{
  "ConnectionStrings": {
    // Before: Direct PostgreSQL connection
    // "DefaultConnection": "Host=postgres;Port=5432;Database=wallow;Username=wallow;Password=..."

    // After: Through PgBouncer
    "DefaultConnection": "Host=pgbouncer;Port=6432;Database=wallow;Username=wallow;Password=...;Pooling=false;Enlist=false"
  }
}
```

**Critical connection string parameters:**

| Parameter | Value | Explanation |
|-----------|-------|-------------|
| `Pooling=false` | Disable | Npgsql's internal pooling is redundant with PgBouncer |
| `Enlist=false` | Disable | Prevents distributed transaction enrollment (not supported in transaction mode) |
| `No Reset On Close=true` | Optional | Prevents DISCARD ALL on connection return |
| `Command Timeout=120` | Adjust | May need longer timeout if waiting for connection from pool |

#### EF Core Configuration

In your infrastructure extensions, ensure EF Core is configured correctly:

```csharp
// Example: Billing module with PgBouncer-compatible settings
public static IServiceCollection AddBillingPersistence(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    services.AddDbContext<BillingDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");

            // Important for PgBouncer transaction pooling:
            // Disable connection reset to prevent DISCARD ALL commands
            // that aren't compatible with transaction pooling
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

        options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
    });

    return services;
}
```

#### Dapper Configuration

For Dapper queries, ensure connections are properly configured:

```csharp
public interface ISqlConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public class PgBouncerConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public PgBouncerConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public NpgsqlConnection CreateConnection()
    {
        // Create connection with PgBouncer-friendly settings
        var connection = new NpgsqlConnection(_connectionString);

        // Don't use NpgsqlConnection.Open() with Pooling=true
        // PgBouncer handles the pooling
        return connection;
    }
}

// Registration
services.AddSingleton<ISqlConnectionFactory, PgBouncerConnectionFactory>();

// Usage in query handler
public class GetItemsHandler
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public GetItemsHandler(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<InvoiceDto>> Handle(
        GetInvoicesQuery query,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        return await connection.QueryAsync<InvoiceDto>(
            """
            SELECT id, invoice_number, status, total_amount, currency
            FROM billing.invoices
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new { query.TenantId, query.PageSize, Offset = query.Page * query.PageSize }
        );
    }
}
```

#### Important: Prepared Statements and Transaction Pooling

**Problem**: PostgreSQL prepared statements are session-scoped. With transaction pooling, your next query might go to a different backend connection that doesn't have your prepared statements.

**Solution**: Disable prepared statement caching or use `Multiplexing=false`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=pgbouncer;Port=6432;Database=wallow;Username=wallow;Password=...;Pooling=false;No Reset On Close=true;Multiplexing=false;Write Buffer Size=32768"
  }
}
```

**Alternative**: Use `Simple` query mode in Npgsql:

```csharp
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

// Force simple query protocol (no prepared statements)
await using var cmd = new NpgsqlCommand("SELECT * FROM items WHERE id = $1", connection)
{
    Parameters = { new() { Value = itemId } }
};

// Or globally via connection string:
// "...;Options=-c default_query_mode=simple"
```

### 3.5 Monitoring PgBouncer

#### SHOW Commands

Connect to the PgBouncer admin database:

```bash
# Connect to PgBouncer admin interface
psql -h localhost -p 6432 -U wallow pgbouncer
```

Useful monitoring commands:

```sql
-- Show overall statistics
SHOW STATS;

-- Show pool status (connections per database/user)
SHOW POOLS;

-- Show active client connections
SHOW CLIENTS;

-- Show active server connections
SHOW SERVERS;

-- Show database configuration
SHOW DATABASES;

-- Show configuration
SHOW CONFIG;

-- Show memory usage
SHOW MEM;

-- Show connection counts over time
SHOW STATS_TOTALS;

-- Show averages
SHOW STATS_AVERAGES;
```

#### Key Metrics to Watch

| Metric | Source | Alert Threshold | Meaning |
|--------|--------|-----------------|---------|
| `cl_active` | SHOW POOLS | > 80% of max_client_conn | Active client connections |
| `cl_waiting` | SHOW POOLS | > 0 for extended periods | Clients waiting for server connection |
| `sv_active` | SHOW POOLS | > 90% of default_pool_size | Active server connections |
| `sv_idle` | SHOW POOLS | < 2 | May need larger min_pool_size |
| `avg_query_time` | SHOW STATS | > baseline | Query performance degradation |
| `total_wait_time` | SHOW STATS | Increasing | Clients waiting too long |

#### Prometheus Metrics Export

Use pgbouncer_exporter for Prometheus integration:

```yaml
# Add to docker-compose.yml
pgbouncer-exporter:
  image: prometheuscommunity/pgbouncer-exporter:latest
  container_name: ${COMPOSE_PROJECT_NAME:-wallow}-pgbouncer-exporter
  environment:
    PGBOUNCER_EXPORTER_HOST: pgbouncer
    PGBOUNCER_EXPORTER_PORT: 6432
    PGBOUNCER_EXPORTER_USER: ${POSTGRES_USER}
    PGBOUNCER_EXPORTER_PASS: ${POSTGRES_PASSWORD}
  ports:
    - "9127:9127"
  depends_on:
    - pgbouncer
  networks:
    - wallow
```

#### Grafana Dashboard

Create a dashboard with these panels:

```json
{
  "title": "PgBouncer Overview",
  "panels": [
    {
      "title": "Client Connections",
      "targets": [
        { "expr": "pgbouncer_pools_client_active_connections" },
        { "expr": "pgbouncer_pools_client_waiting_connections" }
      ]
    },
    {
      "title": "Server Connections",
      "targets": [
        { "expr": "pgbouncer_pools_server_active_connections" },
        { "expr": "pgbouncer_pools_server_idle_connections" }
      ]
    },
    {
      "title": "Query Time (avg)",
      "targets": [
        { "expr": "rate(pgbouncer_stats_total_query_time_seconds[5m]) / rate(pgbouncer_stats_total_query_count[5m])" }
      ]
    },
    {
      "title": "Wait Time",
      "targets": [
        { "expr": "rate(pgbouncer_stats_total_wait_time_seconds[5m])" }
      ]
    }
  ]
}
```

---

## 4. Part 2: Read Replicas

### 4.1 When You Need Read Replicas

Read replicas are valuable when:

1. **Read-Heavy Workloads**: Reports, dashboards, analytics, search queries consume significant resources
2. **Geographic Distribution**: Users in different regions benefit from lower latency reads
3. **Offloading Primary**: Complex aggregation queries shouldn't impact write performance
4. **High Availability**: Replicas can be promoted to primary during failures

**Typical read/write ratios by feature:**

| Feature | Read % | Write % | Replica Candidate |
|---------|--------|---------|-------------------|
| Dashboard/Reports | 95% | 5% | Excellent |
| Search/Filtering | 90% | 10% | Excellent |
| User Sessions | 70% | 30% | Good |
| Invoice Processing | 40% | 60% | Limited |
| File Uploads | 30% | 70% | Metadata reads only |

### 4.2 PostgreSQL Streaming Replication Setup

#### Overview

PostgreSQL streaming replication continuously ships WAL (Write-Ahead Log) records from primary to replicas. The replica applies these changes, maintaining a near-real-time copy.

```
┌─────────────────────┐
│      Primary        │
│                     │
│  ┌───────────────┐  │
│  │ WAL Sender    │──┼──── WAL Stream ────┐
│  └───────────────┘  │                    │
└─────────────────────┘                    │
                                           │
                      ┌────────────────────▼─────────────────────┐
                      │                                          │
        ┌─────────────┴─────────────┐      ┌─────────────────────┴────────────┐
        │        Replica 1          │      │         Replica 2                │
        │                           │      │                                  │
        │  ┌───────────────────┐    │      │  ┌───────────────────┐           │
        │  │ WAL Receiver      │    │      │  │ WAL Receiver      │           │
        │  └───────────────────┘    │      │  └───────────────────┘           │
        │  ┌───────────────────┐    │      │  ┌───────────────────┐           │
        │  │ Startup Process   │    │      │  │ Startup Process   │           │
        │  │ (applies WAL)     │    │      │  │ (applies WAL)     │           │
        │  └───────────────────┘    │      │  └───────────────────┘           │
        └───────────────────────────┘      └──────────────────────────────────┘
```

#### Primary Configuration

Edit `postgresql.conf` on the primary server:

```ini
# postgresql.conf - Primary Server Configuration for Streaming Replication

#------------------------------------------------------------------------------
# CONNECTIONS AND AUTHENTICATION
#------------------------------------------------------------------------------
listen_addresses = '*'              # Listen on all interfaces
max_connections = 200               # Tune based on your needs

#------------------------------------------------------------------------------
# WRITE-AHEAD LOG (WAL)
#------------------------------------------------------------------------------
wal_level = replica                 # Required for replication (minimal, replica, logical)
max_wal_senders = 10                # Max number of concurrent streaming connections
max_replication_slots = 10          # Max number of replication slots

# WAL archiving (optional but recommended for point-in-time recovery)
archive_mode = on
archive_command = 'cp %p /var/lib/postgresql/wal_archive/%f'

# WAL retention
wal_keep_size = 1GB                 # Keep this much WAL for lagging replicas
                                    # Alternative: wal_keep_segments = 64 (older PostgreSQL)

#------------------------------------------------------------------------------
# REPLICATION
#------------------------------------------------------------------------------
# Synchronous replication (optional - impacts write latency)
# synchronous_commit = on           # Wait for WAL to reach replica
# synchronous_standby_names = 'replica1'  # Name(s) of synchronous standby(s)

# Hot standby (allow queries on replica)
hot_standby = on

# Replication slot recommended for reliable replication
# Created manually: SELECT pg_create_physical_replication_slot('replica1_slot');

#------------------------------------------------------------------------------
# LOGGING
#------------------------------------------------------------------------------
log_replication_commands = on       # Log replication-related commands
log_min_messages = info

#------------------------------------------------------------------------------
# CHECKPOINTS
#------------------------------------------------------------------------------
checkpoint_timeout = 10min          # Max time between checkpoints
checkpoint_completion_target = 0.9  # Spread checkpoint over this fraction
max_wal_size = 4GB                  # Max WAL size before checkpoint triggered
min_wal_size = 1GB                  # Min WAL to retain
```

#### pg_hba.conf Configuration

Add replication access for replica servers:

```
# pg_hba.conf - PostgreSQL Client Authentication Configuration

# TYPE  DATABASE        USER            ADDRESS                 METHOD

# Local connections
local   all             all                                     peer

# IPv4 local connections
host    all             all             127.0.0.1/32            scram-sha-256

# IPv6 local connections
host    all             all             ::1/128                 scram-sha-256

# Application connections (adjust network as needed)
host    all             all             10.0.0.0/8              scram-sha-256
host    all             all             172.16.0.0/12           scram-sha-256
host    all             all             192.168.0.0/16          scram-sha-256

# Replication connections from replica servers
# Format: host replication <user> <replica-ip>/32 <auth-method>
host    replication     replicator      10.0.1.0/24             scram-sha-256
host    replication     replicator      192.168.1.100/32        scram-sha-256
host    replication     replicator      192.168.1.101/32        scram-sha-256

# For Docker networks (adjust based on your network configuration)
host    replication     replicator      172.17.0.0/16           scram-sha-256
```

#### Create Replication User and Slot

On the primary:

```bash
# Connect to PostgreSQL
docker exec -it wallow-postgres psql -U postgres

# Create replication user
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'strong-replication-password';

# Create replication slot (prevents WAL deletion before replica catches up)
SELECT pg_create_physical_replication_slot('replica1_slot');

# Verify slot creation
SELECT slot_name, slot_type, active FROM pg_replication_slots;

# Grant necessary permissions
GRANT USAGE ON SCHEMA public TO replicator;
```

#### Replica Setup

**Step 1: Prepare replica server**

```bash
# On replica server
# Stop PostgreSQL if running
sudo systemctl stop postgresql

# Clear existing data directory
sudo rm -rf /var/lib/postgresql/16/main/*
```

**Step 2: Take base backup from primary**

```bash
# On replica server
# Use pg_basebackup to clone the primary
pg_basebackup \
  -h primary-host \
  -D /var/lib/postgresql/16/main \
  -U replicator \
  -P \
  -v \
  -R \
  -X stream \
  -C \
  -S replica1_slot

# Explanation of flags:
# -h: Primary hostname
# -D: Data directory
# -U: Replication user
# -P: Show progress
# -v: Verbose
# -R: Create standby.signal and configure recovery settings
# -X stream: Stream WAL during backup
# -C: Create replication slot (-S specifies name)
# -S: Replication slot name

# Set ownership
sudo chown -R postgres:postgres /var/lib/postgresql/16/main
sudo chmod 700 /var/lib/postgresql/16/main
```

**Step 3: Configure replica**

The `-R` flag creates `standby.signal` and adds connection info to `postgresql.auto.conf`. Verify:

```bash
# Check standby.signal exists
ls -la /var/lib/postgresql/16/main/standby.signal

# View auto-generated configuration
cat /var/lib/postgresql/16/main/postgresql.auto.conf
```

Expected content in `postgresql.auto.conf`:

```ini
# Automatically generated by pg_basebackup
primary_conninfo = 'user=replicator password=strong-replication-password channel_binding=prefer host=primary-host port=5432 sslmode=prefer sslcompression=0 sslcertmode=allow sslsni=1 ssl_min_protocol_version=TLSv1.2 gssencmode=prefer krbsrvname=postgres gssdelegation=0 target_session_attrs=any load_balance_hosts=disable'
primary_slot_name = 'replica1_slot'
```

**Step 4: Start replica**

```bash
# Start PostgreSQL
sudo systemctl start postgresql

# Verify replication status
sudo -u postgres psql -c "SELECT * FROM pg_stat_wal_receiver;"

# Check if in recovery mode
sudo -u postgres psql -c "SELECT pg_is_in_recovery();"
# Should return: t (true)
```

**Step 5: Verify on primary**

```bash
# On primary - check replication status
docker exec wallow-postgres psql -U postgres -c "
SELECT
    client_addr,
    state,
    sent_lsn,
    write_lsn,
    flush_lsn,
    replay_lsn,
    pg_wal_lsn_diff(sent_lsn, replay_lsn) AS replication_lag_bytes
FROM pg_stat_replication;
"
```

### 4.3 Configuring Wallow for Read/Write Splitting

#### Strategy 1: Manual Split (Recommended)

Wallow's architecture naturally supports read/write splitting because:
- **EF Core** is used for write operations (commands)
- **Dapper** can be configured for read operations (queries)
- **Dapper** read queries can optionally use replicas

**Step 1: Define multiple connection strings**

```json
{
  "ConnectionStrings": {
    "Primary": "Host=pgbouncer-primary;Port=6432;Database=wallow;Username=wallow;Password=...;Pooling=false",
    "Replica": "Host=pgbouncer-replica;Port=6432;Database=wallow;Username=readonly;Password=...;Pooling=false"
  }
}
```

**Step 2: Create read-only connection factory**

```csharp
public interface IReadOnlyConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public interface IReadWriteConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public class ReadOnlyConnectionFactory : IReadOnlyConnectionFactory
{
    private readonly string _connectionString;

    public ReadOnlyConnectionFactory(IConfiguration configuration)
    {
        // Use replica connection string for reads
        _connectionString = configuration.GetConnectionString("Replica")
            ?? configuration.GetConnectionString("Primary") // Fallback to primary
            ?? throw new InvalidOperationException("No connection string configured");
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}

public class ReadWriteConnectionFactory : IReadWriteConnectionFactory
{
    private readonly string _connectionString;

    public ReadWriteConnectionFactory(IConfiguration configuration)
    {
        // Always use primary for writes
        _connectionString = configuration.GetConnectionString("Primary")
            ?? throw new InvalidOperationException("Primary connection string required");
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
```

**Step 3: Register in DI**

```csharp
public static IServiceCollection AddDatabaseConnections(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Read-only factory for Dapper queries
    services.AddSingleton<IReadOnlyConnectionFactory, ReadOnlyConnectionFactory>();

    // Read-write factory for commands that need raw SQL
    services.AddSingleton<IReadWriteConnectionFactory, ReadWriteConnectionFactory>();

    // EF Core always uses primary
    services.AddDbContext<BillingDbContext>((sp, options) =>
    {
        var connectionString = configuration.GetConnectionString("Primary");
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
        });
    });

    return services;
}
```

**Step 4: Use in query handlers**

```csharp
// Read handler - uses replica
public class GetInvoicesByTenantHandler
{
    private readonly IReadOnlyConnectionFactory _connectionFactory;

    public GetInvoicesByTenantHandler(IReadOnlyConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<InvoiceDto>> Handle(
        GetInvoicesByTenantQuery query,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        return await connection.QueryAsync<InvoiceDto>(
            """
            SELECT id, number, status, total_amount, currency, created_at
            FROM billing.invoices
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new { query.TenantId, query.PageSize, Offset = query.Page * query.PageSize }
        );
    }
}

// Write handler - uses primary via EF Core
public class CreateInvoiceHandler
{
    private readonly BillingDbContext _dbContext;

    public CreateInvoiceHandler(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand command,
        CancellationToken ct)
    {
        var invoice = Invoice.Create(
            command.TenantId,
            command.UserId,
            command.LineItems
        );

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(invoice.ToDto());
    }
}
```

#### Strategy 2: Connection String Routing with Npgsql

Npgsql 7+ supports target session attributes for automatic routing:

```json
{
  "ConnectionStrings": {
    "Database": "Host=primary,replica1,replica2;Port=5432;Database=wallow;Username=wallow;Password=...;Target Session Attributes=prefer-standby;Load Balance Hosts=true"
  }
}
```

**Target Session Attributes:**

| Value | Behavior |
|-------|----------|
| `any` | Connect to first available host |
| `primary` | Only connect to primary (read-write) |
| `standby` | Only connect to standby (read-only) |
| `prefer-primary` | Prefer primary, fall back to standby |
| `prefer-standby` | Prefer standby, fall back to primary |
| `read-write` | Connect to server accepting writes |
| `read-only` | Connect to read-only server |

**Implementation:**

```csharp
public class SmartConnectionFactory
{
    private readonly string _primaryConnectionString;
    private readonly string _readOnlyConnectionString;

    public SmartConnectionFactory(IConfiguration configuration)
    {
        var baseConnectionString = configuration.GetConnectionString("Database");

        // Parse and modify for different modes
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);

        // Primary connection (writes)
        builder.TargetSessionAttributes = "primary";
        _primaryConnectionString = builder.ToString();

        // Replica connection (reads)
        builder.TargetSessionAttributes = "prefer-standby";
        builder.LoadBalanceHosts = true;
        _readOnlyConnectionString = builder.ToString();
    }

    public NpgsqlConnection CreateReadOnlyConnection()
    {
        return new NpgsqlConnection(_readOnlyConnectionString);
    }

    public NpgsqlConnection CreateReadWriteConnection()
    {
        return new NpgsqlConnection(_primaryConnectionString);
    }
}
```

#### Strategy 3: PgBouncer with Multiple Databases

Configure PgBouncer to route based on database name:

```ini
[databases]
# Write operations - connects to primary
wallow = host=primary port=5432 dbname=wallow

# Read operations - connects to replica
wallow_readonly = host=replica port=5432 dbname=wallow

# Load balanced reads across multiple replicas
wallow_reads = host=replica1,replica2 port=5432 dbname=wallow pool_mode=transaction
```

**Application configuration:**

```json
{
  "ConnectionStrings": {
    "Primary": "Host=pgbouncer;Port=6432;Database=wallow;Username=wallow;Password=...",
    "Replica": "Host=pgbouncer;Port=6432;Database=wallow_readonly;Username=readonly;Password=..."
  }
}
```

### 4.4 Handling Replication Lag

#### What is Replication Lag

Replication lag is the delay between when a transaction commits on the primary and when it's visible on replicas. Typical lag ranges from:
- **Synchronous replication**: 0ms (but impacts write latency)
- **Asynchronous replication**: 10-100ms (normal conditions)
- **Under load**: 100ms - several seconds

#### When Replication Lag Matters

The classic problem is **read-your-writes consistency**:

```
1. User creates invoice on primary
2. Response returns immediately
3. User's next request reads from replica
4. Invoice not yet visible (still replicating)
5. User sees "Invoice not found" error
```

#### Strategies to Handle Replication Lag

**Strategy 1: Route recent writes to primary (Session Affinity)**

```csharp
public class ConnectionRouter
{
    private readonly IReadOnlyConnectionFactory _readOnlyFactory;
    private readonly IReadWriteConnectionFactory _readWriteFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConnectionRouter(
        IReadOnlyConnectionFactory readOnlyFactory,
        IReadWriteConnectionFactory readWriteFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _readOnlyFactory = readOnlyFactory;
        _readWriteFactory = readWriteFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public NpgsqlConnection GetConnectionForRead()
    {
        // Check if user has recent writes (within lag tolerance)
        var context = _httpContextAccessor.HttpContext;
        var lastWriteTime = context?.Items["LastWriteTime"] as DateTime?;

        if (lastWriteTime.HasValue &&
            DateTime.UtcNow - lastWriteTime.Value < TimeSpan.FromSeconds(5))
        {
            // Recent write - use primary to ensure read-your-writes
            return _readWriteFactory.CreateConnection();
        }

        // No recent writes - safe to use replica
        return _readOnlyFactory.CreateConnection();
    }
}

// Middleware to track writes
public class WriteTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // If this was a write operation, record the timestamp
        if (context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            // Could also use a cookie or session
            context.Response.Cookies.Append(
                "last_write",
                DateTime.UtcNow.Ticks.ToString(),
                new CookieOptions { MaxAge = TimeSpan.FromSeconds(30) }
            );
        }
    }
}
```

**Strategy 2: Explicit primary reads for specific operations**

```csharp
public interface IQueryHandler<TQuery, TResult>
{
    bool RequiresPrimaryRead { get; }
    Task<TResult> Handle(TQuery query, CancellationToken ct);
}

// For queries that need fresh data
public class GetInvoiceByIdHandler : IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    // This query might be called right after creation
    public bool RequiresPrimaryRead => true;

    // Implementation uses primary connection
}

// For dashboard/reporting queries
public class GetInvoiceSummaryHandler : IQueryHandler<GetInvoiceSummaryQuery, SummaryDto>
{
    // Dashboard can tolerate slight lag
    public bool RequiresPrimaryRead => false;

    // Implementation uses replica connection
}
```

**Strategy 3: Monitor and wait for replication**

```csharp
public class ReplicationAwareConnectionFactory
{
    private readonly string _primaryConnectionString;
    private readonly string _replicaConnectionString;

    public async Task<NpgsqlConnection> GetConsistentReadConnection(
        string lastWriteLsn,
        TimeSpan maxWait,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(lastWriteLsn))
        {
            // No LSN to wait for - use replica immediately
            return new NpgsqlConnection(_replicaConnectionString);
        }

        // Try to wait for replica to catch up
        var replica = new NpgsqlConnection(_replicaConnectionString);
        await replica.OpenAsync(ct);

        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < maxWait)
        {
            var replicaLsn = await replica.ExecuteScalarAsync<string>(
                "SELECT pg_last_wal_replay_lsn()::text", ct);

            // Compare LSN positions
            if (CompareLsn(replicaLsn, lastWriteLsn) >= 0)
            {
                // Replica has caught up
                return replica;
            }

            await Task.Delay(10, ct);
        }

        // Replica too far behind - fall back to primary
        await replica.DisposeAsync();
        return new NpgsqlConnection(_primaryConnectionString);
    }

    private int CompareLsn(string lsn1, string lsn2)
    {
        // LSN format: "X/XXXXXXXX"
        // Parse and compare numerically
        var parts1 = lsn1.Split('/');
        var parts2 = lsn2.Split('/');

        var high1 = Convert.ToInt64(parts1[0], 16);
        var high2 = Convert.ToInt64(parts2[0], 16);

        if (high1 != high2) return high1.CompareTo(high2);

        var low1 = Convert.ToInt64(parts1[1], 16);
        var low2 = Convert.ToInt64(parts2[1], 16);

        return low1.CompareTo(low2);
    }
}
```

**Strategy 4: Synchronous replication (trade-off)**

For critical data where you cannot tolerate any lag:

```ini
# postgresql.conf on primary
synchronous_commit = on
synchronous_standby_names = 'replica1'
```

**Trade-offs:**
- Pros: Zero lag, guaranteed consistency
- Cons: Write latency increases (must wait for replica acknowledgment)
- Risk: If replica goes down, writes block until timeout

**Compromise - Quorum synchronous replication:**

```ini
# Require acknowledgment from any 1 of 3 replicas
synchronous_standby_names = 'ANY 1 (replica1, replica2, replica3)'
```

### 4.5 Event Store Considerations (If Added)

If you add event sourcing (e.g., Marten) to any module, events are the source of truth and must be written to the primary:

```csharp
public static IServiceCollection AddEventSourcedModule(
    this IServiceCollection services,
    IConfiguration config)
{
    // Event store connection - ALWAYS primary
    var primaryConnectionString = config.GetConnectionString("Primary")!;

    services.AddMarten(opts =>
    {
        // Events must go to primary
        opts.Connection(primaryConnectionString);
        opts.DatabaseSchemaName = "my_module";

        // Event metadata
        opts.Events.MetadataConfig.CausationIdEnabled = true;
        opts.Events.MetadataConfig.CorrelationIdEnabled = true;

        // Inline projections - also use primary
        opts.Projections.Snapshot<MyStateProjection>(SnapshotLifecycle.Inline);
    })
    .IntegrateWithWolverine()
    .UseLightweightSessions();

    return services;
}
```

#### Projections: Can Use Replicas (Async Only)

For async projections that can tolerate lag:

```csharp
public static IServiceCollection AddEventSourcedReadModel(
    this IServiceCollection services,
    IConfiguration config)
{
    var replicaConnectionString = config.GetConnectionString("Replica")!;

    // Read-only document store for projections
    services.AddMarten(opts =>
    {
        opts.Connection(replicaConnectionString);
        opts.DatabaseSchemaName = "my_module_read";

        // Configure as read-only
        opts.AutoCreateSchemaObjects = AutoCreate.None;

        // Async projections can read from replica
        opts.Projections.Snapshot<MyStateProjection>(SnapshotLifecycle.Async);
    })
    .OptimizeArtifactWorkflow()
    .UseLightweightSessions();

    return services;
}
```

#### Dual Store Pattern

For production with replicas, consider separate stores:

```csharp
public interface IEventStore
{
    // Write operations
    Task<long> AppendEventsAsync<T>(Guid streamId, IEnumerable<object> events, CancellationToken ct);
}

public interface IReadStore
{
    // Read operations
    Task<T?> LoadAsync<T>(Guid id, CancellationToken ct) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
}

public class MartenEventStore : IEventStore
{
    private readonly IDocumentSession _primarySession;

    public MartenEventStore(IDocumentStore primaryStore)
    {
        _primarySession = primaryStore.LightweightSession();
    }

    public async Task<long> AppendEventsAsync<T>(
        Guid streamId,
        IEnumerable<object> events,
        CancellationToken ct)
    {
        _primarySession.Events.Append(streamId, events.ToArray());
        await _primarySession.SaveChangesAsync(ct);
        return _primarySession.Events.FetchStreamState(streamId)?.Version ?? 0;
    }
}

public class MartenReadStore : IReadStore
{
    private readonly IQuerySession _replicaSession;

    public MartenReadStore(IDocumentStore replicaStore)
    {
        _replicaSession = replicaStore.QuerySession();
    }

    public async Task<T?> LoadAsync<T>(Guid id, CancellationToken ct) where T : class
    {
        return await _replicaSession.LoadAsync<T>(id, ct);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct) where T : class
    {
        return await _replicaSession.Query<T>().Where(predicate).ToListAsync(ct);
    }
}
```

---

## 5. Part 3: Combined Setup (PgBouncer + Replicas)

### 5.1 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Application Layer                                                           │
├─────────┬─────────┬─────────┬─────────┬─────────────────────────────────────┤
│ Wallow │ Wallow │ Wallow │ Wallow │     Background Workers              │
│ API #1  │ API #2  │ API #3  │ API #4  │     (Hangfire, etc.)                │
│         │         │         │         │                                     │
│ Writes  │ Writes  │ Writes  │ Writes  │         Writes                      │
│ (EF)────┼─────────┼─────────┼─────────┼──────────────────┐                  │
│         │         │         │         │                  │                  │
│ Reads   │ Reads   │ Reads   │ Reads   │         Reads    │                  │
│(Dapper)─┼─────────┼─────────┼─────────┼───────┐          │                  │
└────┬────┴────┬────┴────┬────┴────┬────┴───────┼──────────┼──────────────────┘
     │         │         │         │            │          │
     │  READS  │         │         │            │          │ WRITES
     │         │         │         │            │          │
     └─────────┴─────────┴────┬────┴────────────┘          │
                              │                             │
                              ▼                             ▼
              ┌───────────────────────────┐   ┌───────────────────────────┐
              │    PgBouncer (Reads)      │   │   PgBouncer (Writes)      │
              │    Port 6433              │   │   Port 6432               │
              │                           │   │                           │
              │  • Pool: 100 connections  │   │  • Pool: 50 connections   │
              │  • Mode: transaction      │   │  • Mode: transaction      │
              │  • Load balance replicas  │   │  • Single primary         │
              └─────────────┬─────────────┘   └─────────────┬─────────────┘
                            │                               │
          ┌─────────────────┼─────────────────┐             │
          │                 │                 │             │
          ▼                 ▼                 ▼             ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐ │
│  Replica #1     │ │  Replica #2     │ │  Replica #3     │ │
│  (Hot Standby)  │ │  (Hot Standby)  │ │  (Hot Standby)  │ │
│                 │ │                 │ │                 │ │
│  Read-only      │ │  Read-only      │ │  Read-only      │ │
└────────▲────────┘ └────────▲────────┘ └────────▲────────┘ │
         │                   │                   │          │
         │      Streaming Replication (WAL)      │          │
         └───────────────────┴───────────────────┘          │
                             │                              │
                             │                              │
                    ┌────────┴────────┐                     │
                    │                 │◀────────────────────┘
                    │    Primary      │
                    │  PostgreSQL     │
                    │                 │
                    │  Read-Write     │
                    └─────────────────┘
```

### 5.2 Complete Docker Compose Example

```yaml
# docker-compose.production.yml
# Complete production setup with PgBouncer and Read Replicas

version: '3.8'

services:
  # ============================================
  # PRIMARY DATABASE
  # ============================================
  postgres-primary:
    image: postgres:18-alpine
    container_name: wallow-postgres-primary
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
      # Replication settings
      POSTGRES_INITDB_ARGS: "--data-checksums"
    volumes:
      - postgres_primary_data:/var/lib/postgresql/data
      - ./postgres/primary/postgresql.conf:/etc/postgresql/postgresql.conf:ro
      - ./postgres/primary/pg_hba.conf:/etc/postgresql/pg_hba.conf:ro
      - ./init-db.sql:/docker-entrypoint-initdb.d/01-init-db.sql:ro
      - ./init-replication.sql:/docker-entrypoint-initdb.d/02-init-replication.sql:ro
    command: postgres -c config_file=/etc/postgresql/postgresql.conf -c hba_file=/etc/postgresql/pg_hba.conf
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # REPLICA DATABASE #1
  # ============================================
  postgres-replica1:
    image: postgres:18-alpine
    container_name: wallow-postgres-replica1
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      PGUSER: ${POSTGRES_USER}
      PGPASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_replica1_data:/var/lib/postgresql/data
      - ./postgres/replica/postgresql.conf:/etc/postgresql/postgresql.conf:ro
    entrypoint: |
      bash -c '
        if [ ! -s "/var/lib/postgresql/data/PG_VERSION" ]; then
          echo "Initializing replica from primary..."
          until pg_basebackup -h postgres-primary -D /var/lib/postgresql/data -U replicator -P -v -R -X stream -C -S replica1_slot; do
            echo "Waiting for primary..."
            sleep 5
          done
          chmod 700 /var/lib/postgresql/data
        fi
        exec docker-entrypoint.sh postgres -c config_file=/etc/postgresql/postgresql.conf
      '
    depends_on:
      postgres-primary:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # REPLICA DATABASE #2
  # ============================================
  postgres-replica2:
    image: postgres:18-alpine
    container_name: wallow-postgres-replica2
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      PGUSER: ${POSTGRES_USER}
      PGPASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_replica2_data:/var/lib/postgresql/data
      - ./postgres/replica/postgresql.conf:/etc/postgresql/postgresql.conf:ro
    entrypoint: |
      bash -c '
        if [ ! -s "/var/lib/postgresql/data/PG_VERSION" ]; then
          echo "Initializing replica from primary..."
          until pg_basebackup -h postgres-primary -D /var/lib/postgresql/data -U replicator -P -v -R -X stream -C -S replica2_slot; do
            echo "Waiting for primary..."
            sleep 5
          done
          chmod 700 /var/lib/postgresql/data
        fi
        exec docker-entrypoint.sh postgres -c config_file=/etc/postgresql/postgresql.conf
      '
    depends_on:
      postgres-primary:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # PGBOUNCER - PRIMARY (WRITES)
  # ============================================
  pgbouncer-primary:
    image: bitnami/pgbouncer:1.23.0
    container_name: wallow-pgbouncer-primary
    environment:
      PGBOUNCER_DATABASE: ${POSTGRES_DB}
      PGBOUNCER_PORT: 6432
      PGBOUNCER_BIND_ADDRESS: 0.0.0.0
      POSTGRESQL_HOST: postgres-primary
      POSTGRESQL_PORT: 5432
      POSTGRESQL_USERNAME: ${POSTGRES_USER}
      POSTGRESQL_PASSWORD: ${POSTGRES_PASSWORD}
      PGBOUNCER_POOL_MODE: transaction
      PGBOUNCER_DEFAULT_POOL_SIZE: 50
      PGBOUNCER_MIN_POOL_SIZE: 10
      PGBOUNCER_RESERVE_POOL_SIZE: 10
      PGBOUNCER_MAX_CLIENT_CONN: 500
      PGBOUNCER_MAX_DB_CONNECTIONS: 75
      PGBOUNCER_SERVER_IDLE_TIMEOUT: 600
      PGBOUNCER_LOG_CONNECTIONS: 1
      PGBOUNCER_LOG_DISCONNECTIONS: 1
      PGBOUNCER_ADMIN_USERS: ${POSTGRES_USER}
      PGBOUNCER_STATS_USERS: ${POSTGRES_USER}
    ports:
      - "6432:6432"
    depends_on:
      postgres-primary:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "pg_isready", "-h", "localhost", "-p", "6432", "-U", "${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # PGBOUNCER - REPLICAS (READS)
  # ============================================
  pgbouncer-replicas:
    image: bitnami/pgbouncer:1.23.0
    container_name: wallow-pgbouncer-replicas
    environment:
      PGBOUNCER_DATABASE: ${POSTGRES_DB}
      PGBOUNCER_PORT: 6433
      PGBOUNCER_BIND_ADDRESS: 0.0.0.0
      # Load balance across replicas (comma-separated in custom config)
      POSTGRESQL_HOST: postgres-replica1
      POSTGRESQL_PORT: 5432
      POSTGRESQL_USERNAME: ${POSTGRES_USER}
      POSTGRESQL_PASSWORD: ${POSTGRES_PASSWORD}
      PGBOUNCER_POOL_MODE: transaction
      PGBOUNCER_DEFAULT_POOL_SIZE: 100
      PGBOUNCER_MIN_POOL_SIZE: 20
      PGBOUNCER_RESERVE_POOL_SIZE: 20
      PGBOUNCER_MAX_CLIENT_CONN: 1000
      PGBOUNCER_MAX_DB_CONNECTIONS: 150
      PGBOUNCER_SERVER_IDLE_TIMEOUT: 600
      PGBOUNCER_LOG_CONNECTIONS: 1
      PGBOUNCER_LOG_DISCONNECTIONS: 1
      PGBOUNCER_ADMIN_USERS: ${POSTGRES_USER}
      PGBOUNCER_STATS_USERS: ${POSTGRES_USER}
    volumes:
      - ./pgbouncer/replicas/pgbouncer.ini:/bitnami/pgbouncer/conf/pgbouncer.ini:ro
      - ./pgbouncer/replicas/userlist.txt:/bitnami/pgbouncer/conf/userlist.txt:ro
    ports:
      - "6433:6433"
    depends_on:
      postgres-replica1:
        condition: service_healthy
      postgres-replica2:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "pg_isready", "-h", "localhost", "-p", "6433", "-U", "${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # PGBOUNCER EXPORTER (MONITORING)
  # ============================================
  pgbouncer-exporter:
    image: prometheuscommunity/pgbouncer-exporter:latest
    container_name: wallow-pgbouncer-exporter
    command:
      - "--pgBouncer.connectionString=postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@pgbouncer-primary:6432/pgbouncer?sslmode=disable"
    ports:
      - "9127:9127"
    depends_on:
      - pgbouncer-primary
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # WALLOW API
  # ============================================
  wallow-api:
    image: ${APP_IMAGE}:${APP_TAG}
    container_name: wallow-api
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Primary: "Host=pgbouncer-primary;Port=6432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Pooling=false;Enlist=false"
      ConnectionStrings__Replica: "Host=pgbouncer-replicas;Port=6433;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Pooling=false;Enlist=false"
      # ... other environment variables
    ports:
      - "8080:8080"
    depends_on:
      pgbouncer-primary:
        condition: service_healthy
      pgbouncer-replicas:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - wallow
    restart: unless-stopped

volumes:
  postgres_primary_data:
  postgres_replica1_data:
  postgres_replica2_data:

networks:
  wallow:
    driver: bridge
```

#### Supporting Configuration Files

**postgres/primary/postgresql.conf:**

```ini
# PostgreSQL Primary Configuration
listen_addresses = '*'
max_connections = 200

# Replication
wal_level = replica
max_wal_senders = 10
max_replication_slots = 10
wal_keep_size = 1GB
hot_standby = on
archive_mode = on
archive_command = '/bin/true'

# Performance
shared_buffers = 256MB
effective_cache_size = 768MB
maintenance_work_mem = 64MB
checkpoint_completion_target = 0.9
wal_buffers = 16MB
default_statistics_target = 100
random_page_cost = 1.1
effective_io_concurrency = 200
work_mem = 4MB
min_wal_size = 1GB
max_wal_size = 4GB

# Logging
log_min_messages = info
log_replication_commands = on
```

**postgres/replica/postgresql.conf:**

```ini
# PostgreSQL Replica Configuration
listen_addresses = '*'
max_connections = 200

# Hot Standby
hot_standby = on
hot_standby_feedback = on

# Performance (same as primary)
shared_buffers = 256MB
effective_cache_size = 768MB
maintenance_work_mem = 64MB
work_mem = 4MB

# Recovery (auto-configured by pg_basebackup -R)
# primary_conninfo and primary_slot_name set in postgresql.auto.conf
```

**pgbouncer/replicas/pgbouncer.ini:**

```ini
[databases]
; Load balance across replicas using round-robin
; Each connection attempt goes to the next server in the list
wallow = host=postgres-replica1,postgres-replica2 port=5432 dbname=wallow

[pgbouncer]
listen_addr = 0.0.0.0
listen_port = 6433
auth_file = /bitnami/pgbouncer/conf/userlist.txt
auth_type = md5
pool_mode = transaction
default_pool_size = 50
min_pool_size = 10
reserve_pool_size = 10
max_client_conn = 1000
max_db_connections = 75
server_idle_timeout = 600
log_connections = 1
log_disconnections = 1
admin_users = wallow
stats_users = wallow
```

### 5.3 Connection String Configuration

**appsettings.Production.json:**

```json
{
  "ConnectionStrings": {
    "Primary": "Host=pgbouncer-primary;Port=6432;Database=wallow;Username=wallow;Password=${POSTGRES_PASSWORD};Pooling=false;Enlist=false;No Reset On Close=true;Command Timeout=120",
    "Replica": "Host=pgbouncer-replicas;Port=6433;Database=wallow;Username=wallow;Password=${POSTGRES_PASSWORD};Pooling=false;Enlist=false;No Reset On Close=true;Command Timeout=120",
    "Postgres": "Host=pgbouncer-primary;Port=6432;Database=wallow;Username=wallow;Password=${POSTGRES_PASSWORD};Pooling=false;Enlist=false"
  },
  "Database": {
    "ReadWriteSplit": {
      "Enabled": true,
      "ReplicationLagToleranceSeconds": 5,
      "FallbackToPrimaryOnLag": true
    }
  }
}
```

---

## 6. Monitoring and Alerting

### Key Metrics for PgBouncer

| Metric | Query/Source | Alert Condition | Severity |
|--------|--------------|-----------------|----------|
| Client connections waiting | `SHOW POOLS` → cl_waiting | > 10 for 1 minute | Warning |
| Client connections waiting | `SHOW POOLS` → cl_waiting | > 50 for 1 minute | Critical |
| Pool utilization | sv_active / default_pool_size | > 80% | Warning |
| Pool utilization | sv_active / default_pool_size | > 95% | Critical |
| Average query time | `SHOW STATS` → avg_query_time | > 100ms | Warning |
| Average wait time | `SHOW STATS` → avg_wait_time | > 50ms | Warning |
| Total connections | max_client_conn usage | > 80% | Warning |

### Replication Lag Monitoring

```sql
-- On primary: check replication status
SELECT
    client_addr,
    state,
    sent_lsn,
    write_lsn,
    flush_lsn,
    replay_lsn,
    pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn) AS lag_bytes,
    pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn) / 1024.0 / 1024.0 AS lag_mb
FROM pg_stat_replication;

-- On replica: check recovery status
SELECT
    pg_is_in_recovery() AS is_replica,
    pg_last_wal_receive_lsn() AS last_received,
    pg_last_wal_replay_lsn() AS last_replayed,
    pg_last_xact_replay_timestamp() AS last_replayed_time,
    EXTRACT(EPOCH FROM (now() - pg_last_xact_replay_timestamp())) AS lag_seconds;
```

### PostgreSQL Performance Metrics

```sql
-- Connection usage
SELECT
    count(*) AS total_connections,
    count(*) FILTER (WHERE state = 'active') AS active,
    count(*) FILTER (WHERE state = 'idle') AS idle,
    count(*) FILTER (WHERE state = 'idle in transaction') AS idle_in_transaction
FROM pg_stat_activity
WHERE backend_type = 'client backend';

-- Cache hit ratio (should be > 99%)
SELECT
    sum(heap_blks_hit) / (sum(heap_blks_hit) + sum(heap_blks_read)) AS cache_hit_ratio
FROM pg_statio_user_tables;

-- Index usage
SELECT
    schemaname,
    relname,
    idx_scan,
    seq_scan,
    CASE WHEN (idx_scan + seq_scan) > 0
         THEN round(100.0 * idx_scan / (idx_scan + seq_scan), 2)
         ELSE 0
    END AS idx_scan_pct
FROM pg_stat_user_tables
ORDER BY seq_scan DESC;
```

### Grafana Dashboard Recommendations

Create dashboards for:

1. **PgBouncer Overview**
   - Active vs waiting client connections
   - Server connection pool usage
   - Query time distribution
   - Wait time trends

2. **Replication Health**
   - Lag bytes/time per replica
   - Replication state (streaming, catchup, etc.)
   - WAL generation rate vs replication rate

3. **PostgreSQL Performance**
   - Connection count by state
   - Cache hit ratio
   - Transaction rate (commits/rollbacks)
   - Lock waits

---

## 7. Troubleshooting

### "too many connections" Errors

**Symptom:** Application receives "FATAL: too many connections for role" or "FATAL: sorry, too many clients already"

**Diagnosis:**

```bash
# Check current connections on PostgreSQL
docker exec wallow-postgres psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"

# Check PgBouncer pools
psql -h localhost -p 6432 -U wallow pgbouncer -c "SHOW POOLS;"
```

**Solutions:**

1. **Increase PgBouncer max_client_conn** (if PgBouncer is the limit)
2. **Reduce application pool sizes** (Pooling should be false with PgBouncer)
3. **Add more replicas** for read traffic
4. **Check for connection leaks** (long-running idle in transaction)

### Replication Lag Issues

**Symptom:** `pg_stat_replication` shows large `replay_lsn` lag

**Diagnosis:**

```sql
-- On primary
SELECT
    client_addr,
    pg_wal_lsn_diff(sent_lsn, replay_lsn) AS replay_lag_bytes,
    pg_wal_lsn_diff(pg_current_wal_lsn(), sent_lsn) AS send_lag_bytes
FROM pg_stat_replication;

-- On replica - check for blocking queries
SELECT pid, state, query, query_start
FROM pg_stat_activity
WHERE state != 'idle'
ORDER BY query_start;
```

**Solutions:**

1. **Long-running queries on replica** blocking WAL apply - cancel or optimize
2. **Insufficient I/O on replica** - check disk performance
3. **Network issues** - check connectivity between primary and replica
4. **wal_keep_size too small** - increase to prevent WAL recycling

### PgBouncer Timeout Issues

**Symptom:** "query_timeout" or "server_connect_timeout" errors

**Diagnosis:**

```bash
# Check PgBouncer logs
docker logs wallow-pgbouncer 2>&1 | grep -i timeout

# Check stats
psql -h localhost -p 6432 -U wallow pgbouncer -c "SHOW STATS;"
```

**Solutions:**

1. **Increase query_timeout** in pgbouncer.ini (if queries legitimately take long)
2. **Increase server_connect_timeout** if PostgreSQL is slow to accept connections
3. **Check PostgreSQL performance** - slow queries may be timing out
4. **Increase reserve_pool_size** if pool exhaustion is causing waits

### Prepared Statement Errors

**Symptom:** "prepared statement does not exist" or "cached plan must not change result type"

**Cause:** Transaction pooling returns connections to pool, prepared statements are session-scoped

**Solutions:**

1. **Add to connection string:** `No Reset On Close=true;Multiplexing=false`
2. **Disable prepared statement caching** in EF Core:
   ```csharp
   options.UseNpgsql(connectionString, o => o.UseRelationalNulls());
   ```
3. **Use simple query protocol** via connection string: `Options=-c default_query_mode=simple`

---

## 8. Performance Tuning

### PgBouncer Pool Sizing

**Formula for default_pool_size:**

```
default_pool_size = max(
    average_concurrent_queries_per_db_user_pair,
    (postgresql_max_connections - reserve) / num_pools
)
```

**Example:**
- PostgreSQL max_connections: 200
- Reserve for admin/monitoring: 20
- Number of database/user combinations: 4
- Available: (200 - 20) / 4 = 45 per pool

**Guidelines:**
- Start with default_pool_size = 20-50
- Monitor `cl_waiting` - if > 0 frequently, increase pool
- Monitor `sv_active` - if consistently near pool size, increase
- Never exceed PostgreSQL's capacity

### PostgreSQL Tuning for Replicas

Replicas can be tuned differently than primary since they handle only reads:

```ini
# replica-specific tuning (postgresql.conf)

# Higher work_mem for complex analytical queries
work_mem = 16MB                     # 4x primary's value

# More parallel workers for queries
max_parallel_workers_per_gather = 4
max_parallel_workers = 8

# Larger effective_cache_size if replica has more RAM
effective_cache_size = 2GB

# Reduce checkpoint pressure (replica doesn't checkpoint)
# max_wal_size and min_wal_size less relevant on replica

# Hot standby feedback prevents primary from vacuuming rows needed by replica
hot_standby_feedback = on

# Allow longer queries on replica
statement_timeout = 300000          # 5 minutes (primary might have 60s)
```

### Vacuum and Maintenance Considerations

**On Primary:**
- Autovacuum handles most maintenance
- Large tables may need manual VACUUM ANALYZE after bulk operations

**On Replicas:**
- Replicas cannot vacuum - they replay primary's vacuum operations
- `hot_standby_feedback = on` prevents vacuum conflicts
- Long-running queries on replica can block primary's vacuum (with feedback on)

**Best Practices:**
1. Keep replica query durations reasonable (< 1 minute)
2. Schedule analytics during off-peak hours
3. Monitor `pg_stat_user_tables.n_dead_tup` on primary
4. Use `idle_in_transaction_session_timeout` to kill abandoned transactions

```sql
-- Set on both primary and replicas
ALTER SYSTEM SET idle_in_transaction_session_timeout = '5min';
SELECT pg_reload_conf();
```

---

## Summary

This guide covered the two primary strategies for scaling PostgreSQL in Wallow:

1. **Connection Pooling (PgBouncer)**
   - Solves connection exhaustion
   - Transaction pooling mode for EF Core and Dapper
   - Lightweight multiplexing of thousands of client connections

2. **Read Replicas**
   - Horizontal read scaling
   - Wallow's architecture naturally supports read/write splitting
   - Dapper read queries can use replicas; EF Core writes always use primary

3. **Combined Setup**
   - PgBouncer in front of both primary and replicas
   - Separate pools for reads and writes
   - Maximum scalability with proper connection management

For production deployments, start with PgBouncer alone if connection exhaustion is the immediate problem. Add read replicas when read workload justifies the operational complexity.
