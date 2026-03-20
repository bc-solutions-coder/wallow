# Wallow Platform Roadmap

> Future enhancements and improvements for the Wallow modular monolith platform
>
> Based on comprehensive audit findings (2026-02-21)

---

## 1. Immediate Fixes (P0 - This Sprint)

**Critical security and architecture issues that MUST be fixed before production deployment.**

### Security Critical

#### 1.1 SQL Injection Vulnerabilities
- **Location:** Configuration.CustomFieldIndexManager
- **Impact:** Full database compromise possible
- **Fix:** Replace string interpolation with parameterized queries
- **Effort:** 4 hours
- **Dependencies:** None

#### 1.2 Missing JWT Authentication Configuration
- **Location:** Program.cs
- **Impact:** Application accepts ANY authentication scheme by default
- **Fix:** Add `services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` with Keycloak integration
- **Effort:** 8 hours
- **Dependencies:** None

#### 1.3 Missing Authorization on Financial Endpoints
- **Location:** Billing.InvoicesController
- **Impact:** Any authenticated user can read/modify all invoices (IDOR vulnerability)
- **Fix:** Add [HasPermission] attributes to all controller actions
- **Effort:** 16 hours (audit all 5 modules)
- **Dependencies:** None

#### 1.4 Security Headers Configuration
- **Location:** Program.cs
- **Impact:** Vulnerable to clickjacking, MIME-sniffing, XSS
- **Fix:** Add HSTS, X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy
- **Effort:** 4 hours
- **Dependencies:** None

### Architecture Critical

#### 1.5 Identity SCIM Token Creation Bug
- **Location:** Identity.ScimConfiguration.Create()
- **Impact:** Initial SCIM token creation doesn't return plaintext token to caller
- **Fix:** Return plaintext token before hashing
- **Effort:** 2 hours
- **Dependencies:** None

---

## 2. Short-term Improvements (P1 - Next 2 Sprints)

**High-priority enhancements that improve security, functionality, and platform stability.**

### Security High Priority

#### 2.1 XSS Sanitization Across All User Content
- **Modules:** Announcements, Notifications, Messaging, SignalR
- **Fix:** Add HtmlSanitizer NuGet package, sanitize all HTML before storage
- **Effort:** 16 hours
- **Impact:** Prevents stored XSS attacks

#### 2.2 Rate Limiting Implementation
- **Location:** Program.cs
- **Fix:** Add .NET rate limiting middleware with per-endpoint policies
- **Effort:** 16 hours
- **Impact:** Prevents DoS, brute force, API abuse

#### 2.3 HTTPS Redirection and HSTS
- **Location:** Program.cs
- **Fix:** Add `UseHttpsRedirection()` and `UseHsts()` with proper configuration
- **Effort:** 4 hours
- **Impact:** Enforces encrypted traffic

#### 2.4 Development CORS Hardening
- **Location:** ServiceCollectionExtensions.cs
- **Fix:** Remove `SetIsOriginAllowed(_ => true)` in development, use explicit origins
- **Effort:** 2 hours
- **Impact:** Prevents credential leakage

#### 2.5 Secrets Management Migration
- **Location:** appsettings.json
- **Fix:** Move all secrets to environment variables, User Secrets (dev), Azure Key Vault (production)
- **Effort:** 16 hours
- **Impact:** Removes hardcoded secrets from repository

### Functionality High Priority

#### 2.6 FluentValidation for All Commands
- **Modules:** Announcements, Messaging, Notifications
- **Fix:** Add validators to all command handlers
- **Effort:** 8 hours
- **Impact:** Consistent validation, better error messages

---

## 3. Medium-term Enhancements (P2 - Next Quarter)

**Important improvements that enhance platform capabilities and operational maturity.**

### Platform Capabilities

#### 3.1 API Versioning Strategy
- **Scope:** All API controllers
- **Approach:** URL versioning (v1, v2) with ASP.NET Core API versioning
- **Effort:** 40 hours
- **Impact:** Enables breaking changes without disrupting existing clients

#### 3.2 Notification Preferences System
- **Module:** Notifications, Messaging
- **Features:** Per-channel preferences, quiet hours, frequency limits
- **Effort:** 40 hours
- **Impact:** Reduces notification fatigue, improves user experience

#### 3.3 Notification Cleanup and Retention
- **Module:** Messaging
- **Fix:** Auto-delete read notifications after 90 days, archive after 30 days
- **Effort:** 16 hours
- **Impact:** Prevents unbounded database growth

#### 3.4 Audit Retention Policy
- **Location:** Shared.Infrastructure/Auditing
- **Features:** Archive old entries to cold storage, retention rules by entity type
- **Effort:** 24 hours
- **Impact:** Compliance + cost optimization

#### 3.5 Custom Fields System Completion
- **Module:** Identity (per-tenant profile fields)
- **Features:** Field validation rules, conditional visibility, calculated fields
- **Effort:** 40 hours
- **Impact:** Unlocks full custom field power

#### 3.6 Storage Retention Policy Enforcement
- **Module:** Storage
- **Features:** Auto-delete expired files, archive to Glacier, lifecycle rules
- **Effort:** 24 hours
- **Impact:** Cost optimization, compliance

#### 3.7 Path Traversal and File Upload Security
- **Module:** Storage
- **Features:** Path validation, magic byte detection, filename sanitization
- **Effort:** 16 hours
- **Impact:** Prevents security vulnerabilities

### Operational Maturity

#### 3.8 Dapper Query Automated Tenant Filtering
- **Scope:** All modules using Dapper
- **Approach:** SQL query interceptor or base repository pattern
- **Effort:** 24 hours
- **Impact:** Prevents accidental cross-tenant data leaks

#### 3.9 API Key Rotation Mechanism
- **Module:** Identity
- **Features:** Grace period, expiry warnings, automated rotation
- **Effort:** 16 hours
- **Impact:** Security best practices

#### 3.10 Dependabot Configuration
- **Scope:** Repository root
- **Features:** Daily dependency vulnerability scanning, auto-PR creation
- **Effort:** 4 hours
- **Impact:** Automated security updates

---

## 4. Long-term Vision (P3 - Future)

**Strategic initiatives that position Wallow as an enterprise-grade platform.**

### Deployment & Infrastructure

#### 4.1 Kubernetes Deployment Manifests
- **Deliverables:** Helm charts, kustomize overlays, ingress/service configs
- **Features:** Auto-scaling, health checks, pod disruption budgets
- **Effort:** 80 hours
- **Impact:** Production-grade orchestration

#### 4.2 Multi-Region Deployment Support
- **Approach:** Active-active with CockroachDB or read replicas
- **Features:** Geo-aware routing, data residency compliance
- **Effort:** 160 hours
- **Impact:** Global scale, disaster recovery

#### 4.3 Microservice Extraction Toolkit
- **Scope:** Service template, database splitting, API gateway patterns
- **Approach:** Extract Notifications → Billing → Identity
- **Effort:** 200+ hours
- **Impact:** Independent scaling, team autonomy

### Developer Experience

#### 4.4 GraphQL Gateway (Optional)
- **Approach:** Hot Chocolate or Strawberry Shake over existing CQRS layer
- **Features:** Federation, DataLoader, real-time subscriptions
- **Effort:** 80 hours
- **Impact:** Flexible client queries, reduced API calls

#### 4.5 Mobile SDK & API Client Libraries
- **Languages:** .NET, TypeScript, Swift, Kotlin
- **Features:** Auto-generated from OpenAPI spec, built-in auth
- **Effort:** 120 hours
- **Impact:** Faster client integration

#### 4.6 Plugin/Marketplace System
- **Approach:** MEF or AssemblyLoadContext-based plugin architecture
- **Features:** Module discovery, versioning, sandboxing
- **Effort:** 160 hours
- **Impact:** Third-party extensibility

### Advanced Features

#### 4.7 AI/ML Integration Points
- **Use Cases:** Content moderation, fraud detection (Billing)
- **Approach:** Azure Cognitive Services or OpenAI API integration
- **Effort:** 80+ hours per feature
- **Impact:** Intelligent automation

#### 4.8 White-Label Theming System
- **Module:** Configuration or new Theming module
- **Features:** Per-tenant logos, colors, fonts, custom CSS
- **Effort:** 80 hours
- **Impact:** Multi-customer SaaS ready

#### 4.9 Real-Time Collaboration Features
- **Module:** New Collaboration module
- **Features:** Operational transform (OT), presence, cursors, co-editing
- **Approach:** SignalR + Yjs or Automerge
- **Effort:** 120 hours
- **Impact:** Modern SaaS UX

#### 4.10 Advanced Workflow Designer
- **Module:** Workflows
- **Features:** Visual designer UI, BPMN support, human tasks
- **Effort:** 160 hours
- **Impact:** Low-code automation

---

## 5. Module Enhancement Ideas

**Specific enhancements for each of the 8 core modules and shared infrastructure.**

### Identity
- Multi-factor authentication (TOTP, SMS, hardware keys)
- Passkey/WebAuthn support for passwordless login
- Social login providers (Google, GitHub, Microsoft)
- Session management UI (view/revoke active sessions)
- Login audit trail with geo-location

### Billing
- Payment gateway integrations (Stripe, PayPal, Square)
- Recurring invoicing and subscriptions
- Automated dunning workflow for failed payments
- Tax calculation service integration (Avalara, TaxJar)
- Multi-currency support with exchange rates
- Usage-based billing integration with metering

### Notifications
- Rich HTML email templating engine (Liquid or Handlebars)
- Per-channel notification preferences (email, push)
- Digest mode (daily/weekly notification summaries)
- Push notification support (FCM, APNs)
- Email analytics (open rate, click rate, bounce handling)

### Messaging
- Per-user message preferences and quiet hours
- Message threading and reactions
- Read receipts and delivery status

### Announcements
- Scheduled announcement publishing and expiration
- Audience targeting by role or tenant segment
- Announcement analytics (views, dismissals)

### Storage
- Multiple storage providers (Azure Blob, GCS, Backblaze)
- CDN integration for public assets
- Image transformation service (resize, crop, optimize)
- Virus scanning integration (ClamAV, VirusTotal)
- File versioning with rollback

### Inquiries
- Spam filtering and rate limiting per submitter
- Inquiry routing rules by category or tenant
- Email auto-reply on submission

### Shared Infrastructure

#### Auditing
- Advanced querying (by user, entity, date range)
- Export for compliance audits (JSON, CSV)
- Diff visualization for entity changes
- Alert rules for suspicious activity
- Log forwarding to SIEM (Splunk, Datadog)

#### Background Jobs
- Job dependency graphs (job X runs after Y completes)
- Retry policies with exponential backoff
- Job execution history and logs
- Job performance monitoring
- Manual job triggers from admin UI

#### Workflows
- Visual workflow designer (drag-and-drop)
- Human approval steps with timeout
- Workflow analytics (execution time, failure rate)
- Workflow versioning and rollback
- Integration with external systems (Zapier-style)

---

## 6. Technical Debt Register

**Known TODOs, FIXMEs, stubs, and missing implementations from audit.**

### Critical Gaps

1. **SCIM Token Plaintext** - Identity.ScimConfiguration.Create() doesn't return plaintext token
2. **SQL Injection** - CustomFieldIndexManager uses string interpolation
3. **No JWT Config** - Authentication middleware configured but no JWT provider registered
4. **No [HasPermission]** - Most controllers only have [Authorize], no permission checks

### Missing Implementations

5. **Feature Flag Evaluation** - Percentage and variant flags don't work
6. **Notification Cleanup** - No expiry or deletion logic
7. **Storage Retention** - Retention policy exists but not enforced
8. **Audit Retention** - No archival or cleanup
9. **UserEmail Empty** - Hardcoded to empty string in many events

### Validation Gaps

10. **Announcements Validators** - No command validators

### Security Gaps

11. **Rate Limiting** - No rate limiting anywhere
12. **Security Headers** - No HSTS, CSP, X-Frame-Options, etc.
13. **XSS Sanitization** - Announcements, Notifications lack HTML sanitization
14. **Path Traversal** - LocalStorageProvider doesn't validate path escapes
15. **Filename Sanitization** - Storage doesn't sanitize filenames
16. **SSRF Protection** - Workflows WebhookActivity doesn't validate URLs
17. **HTTPS Redirection** - Not enforced
18. **TLS for DB/MQ** - Connection strings don't enforce SSL/TLS
19. **Secrets in Repo** - appsettings.json contains hardcoded credentials

### Testing Gaps

34. **Integration Test Container Sharing** - Fixed recently but needs validation
35. **Architecture Tests** - Need authorization enforcement tests
36. **Performance Tests** - No load testing suite

### Documentation Gaps

37. **Module README** - No README.md in individual module directories
38. **Module CLAUDE.md** - Only Scheduler has module-level CLAUDE.md
39. **API Documentation** - Scalar configured but needs enhanced descriptions
40. **Deployment Runbooks** - Incident response procedures not documented

---

## 7. Architecture Evolution

**Strategic architectural improvements over time.**

### CQRS Maturity

#### Current State
- Commands and queries separated logically
- Single database for both reads and writes
- Dapper used for complex read queries

#### Evolution Path
1. **Read Database** (P3) - Dedicated PostgreSQL read replica for queries
2. **Materialized Views** (P2) - PostgreSQL materialized views for complex aggregations
3. **Search Index** (P3) - Elasticsearch for full-text search
4. **Query Caching** (P2) - Redis caching layer for hot queries
5. **GraphQL Layer** (P3) - Flexible client-driven queries over CQRS

### Observability Improvements

#### Current State
- Serilog structured logging
- OpenTelemetry configured with OTLP export
- Grafana dashboards via LGTM stack

#### Evolution Path
1. **Custom Metrics** (P2) - Business metrics (orders/min, revenue/hour, active users)
2. **Distributed Tracing** (P2) - W3C Trace Context across all modules and RabbitMQ
3. **Log Correlation** (P2) - Trace IDs in all log entries
4. **Alerting Rules** (P2) - Prometheus alerts for SLI/SLO violations
5. **APM Integration** (P3) - Application Performance Monitoring (Datadog, New Relic)

### Performance Optimization

#### Current State
- No query optimization
- No caching strategy
- No performance budgets

#### Evolution Path
1. **Query Analysis** (P2) - Identify N+1 queries with MiniProfiler or EF Core logging
2. **Caching Strategy** (P2) - Redis for reference data, query results, session state
3. **Read Replicas** (P3) - Offload read traffic to PostgreSQL replicas
4. **Connection Pooling** (P2) - Optimize Npgsql connection pool settings
5. **CDN for Static Assets** (P3) - CloudFront or Cloudflare for Storage module

### Resilience Patterns

#### Current State
- Wolverine durable outbox for message delivery
- No circuit breakers or retry policies

#### Evolution Path
1. **Polly Policies** (P2) - Retry, circuit breaker, timeout for external calls
2. **Saga Compensation** (P2) - Proper rollback logic in all sagas
3. **Idempotency Keys** (P2) - Prevent duplicate command processing
4. **Health Checks** (P2) - Deep health checks for DB, RabbitMQ, Redis, external APIs
5. **Graceful Degradation** (P3) - Fallback behaviors when dependencies fail

### API Evolution

#### Current State
- REST API with OpenAPI/Swagger
- No versioning strategy
- No pagination standards

#### Evolution Path
1. **API Versioning** (P2) - URL versioning (v1, v2) with ASP.NET Core API versioning
2. **Pagination RFC 5988** (P2) - Link headers, consistent page size defaults
3. **HATEOAS** (P3) - Hypermedia links for discoverability (optional)
4. **Rate Limiting** (P1) - Per-endpoint and per-tenant rate limits
5. **API Gateway** (P3) - Kong or YARP for centralized routing, auth, rate limiting

### Data Architecture

#### Current State
- Single PostgreSQL instance
- Separate schema per module

#### Evolution Path
1. **Database per Service** (P3) - If extracting microservices
2. **Multi-Tenancy Models** (P3) - Option for database-per-tenant for enterprise
4. **Backup Strategy** (P2) - Automated backups, point-in-time recovery, backup testing
5. **Data Warehouse** (P3) - ClickHouse or Snowflake for analytics

### Security Hardening

#### Current State
- Basic JWT authentication (after P0 fixes)
- Multi-tenancy enforced via interceptors
- No advanced threat protection

#### Evolution Path
1. **WAF Integration** (P3) - CloudFlare, AWS WAF, or Azure Front Door
2. **Secrets Scanning** (P2) - GitGuardian or TruffleHog in CI/CD
3. **Penetration Testing** (P3) - Annual third-party security audit
4. **Compliance Certifications** (P3) - SOC 2 Type II, ISO 27001
5. **Zero Trust Architecture** (P3) - Service mesh with mTLS (Istio, Linkerd)

---

## Implementation Priorities

### Q1 2026 (Current Quarter)
- Fix all P0 critical issues (security + architecture)
- Implement P1 security hardening
- Module simplification (24 -> 8 modules)

### Q2 2026
- API versioning strategy
- Notification preferences and cleanup
- Email templating improvements

### Q3 2026
- Custom fields system completion
- Audit retention and tamper protection
- Advanced observability (custom metrics, distributed tracing)
- Performance optimization (caching, query optimization)

### Q4 2026
- Microservice extraction toolkit
- Kubernetes deployment manifests
- Advanced workflow features
- Plugin/marketplace system foundation

### 2027 and Beyond
- Multi-region deployment
- AI/ML integration points
- White-label theming
- GraphQL gateway
- Mobile SDK

---

## Success Metrics

### Platform Stability
- Zero critical security vulnerabilities
- 99.9% uptime SLA
- < 200ms p95 API response time
- < 5% error rate

### Developer Experience
- < 30 minutes to local environment setup
- < 1 day to add a new module
- Comprehensive documentation coverage
- Active community contributions

### Business Enablement
- Multi-tenant SaaS ready
- Enterprise compliance (SOC 2, ISO 27001)
- Sub-second page loads
- Support for 10,000+ concurrent users

---

**Document Version:** 1.0
**Last Updated:** 2026-02-21
**Next Review:** Q2 2026
