# Wallow Platform Improvements — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix critical infrastructure gaps, add SMS/Inbox channels, establish config-driven module toggles, and clean up scaffold confusion — making Wallow fork-ready as a generic SaaS platform base.

**Architecture:** Modular monolith with Clean Architecture, DDD, CQRS via Wolverine, RabbitMQ messaging, EF Core with schema-per-module, multi-tenant via Keycloak JWT + TenantSaveChangesInterceptor.

**Tech Stack:** .NET 10, Wolverine, PostgreSQL, RabbitMQ, EF Core, SignalR + Valkey, Keycloak, Hangfire, FluentValidation, NSubstitute, xUnit

---

## Phase 1: Foundation Fixes

> Fix critical infrastructure issues before any feature work. These are correctness and DX improvements that every fork will benefit from.

---

### Epic 1.1: Wolverine Tenant Context Propagation

**Why:** Background message handlers consuming from RabbitMQ have no ITenantContext. Every multi-tenant event-driven workflow silently fails or operates without tenant isolation. This is a P0 correctness bug.

---

#### Task 1.1.1: Create ITenantContextSetter interface

Split write operations out of ITenantContext so domain/application code only sees the read-only surface.

**Files:**
- Create: `src/Shared/Wallow.Shared.Kernel/MultiTenancy/ITenantContextSetter.cs`
- Modify: `src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantContext.cs`

**Steps:**
1. Create `ITenantContextSetter` with `SetTenant()` and `Clear()` methods
2. Remove `SetTenant()` and `Clear()` from `ITenantContext` interface
3. Make `TenantContext` implement both `ITenantContext` and `ITenantContextSetter`
4. Run `dotnet build` to identify all callers of `SetTenant()`/`Clear()` that need updating

```csharp
// ITenantContextSetter.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public interface ITenantContextSetter
{
    void SetTenant(TenantId tenantId, string tenantName = "", string region = RegionConfiguration.PrimaryRegion);
    void Clear();
}
```

---

#### Task 1.1.2: Register ITenantContextSetter in DI

Update the DI registration so `ITenantContextSetter` resolves to the same scoped `TenantContext` instance.

**Files:**
- Modify: Wherever `ITenantContext` is registered (search for `AddScoped<ITenantContext`)

**Steps:**
1. Find the DI registration for `ITenantContext`
2. Register `TenantContext` as the implementation, then register both interfaces to resolve from the same instance
3. Run `dotnet build` to verify

---

#### Task 1.1.3: Update TenantResolutionMiddleware to use ITenantContextSetter

The HTTP middleware that resolves tenant from JWT should inject `ITenantContextSetter` instead of calling `SetTenant()` on `ITenantContext`.

**Files:**
- Modify: `src/Wallow.Api/Middleware/TenantResolutionMiddleware.cs` (or wherever it lives)

**Steps:**
1. Change constructor injection from `ITenantContext` to `ITenantContextSetter`
2. Update all `SetTenant()` and `Clear()` calls to use the setter
3. Run `dotnet build`

---

#### Task 1.1.4: Update all other SetTenant/Clear callers

Any other code calling `SetTenant()` or `Clear()` needs to inject `ITenantContextSetter`.

**Files:**
- Search for: `\.SetTenant\(` and `\.Clear\(\)` on ITenantContext usages
- Likely: test helpers, design-time contexts, integration test bases

**Steps:**
1. `grep -rn "\.SetTenant\(" src/ tests/` to find all callers
2. Update each caller to inject `ITenantContextSetter`
3. Run `dotnet build` and `dotnet test` to verify

---

#### Task 1.1.5: Create TenantStampingMiddleware for Wolverine outbound messages

Stamps the current tenant ID into outbound Wolverine message headers so consumers can restore it.

**Files:**
- Create: `src/Shared/Wallow.Shared.Kernel/Messaging/TenantStampingMiddleware.cs`

**Steps:**
1. Create Wolverine middleware that reads `ITenantContext.TenantId` and stamps it into `Envelope.Headers["X-Tenant-Id"]`
2. Only stamp if `ITenantContext.IsResolved` is true

```csharp
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Kernel.Messaging;

public sealed class TenantStampingMiddleware
{
    public static void Before(IMessageContext context, ITenantContext tenantContext)
    {
        if (tenantContext.IsResolved && context.Envelope is not null)
        {
            context.Envelope.Headers["X-Tenant-Id"] = tenantContext.TenantId.Value.ToString();
        }
    }
}
```

---

#### Task 1.1.6: Create TenantRestoringMiddleware for Wolverine inbound messages

Restores tenant context from message headers when consuming from RabbitMQ.

**Files:**
- Create: `src/Shared/Wallow.Shared.Kernel/Messaging/TenantRestoringMiddleware.cs`

**Steps:**
1. Create Wolverine middleware that reads `X-Tenant-Id` from `Envelope.Headers`
2. Calls `ITenantContextSetter.SetTenant()` with the parsed TenantId

```csharp
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Kernel.Messaging;

public sealed class TenantRestoringMiddleware
{
    public static void Before(IMessageContext context, ITenantContextSetter tenantSetter)
    {
        if (context.Envelope?.Headers.TryGetValue("X-Tenant-Id", out string? tenantIdStr) == true
            && Guid.TryParse(tenantIdStr, out Guid tenantGuid))
        {
            TenantId tenantId = TenantId.Create(tenantGuid);
            tenantSetter.SetTenant(tenantId);
        }
    }
}
```

---

#### Task 1.1.7: Register Wolverine tenant middlewares in Program.cs

Wire both middlewares into the Wolverine pipeline.

**Files:**
- Modify: `src/Wallow.Api/Program.cs` (Wolverine configuration section)

**Steps:**
1. Find the Wolverine `UseWolverine` or `opts` configuration block
2. Add `opts.Policies.AddMiddleware<TenantStampingMiddleware>()` and `opts.Policies.AddMiddleware<TenantRestoringMiddleware>()`
3. Run `dotnet build`

---

#### Task 1.1.8: Write tests for tenant propagation middlewares

**Files:**
- Create: `tests/Wallow.Architecture.Tests/Messaging/TenantStampingMiddlewareTests.cs`
- Create: `tests/Wallow.Architecture.Tests/Messaging/TenantRestoringMiddlewareTests.cs`

**Steps:**
1. Test TenantStampingMiddleware stamps header when tenant is resolved
2. Test TenantStampingMiddleware does not stamp when tenant is not resolved
3. Test TenantRestoringMiddleware sets tenant from valid header
4. Test TenantRestoringMiddleware does nothing when header is missing
5. Test TenantRestoringMiddleware does nothing when header is not a valid GUID
6. Run `dotnet test`

---

### Epic 1.2: DbContext Tenant Query Filter Base Class

**Why:** Every module DbContext duplicates ~20 lines of identical expression tree code for tenant query filters. This is error-prone and violates DRY.

---

#### Task 1.2.1: Create TenantAwareDbContext base class

**Files:**
- Create: `src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantAwareDbContext.cs`

**Steps:**
1. Create abstract generic base class with the shared tenant filter logic
2. Extract the expression tree code from `CommunicationsDbContext.OnModelCreating`

```csharp
using System.Linq.Expressions;
using System.Reflection;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wallow.Shared.Kernel.MultiTenancy;

public abstract class TenantAwareDbContext<TContext> : DbContext where TContext : DbContext
{
#pragma warning disable IDE0052
    private readonly TenantId _tenantId;
#pragma warning restore IDE0052

    protected TenantAwareDbContext(
        DbContextOptions<TContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    protected void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                ParameterExpression parameter = Expression.Parameter(entityType.ClrType, "e");
                MemberExpression property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                ConstantExpression contextExpression = Expression.Constant(this);
                MemberExpression tenantIdField = Expression.Field(
                    contextExpression,
                    typeof(TContext).GetField("_tenantId",
                        BindingFlags.NonPublic | BindingFlags.Instance)!);
                BinaryExpression equals = Expression.Equal(property, tenantIdField);
                LambdaExpression lambda = Expression.Lambda(equals, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
```

---

#### Task 1.2.2: Migrate CommunicationsDbContext to TenantAwareDbContext

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/CommunicationsDbContext.cs`

**Steps:**
1. Change base class from `DbContext` to `TenantAwareDbContext<CommunicationsDbContext>`
2. Remove the `_tenantId` field and the expression tree code from `OnModelCreating`
3. Call `ApplyTenantQueryFilters(modelBuilder)` in `OnModelCreating`
4. Run `dotnet build` then `dotnet test --filter "Communications"`

---

#### Task 1.2.3: Migrate BillingDbContext to TenantAwareDbContext

Same pattern as Task 1.2.2 but for `BillingDbContext`.

**Files:**
- Modify: `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContext.cs`

**Steps:**
1. Change base class, remove boilerplate, call `ApplyTenantQueryFilters`
2. Run `dotnet test --filter "Billing"`

---

#### Task 1.2.4: Migrate IdentityDbContext to TenantAwareDbContext

**Files:**
- Modify: `src/Modules/Identity/Wallow.Identity.Infrastructure/Persistence/IdentityDbContext.cs`

**Steps:**
1. Change base class, remove boilerplate, call `ApplyTenantQueryFilters`
2. Verify that IdentityDbContext now gets the TenantSaveChangesInterceptor (it was missing before)
3. Run `dotnet test --filter "Identity"`

---

#### Task 1.2.5: Migrate ConfigurationDbContext to TenantAwareDbContext

**Files:**
- Modify: `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Persistence/ConfigurationDbContext.cs`

**Steps:**
1. Change base class, remove boilerplate, call `ApplyTenantQueryFilters`
2. Run `dotnet test --filter "Configuration"`

---

#### Task 1.2.6: Migrate StorageDbContext to TenantAwareDbContext

**Files:**
- Modify: `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/StorageDbContext.cs`

**Steps:**
1. Change base class, remove boilerplate, call `ApplyTenantQueryFilters`
2. Run `dotnet test --filter "Storage"`

---

#### Task 1.2.7: Write test verifying TenantAwareDbContext applies filters

**Files:**
- Create: `tests/Wallow.Architecture.Tests/MultiTenancy/TenantAwareDbContextTests.cs`

**Steps:**
1. Create a test DbContext inheriting TenantAwareDbContext with a test entity
2. Verify query filters are applied to ITenantScoped entities
3. Run `dotnet test --filter "TenantAwareDbContext"`

---

### Epic 1.3: Config-Driven Module Toggles

**Why:** Forks must edit source code to disable modules. A config-driven toggle in appsettings.json eliminates this.

---

#### Task 1.3.1: Add module toggle configuration to appsettings.json

**Files:**
- Modify: `src/Wallow.Api/appsettings.json`

**Steps:**
1. Add `Wallow:Modules` section with all 5 current modules defaulting to `true`

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

---

#### Task 1.3.2: Update WallowModules.cs to read configuration

**Files:**
- Modify: `src/Wallow.Api/WallowModules.cs`

**Steps:**
1. Read `Wallow:Modules` section
2. Conditionally register each module based on config value (default: true)
3. Same for `InitializeWallowModulesAsync`

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
    if (modules.GetValue("Communications", defaultValue: true))
        services.AddCommunicationsModule(configuration);
    if (modules.GetValue("Storage", defaultValue: true))
        services.AddStorageModule(configuration);
    if (modules.GetValue("Configuration", defaultValue: true))
        services.AddConfigurationModule(configuration);

    services.AddWallowPlugins(configuration);
    return services;
}
```

---

#### Task 1.3.3: Write test for module toggle behavior

**Files:**
- Create: `tests/Wallow.Architecture.Tests/Modules/ModuleToggleTests.cs`

**Steps:**
1. Test that setting a module to `false` in configuration skips its registration
2. Test that all modules default to `true` when config section is missing
3. Run `dotnet test`

---

### Epic 1.4: Delete Redundant Scaffolds

**Why:** Empty scaffold modules that overlap with implemented modules cause confusion for fork developers.

---

#### Task 1.4.1: Delete redundant scaffold modules

**Files:**
- Delete: `src/Modules/Email/` (entire directory — subsumed by Communications/Channels/Email)
- Delete: `src/Modules/Notifications/` (entire directory — subsumed by Communications/Channels/InApp)
- Delete: `src/Modules/Announcements/` (entire directory — subsumed by Communications/Announcements)
- Delete: `src/Modules/Metering/` (entire directory — absorbed into Billing)
- Delete: `src/Modules/Scheduler/` (entire directory — Hangfire handles job scheduling)
- Delete: corresponding `tests/Modules/` directories for each

**Steps:**
1. Remove project references from `Wallow.sln`
2. Remove project references from `Wallow.Api.csproj` if any
3. Delete the directories
4. Run `dotnet build` to verify no broken references
5. Run `dotnet test`

---

#### Task 1.4.2: Move product-specific scaffolds out of base

Remove scaffolds that are too domain-specific for a generic base. These should be implemented in product forks.

**Files:**
- Delete: `src/Modules/Catalog/` and `tests/Modules/Catalog/`
- Delete: `src/Modules/Inventory/` and `tests/Modules/Inventory/`
- Delete: `src/Modules/Sales/` and `tests/Modules/Sales/`

**Steps:**
1. Remove from solution
2. Delete directories
3. Run `dotnet build` and `dotnet test`

---

### Epic 1.5: Architecture Test Improvements

---

#### Task 1.5.1: Add auto-discovery to TestConstants.AllModules

**Files:**
- Modify: `tests/Wallow.Architecture.Tests/TestConstants.cs`

**Steps:**
1. Replace hardcoded array with reflection-based discovery scanning for `Wallow.*.Domain` assemblies
2. Run `dotnet test --filter "Architecture"` to verify all existing tests still pass

---

#### Task 1.5.2: Add security scanning workflow to CI

**Files:**
- Create: `.github/workflows/security.yml`

**Steps:**
1. Add CodeQL analysis for C#
2. Add Dependabot configuration (`.github/dependabot.yml`)
3. Add Trivy container scan in publish workflow

---

### Epic 1.6: Email Retry Background Job

**Why:** Failed emails stay in Failed status permanently. No retry mechanism exists.

---

#### Task 1.6.1: Create RetryFailedEmailsJob

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/BackgroundJobs/RetryFailedEmailsJob.cs`

**Steps:**
1. Create Hangfire job that queries for failed, retryable EmailMessage entities
2. Calls `ResetForRetry()` then reprocesses via `IEmailService`
3. Respects `CanRetry(maxRetries: 3)` limit

---

#### Task 1.6.2: Register RetryFailedEmailsJob as recurring

**Files:**
- Create or Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/BackgroundJobs/CommunicationsRecurringJobs.cs`

**Steps:**
1. Implement `IRecurringJobRegistration`
2. Register the retry job to run every 5 minutes
3. Wire into Communications module DI registration

---

#### Task 1.6.3: Write tests for RetryFailedEmailsJob

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/RetryFailedEmailsJobTests.cs`

**Steps:**
1. Test that failed retryable emails are picked up
2. Test that emails exceeding max retries are skipped
3. Test that successful retries mark as Sent
4. Run `dotnet test`

---

## Phase 2: Communications Expansion — SMS & Channel Unification

> Add SMS as a first-class channel and unify the notification delivery model.

---

### Epic 2.1: Email Provider Abstraction (Retrofit)

**Why:** Current email is hardcoded to SMTP/MailKit. No way to plug in SendGrid, SES, etc. without modifying the module.

---

#### Task 2.1.1: Create IEmailProvider interface

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Email/Interfaces/IEmailProvider.cs`

```csharp
namespace Wallow.Communications.Application.Channels.Email.Interfaces;

public interface IEmailProvider
{
    string ProviderName { get; }
    Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken ct = default);
}

public sealed record EmailDeliveryRequest(
    string To,
    string? From,
    string Subject,
    string Body,
    byte[]? Attachment = null,
    string? AttachmentName = null,
    string? AttachmentContentType = null);

public sealed record EmailDeliveryResult(bool Success, string? ErrorMessage = null);
```

---

#### Task 2.1.2: Refactor SmtpEmailService to implement IEmailProvider

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/SmtpEmailService.cs`
- Rename to: `SmtpEmailProvider.cs`

**Steps:**
1. Rename class to `SmtpEmailProvider`
2. Implement `IEmailProvider` instead of `IEmailService`
3. Move the MailKit SMTP logic into the new method signatures
4. Run `dotnet build`

---

#### Task 2.1.3: Create EmailProviderAdapter implementing IEmailService

Bridge the existing `IEmailService` (from Shared.Contracts) to the new `IEmailProvider` so existing consumers don't break.

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/EmailProviderAdapter.cs`

**Steps:**
1. Implement `IEmailService` by delegating to `IEmailProvider`
2. Register as `IEmailService` in DI
3. Register `SmtpEmailProvider` as `IEmailProvider` in DI

---

#### Task 2.1.4: Add email provider configuration

**Files:**
- Modify: `src/Wallow.Api/appsettings.json`

**Steps:**
1. Add `Communications:Email:Provider` setting (default: "Smtp")
2. Update DI registration to select provider based on config

---

#### Task 2.1.5: Write tests for email provider abstraction

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/SmtpEmailProviderTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/EmailProviderAdapterTests.cs`

---

### Epic 2.2: SMS Channel

**Why:** No SMS channel exists. Needed for OTP, alerts, transactional messaging.

---

#### Task 2.2.1: Create SMS domain model — PhoneNumber value object

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/ValueObjects/PhoneNumber.cs`

**Steps:**
1. Create E.164 validated value object (starts with +, 7-15 digits)
2. Normalize to E.164 format

---

#### Task 2.2.2: Create SMS domain model — SmsMessageId and SmsStatus

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Identity/SmsMessageId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Enums/SmsStatus.cs`

---

#### Task 2.2.3: Create SMS domain model — SmsMessage aggregate

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Entities/SmsMessage.cs`

**Steps:**
1. Mirror `EmailMessage` pattern: Create, MarkAsSent, MarkAsFailed, ResetForRetry, CanRetry
2. Properties: To (PhoneNumber), Body, Status, SentAt, FailureReason, RetryCount
3. Domain events: SmsSentDomainEvent, SmsFailedDomainEvent

---

#### Task 2.2.4: Create SMS domain events

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Events/SmsSentDomainEvent.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Events/SmsFailedDomainEvent.cs`

---

#### Task 2.2.5: Create SMS preference entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Identity/SmsPreferenceId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Sms/Entities/SmsPreference.cs`

Mirror `EmailPreference` pattern.

---

#### Task 2.2.6: Write SMS domain tests

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Sms/PhoneNumberTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Sms/SmsMessageTests.cs`

**Steps:**
1. Test PhoneNumber validation (valid E.164, invalid formats, normalization)
2. Test SmsMessage lifecycle (Create → MarkAsSent, Create → MarkAsFailed → ResetForRetry)
3. Test domain events are raised correctly
4. Run `dotnet test`

---

#### Task 2.2.7: Create ISmsProvider interface

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Interfaces/ISmsProvider.cs`

```csharp
public interface ISmsProvider
{
    string ProviderName { get; }
    Task<SmsDeliveryResult> SendAsync(string to, string body, CancellationToken ct = default);
}

public sealed record SmsDeliveryResult(bool Success, string? MessageSid = null, string? ErrorMessage = null);
```

---

#### Task 2.2.8: Create ISmsMessageRepository interface

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Interfaces/ISmsMessageRepository.cs`

---

#### Task 2.2.9: Create SendSmsCommand and handler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsValidator.cs`

---

#### Task 2.2.10: Create SendSmsRequestedEvent integration event

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Communications/Sms/Events/SendSmsRequestedEvent.cs`

```csharp
namespace Wallow.Shared.Contracts.Communications.Sms.Events;

public sealed record SendSmsRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string To { get; init; }
    public required string Body { get; init; }
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
}
```

---

#### Task 2.2.11: Create SendSmsRequestedEventHandler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/EventHandlers/SendSmsRequestedEventHandler.cs`

Mirror `SendEmailRequestedEventHandler` pattern — any module publishes the event, Communications handles it.

---

#### Task 2.2.12: Create NullSmsProvider (dev/test)

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/NullSmsProvider.cs`

Logs SMS to console/telemetry but doesn't actually send. Default provider for development.

---

#### Task 2.2.13: Create TwilioSmsProvider

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/TwilioSmsProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/TwilioSettings.cs`

**Steps:**
1. Implement ISmsProvider using Twilio REST API (HttpClient, no SDK dependency)
2. Config: AccountSid, AuthToken, FromNumber from appsettings

---

#### Task 2.2.14: Create SmsMessage EF Core configuration and repository

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/SmsMessageConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/SmsPreferenceConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/SmsMessageRepository.cs`

---

#### Task 2.2.15: Update CommunicationsDbContext for SMS entities

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/CommunicationsDbContext.cs`

**Steps:**
1. Add `DbSet<SmsMessage>` and `DbSet<SmsPreference>`
2. Create EF migration: `dotnet ef migrations add AddSmsChannel --project src/Modules/Communications/Wallow.Communications.Infrastructure --startup-project src/Wallow.Api --context CommunicationsDbContext`

---

#### Task 2.2.16: Register SMS services in Communications module DI

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Extensions/InfrastructureExtensions.cs`

**Steps:**
1. Register `ISmsProvider` (NullSmsProvider by default, TwilioSmsProvider via config)
2. Register `ISmsMessageRepository`
3. Add SMS config section to appsettings

---

#### Task 2.2.17: Register SMS messaging routes in Program.cs

**Files:**
- Modify: `src/Wallow.Api/Program.cs`

**Steps:**
1. Add `opts.PublishMessage<SmsSentEvent>()` routing
2. Add `SendSmsRequestedEvent` to the communications inbox queue binding

---

#### Task 2.2.18: Write SMS application layer tests

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Sms/SendSmsHandlerTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Sms/SendSmsValidatorTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Sms/SendSmsRequestedEventHandlerTests.cs`

---

### Epic 2.3: Unified Channel Preference Model

**Why:** Email and InApp have separate NotificationType enums with overlapping values. Adding SMS would create a third. Unify into one extensible model.

---

#### Task 2.3.1: Create unified NotificationType as string constants

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/NotificationTypes.cs`

**Steps:**
1. Create a static class with string constants replacing the separate enums
2. This allows forks to add custom types without modifying the enum

```csharp
namespace Wallow.Communications.Domain;

public static class NotificationTypes
{
    public const string TaskAssigned = "task_assigned";
    public const string TaskCompleted = "task_completed";
    public const string TaskComment = "task_comment";
    public const string BillingInvoice = "billing_invoice";
    public const string SystemNotification = "system_notification";
    public const string SystemAlert = "system_alert";
    public const string Mention = "mention";
    public const string Announcement = "announcement";
}
```

---

#### Task 2.3.2: Create ChannelPreference aggregate

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Preferences/Identity/ChannelPreferenceId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Preferences/Enums/ChannelType.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Preferences/Entities/ChannelPreference.cs`

```csharp
public enum ChannelType { Email, Sms, InApp, Push, Webhook }
```

---

#### Task 2.3.3: Migrate existing EmailPreference data to ChannelPreference

**Files:**
- Create EF migration that copies EmailPreference rows into ChannelPreference with ChannelType.Email

**Steps:**
1. Add ChannelPreference to CommunicationsDbContext
2. Create migration
3. Write SQL data migration in the migration Up() method

---

#### Task 2.3.4: Update preference commands/queries to use ChannelPreference

**Files:**
- Modify: Commands and queries in `Channels/Email/` that reference EmailPreference
- Create: New generic preference commands in `Preferences/` namespace

---

#### Task 2.3.5: Write tests for ChannelPreference

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Preferences/ChannelPreferenceTests.cs`

---

## Phase 3: User Inbox & Messaging

> Add user-to-user messaging as a new sub-domain within Communications.

---

### Epic 3.1: Enhanced System Inbox

**Why:** Current Notification entity lacks ActionUrl, SourceModule, expiry, and archive — basic inbox features.

---

#### Task 3.1.1: Add properties to Notification entity

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Domain/Channels/InApp/Entities/Notification.cs`

**Steps:**
1. Add `ActionUrl` (string?), `SourceModule` (string?), `ExpiresAt` (DateTime?), `IsArchived` (bool)
2. Add `Archive()` method
3. Add `IsExpired` computed property
4. Update `Create()` factory to accept new optional parameters

---

#### Task 3.1.2: Update Notification EF configuration

**Files:**
- Modify: The Notification EF configuration file to map new columns
- Create migration

---

#### Task 3.1.3: Add ArchiveNotification command

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationHandler.cs`

---

#### Task 3.1.4: Update GetUserNotificationsQuery to filter expired/archived

**Files:**
- Modify: The existing GetUserNotifications query to exclude expired and archived by default

---

#### Task 3.1.5: Write tests for enhanced Notification

**Files:**
- Modify/Create: Notification domain tests and ArchiveNotification handler tests

---

### Epic 3.2: User-to-User Messaging Domain

---

#### Task 3.2.1: Create Messaging identity types

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Identity/ConversationId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Identity/MessageId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Identity/ParticipantId.cs`

---

#### Task 3.2.2: Create ConversationStatus and MessageStatus enums

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Enums/ConversationStatus.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Enums/MessageStatus.cs`

---

#### Task 3.2.3: Create Participant entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Entities/Participant.cs`

```csharp
public sealed class Participant : Entity<ParticipantId>
{
    public ConversationId ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LastReadAt { get; private set; }
    public bool IsActive { get; private set; }
    // Factory, MarkRead, Leave methods
}
```

---

#### Task 3.2.4: Create Message entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Entities/Message.cs`

---

#### Task 3.2.5: Create Conversation aggregate root

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Entities/Conversation.cs`

**Steps:**
1. Aggregate root with Participants and Messages collections
2. `CreateDirect(tenantId, initiatorId, recipientId)` factory
3. `CreateGroup(tenantId, creatorId, subject, memberIds)` factory
4. `SendMessage(senderId, body)` — raises `MessageSentDomainEvent`
5. `AddParticipant(userId)` — raises `ParticipantAddedDomainEvent`
6. `MarkReadBy(userId)` — updates participant's LastReadAt
7. `Archive()` — sets status to Archived

---

#### Task 3.2.6: Create Messaging domain events

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Events/MessageSentDomainEvent.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Events/ConversationCreatedDomainEvent.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Messaging/Events/ParticipantAddedDomainEvent.cs`

---

#### Task 3.2.7: Write comprehensive Messaging domain tests

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Messaging/ConversationTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Messaging/MessageTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Messaging/ParticipantTests.cs`

**Tests:**
1. CreateDirect — 2 participants, no subject, not group
2. CreateGroup — N participants, has subject, is group
3. SendMessage — only by active participants, raises event
4. MarkReadBy — updates LastReadAt
5. AddParticipant — only to group conversations
6. Archive — status changes, no new messages allowed

---

### Epic 3.3: User-to-User Messaging Application Layer

---

#### Task 3.3.1: Create IConversationRepository interface

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Interfaces/IConversationRepository.cs`

---

#### Task 3.3.2: Create CreateConversation command and handler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationValidator.cs`

---

#### Task 3.3.3: Create SendMessage command and handler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/SendMessage/SendMessageCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/SendMessage/SendMessageHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/SendMessage/SendMessageValidator.cs`

---

#### Task 3.3.4: Create MarkConversationRead command and handler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/MarkConversationRead/MarkConversationReadCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Commands/MarkConversationRead/MarkConversationReadHandler.cs`

---

#### Task 3.3.5: Create GetConversations query

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetConversations/GetConversationsQuery.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetConversations/GetConversationsHandler.cs`

Paged query, sorted by last activity, includes last message preview and unread count.

---

#### Task 3.3.6: Create GetMessages query

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetMessages/GetMessagesQuery.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetMessages/GetMessagesHandler.cs`

Cursor-based pagination for performance.

---

#### Task 3.3.7: Create GetUnreadConversationCount query

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetUnreadConversationCount/GetUnreadConversationCountQuery.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/Queries/GetUnreadConversationCount/GetUnreadConversationCountHandler.cs`

---

#### Task 3.3.8: Create MessageSent event handler (notifications + SignalR)

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/EventHandlers/MessageSentEventHandler.cs`

**Steps:**
1. On MessageSentDomainEvent, create in-app Notification for each participant (except sender)
2. Push SignalR event via IRealtimeDispatcher

---

#### Task 3.3.9: Create Messaging DTOs

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/DTOs/ConversationDto.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/DTOs/MessageDto.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Messaging/DTOs/ParticipantDto.cs`

---

#### Task 3.3.10: Write Messaging application layer tests

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Messaging/CreateConversationHandlerTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Messaging/SendMessageHandlerTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Messaging/MarkConversationReadHandlerTests.cs`
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Messaging/MessageSentEventHandlerTests.cs`

---

### Epic 3.4: User-to-User Messaging Infrastructure & API

---

#### Task 3.4.1: Create EF Core configurations for Messaging entities

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/MessageConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs`

---

#### Task 3.4.2: Add Messaging DbSets and create migration

**Files:**
- Modify: `CommunicationsDbContext.cs` — add `DbSet<Conversation>`, `DbSet<Message>`, `DbSet<Participant>`
- Create migration: `AddMessaging`

---

#### Task 3.4.3: Create ConversationRepository

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/ConversationRepository.cs`

---

#### Task 3.4.4: Create ConversationsController

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Controllers/ConversationsController.cs`

**Endpoints:**
- `POST /api/v1/conversations` — create
- `GET /api/v1/conversations` — list (paged)
- `GET /api/v1/conversations/{id}/messages` — get messages
- `POST /api/v1/conversations/{id}/messages` — send message
- `POST /api/v1/conversations/{id}/read` — mark read
- `GET /api/v1/conversations/unread-count` — unread count

---

#### Task 3.4.5: Create API request/response contracts

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Messaging/CreateConversationRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Messaging/SendMessageRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Messaging/ConversationResponse.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Messaging/MessageResponse.cs`

---

#### Task 3.4.6: Register Messaging services in DI

**Files:**
- Modify: Communications infrastructure extensions

---

#### Task 3.4.7: Create Messaging integration events in Shared.Contracts

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Communications/Messaging/Events/MessageSentEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Communications/Messaging/Events/ConversationCreatedEvent.cs`

---

#### Task 3.4.8: Write Messaging integration tests

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Integration/Messaging/ConversationIntegrationTests.cs`

**Tests:**
1. Create conversation → 201
2. Send message → 201
3. Get messages → returns sent message
4. Mark read → updates LastReadAt
5. Get conversations → sorted by last activity

---

## Phase 4: Quality & Documentation

---

### Epic 4.1: Test Coverage Improvements

---

#### Task 4.1.1: Add InApp notification domain tests for new properties

**Files:**
- Modify: Existing notification tests to cover ActionUrl, SourceModule, ExpiresAt, Archive

---

#### Task 4.1.2: Add SMS integration test

**Files:**
- Create: `tests/Modules/Communications/Wallow.Communications.Tests/Integration/Sms/SmsIntegrationTests.cs`

---

#### Task 4.1.3: Add builders to Tests.Common

**Files:**
- Create: `tests/Wallow.Tests.Common/Builders/ConversationBuilder.cs`
- Create: `tests/Wallow.Tests.Common/Builders/SmsMessageBuilder.cs`
- Create: `tests/Wallow.Tests.Common/Builders/NotificationBuilder.cs`

---

### Epic 4.2: Documentation

---

#### Task 4.2.1: Update module-creation guide for SMS/Messaging patterns

**Files:**
- Modify: `docs/claude/module-creation.md`

---

#### Task 4.2.2: Document fork workflow

**Files:**
- Create: `docs/FORK_GUIDE.md`

Document: how to fork, configure modules, add product-specific plugins, sync upstream, contribute back.

---

#### Task 4.2.3: Document channel architecture

**Files:**
- Create: `docs/claude/communications-channels.md`

Document: channel model, how to add new channels, provider abstraction, preference model.

---

## Summary: Task Count by Phase

| Phase | Epics | Tasks |
|-------|-------|-------|
| Phase 1: Foundation Fixes | 6 | 20 |
| Phase 2: SMS & Channels | 3 | 18 |
| Phase 3: User Messaging | 4 | 20 |
| Phase 4: Quality & Docs | 2 | 6 |
| **Total** | **15** | **64** |

## Dependency Graph

```
Phase 1 (Foundation) ─┬─► Phase 2 (SMS/Channels)
                      │
                      └─► Phase 3 (Messaging) ─► Phase 4 (Docs)
```

Phase 2 and Phase 3 can run in parallel after Phase 1 completes.
Within each phase, epics are sequential but tasks within an epic can often be parallelized (domain tasks in parallel, then infra tasks, then tests).
