# Wallow Deployment Strategies

This guide helps you scale Wallow as your workload grows. Start simple, measure everything, and scale only the components that become bottlenecks.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Architecture Overview](#2-architecture-overview)
3. [Decision Flowchart](#3-decision-flowchart)
4. [Strategy Quick Reference](#4-strategy-quick-reference)
5. [Metrics to Monitor](#5-metrics-to-monitor)
6. [Cost Considerations](#6-cost-considerations)
7. [Detailed Guides](#7-detailed-guides)

---

## 1. Executive Summary

### What Scaling Means for a Modular Monolith

Wallow is a **modular monolith** — a single deployable unit composed of 5 autonomous modules (Identity, Billing, Communications, Storage, Configuration) that communicate via RabbitMQ events. This architecture provides:

- **Simpler operations** than microservices (one deployment, one codebase)
- **Clear module boundaries** that enable future extraction if needed
- **Horizontal scalability** through stateless API instances

Unlike microservices, you don't start with distributed complexity. Unlike a traditional monolith, module boundaries are enforced, making selective extraction possible later.

### Why Wallow Scales Horizontally

Wallow is designed for horizontal scaling from day one:

| Component | Stateless? | Scalable? | Notes |
|-----------|------------|-----------|-------|
| API instances | Yes | Unlimited | No sticky sessions, no local state |
| Background workers | Yes | Unlimited | Compete for messages via RabbitMQ |
| PostgreSQL | No | With replicas | Primary for writes, replicas for reads |
| RabbitMQ | Clustered | Yes | Built-in clustering and federation |
| Valkey/Redis | Clustered | Yes | Used only for caching and SignalR |

**The rule**: Scale compute (API, workers) easily. Scale data (PostgreSQL) carefully.

---

## 2. Architecture Overview

### Single-Instance Deployment (Starting Point)

```
                                 ┌─────────────────────────────────────────────┐
                                 │                Single Server                │
                                 │                                             │
    Clients ────────────────────>│  ┌─────────────────────────────────────┐   │
                                 │  │            Wallow API              │   │
                                 │  │  ┌─────────┐ ┌─────────┐ ┌───────┐  │   │
                                 │  │  │Identity │ │ Billing │ │ ...   │  │   │
                                 │  │  └─────────┘ └─────────┘ └───────┘  │   │
                                 │  └──────────────────┬──────────────────┘   │
                                 │                     │                      │
                                 │         ┌───────────┼───────────┐          │
                                 │         │           │           │          │
                                 │         ▼           ▼           ▼          │
                                 │  ┌──────────┐ ┌──────────┐ ┌──────────┐   │
                                 │  │PostgreSQL│ │ RabbitMQ │ │  Valkey  │   │
                                 │  └──────────┘ └──────────┘ └──────────┘   │
                                 │                                             │
                                 └─────────────────────────────────────────────┘
```

### Horizontally Scaled Deployment (Target State)

```
                           ┌─────────────────────────────────────────────────────────────────┐
                           │                        Load Balancer                            │
                           │                    (Caddy/Nginx/HAProxy)                        │
                           └────────────────────────────┬────────────────────────────────────┘
                                                        │
                    ┌───────────────────────────────────┼───────────────────────────────────┐
                    │                                   │                                   │
                    ▼                                   ▼                                   ▼
        ┌───────────────────────┐         ┌───────────────────────┐         ┌───────────────────────┐
        │    Wallow API #1     │         │    Wallow API #2     │         │    Wallow API #N     │
        │  (All modules loaded) │         │  (All modules loaded) │         │  (All modules loaded) │
        └───────────┬───────────┘         └───────────┬───────────┘         └───────────┬───────────┘
                    │                                 │                                   │
                    └─────────────────────────────────┼───────────────────────────────────┘
                                                      │
              ┌───────────────────────────────────────┼───────────────────────────────────┐
              │                                       │                                   │
              ▼                                       ▼                                   ▼
   ┌─────────────────────┐             ┌─────────────────────┐             ┌─────────────────────┐
   │      PostgreSQL     │             │       RabbitMQ      │             │    Valkey/Redis     │
   │                     │             │                     │             │                     │
   │  ┌───────────────┐  │             │  ┌───────────────┐  │             │  ┌───────────────┐  │
   │  │    Primary    │  │             │  │    Queues     │  │             │  │    Cache      │  │
   │  │   (writes)    │  │             │  │   • identity  │  │             │  │               │  │
   │  └───────┬───────┘  │             │  │   • billing   │  │             │  ├───────────────┤  │
   │          │          │             │  │   • comms     │  │             │  │   SignalR     │  │
   │          ▼          │             │  │   • storage   │  │             │  │   Backplane   │  │
   │  ┌───────────────┐  │             │  └───────────────┘  │             │  └───────────────┘  │
   │  │   Replica(s)  │  │             │                     │             │                     │
   │  │   (reads)     │  │             │                     │             │                     │
   │  └───────────────┘  │             │                     │             │                     │
   │                     │             │                     │             │                     │
   │    ┌───────────┐    │             └─────────────────────┘             └─────────────────────┘
   │    │ PgBouncer │    │
   │    │  (pool)   │    │
   │    └───────────┘    │
   └─────────────────────┘
```

### Full Production Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                         INTERNET                                                │
└────────────────────────────────────────────┬────────────────────────────────────────────────────┘
                                             │
                                             ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                       CDN (Optional)                                            │
│                              Static assets, geographic caching                                  │
└────────────────────────────────────────────┬────────────────────────────────────────────────────┘
                                             │
                                             ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                      LOAD BALANCER                                              │
│                        Health checks, SSL termination, routing                                  │
│                                                                                                 │
│    ┌────────────────────────────┐                    ┌────────────────────────────┐            │
│    │  api.yourdomain.com/*      │                    │  auth.yourdomain.com/*     │            │
│    │       → API Pool           │                    │       → Keycloak           │            │
│    └────────────────────────────┘                    └────────────────────────────┘            │
└────────────────────────────────────────────┬────────────────────────────────────────────────────┘
                                             │
                    ┌────────────────────────┼────────────────────────┐
                    │                        │                        │
                    ▼                        ▼                        ▼
┌──────────────────────────┐  ┌──────────────────────────┐  ┌──────────────────────────┐
│      API Instance 1      │  │      API Instance 2      │  │      API Instance N      │
│                          │  │                          │  │                          │
│  ┌────────────────────┐  │  │  ┌────────────────────┐  │  │  ┌────────────────────┐  │
│  │      Identity      │  │  │  │      Identity      │  │  │  │      Identity      │  │
│  │      Billing       │  │  │  │      Billing       │  │  │  │      Billing       │  │
│  │   Communications   │  │  │  │   Communications   │  │  │  │   Communications   │  │
│  │      Storage       │  │  │  │      Storage       │  │  │  │      Storage       │  │
│  │   Configuration    │  │  │  │   Configuration    │  │  │  │   Configuration    │  │
│  └────────────────────┘  │  │  └────────────────────┘  │  │  └────────────────────┘  │
│                          │  │                          │  │                          │
│  ┌────────────────────┐  │  │  ┌────────────────────┐  │  │  ┌────────────────────┐  │
│  │   Hangfire Agent   │  │  │  │   Hangfire Agent   │  │  │  │   Hangfire Agent   │  │
│  └────────────────────┘  │  │  └────────────────────┘  │  │  └────────────────────┘  │
└────────────┬─────────────┘  └────────────┬─────────────┘  └────────────┬─────────────┘
             │                             │                             │
             └─────────────────────────────┼─────────────────────────────┘
                                           │
         ┌─────────────────────────────────┼─────────────────────────────────┐
         │                                 │                                 │
         ▼                                 ▼                                 ▼
┌─────────────────────┐      ┌─────────────────────────┐      ┌─────────────────────┐
│     PostgreSQL      │      │        RabbitMQ         │      │       Valkey        │
│                     │      │                         │      │                     │
│  ┌───────────────┐  │      │  ┌─────────────────┐    │      │  ┌───────────────┐  │
│  │    Primary    │◄─┼──────┼──│   Exchanges     │    │      │  │ Distributed   │  │
│  │    (RW)       │  │      │  │  • identity     │    │      │  │    Cache      │  │
│  └───────┬───────┘  │      │  │  • billing      │    │      │  └───────────────┘  │
│          │          │      │  │  • comms        │    │      │                     │
│    Streaming        │      │  │  • storage      │    │      │  ┌───────────────┐  │
│    Replication      │      │  └─────────────────┘    │      │  │   SignalR     │  │
│          │          │      │                         │      │  │   Backplane   │  │
│          ▼          │      │  ┌─────────────────┐    │      │  └───────────────┘  │
│  ┌───────────────┐  │      │  │    Queues       │    │      │                     │
│  │   Replica 1   │  │      │  │  • *-inbox      │    │      │  ┌───────────────┐  │
│  │    (RO)       │  │      │  │  • *-outbox     │    │      │  │   Session     │  │
│  └───────────────┘  │      │  └─────────────────┘    │      │  │   Store       │  │
│  ┌───────────────┐  │      │                         │      │  └───────────────┘  │
│  │   Replica 2   │  │      │  ┌─────────────────┐    │      │                     │
│  │    (RO)       │  │      │  │  Durable Outbox │    │      └─────────────────────┘
│  └───────────────┘  │      │  │  (Wolverine)    │    │
│                     │      │  └─────────────────┘    │
│  ┌───────────────┐  │      │                         │
│  │   PgBouncer   │  │      └─────────────────────────┘
│  │  (Pooling)    │  │
│  └───────────────┘  │
│                     │
└─────────────────────┘
```

---

## 3. Decision Flowchart

Use this flowchart to identify which scaling strategy addresses your specific bottleneck.

```
                              ┌─────────────────────────────┐
                              │   What's the bottleneck?    │
                              └──────────────┬──────────────┘
                                             │
           ┌─────────────────┬───────────────┼───────────────┬─────────────────┐
           │                 │               │               │                 │
           ▼                 ▼               ▼               ▼                 ▼
    ┌─────────────┐   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   ┌─────────────┐
    │ API is CPU/ │   │  Database   │ │   Read-    │ │  Background │   │   Single    │
    │memory bound │   │ connections │ │   heavy    │ │    jobs     │   │  module is  │
    │             │   │  exhausted  │ │  workload  │ │  competing  │   │ bottleneck  │
    └──────┬──────┘   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘   └──────┬──────┘
           │                 │               │               │                 │
           ▼                 ▼               ▼               ▼                 ▼
    ┌─────────────┐   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   ┌─────────────┐
    │  Horizontal │   │  PgBouncer  │ │    Read     │ │   Worker    │   │   Module    │
    │   Scaling   │   │  Connection │ │  Replicas   │ │ Separation  │   │ Extraction  │
    │             │   │   Pooling   │ │             │ │             │   │             │
    │  Add more   │   │  Pool 1000  │ │  Route GET  │ │  Separate   │   │  Extract to │
    │ API servers │   │  connections│ │  to replica │ │  Hangfire   │   │ microservice│
    │  behind LB  │   │  to ~100    │ │  cluster    │ │  workers    │   │             │
    └──────┬──────┘   └──────┬──────┘ └──────┬──────┘ └──────┬──────┘   └──────┬──────┘
           │                 │               │               │                 │
           ▼                 ▼               ▼               ▼                 ▼
    ┌─────────────┐   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   ┌─────────────┐
    │ Complexity: │   │ Complexity: │ │ Complexity: │ │ Complexity: │   │ Complexity: │
    │     LOW     │   │     LOW     │ │   MEDIUM    │ │   MEDIUM    │   │    HIGH     │
    │             │   │             │ │             │ │             │   │             │
    │ Guide: 01   │   │ Guide: 02   │ │ Guide: 02   │ │ Guide: 03   │   │ Guide: 04   │
    └─────────────┘   └─────────────┘ └─────────────┘ └─────────────┘   └─────────────┘
```

### Quick Decision Guide

| Symptom | Likely Cause | First Action |
|---------|--------------|--------------|
| High API response times + high CPU | Compute bottleneck | Add API instances |
| "too many connections" errors | Connection exhaustion | Add PgBouncer |
| Slow read queries, writes are fine | Read contention | Add read replicas |
| API slows down when jobs run | Resource contention | Separate workers |
| One endpoint is 10x slower than others | Module-specific issue | Profile that module |
| Everything is slow | Need to measure | Add observability first |

---

## 4. Strategy Quick Reference

| Strategy | Complexity | When to Use | Prerequisites | Guide |
|----------|:----------:|-------------|---------------|:-----:|
| **Horizontal Scaling** | Low | API CPU/memory at capacity | Load balancer, shared state externalized | [01](./01-horizontal-scaling.md) |
| **PgBouncer** | Low | Connection pool exhausted (>100 connections) | None | [02](./02-database-scaling.md) |
| **Read Replicas** | Medium | Read-heavy workload, acceptable replication lag | PostgreSQL streaming replication | [02](./02-database-scaling.md) |
| **Worker Separation** | Medium | Background jobs impacting API performance | Separate deployment configuration | [03](./03-worker-separation.md) |
| **Module Extraction** | High | Single module dominates resources, different scaling needs | Service mesh, additional infrastructure | [04](./04-module-extraction.md) |

### Recommended Progression

Most applications should scale in this order:

```
1. Optimize First          Don't scale what you can fix
   └── Profiling, query optimization, caching

2. Horizontal Scaling      Easiest wins
   └── Add API instances behind load balancer

3. Connection Pooling      Database sanity
   └── PgBouncer in front of PostgreSQL

4. Read Replicas           Read scaling
   └── Offload reporting, dashboards, search

5. Worker Separation       Job isolation
   └── Dedicated Hangfire workers

6. Module Extraction       Last resort
   └── Only when a module needs independent scaling
```

---

## 5. Metrics to Monitor

### Key Performance Indicators

Before scaling, establish baselines. After scaling, verify improvement.

#### API Performance

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|:-----------------:|:------------------:|--------|
| P95 response time | > 500ms | > 2000ms | Profile endpoints, add instances |
| P99 response time | > 1000ms | > 5000ms | Check for outliers, slow queries |
| Request rate | N/A | Near capacity | Horizontal scaling |
| Error rate (5xx) | > 0.1% | > 1% | Check logs, investigate errors |

#### Compute Resources

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|:-----------------:|:------------------:|--------|
| CPU usage | > 70% sustained | > 90% sustained | Add instances |
| Memory usage | > 80% | > 95% | Check for leaks, add instances |
| GC pause time | > 100ms | > 500ms | Memory pressure, profile allocations |

#### Database Health

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|:-----------------:|:------------------:|--------|
| Active connections | > 80% of max | > 95% of max | Add PgBouncer |
| Query time (P95) | > 100ms | > 1000ms | Query optimization |
| Replication lag | > 1 second | > 10 seconds | Check replica health |
| Dead tuples ratio | > 10% | > 20% | Run VACUUM |
| Index hit ratio | < 99% | < 95% | Add/review indexes |

#### Message Queue Health

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|:-----------------:|:------------------:|--------|
| Queue depth | > 1000 messages | > 10000 messages | Add consumers, check handlers |
| Consumer lag | Growing | Growing rapidly | Add workers |
| Message age | > 1 minute | > 5 minutes | Processing bottleneck |
| Dead letter queue | Any messages | Growing | Fix handler errors |

#### Cache Performance

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|:-----------------:|:------------------:|--------|
| Hit ratio | < 90% | < 70% | Review cache strategy |
| Evictions/sec | High | Very high | Increase cache size |
| Memory usage | > 80% | > 95% | Increase capacity |

### Observability Stack

Wallow includes OpenTelemetry instrumentation. Metrics flow to Grafana:

```bash
# Local development
http://localhost:3001  # Grafana dashboards

# Key dashboards
- API Performance (response times, throughput)
- Database Performance (connections, query times)
- RabbitMQ (queue depths, message rates)
- System Resources (CPU, memory, disk)
```

### Setting Up Alerts

Example Grafana alert rules:

```yaml
# High error rate
- alert: HighErrorRate
  expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.01
  for: 5m
  labels:
    severity: warning

# Slow responses
- alert: SlowResponses
  expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 0.5
  for: 10m
  labels:
    severity: warning

# Database connection exhaustion
- alert: DatabaseConnectionsHigh
  expr: pg_stat_activity_count / pg_settings_max_connections > 0.8
  for: 5m
  labels:
    severity: critical
```

---

## 6. Cost Considerations

### Relative Costs by Strategy

| Strategy | Infrastructure Cost | Operational Complexity | Time to Implement |
|----------|:-------------------:|:----------------------:|:-----------------:|
| **Optimization** | None | Low | Days |
| **Horizontal Scaling** | + 50-100% per instance | Low | Hours |
| **PgBouncer** | + 5% (small VM) | Low | Hours |
| **Read Replicas** | + 50-100% per replica | Medium | Days |
| **Worker Separation** | + 25-50% (smaller instances) | Medium | Days |
| **Module Extraction** | + 100-200% | High | Weeks/Months |

### Cost Optimization Tips

1. **Start with optimization** - Fixing a slow query is cheaper than adding servers
2. **Right-size instances** - Monitor actual usage before upgrading
3. **Use reserved instances** - 30-60% savings for predictable workloads
4. **Scale down off-peak** - Use autoscaling where available
5. **Consider managed services** - Managed PostgreSQL/RabbitMQ can be cheaper than DIY ops

### When to Use Managed Services

| Component | Self-Hosted | Managed | Recommendation |
|-----------|:-----------:|:-------:|----------------|
| PostgreSQL | Lower cost, full control | Higher cost, less ops | Managed if team < 3 devs |
| RabbitMQ | Lower cost | Higher cost, HA built-in | Managed for production |
| Valkey/Redis | Very low cost | Moderate cost | Self-hosted usually fine |
| Kubernetes | High ops overhead | Moderate cost | Managed if using K8s |

---

## 7. Detailed Guides

Each guide provides step-by-step instructions, configuration examples, and rollback procedures.

### [01. Horizontal Scaling](./01-horizontal-scaling.md)

Add API instances behind a load balancer. Covers:
- Load balancer configuration (Caddy, Nginx, HAProxy)
- Health check setup
- Session management with Valkey
- SignalR sticky sessions (when needed)
- Docker Compose and Kubernetes examples

### [02. Database Scaling](./02-database-scaling.md)

Scale PostgreSQL for higher throughput. Covers:
- PgBouncer connection pooling
- Streaming replication setup
- Read replica routing in EF Core
- Connection string management
- Failover procedures

### [03. Worker Separation](./03-worker-separation.md)

Isolate background jobs from API workload. Covers:
- Hangfire server separation
- RabbitMQ consumer isolation
- Resource allocation strategies
- Deployment configurations
- Queue priority management

### [04. Module Extraction](./04-module-extraction.md)

Extract a module to an independent service. Covers:
- When extraction is justified
- Data migration strategies
- API gateway patterns
- Event synchronization
- Rollback procedures
- Case study: Extracting Billing

---

## Quick Start Checklist

Before scaling, verify you have:

- [ ] **Observability** - Metrics and dashboards in place
- [ ] **Baselines** - Know your current performance numbers
- [ ] **Bottleneck identified** - Measured, not guessed
- [ ] **Optimization attempted** - Scaling hides problems
- [ ] **Rollback plan** - Every change should be reversible

---

## Related Documentation

- [Deployment Guide](../DEPLOYMENT_GUIDE.md) - Initial deployment and CI/CD
- [Developer Guide](../DEVELOPER_GUIDE.md) - Development workflows
- [Wallow Architecture](../WALLOW.md) - System architecture overview
