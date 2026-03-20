# Wallow Architecture Diagrams

> Visual architecture documentation for the Wallow modular monolith platform

**Date:** 2026-02-28
**System:** Wallow Platform - 8 Modules + Shared Infrastructure
**Technology:** .NET 10, PostgreSQL, RabbitMQ, Wolverine, Keycloak, Hangfire, Elsa 3

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Module Interaction & Event Flow](#2-module-interaction--event-flow)
3. [Request Data Flow](#3-request-data-flow)
4. [Deployment Architecture](#4-deployment-architecture)
5. [Security Boundaries](#5-security-boundaries)
6. [Module Dependency Graph](#6-module-dependency-graph)

---

## 1. System Overview

High-level view of all 8 modules and shared infrastructure.

```mermaid
graph TB
    subgraph "Platform Modules (8)"
        Identity[Identity<br/>Users, SSO, SCIM, API Keys, Service Accounts]
        Billing[Billing<br/>Invoices, Payments, Subscriptions, Metering]
        Notifications[Notifications<br/>Email Delivery via MailKit]
        Messaging[Messaging<br/>In-App Real-Time via SignalR]
        Announcements[Announcements<br/>Broadcast Announcements]
        Storage[Storage<br/>S3, Local FS, Buckets]
        Inquiries[Inquiries<br/>Contact Form Processing]
        Showcases[Showcases<br/>Public Showcase Listings]
    end

    subgraph "Shared Infrastructure"
        SharedKernel[Shared.Kernel<br/>DDD Primitives, Multi-tenancy, Result Pattern]
        SharedInfra[Shared.Infrastructure<br/>Auditing, Background Jobs, Workflows]
        SharedContracts[Shared.Contracts<br/>Integration Events, Service Interfaces]
    end

    subgraph "API Host"
        Api[Wallow.Api<br/>Composition Root, Middleware Pipeline]
    end

    Identity -.-> SharedContracts
    Billing -.-> SharedContracts
    Notifications -.-> SharedContracts

    Api --> Identity
    Api --> Billing
    Api --> Notifications
    Api --> Messaging
    Api --> Announcements
    Api --> Storage
    Api --> Inquiries
    Api --> Showcases
```

---

## 2. Module Interaction & Event Flow

Integration events flowing between modules via RabbitMQ (Wolverine).

```mermaid
graph LR
    subgraph Publishers
        Identity[Identity]
        Billing[Billing]
    end

    subgraph Consumers
        Notifications[Notifications]
    end

    Identity -->|UserRegistered| Notifications
    Identity -->|PasswordResetRequested| Notifications
    Billing -->|InvoiceCreated| Notifications
    Billing -->|InvoicePaid| Notifications
    Billing -->|InvoiceOverdue| Notifications
```

**Key Points:**
- Modules communicate only via integration events through Shared.Contracts
- Notifications subscribes to events from other modules and delivers through appropriate channels
- Modules never call Notifications directly

---

## 3. Request Data Flow

How a typical HTTP request flows through the system.

```mermaid
sequenceDiagram
    participant Client
    participant Middleware
    participant TenantRes as Tenant Resolution
    participant Auth as Authorization
    participant Controller
    participant Wolverine
    participant Handler
    participant Repo as Repository
    participant DB as PostgreSQL
    participant Bus as RabbitMQ

    Client->>Middleware: HTTP Request (JWT)
    Middleware->>Middleware: GlobalExceptionHandler
    Middleware->>Middleware: Serilog Logging
    Middleware->>Middleware: API Key / JWT Auth

    Middleware->>TenantRes: Extract org claim
    TenantRes->>TenantRes: Set ITenantContext

    Middleware->>Auth: Permission Expansion
    Auth->>Auth: Roles → Permissions
    Auth->>Auth: HasPermission check

    Middleware->>Controller: Route to endpoint
    Controller->>Wolverine: Send command/query

    alt Command (Write)
        Wolverine->>Handler: Handle command
        Handler->>Repo: Save aggregate
        Repo->>DB: EF Core SaveChanges
        Note over Repo,DB: TenantSaveChangesInterceptor<br/>auto-stamps TenantId
        Repo->>Bus: Publish integration event<br/>(via Wolverine outbox)
        Handler->>Wolverine: Return Result<T>
    else Query (Read)
        Wolverine->>Handler: Handle query
        Handler->>DB: Dapper query
        Note over Handler,DB: Tenant filter<br/>in WHERE clause
        DB->>Handler: Return data
        Handler->>Wolverine: Return DTO
    end

    Wolverine->>Controller: Result
    Controller->>Client: HTTP Response (200/400/404)

    Bus-->>Bus: Async event delivery
    Bus-->>Handler: Consumer handles event
```

**Key Points:**
- **Tenant Resolution**: JWT `org` claim → ITenantContext
- **Authorization**: Role-based → Permission expansion → HasPermission attribute
- **CQRS**: Commands use EF Core + outbox, Queries use Dapper
- **Multi-tenancy**: Automatic via interceptor (writes) and query filters (reads)

---

## 4. Deployment Architecture

Docker Compose services, CI/CD pipeline, and environment promotion.

```mermaid
graph TB
    subgraph "Docker Compose (Local/Server)"
        Postgres[(PostgreSQL 18<br/>Port 5432)]
        RabbitMQ[RabbitMQ 4.2<br/>AMQP: 5672<br/>Mgmt: 15672]
        Valkey[Valkey 8<br/>Port 6379]
        Keycloak[Keycloak 26.0<br/>Port 8080]
        Mailpit[Mailpit<br/>SMTP: 1025<br/>Web: 8025]
        Grafana[Grafana LGTM<br/>UI: 3000<br/>OTLP: 4317/4318]

        Api[Wallow.Api<br/>ASP.NET Core<br/>Port 5000]
    end

    Api --> Postgres
    Api --> RabbitMQ
    Api --> Valkey
    Api --> Keycloak
    Api --> Mailpit
    Api --> Grafana

    subgraph "CI/CD Pipeline (GitHub Actions)"
        Dev[Push to dev]
        Main[Push to main]
        Tag[Tag v*]

        BuildDev[deploy-dev.yml<br/>Build + Test]
        BuildStg[deploy-staging.yml<br/>Build + Test]
        BuildProd[deploy-prod.yml<br/>Build + Test]

        GHCR[GitHub Container Registry<br/>ghcr.io]

        DevServer[Development Server]
        StgServer[Staging Server]
        ProdServer[Production Server]
    end

    Dev --> BuildDev
    Main --> BuildStg
    Tag --> BuildProd

    BuildDev --> GHCR
    BuildStg --> GHCR
    BuildProd --> GHCR

    GHCR -->|dev tag| DevServer
    GHCR -->|staging tag| StgServer
    GHCR -->|v1.2.3 tag| ProdServer
```

**CI/CD Flow:**
- **Dev**: Push to `dev` → deploy-dev.yml → ghcr.io:dev → Dev server
- **Staging**: Push to `main` → deploy-staging.yml → ghcr.io:staging → Staging server
- **Production**: Tag `v*` → deploy-prod.yml → ghcr.io:v1.2.3 → Prod server

---

## 5. Security Boundaries

Authentication, authorization, and multi-tenant isolation layers.

```mermaid
graph TB
    Client[Client Application<br/>Web/Mobile]

    subgraph "API Host Security Layers"
        ApiKey[API Key Auth<br/>Middleware]
        JWT[JWT Bearer Auth<br/>Keycloak OIDC]
        TenantRes[Tenant Resolution<br/>JWT org claim]
        PermExp[Permission Expansion<br/>Roles → Permissions]
        AuthZ[Authorization<br/>HasPermission attribute]
        SCIM[SCIM Auth<br/>SCIM token hash]
    end

    subgraph "Keycloak Realm: wallow"
        Users[(Users)]
        Orgs[(Organizations<br/>tenant mapping)]
        Roles[(Roles)]
        SA[Service Accounts]
        SSO[SSO Providers<br/>SAML/OIDC]
    end

    subgraph "Multi-Tenant Isolation"
        TenantCtx[ITenantContext<br/>Current tenant state]
        SaveInterceptor[TenantSaveChangesInterceptor<br/>Auto-stamp TenantId]
        QueryFilter[TenantQueryExtensions<br/>Auto-filter reads]
    end

    subgraph "Data Access"
        EFCore[EF Core<br/>Write operations]
        Dapper[Dapper<br/>Read queries]
        DB[(PostgreSQL<br/>Separate schemas per module)]
    end

    Client -->|API Key| ApiKey
    Client -->|JWT| JWT
    Client -->|SCIM request| SCIM

    ApiKey --> TenantRes
    JWT --> TenantRes
    SCIM --> TenantRes

    TenantRes --> TenantCtx
    TenantCtx --> PermExp

    PermExp --> AuthZ

    AuthZ --> EFCore
    AuthZ --> Dapper

    EFCore --> SaveInterceptor
    SaveInterceptor --> DB

    Dapper --> QueryFilter
    QueryFilter --> DB

    Keycloak --> Users
    Keycloak --> Orgs
    Keycloak --> Roles
    Keycloak --> SA
    Keycloak --> SSO
```

**Security Layers:**
1. **Authentication**: API Key OR JWT OR SCIM token
2. **Tenant Resolution**: Extract `org` claim from JWT → ITenantContext
3. **Permission Expansion**: Roles → Permissions via RolePermissionMapping
4. **Authorization**: `[HasPermission("InvoicesRead")]` attribute
5. **Data Isolation**:
   - **Writes**: TenantSaveChangesInterceptor auto-stamps TenantId
   - **Reads**: Query filters auto-add `WHERE TenantId = @current`

---

## 6. Module Dependency Graph

Shows module dependencies on shared infrastructure and cross-module service interfaces.

```mermaid
graph TB
    subgraph "8 Modules"
        M1[Identity]
        M2[Billing]
        M3[Notifications]
        M4[Messaging]
        M5[Announcements]
        M6[Storage]
        M7[Inquiries]
        M8[Showcases]
    end

    subgraph "Shared Infrastructure"
        SK[Shared.Kernel<br/>Entity, AggregateRoot<br/>ITenantScoped<br/>Result&lt;T&gt;<br/>TenantId]

        SI[Shared.Infrastructure<br/>Auditing (Audit.NET)<br/>Background Jobs (Hangfire)<br/>Workflows (Elsa 3)]

        SC[Shared.Contracts<br/>Integration Events<br/>Service Interfaces]
    end

    M1 --> SK
    M2 --> SK
    M3 --> SK
    M4 --> SK
    M5 --> SK
    M6 --> SK
    M7 --> SK
    M8 --> SK

    M1 -.->|publishes events| SC
    M2 -.->|publishes events| SC
    M3 -.->|consumes events| SC

    subgraph "Cross-Module Service Interfaces"
        IUserQuery[IUserQueryService]
        IInvoiceQuery[IInvoiceQueryService]
        IMeteringQuery[IMeteringQueryService]
    end

    SC -.-> IUserQuery
    SC -.-> IInvoiceQuery
    SC -.-> IMeteringQuery

    M1 -->|implements| IUserQuery
    M2 -->|implements| IInvoiceQuery
    M2 -->|implements| IMeteringQuery

    subgraph "External Dependencies"
        Keycloak[Keycloak<br/>Identity Provider]
        MailKit[MailKit<br/>SMTP]
        S3[S3-compatible<br/>Storage]
        Elsa[Elsa Workflows<br/>Engine]
    end

    M1 --> Keycloak
    M3 --> MailKit
    M6 --> S3
    SI --> Elsa
```

**Dependency Rules:**
- Modules depend on **Shared.Kernel** for DDD primitives, multi-tenancy
- Modules depend on **Shared.Contracts** for integration events only (zero external deps)
- **Shared.Infrastructure** provides cross-cutting: Auditing, Background Jobs, Workflows
- No direct module-to-module references (enforced by project structure)

---

## Module Quick Reference

| Module | Architecture | Key Features | Status |
|--------|-------------|-------------|--------|
| **Identity** | EF Core | Keycloak integration, API keys, service accounts, SSO, SCIM, RBAC | Production |
| **Billing** | EF Core | Invoices, payments, subscriptions, metered usage tracking | Production |
| **Notifications** | EF Core | Email delivery via MailKit, SMTP configuration | Production |
| **Messaging** | EF Core | In-app real-time messages via SignalR | Production |
| **Announcements** | EF Core | Broadcast announcements with targeting rules | Production |
| **Storage** | EF Core | S3/Local FS, buckets, presigned URLs | Production |
| **Inquiries** | EF Core | Contact form processing and routing | Production |
| **Showcases** | EF Core | Public-facing showcase listings | Production |

---

## References

- **Design Docs**: `docs/plans/*.md`
- **Developer Guide**: `docs/DEVELOPER_GUIDE.md`
- **Deployment Guide**: `docs/DEPLOYMENT_GUIDE.md`
