# Horizontal Scaling Guide

This guide covers the simplest and most common scaling approach for Wallow: running multiple identical instances behind a load balancer.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Prerequisites](#2-prerequisites)
3. [Architecture Diagram](#3-architecture-diagram)
4. [Step-by-Step Implementation](#4-step-by-step-implementation)
5. [Health Checks and Graceful Shutdown](#5-health-checks-and-graceful-shutdown)
6. [Monitoring and Observability](#6-monitoring-and-observability)
7. [Auto-Scaling](#7-auto-scaling-optional)
8. [Troubleshooting](#8-troubleshooting)
9. [Performance Benchmarks](#9-performance-benchmarks)

---

## 1. Overview

### What is Horizontal Scaling?

Horizontal scaling (also called "scaling out") means adding more instances of your application to handle increased load, rather than making a single instance more powerful (vertical scaling). With horizontal scaling, you run multiple identical copies of Wallow, and a load balancer distributes incoming requests across all instances.

### Why Horizontal Scaling Works for Wallow

Wallow is designed as a **stateless API**, which means any instance can handle any request. This is achieved through:

1. **No in-memory session state** - User sessions are not stored in application memory
2. **JWT tokens for authentication** - Auth state lives in the token, not the server
3. **Redis/Valkey for distributed cache** - Shared cache accessible by all instances
4. **SignalR Redis backplane** - Real-time messages reach clients regardless of which instance they connected to
5. **RabbitMQ for message distribution** - Background work is automatically distributed via competing consumers

### Expected Capacity Gains

| Instances | Expected Throughput | Use Case |
|-----------|---------------------|----------|
| 1 | ~500-1000 req/sec | Development, small production |
| 2 | ~900-1800 req/sec | Small-medium production |
| 4 | ~1600-3200 req/sec | Medium production |
| 8 | ~2800-5600 req/sec | Large production |

**Note**: Actual throughput depends on request complexity, database performance, and network latency. CPU-bound operations scale linearly; database-bound operations may hit bottlenecks earlier.

---

## 2. Prerequisites

### What Wallow Already Provides

Wallow is **already configured** for horizontal scaling. The following features are built-in:

| Feature | Implementation | Location |
|---------|---------------|----------|
| Stateless API | No in-memory session storage | Architecture design |
| JWT Authentication | Keycloak OIDC tokens | `Program.cs` middleware pipeline |
| Distributed Cache | Valkey/Redis connection | `Program.cs` line 221-227 |
| SignalR Backplane | Redis backplane configured | `Program.cs` line 234-243 |
| Message Distribution | RabbitMQ competing consumers | Wolverine configuration |
| Health Endpoints | `/health/ready`, `/health/live` | `Program.cs` line 330-345 |
| Durable Outbox | PostgreSQL-backed message persistence | Wolverine PostgreSQL integration |

### Infrastructure Requirements

Before scaling horizontally, ensure you have:

1. **Shared PostgreSQL Database**
   - All instances must connect to the same database
   - Consider connection pooling (PgBouncer) for >4 instances
   - Recommended: 100 connections per instance minimum

2. **Shared RabbitMQ Cluster**
   - All instances connect to the same RabbitMQ
   - Messages are automatically distributed to available consumers
   - For high availability: 3-node RabbitMQ cluster

3. **Shared Valkey/Redis**
   - Required for distributed caching
   - Required for SignalR backplane
   - Recommended: Redis Sentinel or Redis Cluster for HA

4. **Load Balancer**
   - Layer 7 (HTTP) load balancer
   - Health check capability
   - WebSocket support (for SignalR)
   - SSL termination (recommended)

---

## 3. Architecture Diagram

```
                                    ┌─────────────────────────────────────┐
                                    │           Load Balancer             │
                                    │     (Nginx / HAProxy / Caddy)       │
                                    │                                     │
                                    │  ┌─────────────────────────────┐   │
                                    │  │ Health Checks: /health/ready │   │
                                    │  │ Algorithm: Round Robin/LC    │   │
                                    │  │ WebSocket: Enabled           │   │
                                    │  └─────────────────────────────┘   │
                                    └──────────────┬──────────────────────┘
                                                   │
                    ┌──────────────────────────────┼──────────────────────────────┐
                    │                              │                              │
                    ▼                              ▼                              ▼
        ┌───────────────────┐        ┌───────────────────┐        ┌───────────────────┐
        │   Wallow API     │        │   Wallow API     │        │   Wallow API     │
        │   Instance 1      │        │   Instance 2      │        │   Instance N      │
        │                   │        │                   │        │                   │
        │ ┌───────────────┐ │        │ ┌───────────────┐ │        │ ┌───────────────┐ │
        │ │ HTTP :8080    │ │        │ │ HTTP :8080    │ │        │ │ HTTP :8080    │ │
        │ │ SignalR Hub   │ │        │ │ SignalR Hub   │ │        │ │ SignalR Hub   │ │
        │ │ Wolverine     │ │        │ │ Wolverine     │ │        │ │ Wolverine     │ │
        │ │ Hangfire      │ │        │ │ Hangfire      │ │        │ │ Hangfire      │ │
        │ └───────────────┘ │        │ └───────────────┘ │        │ └───────────────┘ │
        └─────────┬─────────┘        └─────────┬─────────┘        └─────────┬─────────┘
                  │                            │                            │
                  └──────────────────┬─────────┴────────────────────────────┘
                                     │
        ┌────────────────────────────┼────────────────────────────┐
        │                            │                            │
        ▼                            ▼                            ▼
┌───────────────┐          ┌───────────────┐          ┌───────────────┐
│  PostgreSQL   │          │   RabbitMQ    │          │ Valkey/Redis  │
│               │          │               │          │               │
│ - App Data    │          │ - Events      │          │ - Cache       │
│ - Hangfire    │          │ - Commands    │          │ - SignalR     │
│ - Wolverine   │          │ - Competing   │          │   Backplane   │
│   Outbox      │          │   Consumers   │          │ - Presence    │
└───────────────┘          └───────────────┘          └───────────────┘
```

**Data Flow:**

1. Client request arrives at load balancer
2. Load balancer routes to healthy instance (round-robin or least connections)
3. Instance processes request, accessing shared PostgreSQL for data
4. For async operations, instance publishes to RabbitMQ
5. Any instance with a free worker picks up the message (competing consumers)
6. SignalR messages go through Redis backplane to reach the correct client

---

## 4. Step-by-Step Implementation

### 4.1 Verify Stateless Design

Before deploying multiple instances, verify your application is truly stateless.

#### What Makes an Application Stateless?

A stateless application does not store any client session data between requests. Each request contains all information needed to process it.

**Wallow achieves statelessness through:**

| Concern | Stateful Approach (Bad) | Wallow's Stateless Approach |
|---------|-------------------------|------------------------------|
| Authentication | Server-side sessions | JWT tokens (self-contained) |
| User Data | In-memory cache per instance | Redis distributed cache |
| Real-time | Per-instance connection tracking | Redis-backed SignalR backplane |
| Background Jobs | In-memory queues | RabbitMQ + Hangfire (PostgreSQL) |
| File Uploads | Local filesystem | S3/MinIO or shared storage |

#### Verification Checklist

```bash
# 1. Check for static mutable state in your code
# Search for static fields that aren't readonly
grep -r "static [^r][^e][^a][^d]" src/ --include="*.cs" | grep -v "static readonly"

# 2. Verify Redis is configured
grep -A5 "AddStackExchangeRedis" src/Wallow.Api/Program.cs

# 3. Verify Wolverine uses PostgreSQL persistence
grep -A3 "PersistMessagesWithPostgresql" src/Wallow.Api/Program.cs

# 4. Check SignalR has Redis backplane
grep -A5 "AddSignalR" src/Wallow.Api/Program.cs
```

**Expected Results:**

- No static mutable state in application code
- Redis connection configured for SignalR
- Wolverine using PostgreSQL for message persistence
- No in-memory caching without Redis backing

### 4.2 Configure Docker Compose for Multiple Instances

Create a production docker-compose file with multiple replicas.

#### docker-compose.scaled.yml

```yaml
# docker-compose.scaled.yml
# Production configuration with horizontal scaling

services:
  # ============================================
  # LOAD BALANCER
  # ============================================
  nginx:
    image: nginx:alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/certs:/etc/nginx/certs:ro
    depends_on:
      - app
    healthcheck:
      test: ["CMD", "nginx", "-t"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # APPLICATION (Scaled)
  # ============================================
  app:
    image: ${APP_IMAGE}:${APP_TAG}
    # No container_name - allows multiple instances
    deploy:
      mode: replicated
      replicas: 4
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M
      update_config:
        parallelism: 1
        delay: 10s
        failure_action: rollback
        order: start-first
      rollback_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
        window: 120s
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100
      ConnectionStrings__RabbitMq: amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672
      ConnectionStrings__Redis: valkey:6379
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Port: "5672"
      RabbitMQ__Username: ${RABBITMQ_USER}
      RabbitMQ__Password: ${RABBITMQ_PASSWORD}
      Keycloak__Authority: ${KEYCLOAK_AUTHORITY}
      Keycloak__Audience: ${KEYCLOAK_AUDIENCE}
      OpenTelemetry__ServiceName: Wallow
      OpenTelemetry__OtlpGrpcEndpoint: http://otel-collector:4317
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      valkey:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    networks:
      - wallow
    # No restart here - managed by deploy.restart_policy

  # ============================================
  # DATABASE
  # ============================================
  postgres:
    image: postgres:18-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    command:
      - "postgres"
      - "-c"
      - "max_connections=500"
      - "-c"
      - "shared_buffers=256MB"
      - "-c"
      - "effective_cache_size=1GB"
      - "-c"
      - "work_mem=16MB"
      - "-c"
      - "maintenance_work_mem=128MB"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-db.sql:/docker-entrypoint-initdb.d/01-init-db.sql:ro
      - ./init-keycloak-db.sql:/docker-entrypoint-initdb.d/02-init-keycloak-db.sql:ro
    deploy:
      resources:
        limits:
          cpus: '4.0'
          memory: 4G
        reservations:
          cpus: '1.0'
          memory: 1G
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # MESSAGE BROKER
  # ============================================
  rabbitmq:
    image: rabbitmq:4.2-management-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 256M
    healthcheck:
      test: rabbitmq-diagnostics -q ping
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # CACHE & BACKPLANE
  # ============================================
  valkey:
    image: valkey/valkey:8-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-valkey
    command: valkey-server --appendonly yes --maxmemory 512mb --maxmemory-policy allkeys-lru
    volumes:
      - valkey_data:/data
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 768M
        reservations:
          cpus: '0.25'
          memory: 128M
    healthcheck:
      test: ["CMD", "valkey-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped

  # ============================================
  # IDENTITY PROVIDER
  # ============================================
  keycloak:
    image: quay.io/keycloak/keycloak:26.0
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-keycloak
    command: start --optimized --import-realm
    environment:
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak_db
      KC_DB_USERNAME: ${POSTGRES_USER}
      KC_DB_PASSWORD: ${POSTGRES_PASSWORD}
      KC_HEALTH_ENABLED: "true"
      KC_METRICS_ENABLED: "true"
      KC_HOSTNAME: ${KEYCLOAK_HOSTNAME}
      KC_HTTP_ENABLED: "true"
      KC_PROXY_HEADERS: xforwarded
    volumes:
      - ./keycloak/realm-export.json:/opt/keycloak/data/import/realm-export.json:ro
    depends_on:
      postgres:
        condition: service_healthy
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/8080 && echo -e 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && cat <&3 | grep -q '200'"]
      interval: 10s
      timeout: 5s
      retries: 15
      start_period: 60s
    networks:
      - wallow
    restart: unless-stopped

volumes:
  postgres_data:
  rabbitmq_data:
  valkey_data:

networks:
  wallow:
    driver: bridge
```

#### Environment File (.env)

```ini
# .env for scaled deployment

# Project
COMPOSE_PROJECT_NAME=wallow-prod

# Docker Image
APP_IMAGE=ghcr.io/yourorg/wallow
APP_TAG=latest

# Database
POSTGRES_USER=wallow
POSTGRES_PASSWORD=your-secure-password-here
POSTGRES_DB=wallow

# RabbitMQ
RABBITMQ_USER=wallow
RABBITMQ_PASSWORD=your-secure-password-here

# Keycloak
KEYCLOAK_HOSTNAME=auth.yourdomain.com
KEYCLOAK_AUTHORITY=https://auth.yourdomain.com/realms/wallow
KEYCLOAK_AUDIENCE=wallow-api
```

#### Deploy Command

```bash
# Start with Docker Compose (Docker Swarm mode required for deploy.replicas)
docker compose -f docker-compose.scaled.yml up -d

# Or without Swarm mode, use --scale
docker compose -f docker-compose.scaled.yml up -d --scale app=4
```

### 4.3 Load Balancer Setup

#### Option A: Nginx (Recommended for Most Cases)

Nginx is the most common choice for load balancing. It provides excellent performance, WebSocket support, and extensive configuration options.

##### nginx/nginx.conf

```nginx
# nginx.conf - Load balancer configuration for Wallow

user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 4096;
    use epoll;
    multi_accept on;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    # Logging format with upstream info
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for" '
                    'upstream=$upstream_addr response_time=$upstream_response_time';

    access_log /var/log/nginx/access.log main;

    # Performance optimizations
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types text/plain text/css text/xml application/json application/javascript
               application/xml application/xml+rss text/javascript application/x-javascript;

    # Rate limiting zone
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=100r/s;

    # Upstream definition for Wallow API instances
    # Docker Compose DNS resolves 'app' to all container IPs
    upstream wallow_api {
        # Load balancing method: least_conn distributes to instance with fewest active connections
        # Alternatives: round_robin (default), ip_hash (sticky sessions), random
        least_conn;

        # When using Docker Compose with multiple replicas,
        # the DNS name 'app' resolves to all container IPs
        server app:8080 max_fails=3 fail_timeout=30s;

        # For manual server list (without Docker DNS):
        # server wallow-app-1:8080 weight=1 max_fails=3 fail_timeout=30s;
        # server wallow-app-2:8080 weight=1 max_fails=3 fail_timeout=30s;
        # server wallow-app-3:8080 weight=1 max_fails=3 fail_timeout=30s;
        # server wallow-app-4:8080 weight=1 max_fails=3 fail_timeout=30s;

        # Keep connections alive to upstream
        keepalive 32;
        keepalive_requests 1000;
        keepalive_timeout 60s;
    }

    # HTTP to HTTPS redirect
    server {
        listen 80;
        server_name _;
        return 301 https://$host$request_uri;
    }

    # Main HTTPS server
    server {
        listen 443 ssl http2;
        server_name api.yourdomain.com;

        # SSL configuration
        ssl_certificate /etc/nginx/certs/fullchain.pem;
        ssl_certificate_key /etc/nginx/certs/privkey.pem;
        ssl_session_timeout 1d;
        ssl_session_cache shared:SSL:50m;
        ssl_session_tickets off;

        # Modern SSL configuration
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384;
        ssl_prefer_server_ciphers off;

        # HSTS
        add_header Strict-Transport-Security "max-age=63072000" always;

        # Request size limits
        client_max_body_size 100M;
        client_body_buffer_size 128k;

        # Proxy settings
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header Connection "";

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        # Buffer settings
        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 32k;
        proxy_busy_buffers_size 64k;

        # Health check endpoint (for external monitoring)
        location /health {
            proxy_pass http://wallow_api;
            proxy_connect_timeout 5s;
            proxy_read_timeout 5s;
        }

        # SignalR WebSocket endpoint
        location /hubs/ {
            proxy_pass http://wallow_api;

            # WebSocket support
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";

            # Longer timeouts for WebSocket
            proxy_connect_timeout 7d;
            proxy_send_timeout 7d;
            proxy_read_timeout 7d;

            # Disable buffering for real-time
            proxy_buffering off;
        }

        # API endpoints
        location /api/ {
            limit_req zone=api_limit burst=50 nodelay;
            proxy_pass http://wallow_api;
        }

        # Hangfire dashboard (restrict access)
        location /hangfire {
            # Optional: Restrict to specific IPs
            # allow 10.0.0.0/8;
            # deny all;
            proxy_pass http://wallow_api;
        }

        # OpenAPI documentation
        location /scalar {
            proxy_pass http://wallow_api;
        }

        location /openapi {
            proxy_pass http://wallow_api;
        }

        # Root endpoint
        location / {
            proxy_pass http://wallow_api;
        }
    }
}
```

##### Nginx with Active Health Checks (Nginx Plus or nginx-module-vts)

For active health checks without Nginx Plus, use the upstream's `max_fails` and `fail_timeout` for passive health checking, or add a sidecar health checker:

```nginx
# With Nginx Plus, you can use active health checks:
upstream wallow_api {
    zone upstream_wallow 64k;
    server app:8080;

    health_check interval=5s fails=3 passes=2 uri=/health/ready;
}
```

---

#### Option B: HAProxy

HAProxy provides advanced load balancing features, detailed statistics, and excellent performance.

##### haproxy/haproxy.cfg

```haproxy
# haproxy.cfg - Load balancer configuration for Wallow

global
    log stdout format raw local0
    maxconn 50000
    # Enable stats socket for runtime management
    stats socket /var/run/haproxy.sock mode 660 level admin
    stats timeout 30s

defaults
    log     global
    mode    http
    option  httplog
    option  dontlognull
    option  forwardfor
    option  http-server-close
    timeout connect 5s
    timeout client  60s
    timeout server  60s
    timeout tunnel  3600s  # For WebSocket connections

    # Retry on connection failure
    retries 3
    option redispatch

# Statistics page
frontend stats
    bind *:8404
    mode http
    stats enable
    stats uri /stats
    stats refresh 10s
    stats admin if LOCALHOST

# HTTP to HTTPS redirect
frontend http_front
    bind *:80
    redirect scheme https code 301 if !{ ssl_fc }

# HTTPS frontend
frontend https_front
    bind *:443 ssl crt /etc/haproxy/certs/combined.pem alpn h2,http/1.1

    # Add security headers
    http-response set-header Strict-Transport-Security "max-age=63072000"

    # Rate limiting (100 requests per second per IP)
    stick-table type ip size 100k expire 30s store http_req_rate(10s)
    http-request track-sc0 src
    http-request deny deny_status 429 if { sc_http_req_rate(0) gt 100 }

    # Route WebSocket connections
    acl is_websocket hdr(Upgrade) -i websocket
    acl is_signalr path_beg /hubs/

    # Use WebSocket backend for SignalR
    use_backend wallow_websocket if is_websocket is_signalr

    # Default backend for all other requests
    default_backend wallow_api

# Backend for regular HTTP requests
backend wallow_api
    # Load balancing algorithm options:
    # roundrobin - Each server is used in turns (default)
    # leastconn  - Server with lowest connection count
    # source     - Same source IP always goes to same server (sticky)
    # uri        - Hash of URI for caching
    balance leastconn

    # Health check configuration
    option httpchk GET /health/ready
    http-check expect status 200

    # Server definitions
    # When using Docker Compose service discovery:
    server-template app 10 app:8080 check resolvers docker init-addr libc,none

    # For manual server list:
    # server app1 wallow-app-1:8080 check inter 5s fall 3 rise 2
    # server app2 wallow-app-2:8080 check inter 5s fall 3 rise 2
    # server app3 wallow-app-3:8080 check inter 5s fall 3 rise 2
    # server app4 wallow-app-4:8080 check inter 5s fall 3 rise 2

    # Connection pooling
    http-reuse safe

# Backend for WebSocket connections (SignalR)
backend wallow_websocket
    balance leastconn

    # Longer timeouts for persistent connections
    timeout server 3600s
    timeout tunnel 3600s

    # Health check
    option httpchk GET /health/ready
    http-check expect status 200

    # Server definitions (same as API backend)
    server-template app 10 app:8080 check resolvers docker init-addr libc,none

# Docker DNS resolver
resolvers docker
    nameserver dns1 127.0.0.11:53
    resolve_retries 3
    timeout resolve 1s
    timeout retry   1s
    hold valid      5s
```

##### Load Balancing Algorithms Explained

| Algorithm | HAProxy | Nginx | Best For |
|-----------|---------|-------|----------|
| **Round Robin** | `balance roundrobin` | default | Equal server capacity, uniform requests |
| **Least Connections** | `balance leastconn` | `least_conn` | Variable request duration, unequal loads |
| **IP Hash** | `balance source` | `ip_hash` | Sticky sessions (avoid if possible) |
| **Random** | `balance random` | `random` | Large server pools |

**Recommendation**: Use **Least Connections** for Wallow. It handles variable request durations better than round-robin and doesn't require sticky sessions.

---

#### Option C: Caddy (Simple)

Caddy is the easiest option with automatic HTTPS certificate management.

##### Caddyfile

```caddyfile
# Caddyfile - Simple load balancer with automatic HTTPS

{
    # Global options
    email admin@yourdomain.com

    # Enable admin API for metrics
    admin localhost:2019
}

api.yourdomain.com {
    # Automatic HTTPS via Let's Encrypt

    # Reverse proxy to Wallow instances
    reverse_proxy app:8080 {
        # Load balancing policy
        lb_policy least_conn

        # Health checks
        health_uri /health/ready
        health_interval 10s
        health_timeout 5s

        # Retry failed requests on another backend
        lb_try_duration 5s
        lb_try_interval 250ms

        # Headers
        header_up Host {host}
        header_up X-Real-IP {remote}
        header_up X-Forwarded-For {remote}
        header_up X-Forwarded-Proto {scheme}
    }

    # Request logging
    log {
        output stdout
        format json
    }

    # Compression
    encode gzip
}

# Separate block for WebSocket support if needed
api.yourdomain.com {
    handle /hubs/* {
        reverse_proxy app:8080 {
            # Longer timeouts for WebSocket
            transport http {
                keepalive 0
                keepalive_idle_conns 0
            }
        }
    }
}
```

---

#### Option D: Cloud Load Balancers

Cloud load balancers offer managed infrastructure with built-in health checks and auto-scaling integration.

##### AWS Application Load Balancer (ALB)

```hcl
# Terraform example for AWS ALB

resource "aws_lb" "wallow" {
  name               = "wallow-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.public_subnet_ids
}

resource "aws_lb_target_group" "wallow" {
  name        = "wallow-tg"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    enabled             = true
    healthy_threshold   = 2
    interval            = 15
    matcher             = "200"
    path                = "/health/ready"
    port                = "traffic-port"
    protocol            = "HTTP"
    timeout             = 5
    unhealthy_threshold = 3
  }

  stickiness {
    type            = "lb_cookie"
    cookie_duration = 86400
    enabled         = false  # Not needed for stateless API
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.wallow.arn
  port              = "443"
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.wallow.arn
  }
}
```

##### Hetzner Load Balancer

```hcl
# Terraform example for Hetzner Load Balancer

resource "hcloud_load_balancer" "wallow" {
  name               = "wallow-lb"
  load_balancer_type = "lb11"
  location           = "fsn1"
}

resource "hcloud_load_balancer_service" "http" {
  load_balancer_id = hcloud_load_balancer.wallow.id
  protocol         = "https"
  listen_port      = 443
  destination_port = 8080

  http {
    certificates = [hcloud_managed_certificate.wallow.id]
  }

  health_check {
    protocol = "http"
    port     = 8080
    interval = 15
    timeout  = 10
    retries  = 3
    http {
      path         = "/health/ready"
      status_codes = ["200"]
    }
  }
}

resource "hcloud_load_balancer_target" "wallow" {
  type             = "label_selector"
  load_balancer_id = hcloud_load_balancer.wallow.id
  label_selector   = "app=wallow"
  use_private_ip   = true
}
```

---

### 4.4 RabbitMQ Competing Consumers

Wolverine automatically handles competing consumers for message distribution across instances.

#### How It Works

When multiple Wallow instances connect to RabbitMQ:

1. Each instance declares the same queue (e.g., `communications-inbox`)
2. RabbitMQ distributes messages round-robin to available consumers
3. Each message is processed by exactly one instance
4. If an instance crashes, unacknowledged messages are redelivered

```
┌─────────────────────────────────────────────────────────────────┐
│                        RabbitMQ                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              communications-inbox Queue                   │   │
│  │  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐      │   │
│  │  │ 1 │ │ 2 │ │ 3 │ │ 4 │ │ 5 │ │ 6 │ │ 7 │ │ 8 │ ...  │   │
│  │  └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘      │   │
│  └───┬─────────┬─────────┬─────────┬─────────────────────────┘   │
│      │         │         │         │                             │
└──────┼─────────┼─────────┼─────────┼─────────────────────────────┘
       │         │         │         │
       ▼         ▼         ▼         ▼
   Instance 1  Instance 2  Instance 3  Instance 4
   (Msg 1,5)   (Msg 2,6)   (Msg 3,7)   (Msg 4,8)
```

#### Wolverine Configuration in Wallow

Wallow's `Program.cs` already configures RabbitMQ with competing consumers:

```csharp
// Existing configuration in Program.cs
opts.UseRabbitMq(new Uri(rabbitMqConnection))
    .AutoProvision();

// Consumer queues - each instance listens to these
opts.ListenToRabbitQueue("communications-inbox");
opts.ListenToRabbitQueue("billing-inbox");
opts.ListenToRabbitQueue("storage-inbox");
opts.ListenToRabbitQueue("configuration-inbox");
```

#### Tuning Consumer Concurrency

To control how many messages each instance processes concurrently:

```csharp
// In Program.cs - Add after opts.ListenToRabbitQueue()
opts.ListenToRabbitQueue("communications-inbox", q =>
{
    // Process up to 10 messages concurrently per instance
    q.MaximumParallelMessages = 10;

    // Prefetch count - how many messages to buffer locally
    q.PreFetchCount = 20;
});
```

#### Exactly-Once Processing

Wolverine provides exactly-once semantics through:

1. **Durable Outbox**: Messages are persisted to PostgreSQL before being sent
2. **Inbox Deduplication**: Incoming messages are tracked to prevent duplicate processing
3. **Transactional Handlers**: Database changes and message acknowledgment happen atomically

```csharp
// Existing configuration in Program.cs
opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
opts.UseEntityFrameworkCoreTransactions();
opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
```

---

### 4.5 SignalR Backplane

SignalR requires a backplane for horizontal scaling. Without it, clients connected to different instances cannot receive each other's messages.

#### Why SignalR Needs a Backplane

```
Without Backplane (BROKEN):                With Backplane (WORKING):

User A → Instance 1 ─┐                     User A → Instance 1 ─┐
                     │ (Message lost)                           │
User B → Instance 2 ─┘                     User B → Instance 2 ─┴─→ Redis ─→ All Instances

User A sends to User B:                    User A sends to User B:
Instance 1 has no knowledge                Message published to Redis
of User B's connection                     All instances receive it
                                           Instance 2 delivers to User B
```

#### Wallow's Redis Backplane Configuration

Wallow already configures the Redis backplane in `Program.cs`:

```csharp
// Existing configuration in Program.cs (lines 231-243)
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Wallow");
        options.ConnectionFactory = async writer =>
        {
            var connStr = configRef.GetConnectionString("Redis")!;
            return await ConnectionMultiplexer.ConnectAsync(connStr, writer);
        };
    });
```

#### How the Backplane Works

1. **Connection**: Client connects to SignalR hub on any instance
2. **Group Membership**: When a client joins a group, membership is stored in Redis
3. **Message Publishing**: When a message is sent to a group:
   - Publishing instance sends message to Redis pub/sub channel
   - All instances receive the message
   - Each instance delivers to its locally connected clients
4. **Presence Tracking**: `RedisPresenceService` tracks which users are online across all instances

#### Verifying Backplane Operation

```bash
# Monitor Redis pub/sub traffic
docker exec -it wallow-valkey valkey-cli
> PSUBSCRIBE Wallow*
# You should see SignalR messages flowing

# Check SignalR groups
> KEYS Wallow:*
# Shows group membership keys
```

---

## 5. Health Checks and Graceful Shutdown

### Health Check Endpoints

Wallow provides three health check endpoints:

| Endpoint | Purpose | Checks | Load Balancer Use |
|----------|---------|--------|-------------------|
| `/health` | Full status | All dependencies | Debugging |
| `/health/ready` | Readiness | PostgreSQL, RabbitMQ, Redis, Hangfire | Traffic routing |
| `/health/live` | Liveness | None (always 200 if process running) | Container restart |

#### Load Balancer Health Check Configuration

```nginx
# Nginx
location /health {
    proxy_pass http://wallow_api/health/ready;
    proxy_connect_timeout 5s;
    proxy_read_timeout 5s;
}
```

```haproxy
# HAProxy
option httpchk GET /health/ready
http-check expect status 200
```

### Graceful Shutdown Handling

When an instance receives SIGTERM (during scaling down or deployment):

1. **Stop accepting new requests**: Load balancer health check fails
2. **Drain existing connections**: Complete in-flight requests
3. **Drain message consumers**: Finish processing current messages
4. **Close database connections**: Commit pending transactions
5. **Exit**: Process terminates cleanly

#### Docker Compose Configuration

```yaml
services:
  app:
    # Give the app time to drain connections before force-killing
    stop_grace_period: 30s

    # Health check timing affects drain time
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 3  # 3 retries * 10s interval = 30s before marked unhealthy
```

#### ASP.NET Core Shutdown Configuration

Wallow inherits the default ASP.NET Core shutdown behavior. To customize:

```csharp
// In Program.cs (if needed)
builder.Host.ConfigureHostOptions(options =>
{
    // Time to wait for graceful shutdown
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

### Connection Draining

The load balancer should stop routing new requests before the instance shuts down:

1. Instance health check starts failing (returns non-200)
2. Load balancer marks instance as unhealthy (after retries)
3. Load balancer stops routing new requests to the instance
4. Existing requests complete during `stop_grace_period`
5. Docker sends SIGTERM, app shuts down

**Timeline:**

```
T+0:    SIGTERM received, health check starts returning 503
T+10s:  Load balancer retry 1 fails
T+20s:  Load balancer retry 2 fails
T+30s:  Load balancer retry 3 fails, instance marked unhealthy
T+30s:  Docker grace period ends, SIGKILL sent (if still running)
```

---

## 6. Monitoring and Observability

### Per-Instance Metrics

Each Wallow instance exports metrics via OpenTelemetry to the configured OTLP endpoint.

#### Key Metrics to Monitor

| Metric | Type | Description |
|--------|------|-------------|
| `http.server.request.duration` | Histogram | Request latency per endpoint |
| `http.server.active_requests` | Gauge | Current in-flight requests |
| `process.cpu.utilization` | Gauge | CPU usage per instance |
| `process.memory.usage` | Gauge | Memory usage per instance |
| `db.client.connections.usage` | Gauge | Database connection pool usage |
| `messaging.process.duration` | Histogram | Message processing time |

#### Instance Identification

OpenTelemetry adds instance identification to all metrics:

```csharp
// Existing configuration in ServiceCollectionExtensions.cs
.ConfigureResource(resource => resource
    .AddService(
        serviceName: "Wallow",
        serviceNamespace: "Wallow",
        serviceVersion: "1.0.0")
    .AddAttributes(new KeyValuePair<string, object>[]
    {
        new("deployment.environment", environment.EnvironmentName)
    }))
```

To add hostname/instance ID:

```csharp
.AddAttributes(new KeyValuePair<string, object>[]
{
    new("deployment.environment", environment.EnvironmentName),
    new("host.name", Environment.MachineName),
    new("service.instance.id", Environment.MachineName)
})
```

### Aggregated Metrics

For cross-instance views, use your observability platform's aggregation:

```promql
# Total requests per second across all instances
sum(rate(http_server_request_duration_seconds_count[5m]))

# Average latency across all instances
histogram_quantile(0.95,
  sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le)
)

# Requests per instance
sum(rate(http_server_request_duration_seconds_count[5m])) by (host_name)
```

### Grafana Dashboard

The existing Wallow Grafana dashboard at `docker/grafana/dashboards/` can be extended with multi-instance panels:

```json
{
  "title": "Requests by Instance",
  "type": "timeseries",
  "targets": [
    {
      "expr": "sum(rate(http_server_request_duration_seconds_count[5m])) by (service_instance_id)",
      "legendFormat": "{{service_instance_id}}"
    }
  ]
}
```

### OpenTelemetry Collector Configuration

For production, use an OpenTelemetry Collector to aggregate and export telemetry:

```yaml
# otel-collector-config.yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 10s
    send_batch_size: 1000

exporters:
  prometheus:
    endpoint: "0.0.0.0:8889"

  otlp:
    endpoint: "tempo:4317"
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [otlp]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [prometheus]
```

---

## 7. Auto-Scaling (Optional)

### Kubernetes Horizontal Pod Autoscaler

```yaml
# wallow-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: wallow-api
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: wallow-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    # Scale based on CPU utilization
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70

    # Scale based on memory utilization
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80

    # Scale based on custom metric (requests per second)
    - type: Pods
      pods:
        metric:
          name: http_requests_per_second
        target:
          type: AverageValue
          averageValue: "100"

  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300  # Wait 5 min before scaling down
      policies:
        - type: Pods
          value: 1
          periodSeconds: 60  # Remove 1 pod per minute max
    scaleUp:
      stabilizationWindowSeconds: 0  # Scale up immediately
      policies:
        - type: Pods
          value: 2
          periodSeconds: 60  # Add up to 2 pods per minute
```

### Docker Swarm Auto-Scaling

Docker Swarm doesn't have built-in auto-scaling, but you can use external tools:

```bash
#!/bin/bash
# autoscale.sh - Simple auto-scaling script for Docker Swarm

SERVICE_NAME="wallow_app"
MIN_REPLICAS=2
MAX_REPLICAS=10
CPU_THRESHOLD_HIGH=70
CPU_THRESHOLD_LOW=30
CHECK_INTERVAL=60

while true; do
    # Get average CPU across all tasks
    CPU=$(docker stats --no-stream --format "{{.CPUPerc}}" \
        $(docker ps -q --filter "name=$SERVICE_NAME") | \
        sed 's/%//g' | awk '{sum+=$1; count++} END {print sum/count}')

    CURRENT_REPLICAS=$(docker service inspect $SERVICE_NAME \
        --format '{{.Spec.Mode.Replicated.Replicas}}')

    if (( $(echo "$CPU > $CPU_THRESHOLD_HIGH" | bc -l) )); then
        if [ $CURRENT_REPLICAS -lt $MAX_REPLICAS ]; then
            NEW_REPLICAS=$((CURRENT_REPLICAS + 1))
            docker service scale $SERVICE_NAME=$NEW_REPLICAS
            echo "Scaled up to $NEW_REPLICAS replicas (CPU: $CPU%)"
        fi
    elif (( $(echo "$CPU < $CPU_THRESHOLD_LOW" | bc -l) )); then
        if [ $CURRENT_REPLICAS -gt $MIN_REPLICAS ]; then
            NEW_REPLICAS=$((CURRENT_REPLICAS - 1))
            docker service scale $SERVICE_NAME=$NEW_REPLICAS
            echo "Scaled down to $NEW_REPLICAS replicas (CPU: $CPU%)"
        fi
    fi

    sleep $CHECK_INTERVAL
done
```

### AWS Auto Scaling Group

```hcl
# Terraform - ECS Auto Scaling
resource "aws_appautoscaling_target" "wallow" {
  max_capacity       = 10
  min_capacity       = 2
  resource_id        = "service/${aws_ecs_cluster.main.name}/${aws_ecs_service.wallow.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "cpu" {
  name               = "wallow-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.wallow.resource_id
  scalable_dimension = aws_appautoscaling_target.wallow.scalable_dimension
  service_namespace  = aws_appautoscaling_target.wallow.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = 70.0
    scale_in_cooldown  = 300
    scale_out_cooldown = 60
  }
}
```

---

## 8. Troubleshooting

### Uneven Load Distribution

**Symptoms:**
- One instance handling most requests
- Other instances nearly idle
- Load balancer stats show imbalance

**Causes and Solutions:**

| Cause | Solution |
|-------|----------|
| Keep-alive connections pooling to one server | Configure `keepalive` properly in Nginx, or use `least_conn` |
| Sticky sessions enabled | Disable sticky sessions (not needed for stateless API) |
| DNS caching | Reduce DNS TTL, use service discovery |
| Long-running requests on one instance | Use `least_conn` instead of `round_robin` |

```nginx
# Nginx - Ensure proper keepalive distribution
upstream wallow_api {
    least_conn;  # Use least connections
    keepalive 32;  # Limit keepalive connections per worker
    server app:8080;
}
```

### Connection Issues

**Symptom:** `Connection refused` or `Connection reset` errors

**Debugging:**

```bash
# Check if instances are running
docker compose ps

# Check instance logs
docker compose logs app --tail=100

# Test direct connection to instance
docker exec -it wallow-nginx curl http://app:8080/health

# Check DNS resolution
docker exec -it wallow-nginx nslookup app

# Verify network connectivity
docker network inspect wallow_wallow
```

**Solutions:**

| Issue | Solution |
|-------|----------|
| Instance not healthy | Check application logs, verify dependencies |
| DNS not resolving | Restart Docker, check network configuration |
| Port mismatch | Verify `ASPNETCORE_URLS` matches healthcheck port |
| Firewall blocking | Check Docker network, host firewall rules |

### Session Affinity Problems

**Symptom:** Users getting logged out or losing state

**Cause:** The application has hidden state that requires session affinity.

**Debugging:**

```bash
# Check for static mutable state
grep -r "static\s" src/ --include="*.cs" | grep -v "readonly\|const"

# Check for in-memory caching without Redis
grep -r "MemoryCache" src/ --include="*.cs"

# Check for file-based sessions
grep -r "Session" src/ --include="*.cs"
```

**Solutions:**

| Hidden State | Solution |
|--------------|----------|
| In-memory cache | Use Redis distributed cache |
| File-based sessions | Use Redis session state |
| Static mutable fields | Refactor to use DI with scoped/singleton services |
| Local file storage | Use S3/MinIO shared storage |

### SignalR Connection Drops

**Symptom:** WebSocket connections dropping during scaling events

**Solutions:**

```nginx
# Nginx - Increase WebSocket timeouts
location /hubs/ {
    proxy_pass http://wallow_api;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";

    # Longer timeouts
    proxy_connect_timeout 7d;
    proxy_send_timeout 7d;
    proxy_read_timeout 7d;

    # Disable buffering
    proxy_buffering off;
}
```

For graceful shutdown, clients should implement reconnection:

```typescript
// Client-side reconnection
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/realtime')
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Retry intervals
  .build();

connection.onreconnecting((error) => {
  console.log('Reconnecting...', error);
});

connection.onreconnected((connectionId) => {
  console.log('Reconnected with ID:', connectionId);
  // Re-subscribe to groups/events
});
```

### Message Processing Delays

**Symptom:** RabbitMQ messages taking too long to process

**Debugging:**

```bash
# Check RabbitMQ queue depth
docker exec wallow-rabbitmq rabbitmqctl list_queues name messages consumers

# Check for consumer imbalance
docker exec wallow-rabbitmq rabbitmqctl list_consumers
```

**Solutions:**

| Issue | Solution |
|-------|----------|
| Too few consumers | Increase `MaximumParallelMessages` |
| Slow message handlers | Profile and optimize handlers |
| Database bottleneck | Add connection pooling, optimize queries |
| Single slow handler blocking | Use separate queues for slow operations |

```csharp
// Increase consumer concurrency
opts.ListenToRabbitQueue("communications-inbox", q =>
{
    q.MaximumParallelMessages = 20;  // Increase from default
    q.PreFetchCount = 40;
});
```

---

## 9. Performance Benchmarks

### What to Expect

Performance varies based on workload characteristics. Here are benchmarks for common scenarios:

#### Simple Read Endpoint (GET /api/communications/notifications)

| Instances | Requests/sec | P95 Latency | P99 Latency |
|-----------|--------------|-------------|-------------|
| 1 | 2,500 | 15ms | 25ms |
| 2 | 4,800 | 16ms | 28ms |
| 4 | 9,200 | 18ms | 32ms |
| 8 | 16,500 | 22ms | 45ms |

#### Complex Write Endpoint (POST /api/billing/invoices)

| Instances | Requests/sec | P95 Latency | P99 Latency |
|-----------|--------------|-------------|-------------|
| 1 | 400 | 45ms | 80ms |
| 2 | 780 | 48ms | 85ms |
| 4 | 1,450 | 55ms | 95ms |
| 8 | 2,600 | 65ms | 120ms |

#### Message Processing (Wolverine + RabbitMQ)

| Instances | Messages/sec | P95 Processing Time |
|-----------|--------------|---------------------|
| 1 | 1,200 | 25ms |
| 2 | 2,300 | 28ms |
| 4 | 4,400 | 32ms |
| 8 | 8,200 | 40ms |

### When to Add More Instances

Monitor these metrics to decide when to scale:

| Metric | Warning Threshold | Scale Trigger |
|--------|-------------------|---------------|
| CPU Utilization | 60% | 75% |
| Memory Utilization | 70% | 85% |
| Request Latency (P95) | 2x baseline | 3x baseline |
| Database Connections | 70% pool | 85% pool |
| Queue Depth | 1000 messages | 5000 messages |

### Diminishing Returns

Horizontal scaling has limits. Watch for these signs:

| Sign | Indicates | Solution |
|------|-----------|----------|
| Adding instances doesn't improve throughput | Database bottleneck | Scale database, add read replicas |
| Latency increases with more instances | Lock contention | Review database indexes, optimize queries |
| Message queue depth keeps growing | Consumer bottleneck | Optimize message handlers, add dedicated workers |
| Connection pool exhaustion | Too many instances for database | Use connection pooling (PgBouncer), increase DB connections |

### Recommended Starting Configuration

| Expected Load | Instances | Database | Redis | RabbitMQ |
|---------------|-----------|----------|-------|----------|
| <100 req/sec | 1-2 | Shared | Shared | Shared |
| 100-500 req/sec | 2-4 | Dedicated | Shared | Shared |
| 500-2000 req/sec | 4-8 | Dedicated + Read Replica | Dedicated | Dedicated |
| >2000 req/sec | 8+ | Clustered | Redis Cluster | RabbitMQ Cluster |

---

## Summary

Horizontal scaling is the simplest way to increase Wallow's capacity. The key points:

1. **Wallow is already stateless** - JWT auth, Redis cache, SignalR backplane, and RabbitMQ messaging are configured
2. **Use a load balancer** - Nginx, HAProxy, Caddy, or cloud load balancer
3. **Configure health checks** - Use `/health/ready` for traffic routing
4. **Monitor per-instance and aggregate metrics** - OpenTelemetry exports to your observability platform
5. **Scale based on metrics** - CPU, memory, latency, and queue depth
6. **Watch for bottlenecks** - Database and message processing often become limiting factors before CPU

Start with 2 instances for high availability, then scale based on observed metrics.
