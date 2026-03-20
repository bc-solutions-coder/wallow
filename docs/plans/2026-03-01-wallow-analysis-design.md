# Wallow Platform Analysis & Strategic Design

**Date:** 2026-03-01
**Scope:** Complete codebase analysis, fork strategy, SMS integration, inbox/messaging, module expansion

---

## 1. Verdict: Is Wallow a Solid Generic Base?

**Yes — with targeted fixes.** The 5 implemented modules (Billing, Communications, Identity, Configuration, Storage) are architecturally exemplary. The shared infrastructure is well-designed. The CI/CD pipeline is production-ready with 90% coverage enforcement, automated releases, and Docker publishing.

Wallow is ready to fork into your first product. The fixes below are improvements, not blockers.

---

## 2. What's Excellent (Keep As-Is)

| Area | Why It's Good |
|------|---------------|
| **DDD building blocks** | Strongly-typed IDs, `AggregateRoot<TId>`, `Entity<TId>`, `ValueObject`, Result pattern with Map/Bind |
| **Module isolation** | Schema-per-module, no cross-module references, architecture tests enforce boundaries |
| **Wolverine CQRS + messaging** | Single mental model for in-process commands and distributed events, configurable transport |
| **Keycloak delegation** | No custom auth code — forks get enterprise SSO, SCIM, and SSO for free |
| **CI/CD pipeline** | 90% coverage gate, release-please semver, Docker publish to GHCR, format enforcement |
| **Billing module** | Gold standard reference: state machine aggregates, Dapper reads, metering, Hangfire jobs |
| **Communications channel model** | Elegant `Channels/Email`, `Channels/InApp`, `Announcements` hierarchy — extensible for SMS/Push |
| **Plugin system** | AssemblyLoadContext isolation, manifest permissions — forks can ship closed-source extensions |
| **OpenTelemetry + Serilog** | End-to-end observability out of the box |
| **Audit interceptor** | Compliance-ready cross-cutting audit trail without module code changes |

---

## 3. Critical Fixes (Do Before Forking)

### 3.1 Tenant Context in Wolverine Message Handlers

**Problem:** Background message handlers processing RabbitMQ messages have no `ITenantContext`. Every message-driven workflow in forks will silently fail or operate without tenant isolation.

**Fix:** Add Wolverine middleware that extracts `TenantId` from message headers and sets `ITenantContext`:

```csharp
// Wolverine middleware
public class TenantContextMiddleware
{
    public static void Before(IMessageContext context, ITenantContext tenantContext)
    {
        if (context.Envelope?.Headers?.TryGetValue("X-Tenant-Id", out string? tenantId) == true
            && Guid.TryParse(tenantId, out Guid tid))
        {
            tenantContext.SetTenant(tid);
        }
    }
}

// When publishing, stamp tenant header
public class TenantStampingMiddleware
{
    public static void Before(IMessageContext context, ITenantContext tenantContext)
    {
        if (tenantContext.IsResolved)
        {
            context.Envelope!.Headers["X-Tenant-Id"] = tenantContext.TenantId.ToString();
        }
    }
}
```

**Priority:** P0 — this is a correctness bug for any multi-tenant fork.

### 3.2 DbContext Tenant Query Filter Boilerplate

**Problem:** Every module DbContext duplicates ~20 lines of identical expression tree code for tenant filters.

**Fix:** Create `TenantAwareDbContext<TContext>` base class in `Wallow.Shared.Infrastructure`:

```csharp
public abstract class TenantAwareDbContext<TContext> : DbContext where TContext : DbContext
{
    private readonly TenantId _tenantId;

    protected TenantAwareDbContext(DbContextOptions<TContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantId = tenantContext.IsResolved
            ? TenantId.Create(tenantContext.TenantId)
            : TenantId.Empty;
    }

    protected void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => EF.Property<TenantId>(e, "TenantId") == _tenantId);
    }
}
```

**Priority:** P1 — reduces per-module boilerplate and prevents copy-paste bugs.

### 3.3 Split ITenantContext Read/Write

**Problem:** `ITenantContext` exposes both `TenantId` (read) and `SetTenant()`/`Clear()` (write) — middleware concerns leak into domain.

**Fix:** Split into `ITenantContextAccessor` (read-only, injected into domain/application) and `ITenantContextSetter` (write, used only by middleware).

**Priority:** P1 — clean API for fork developers.

### 3.4 Delete Redundant Scaffold Modules

**Problem:** 6 scaffolds overlap with implemented modules, causing confusion:
- `Email` → subsumed by `Communications/Channels/Email`
- `Notifications` → subsumed by `Communications/Channels/InApp`
- `Announcements` → subsumed by `Communications/Announcements`
- `Metering` → absorbed into `Billing` (migration confirms)

**Fix:** Delete the redundant scaffolds.

**Priority:** P1 — reduces confusion for fork developers.

### 3.5 Configuration Module Consistency

**Problem:** Configuration collocates command + handler in single files, unlike all other modules.

**Fix:** Refactor to one-file-per-class in subdirectories, matching Billing/Communications pattern.

**Priority:** P2 — consistency matters for a platform base.

### 3.6 Architecture Test Auto-Discovery

**Problem:** `TestConstants.AllModules` is a hardcoded list of 5 modules. New module implementations won't be covered by architecture tests until manually added.

**Fix:** Use assembly scanning to auto-discover modules:

```csharp
public static IEnumerable<string> AllModules => AppDomain.CurrentDomain
    .GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Wallow.") == true
        && a.GetName().Name!.EndsWith(".Domain"))
    .Select(a => a.GetName().Name!.Split('.')[1]);
```

**Priority:** P2 — prevents architectural drift in forks.

---

## 4. Configuration-Driven Module Toggles

Add to `WallowModules.cs` so forks control modules via `appsettings.json`:

```json
{
  "Wallow": {
    "Modules": {
      "Identity": true,
      "Billing": true,
      "Communications": true,
      "Storage": true,
      "Configuration": true
    }
  }
}
```

```csharp
public static IServiceCollection AddWallowModules(
    this IServiceCollection services,
    IConfiguration configuration)
{
    IConfigurationSection modules = configuration.GetSection("Wallow:Modules");

    if (modules.GetValue("Identity", defaultValue: true))
        services.AddIdentityModule(configuration);
    if (modules.GetValue("Billing", defaultValue: true))
        services.AddBillingModule(configuration);
    // ... etc

    services.AddWallowPlugins(configuration);
    return services;
}
```

Forks override in their `appsettings.json` — no source code changes needed to disable modules.

---

## 5. Fork Strategy: Git Fork with Upstream Remote

### Recommended Workflow

```
wallow (upstream)          your-product (fork)
    │                            │
    ├── main ◄───── PR ──────── feature-branches
    │                            │
    │   generic improvements     │   product-specific code
    │   flow back via PR         │   lives only in fork
    │                            │
    v2.0 ──── git merge ──────► fork pulls upstream
    v2.1 ──── git merge ──────► fork pulls upstream
```

**Setup (one-time):**
1. Fork `wallow` on GitHub
2. `git remote add upstream git@github.com:you/wallow.git`
3. Fork inherits all CI workflows — they work immediately
4. Fork gets its own release-please config and independent versioning

**Ongoing sync:**
```bash
git fetch upstream
git rebase upstream/main    # or merge
git push origin main
```

**Contributing back:**
- Build feature in fork, refine it, then re-implement generically in Wallow via PR
- Convention: prefix upstream-intended commits with `[wallow]` for easy identification

**Product-specific modules:** Implement as Wallow plugins (using existing plugin system) so they don't conflict during upstream rebases.

### Why Not NuGet Packages?

Too early. At v0.2.0 with active iteration, the API surface isn't stable enough. NuGet makes sense once core modules stabilize (v1.0+). The fork model lets you iterate faster now and package later.

---

## 6. SMS Integration Design

Add SMS as a third channel inside the Communications module, mirroring the email pattern:

### Domain

```
Communications/Domain/Channels/Sms/
    Identity/SmsMessageId.cs
    Enums/SmsStatus.cs              # Pending, Sent, Failed, Undeliverable
    Entities/SmsMessage.cs          # aggregate: To, Body, Status, RetryCount
    Entities/SmsPreference.cs       # per-user opt-out per notification type
    Events/SmsSentDomainEvent.cs
    Events/SmsFailedDomainEvent.cs
    ValueObjects/PhoneNumber.cs     # E.164 validated value object
```

### Provider Abstraction (also retrofit for Email)

```csharp
// Application layer
public interface ISmsProvider
{
    string ProviderName { get; }
    Task<SmsResult> SendAsync(string to, string body, CancellationToken ct = default);
}

// Infrastructure implementations
public sealed class TwilioSmsProvider : ISmsProvider { ... }
public sealed class NullSmsProvider : ISmsProvider { ... }  // dev/test
```

Configuration-driven provider selection:
```json
{
  "Communications": {
    "Sms": {
      "Provider": "Twilio",
      "Twilio": {
        "AccountSid": "...",
        "AuthToken": "...",
        "FromNumber": "+1..."
      }
    }
  }
}
```

### Cross-Module Integration

Any module sends SMS by publishing `SendSmsRequestedEvent` (in `Shared.Contracts`). Communications handles it — same pattern as `SendEmailRequestedEvent`.

### Retrofit Email Provider Abstraction

Current email is hardcoded to SMTP/MailKit. Refactor to match:

```csharp
public interface IEmailProvider
{
    string ProviderName { get; }
    Task SendAsync(EmailProviderRequest request, CancellationToken ct = default);
}

// Implementations: SmtpEmailProvider, SendGridEmailProvider, etc.
```

---

## 7. User Inbox & Messaging Design

Two separate concerns, both inside Communications:

### 7a. Enhanced System Inbox (extend existing Notification aggregate)

The current `Notification` entity already serves as a system inbox. Add:

```csharp
public string? ActionUrl { get; private set; }      // deep link to relevant resource
public string? SourceModule { get; private set; }    // which module generated it
public DateTime? ExpiresAt { get; private set; }     // auto-cleanup
public bool IsArchived { get; private set; }         // user can archive
```

### 7b. User-to-User Messaging (new sub-domain in Communications)

```
Communications/Domain/Messaging/
    Identity/ConversationId.cs, MessageId.cs
    Entities/
        Conversation.cs     # aggregate root: participants, subject, IsGroup
        Message.cs          # entity: sender, body, sentAt, editedAt
        Participant.cs      # entity: userId, joinedAt, lastReadAt
    Enums/ConversationStatus.cs
    Events/
        MessageSentDomainEvent.cs
        ConversationCreatedDomainEvent.cs
```

**Conversation aggregate:**
- `CreateDirect(tenantId, initiatorId, recipientId)` — 1:1 chat
- `CreateGroup(tenantId, creatorId, subject, memberIds)` — group chat
- `SendMessage(senderId, body)` — raises `MessageSentDomainEvent`
- `MarkReadBy(userId)` — updates participant's `LastReadAt`

**API endpoints:**
```
POST   /api/v1/conversations                # create
GET    /api/v1/conversations                # list (paged, sorted by last activity)
GET    /api/v1/conversations/{id}/messages   # messages (cursor-based pagination)
POST   /api/v1/conversations/{id}/messages   # send message
POST   /api/v1/conversations/{id}/read       # mark read
```

**Integration:** `MessageSentDomainEvent` triggers in-app notification + SignalR push for each participant. Offline users get optional email digest (configurable via channel preferences).

**Generic design:** No product-specific concepts in the domain. Body supports plain text + markdown. Product-specific context (task links, ticket references) would be added by fork modules that publish messages via Wolverine commands.

---

## 8. Unified Channel Abstraction

Introduce a thin channel layer to unify Email, SMS, InApp, and future Push/Webhook:

```csharp
public interface INotificationChannel
{
    ChannelType ChannelType { get; }
    Task<ChannelDeliveryResult> DeliverAsync(
        NotificationDeliveryRequest request,
        CancellationToken ct = default);
}

public enum ChannelType { Email, Sms, InApp, Push, Webhook }
```

**Generalized user preferences** (replacing separate `EmailPreference` / future `SmsPreference`):

```csharp
public sealed class ChannelPreference : AggregateRoot<ChannelPreferenceId>, ITenantScoped
{
    public Guid UserId { get; private set; }
    public ChannelType Channel { get; private set; }
    public string NotificationType { get; private set; }  // string for extensibility
    public bool IsEnabled { get; private set; }
}
```

A `NotificationDispatcher` selects channels based on user preferences, tenant configuration, and event urgency.

---

## 9. Scaffold Module Recommendations

### Delete (redundant with implemented modules)
- `Email` — subsumed by Communications/Channels/Email
- `Notifications` — subsumed by Communications/Channels/InApp
- `Announcements` — subsumed by Communications/Announcements
- `Metering` — absorbed into Billing
- `Scheduler` — Hangfire handles job scheduling

---

## 10. Additional Gaps to Address

| Gap | Impact | Effort |
|-----|--------|--------|
| **Email retry background job** | Failed emails stay failed permanently | Low |
| **Per-tenant email/SMS provider config** | Required for white-label SaaS | High |
| **Event versioning strategy** | Schema changes require coordinated deploys | Medium |
| **Security scanning (Dependabot, CodeQL, Trivy)** | No vulnerability scanning in CI | Low |
| **IdentityDbContext missing TenantSaveChangesInterceptor** | Tenant-scoped Identity entities won't auto-stamp | Low |
| **AllTenants() bypasses ALL query filters** | Will break soft-delete if added later | Low |
| **Elsa auth competing with Keycloak** | Two auth systems to manage | Medium |
| **Feature flag caching** | DB query per flag evaluation | Medium |
| **Duplicate NotificationType enums** | Email and InApp use separate enums with same values | Low |
| **Hardcoded version "1.0.0" on root endpoint** | Should read from assembly | Low |
| **Template engine (basic string replacement)** | Not production-grade for complex emails | Medium |

---

## 11. Recommended Execution Order

### Phase 1: Foundation Fixes (before forking)
1. Tenant context in Wolverine handlers (P0)
2. DbContext base class for tenant filters (P1)
3. Split ITenantContext read/write (P1)
4. Delete redundant scaffolds (P1)
5. Config-driven module toggles (P1)
6. Architecture test auto-discovery (P2)
7. Fix Configuration module consistency (P2)
8. Email retry background job (P2)
9. Fix IdentityDbContext interceptor gap (P2)
10. Add security scanning to CI (P2)

### Phase 2: Communications Expansion
1. Email provider abstraction (retrofit)
2. SMS channel + Twilio provider
3. Enhanced system inbox (Notification aggregate additions)
4. Unified channel abstraction + ChannelPreference
5. Duplicate NotificationType cleanup

### Phase 3: User Messaging
1. Conversation/Message/Participant domain model
2. Application layer (commands, queries, handlers)
3. SignalR real-time message delivery
4. API endpoints
5. Email digest for offline users

### Phase 4: Fork Your First Product
1. Fork Wallow on GitHub
2. Configure upstream remote
3. Disable unneeded modules via appsettings.json
4. Add product-specific modules as plugins
5. Build, refine, then genericize and PR back

### Phase 5: Module Expansion (ongoing)

Additional modules are built in product forks first, then genericized back into Wallow once stable.

---

## 12. Claude Cross-Project Workflow

For your workflow of "build in fork, then genericize back to Wallow":

1. **In the fork project**, build the feature with product-specific needs
2. **When ready to genericize**, ask Claude in the Wallow project:
   > "I built [feature] in my fork at [path]. Create a plan to genericize it for the base platform."
3. Claude reads the fork implementation, identifies product-specific vs generic parts, and creates a plan
4. Implementation happens in Wallow with proper tests
5. Fork then pulls upstream to get the generic version

This works because Claude can read files across projects. Keep both repos cloned locally and reference paths directly.
