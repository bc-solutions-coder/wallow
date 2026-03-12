# Communications Module Split v2 — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the Communications module into three independent modules — Notifications, Messaging, and Announcements — using a pure reactive notification pattern where Notifications subscribes to domain events and owns all delivery logic.

**Architecture:** Each new module follows the standard 4-project Clean Architecture pattern (Domain → Application → Infrastructure → Api) with its own DbContext, database schema, feature flag, and test project. The Notifications module is a reactive delivery engine that subscribes to domain events from other modules. No `Send*RequestedEvent` commands exist — modules publish facts, Notifications reacts. Cross-module communication uses integration events via Shared.Contracts.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL (schema-per-module), Wolverine (CQRS + messaging), FluentValidation, xUnit, NSubstitute, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-12-communications-module-split-design-v2.md`

---

## Chunk 1: Shared Contracts Reorganization

Reorganize the shared contracts: delete command-as-event types, move remaining events to new namespaces, add new confirmation events, enrich existing events with data needed by reactive handlers.

### Task 1.1: Create Notifications namespace and move email confirmation event

**Files:**
- Create: `src/Shared/Foundry.Shared.Contracts/Notifications/Events/EmailSentEvent.cs`
- Create: `src/Shared/Foundry.Shared.Contracts/Notifications/Events/SmsSentEvent.cs`
- Create: `src/Shared/Foundry.Shared.Contracts/Notifications/Events/PushSentEvent.cs`
- Create: `src/Shared/Foundry.Shared.Contracts/Notifications/Events/NotificationCreatedEvent.cs`

**Note:** Old Communications files are deleted later in Task 1.4, not here.

- [ ] **Step 1:** Create `Notifications/Events/EmailSentEvent.cs` — copy from Communications, change namespace:

```csharp
namespace Foundry.Shared.Contracts.Notifications.Events;

public sealed record EmailSentEvent : IntegrationEvent
{
    public required Guid EmailId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ToAddress { get; init; }
    public required string Subject { get; init; }
    public required string TemplateName { get; init; }
    public required DateTime SentAt { get; init; }
}
```

- [ ] **Step 2:** Create new `Notifications/Events/SmsSentEvent.cs`:

```csharp
namespace Foundry.Shared.Contracts.Notifications.Events;

public sealed record SmsSentEvent : IntegrationEvent
{
    public required Guid SmsId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ToNumber { get; init; }
    public required DateTime SentAt { get; init; }
}
```

- [ ] **Step 3:** Create new `Notifications/Events/PushSentEvent.cs`:

```csharp
namespace Foundry.Shared.Contracts.Notifications.Events;

public sealed record PushSentEvent : IntegrationEvent
{
    public required Guid PushMessageId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid RecipientId { get; init; }
    public required DateTime SentAt { get; init; }
}
```

- [ ] **Step 4:** Create `Notifications/Events/NotificationCreatedEvent.cs` — copy from Communications:

```csharp
namespace Foundry.Shared.Contracts.Notifications.Events;

public sealed record NotificationCreatedEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public required DateTime CreatedAt { get; init; }
}
```

- [ ] **Step 5:** Build contracts project to verify:

Run: `dotnet build src/Shared/Foundry.Shared.Contracts/Foundry.Shared.Contracts.csproj`
Expected: Build succeeds

- [ ] **Step 6:** Commit

```bash
git add src/Shared/Foundry.Shared.Contracts/Notifications/
git commit -m "refactor(contracts): create Notifications namespace with delivery confirmation events"
```

### Task 1.2: Move Messaging and Announcements contracts

**Files:**
- Create: `src/Shared/Foundry.Shared.Contracts/Messaging/Events/ConversationCreatedIntegrationEvent.cs`
- Create: `src/Shared/Foundry.Shared.Contracts/Messaging/Events/MessageSentIntegrationEvent.cs`
- Create: `src/Shared/Foundry.Shared.Contracts/Announcements/Events/AnnouncementPublishedEvent.cs`

- [ ] **Step 1:** Create `Messaging/Events/ConversationCreatedIntegrationEvent.cs` — copy from Communications, change namespace to `Foundry.Shared.Contracts.Messaging.Events`:

```csharp
namespace Foundry.Shared.Contracts.Messaging.Events;

public sealed record ConversationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required IReadOnlyList<Guid> ParticipantIds { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required Guid TenantId { get; init; }
}
```

- [ ] **Step 2:** Create `Messaging/Events/MessageSentIntegrationEvent.cs` — copy from Communications, change namespace, **add `ParticipantIds`**:

```csharp
namespace Foundry.Shared.Contracts.Messaging.Events;

public sealed record MessageSentIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required Guid MessageId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset SentAt { get; init; }
    public required Guid TenantId { get; init; }
    public required IReadOnlyList<Guid> ParticipantIds { get; init; }
}
```

- [ ] **Step 3:** Create `Announcements/Events/AnnouncementPublishedEvent.cs` — copy from Communications, change namespace, **add `TargetUserIds`**:

```csharp
namespace Foundry.Shared.Contracts.Announcements.Events;

public sealed record AnnouncementPublishedEvent : IntegrationEvent
{
    public required Guid AnnouncementId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string Type { get; init; }
    public required string Target { get; init; }
    public required string? TargetValue { get; init; }
    public required bool IsPinned { get; init; }
    public required IReadOnlyList<Guid> TargetUserIds { get; init; }
}
```

- [ ] **Step 4:** Build contracts project:

Run: `dotnet build src/Shared/Foundry.Shared.Contracts/Foundry.Shared.Contracts.csproj`
Expected: Build succeeds

- [ ] **Step 5:** Commit

```bash
git add src/Shared/Foundry.Shared.Contracts/Messaging/ src/Shared/Foundry.Shared.Contracts/Announcements/
git commit -m "refactor(contracts): move Messaging and Announcements contracts to own namespaces"
```

### Task 1.3: Enrich InquirySubmittedEvent with AdminEmail

**Files:**
- Modify: `src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquirySubmittedEvent.cs`

- [ ] **Step 1:** Read `InquirySubmittedEvent` and add `AdminEmail` property:

Add: `public string? AdminEmail { get; init; }`

This allows the Inquiries module to provide the admin email address when publishing the event, so the Notifications module doesn't need to read Inquiries configuration.

- [ ] **Step 2:** Find where `InquirySubmittedEvent` is published in the Inquiries module (likely in a domain event handler that promotes the domain event to an integration event). Update it to populate `AdminEmail` from `IConfiguration["Inquiries:AdminEmail"]` before publishing. If the event is published from a command handler, update that instead. Search: `grep -r "InquirySubmittedEvent" src/Modules/Inquiries/ --include="*.cs"` to locate the exact file.

- [ ] **Step 3:** Build:

Run: `dotnet build src/Shared/Foundry.Shared.Contracts/Foundry.Shared.Contracts.csproj`
Expected: Build succeeds

- [ ] **Step 4:** Commit

```bash
git add -A
git commit -m "refactor(contracts): add AdminEmail to InquirySubmittedEvent"
```

### Task 1.4: Delete old Communications contracts and update references

**Files:**
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Email/Events/SendEmailRequestedEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Email/Events/EmailSentEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Email/IEmailService.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Sms/Events/SendSmsRequestedEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Push/Events/SendPushRequestedEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Notifications/Events/NotificationCreatedEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Messaging/Events/ConversationCreatedIntegrationEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Messaging/Events/MessageSentIntegrationEvent.cs`
- Delete: `src/Shared/Foundry.Shared.Contracts/Communications/Announcements/Events/AnnouncementPublishedEvent.cs`

- [ ] **Step 1:** Delete the entire `Communications/` directory under Shared.Contracts:

```bash
rm -rf src/Shared/Foundry.Shared.Contracts/Communications/
```

- [ ] **Step 2:** Find all `using Foundry.Shared.Contracts.Communications` references across the codebase and update to the new namespaces:
- `Foundry.Shared.Contracts.Communications.Email.Events` → `Foundry.Shared.Contracts.Notifications.Events`
- `Foundry.Shared.Contracts.Communications.Email` → delete (IEmailService moves to Notifications internal)
- `Foundry.Shared.Contracts.Communications.Notifications.Events` → `Foundry.Shared.Contracts.Notifications.Events`
- `Foundry.Shared.Contracts.Communications.Messaging.Events` → `Foundry.Shared.Contracts.Messaging.Events`
- `Foundry.Shared.Contracts.Communications.Announcements.Events` → `Foundry.Shared.Contracts.Announcements.Events`
- `Foundry.Shared.Contracts.Communications.Sms.Events` → delete (SendSmsRequestedEvent eliminated)
- `Foundry.Shared.Contracts.Communications.Push.Events` → delete (SendPushRequestedEvent eliminated)

- [ ] **Step 3:** The Communications module will have broken references to deleted types. This is expected — the entire Communications module is deleted in Task 8.5. To keep the solution building during the transition, temporarily exclude the Communications projects from the solution build by adding `<ExcludeFromBuild>true</ExcludeFromBuild>` to each Communications .csproj's PropertyGroup, or simply remove them from the solution now:

```bash
dotnet sln Foundry.sln remove \
    src/Modules/Communications/Foundry.Communications.Domain/Foundry.Communications.Domain.csproj \
    src/Modules/Communications/Foundry.Communications.Application/Foundry.Communications.Application.csproj \
    src/Modules/Communications/Foundry.Communications.Infrastructure/Foundry.Communications.Infrastructure.csproj \
    src/Modules/Communications/Foundry.Communications.Api/Foundry.Communications.Api.csproj \
    tests/Modules/Communications/Foundry.Communications.Tests/Foundry.Communications.Tests.csproj
```

Also update `src/Foundry.Api/Foundry.Api.csproj` to remove the Communications project references now (they will be replaced in Task 8.1). And remove the Communications `using` and registration from `FoundryModules.cs` now (they will be replaced in Task 8.2). This ensures the solution builds throughout the transition.

- [ ] **Step 4:** Build contracts project (should succeed since it's self-contained):

Run: `dotnet build src/Shared/Foundry.Shared.Contracts/Foundry.Shared.Contracts.csproj`
Expected: Build succeeds

- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "refactor(contracts): delete Communications namespace, update all references"
```

**Note:** The full solution will NOT build at this point because Communications module handlers still reference deleted types. This is intentional — those handlers are rewritten in Phases 3-6.

---

## Chunk 2: Module Project Scaffolding

Create the 12 new source projects (4 per module) and 3 test projects. Wire them into the solution. No business code yet — just .csproj files, empty DbContexts, and ModuleExtensions stubs.

### Task 2.1: Create Notifications module projects

**Files to create:**
- `src/Modules/Notifications/Foundry.Notifications.Domain/Foundry.Notifications.Domain.csproj`
- `src/Modules/Notifications/Foundry.Notifications.Application/Foundry.Notifications.Application.csproj`
- `src/Modules/Notifications/Foundry.Notifications.Infrastructure/Foundry.Notifications.Infrastructure.csproj`
- `src/Modules/Notifications/Foundry.Notifications.Api/Foundry.Notifications.Api.csproj`
- `tests/Modules/Notifications/Foundry.Notifications.Tests/Foundry.Notifications.Tests.csproj`

- [ ] **Step 1:** Create Domain .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Notifications.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Kernel\Foundry.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2:** Create Application .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Notifications.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Foundry.Notifications.Domain\Foundry.Notifications.Domain.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Kernel\Foundry.Shared.Kernel.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Contracts\Foundry.Shared.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="JetBrains.Annotations" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3:** Create Infrastructure .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Notifications.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Foundry.Notifications.Domain\Foundry.Notifications.Domain.csproj" />
    <ProjectReference Include="..\Foundry.Notifications.Application\Foundry.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Contracts\Foundry.Shared.Contracts.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Infrastructure\Foundry.Shared.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.Resilience" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4:** Create Api .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Notifications.Api</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Foundry.Notifications.Application\Foundry.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Api\Foundry.Shared.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5:** Create Tests .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Notifications.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Bogus" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="WireMock.Net" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Foundry.Notifications.Domain\Foundry.Notifications.Domain.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Foundry.Notifications.Application\Foundry.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Foundry.Notifications.Infrastructure\Foundry.Notifications.Infrastructure.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Foundry.Notifications.Api\Foundry.Notifications.Api.csproj" />
    <ProjectReference Include="..\..\..\Foundry.Tests.Common\Foundry.Tests.Common.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6:** Add all 5 projects to `Foundry.sln`:

```bash
dotnet sln Foundry.sln add \
    src/Modules/Notifications/Foundry.Notifications.Domain/Foundry.Notifications.Domain.csproj \
    src/Modules/Notifications/Foundry.Notifications.Application/Foundry.Notifications.Application.csproj \
    src/Modules/Notifications/Foundry.Notifications.Infrastructure/Foundry.Notifications.Infrastructure.csproj \
    src/Modules/Notifications/Foundry.Notifications.Api/Foundry.Notifications.Api.csproj \
    tests/Modules/Notifications/Foundry.Notifications.Tests/Foundry.Notifications.Tests.csproj
```

- [ ] **Step 7:** Create stub `NotificationsDbContext.cs` so the project compiles:

Create: `src/Modules/Notifications/Foundry.Notifications.Infrastructure/Persistence/NotificationsDbContext.cs`

```csharp
using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : TenantAwareDbContext<NotificationsDbContext>
{
    public NotificationsDbContext(
        DbContextOptions<NotificationsDbContext> options,
        ITenantContext tenantContext) : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

- [ ] **Step 8:** Create stub `NotificationsModuleExtensions.cs`:

Create: `src/Modules/Notifications/Foundry.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs`

```csharp
using Foundry.Notifications.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foundry.Notifications.Infrastructure.Extensions;

public static partial class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddNotificationsPersistence(configuration);
        return services;
    }

    private static IServiceCollection AddNotificationsPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>((sp, options) =>
        {
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        return services;
    }

    public static async Task<WebApplication> InitializeNotificationsModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("NotificationsModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notifications module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
```

- [ ] **Step 9:** Commit

```bash
git add -A
git commit -m "chore: scaffold Notifications module projects"
```

### Task 2.2: Create Messaging module projects

**Files to create:** Same pattern as Notifications but with `Foundry.Messaging.*` namespaces.

- [ ] **Step 1:** Create Domain .csproj (RootNamespace: `Foundry.Messaging.Domain`, references: Shared.Kernel)
- [ ] **Step 2:** Create Application .csproj (RootNamespace: `Foundry.Messaging.Application`, same packages as Notifications)
- [ ] **Step 3:** Create Infrastructure .csproj (RootNamespace: `Foundry.Messaging.Infrastructure`, packages: Dapper, EF Core, Npgsql, Wolverine — no MailKit, no Resilience)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Messaging.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Foundry.Messaging.Domain\Foundry.Messaging.Domain.csproj" />
    <ProjectReference Include="..\Foundry.Messaging.Application\Foundry.Messaging.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Contracts\Foundry.Shared.Contracts.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Infrastructure\Foundry.Shared.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4:** Create Api .csproj (same pattern)
- [ ] **Step 5:** Create Tests .csproj (references Messaging module layers)
- [ ] **Step 6:** Add to `Foundry.sln`
- [ ] **Step 7:** Create stub `MessagingDbContext.cs` (schema: `"messaging"`)
- [ ] **Step 8:** Create stub `MessagingModuleExtensions.cs`
- [ ] **Step 9:** Commit

```bash
git add -A
git commit -m "chore: scaffold Messaging module projects"
```

### Task 2.3: Create Announcements module projects

**Files to create:** Same pattern with `Foundry.Announcements.*` namespaces.

- [ ] **Step 1-5:** Same pattern. Infrastructure needs EF Core + Npgsql + Wolverine (no Dapper, no MailKit).

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Foundry.Announcements.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Foundry.Announcements.Domain\Foundry.Announcements.Domain.csproj" />
    <ProjectReference Include="..\Foundry.Announcements.Application\Foundry.Announcements.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Contracts\Foundry.Shared.Contracts.csproj" />
    <ProjectReference Include="..\..\..\Shared\Foundry.Shared.Infrastructure\Foundry.Shared.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6:** Add to `Foundry.sln`
- [ ] **Step 7:** Create stub `AnnouncementsDbContext.cs` (schema: `"announcements"`)
- [ ] **Step 8:** Create stub `AnnouncementsModuleExtensions.cs`
- [ ] **Step 9:** Commit

```bash
git add -A
git commit -m "chore: scaffold Announcements module projects"
```

---

## Chunk 3: Move Notifications Module Code

Move all notification-related code (Email, SMS, Push, InApp, Preferences) from Communications to the new Notifications module. Update namespaces from `Foundry.Communications.*` to `Foundry.Notifications.*`.

**Namespace mapping:**
- `Foundry.Communications.Domain.Channels.Email.*` → `Foundry.Notifications.Domain.Channels.Email.*`
- `Foundry.Communications.Domain.Channels.InApp.*` → `Foundry.Notifications.Domain.Channels.InApp.*`
- `Foundry.Communications.Domain.Channels.Push.*` → `Foundry.Notifications.Domain.Channels.Push.*`
- `Foundry.Communications.Domain.Channels.Sms.*` → `Foundry.Notifications.Domain.Channels.Sms.*`
- `Foundry.Communications.Domain.Preferences.*` → `Foundry.Notifications.Domain.Preferences.*`
- `Foundry.Communications.Domain.Enums.NotificationType` → `Foundry.Notifications.Domain.Enums.NotificationType`

### Task 3.1: Move Notifications Domain layer

**Move files from Communications.Domain → Notifications.Domain:**

Domain/Channels/Email/ (all files):
- `Entities/EmailMessage.cs`, `Entities/EmailPreference.cs`
- `Enums/EmailStatus.cs`
- `Events/EmailFailedDomainEvent.cs`, `Events/EmailSentDomainEvent.cs`
- `Exceptions/InvalidEmailAddressException.cs`
- `Identity/EmailMessageId.cs`, `Identity/EmailPreferenceId.cs`
- `ValueObjects/EmailAddress.cs`, `ValueObjects/EmailContent.cs`

Domain/Channels/InApp/ (all files):
- `Entities/Notification.cs`
- `Events/NotificationCreatedDomainEvent.cs`, `Events/NotificationReadDomainEvent.cs`
- `Identity/NotificationId.cs`

Domain/Channels/Push/ (all files):
- `DeviceRegistration.cs`, `Entities/PushMessage.cs`, `Entities/TenantPushConfiguration.cs`
- `Enums/PushPlatform.cs`, `Enums/PushStatus.cs`
- `Events/PushMessageFailedDomainEvent.cs`, `Events/PushMessageSentDomainEvent.cs`
- `Identity/DeviceRegistrationId.cs`, `Identity/PushMessageId.cs`, `Identity/TenantPushConfigurationId.cs`

Domain/Channels/Sms/ (all files):
- `Entities/SmsMessage.cs`, `Entities/SmsPreference.cs`
- `Enums/SmsStatus.cs`
- `Events/SmsFailedDomainEvent.cs`, `Events/SmsSentDomainEvent.cs`
- `Exceptions/InvalidPhoneNumberException.cs`
- `Identity/SmsMessageId.cs`, `Identity/SmsPreferenceId.cs`
- `ValueObjects/PhoneNumber.cs`

Domain/Preferences/ (all files):
- `ChannelType.cs`, `Entities/ChannelPreference.cs`
- `Events/ChannelPreferenceCreatedEvent.cs`, `Identity/ChannelPreferenceId.cs`

Domain/Enums/:
- `NotificationType.cs`

- [ ] **Step 1:** Copy ONLY the files listed above to corresponding paths under `src/Modules/Notifications/Foundry.Notifications.Domain/`. Do NOT copy `Messaging/`, `Announcements/`, or `Exceptions/ConversationException.cs` — those belong to Chunks 4 and 5.
- [ ] **Step 2:** Find-and-replace all `namespace Foundry.Communications.Domain` → `namespace Foundry.Notifications.Domain` in the copied files
- [ ] **Step 3:** Find-and-replace all `using Foundry.Communications.Domain` → `using Foundry.Notifications.Domain` in the copied files
- [ ] **Step 4:** Verify build: `dotnet build src/Modules/Notifications/Foundry.Notifications.Domain/`
- [ ] **Step 5:** Commit

```bash
git add src/Modules/Notifications/Foundry.Notifications.Domain/
git commit -m "feat(notifications): move domain layer from Communications"
```

### Task 3.2: Move Notifications Application layer

**Move from Communications.Application → Notifications.Application:**

Channels/Email/ (entire directory — Commands, DTOs, EventHandlers, Extensions, Interfaces, Mappings, Queries, Telemetry)
Channels/InApp/ (entire directory)
Channels/Push/ (entire directory)
Channels/Sms/ (entire directory)
Channels/Preferences/ (entire directory)
Preferences/ (top-level — Commands, DTOs, Interfaces, Queries)
Settings/CommunicationsSettingKeys.cs → Settings/NotificationsSettingKeys.cs

**Critical changes for reactive pattern:**
- **Delete** all `Send*RequestedEventHandler` classes — these handled the command-as-event pattern. They will be replaced by reactive handlers in Task 6.1.
- **Keep** the internal interfaces (IEmailService, IEmailTemplateService, INotificationService, INotificationPreferenceChecker, etc.) — these are now internal to Notifications.
- **Delete** the old `UserRegisteredEventHandler` (email), `UserRegisteredEventHandler` (in-app), `PasswordResetRequestedEventHandler`, `AnnouncementPublishedEventHandler` — these will be rewritten as reactive handlers in Phase 6.
- **Keep** all commands, queries, DTOs, interfaces, repositories for the notification CRUD operations (reading notifications, managing preferences, etc.)

- [ ] **Step 1:** Copy all Channels/ and Preferences/ directories to Notifications.Application
- [ ] **Step 2:** Create `Settings/NotificationsSettingKeys.cs` — copy from `CommunicationsSettingKeys.cs`, rename class to `NotificationsSettingKeys`, change key prefix from `communications` to `notifications`
- [ ] **Step 3:** Create `Extensions/ApplicationExtensions.cs` for Notifications — register validators from assembly
- [ ] **Step 4:** Find-and-replace namespaces: `Foundry.Communications.Application` → `Foundry.Notifications.Application`
- [ ] **Step 5:** Update domain references: `using Foundry.Communications.Domain` → `using Foundry.Notifications.Domain`
- [ ] **Step 6:** Delete event handler files that will be rewritten as reactive handlers:
  - `Channels/Email/EventHandlers/SendEmailRequestedEventHandler.cs`
  - `Channels/Email/EventHandlers/UserRegisteredEventHandler.cs`
  - `Channels/Email/EventHandlers/PasswordResetRequestedEventHandler.cs`
  - `Channels/InApp/EventHandlers/UserRegisteredEventHandler.cs`
  - `Channels/InApp/EventHandlers/AnnouncementPublishedEventHandler.cs`
  - `Channels/Sms/EventHandlers/SendSmsRequestedEventHandler.cs`
  - `Channels/Push/EventHandlers/SendPushRequestedEventHandler.cs`
  - `EventHandlers/InquirySubmittedEventHandler.cs` (top-level — references deleted SendEmailRequestedEvent)
- [ ] **Step 7:** Move `IEmailService` interface from wherever it was in Shared.Contracts into `Channels/Email/Interfaces/IEmailService.cs` (it's now internal to Notifications)
- [ ] **Step 8:** Verify build: `dotnet build src/Modules/Notifications/Foundry.Notifications.Application/`
- [ ] **Step 9:** Commit

```bash
git add src/Modules/Notifications/Foundry.Notifications.Application/
git commit -m "feat(notifications): move application layer from Communications"
```

### Task 3.3: Move Notifications Infrastructure layer

**Move from Communications.Infrastructure → Notifications.Infrastructure:**

- Persistence/NotificationsDbContext.cs — update the stub with all notification-related DbSets
- Persistence/Configurations/ — all notification-related EF configs
- Persistence/Repositories/ — all notification-related repositories
- Services/ — all notification delivery services (SMTP, SMS, Push, SignalR, template, preference checker)
- Jobs/ — RetryFailedEmailsJob
- Extensions/ — update NotificationsModuleExtensions with all service registrations

- [ ] **Step 1:** Update `NotificationsDbContext.cs` — add all notification-related DbSets (EmailMessages, SmsMessages, PushMessages, TenantPushConfigurations, DeviceRegistrations, Notifications, ChannelPreferences, EmailPreferences, SmsPreferences) and settings DbSets. Call `ApplySettingsConfigurations()`.
- [ ] **Step 2:** Copy all notification-related EF configurations and update namespaces
- [ ] **Step 3:** Copy all notification-related repositories, update namespaces, change DbContext type from `CommunicationsDbContext` to `NotificationsDbContext`
- [ ] **Step 4:** Copy all notification-related services, update namespaces
- [ ] **Step 5:** Copy Jobs/RetryFailedEmailsJob.cs, update namespace and DbContext reference
- [ ] **Step 6:** Update `NotificationsModuleExtensions.cs` — wire all notification-related services, register `NotificationsDbContext`, call `AddSettings<NotificationsDbContext, NotificationsSettingKeys>("notifications")`. Copy the full service registration pattern from `CommunicationsModuleExtensions`.
- [ ] **Step 7:** Verify build: `dotnet build src/Modules/Notifications/Foundry.Notifications.Infrastructure/`
- [ ] **Step 8:** Commit

```bash
git add src/Modules/Notifications/Foundry.Notifications.Infrastructure/
git commit -m "feat(notifications): move infrastructure layer from Communications"
```

### Task 3.4: Move Notifications Api layer

**Move from Communications.Api → Notifications.Api:**

Contracts/InApp/Responses/ — NotificationResponse, PagedNotificationResponse, UnreadCountResponse
Contracts/Preferences/ — SetChannelEnabledRequest, SetNotificationTypeEnabledRequest, UserNotificationSettingsResponse
Contracts/Push/ — DeviceRegistrationResponse, RegisterDeviceRequest, SendPushRequest, TenantPushConfigResponse, UpsertTenantPushConfigRequest
Controllers/ — NotificationsController, UserNotificationSettingsController, PushDevicesController, PushConfigurationController, NotificationsSettingsController (renamed from CommunicationsSettingsController)

- [ ] **Step 1:** Copy all notification-related contracts and controllers
- [ ] **Step 2:** Update namespaces from `Foundry.Communications.Api` to `Foundry.Notifications.Api`
- [ ] **Step 3:** Update `using` references to Application layer
- [ ] **Step 4:** Rename `CommunicationsSettingsController` to `NotificationsSettingsController`
- [ ] **Step 5:** Update route attributes on all controllers — replace `communications` with `notifications` in any `[Route("api/v1/communications/...")]` attributes
- [ ] **Step 6:** Verify build: `dotnet build src/Modules/Notifications/Foundry.Notifications.Api/`
- [ ] **Step 7:** Commit

```bash
git add src/Modules/Notifications/Foundry.Notifications.Api/
git commit -m "feat(notifications): move API layer from Communications"
```

### Task 3.5: Move Notifications tests

**Move from Communications.Tests → Notifications.Tests:**

Move all test files that test notification-related code (domain, application, infrastructure, API tests for email, SMS, push, in-app, preferences).

- [ ] **Step 1:** Copy test files, preserving directory structure
- [ ] **Step 2:** Update namespaces from `Foundry.Communications.Tests` to `Foundry.Notifications.Tests`
- [ ] **Step 3:** Update `using` references to the new module namespaces
- [ ] **Step 4:** Delete tests for event handlers that were deleted (Send*RequestedEventHandler tests, old UserRegisteredEventHandler tests, etc.) — new reactive handler tests will be written in Phase 6
- [ ] **Step 5:** Rename `CommunicationsDbContextTests` to `NotificationsDbContextTests`
- [ ] **Step 6:** Verify tests compile: `dotnet build tests/Modules/Notifications/Foundry.Notifications.Tests/`
- [ ] **Step 7:** Run tests: `dotnet test tests/Modules/Notifications/Foundry.Notifications.Tests/`
- [ ] **Step 8:** Commit

```bash
git add tests/Modules/Notifications/
git commit -m "test(notifications): move tests from Communications"
```

---

## Chunk 4: Move Messaging Module Code

Move all conversation/messaging code from Communications to the new Messaging module.

**Namespace mapping:**
- `Foundry.Communications.Domain.Messaging.*` → `Foundry.Messaging.Domain.*`
- `Foundry.Communications.Domain.Exceptions.ConversationException` → `Foundry.Messaging.Domain.Exceptions.ConversationException`
- `Foundry.Communications.Application.Messaging.*` → `Foundry.Messaging.Application.*`

### Task 4.1: Move Messaging Domain layer

**Files:**
- Domain/Entities/ — Conversation.cs, Message.cs, Participant.cs
- Domain/Enums/ — ConversationStatus.cs, MessageStatus.cs
- Domain/Events/ — ConversationCreatedDomainEvent.cs, MessageSentDomainEvent.cs
- Domain/Identity/ — ConversationId.cs, MessageId.cs, ParticipantId.cs
- Domain/Exceptions/ — ConversationException.cs

- [ ] **Step 1:** Copy all messaging domain files to `src/Modules/Messaging/Foundry.Messaging.Domain/`
- [ ] **Step 2:** Update namespaces from `Foundry.Communications.Domain.Messaging` to `Foundry.Messaging.Domain` (drop the `Messaging` level since it IS the module now)
- [ ] **Step 3:** Move `ConversationException.cs` to `Foundry.Messaging.Domain.Exceptions`
- [ ] **Step 4:** Verify build: `dotnet build src/Modules/Messaging/Foundry.Messaging.Domain/`
- [ ] **Step 5:** Commit

```bash
git add src/Modules/Messaging/Foundry.Messaging.Domain/
git commit -m "feat(messaging): move domain layer from Communications"
```

### Task 4.2: Move Messaging Application layer

**Files:**
- Commands/ — CreateConversation/, SendMessage/, MarkConversationRead/
- Queries/ — GetConversations/, GetMessages/, GetUnreadConversationCount/
- EventHandlers/ — ConversationCreatedEventHandler.cs, MessageSentEventHandler.cs
- Interfaces/ — IConversationRepository.cs, IMessagingQueryService.cs
- DTOs/ — ConversationDto.cs, MessageDto.cs, ParticipantDto.cs
- Extensions/ — ApplicationExtensions.cs (new)

- [ ] **Step 1:** Copy all messaging application files
- [ ] **Step 2:** Update namespaces from `Foundry.Communications.Application.Messaging` to `Foundry.Messaging.Application`
- [ ] **Step 3:** **Critical change:** Update `MessageSentEventHandler` — remove `INotificationService` dependency. This handler should now ONLY publish `MessageSentIntegrationEvent` (enriched with `ParticipantIds`). Notifications will handle the notification delivery reactively.
- [ ] **Step 4:** Update `MessageSentEventHandler` to load conversation participants and include `ParticipantIds` (excluding sender) in `MessageSentIntegrationEvent`. The handler must filter out `event.SenderId` from the participant list before setting `ParticipantIds`.
- [ ] **Step 5:** Create `Extensions/ApplicationExtensions.cs` — register validators
- [ ] **Step 6:** Verify build: `dotnet build src/Modules/Messaging/Foundry.Messaging.Application/`
- [ ] **Step 7:** Commit

```bash
git add src/Modules/Messaging/Foundry.Messaging.Application/
git commit -m "feat(messaging): move application layer from Communications"
```

### Task 4.3: Move Messaging Infrastructure layer

**Files:**
- Persistence/MessagingDbContext.cs (update stub — schema: `messaging`, DbSets: Conversations, Participants, Messages)
- Persistence/Configurations/ — ConversationConfiguration.cs, MessageConfiguration.cs, ParticipantConfiguration.cs
- Persistence/Repositories/ — ConversationRepository.cs
- Services/ — MessagingQueryService.cs
- Extensions/ — update MessagingModuleExtensions.cs

- [ ] **Step 1:** Update `MessagingDbContext.cs` — 3 DbSets
- [ ] **Step 2:** Copy EF configurations (3 files), update namespaces and DbContext references
- [ ] **Step 3:** Copy `ConversationRepository.cs`, update namespace and DbContext
- [ ] **Step 4:** Copy `MessagingQueryService.cs`, update namespace
- [ ] **Step 5:** Update `MessagingModuleExtensions.cs` — register DbContext, repositories, query service
- [ ] **Step 6:** Verify build: `dotnet build src/Modules/Messaging/Foundry.Messaging.Infrastructure/`
- [ ] **Step 7:** Commit

```bash
git add src/Modules/Messaging/Foundry.Messaging.Infrastructure/
git commit -m "feat(messaging): move infrastructure layer from Communications"
```

### Task 4.4: Move Messaging Api layer

**Files:**
- Contracts/Requests/ — CreateConversationRequest.cs, SendMessageRequest.cs
- Contracts/Responses/ — ConversationResponse.cs, MessagePageResponse.cs, MessageResponse.cs, UnreadCountResponse.cs
- Controllers/ — ConversationsController.cs

- [ ] **Step 1:** Copy contracts and controller
- [ ] **Step 2:** Update namespaces
- [ ] **Step 3:** Verify build: `dotnet build src/Modules/Messaging/Foundry.Messaging.Api/`
- [ ] **Step 4:** Commit

```bash
git add src/Modules/Messaging/Foundry.Messaging.Api/
git commit -m "feat(messaging): move API layer from Communications"
```

### Task 4.5: Move Messaging tests

- [ ] **Step 1:** Copy test files (domain, application, integration), update namespaces
- [ ] **Step 2:** Update `MessageSentEventHandlerTests` to reflect the new pattern — handler only publishes `MessageSentIntegrationEvent` with `ParticipantIds` (excluding sender), no longer calls `INotificationService`. Add explicit assertion: `publishedEvent.ParticipantIds.Should().NotContain(senderId)`
- [ ] **Step 3:** Verify tests compile and run: `dotnet test tests/Modules/Messaging/Foundry.Messaging.Tests/`
- [ ] **Step 4:** Commit

```bash
git add tests/Modules/Messaging/
git commit -m "test(messaging): move tests from Communications"
```

---

## Chunk 5: Move Announcements Module Code

Move all announcement and changelog code from Communications to the new Announcements module.

**Namespace mapping:**
- `Foundry.Communications.Domain.Announcements.*` → `Foundry.Announcements.Domain.*`
- `Foundry.Communications.Application.Announcements.*` → `Foundry.Announcements.Application.*`

### Task 5.1: Move Announcements Domain layer

**Files:**
- Entities/ — Announcement.cs, AnnouncementDismissal.cs, ChangelogEntry.cs, ChangelogItem.cs
- Enums/ — AnnouncementStatus.cs, AnnouncementTarget.cs, AnnouncementType.cs, ChangeType.cs
- Identity/ — AnnouncementId.cs, AnnouncementDismissalId.cs, ChangelogEntryId.cs, ChangelogItemId.cs

- [ ] **Step 1:** Copy all files, update namespaces from `Foundry.Communications.Domain.Announcements` to `Foundry.Announcements.Domain`
- [ ] **Step 2:** Verify build: `dotnet build src/Modules/Announcements/Foundry.Announcements.Domain/`
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Foundry.Announcements.Domain/
git commit -m "feat(announcements): move domain layer from Communications"
```

### Task 5.2: Move Announcements Application layer

**Files:**
- Commands/ — ArchiveAnnouncement/, CreateAnnouncement/, CreateChangelogEntry/, DismissAnnouncement/, PublishAnnouncement/, PublishChangelogEntry/, UpdateAnnouncement/
- Queries/ — GetActiveAnnouncements/, GetAllAnnouncements/, GetChangelog/, GetChangelogEntry/, GetLatestChangelog/
- Services/ — AnnouncementTargetingService.cs
- Interfaces/ — IAnnouncementRepository.cs, IAnnouncementDismissalRepository.cs, IChangelogRepository.cs
- DTOs/ — AnnouncementDto.cs, ChangelogEntryDto.cs
- EventHandlers/ — AnnouncementPublishedDomainEventHandler.cs (publishes enriched integration event)
- Extensions/ — ApplicationExtensions.cs (new)

- [ ] **Step 1:** Copy all files, update namespaces
- [ ] **Step 2:** **Critical change:** Update `AnnouncementPublishedDomainEventHandler` (the domain event handler that publishes the integration event). It must now use `AnnouncementTargetingService` to resolve `TargetUserIds` and include them in `AnnouncementPublishedEvent`.
- [ ] **Step 3:** Create `Extensions/ApplicationExtensions.cs` — register validators and `AnnouncementTargetingService`
- [ ] **Step 4:** Verify build: `dotnet build src/Modules/Announcements/Foundry.Announcements.Application/`
- [ ] **Step 5:** Commit

```bash
git add src/Modules/Announcements/Foundry.Announcements.Application/
git commit -m "feat(announcements): move application layer from Communications"
```

### Task 5.3: Move Announcements Infrastructure layer

- [ ] **Step 1:** Update `AnnouncementsDbContext.cs` — DbSets for Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems
- [ ] **Step 2:** Copy EF configurations, update namespaces and DbContext references:
  - `AnnouncementConfiguration.cs`
  - `AnnouncementDismissalConfiguration.cs`
  - `ChangelogEntryConfiguration.cs`
  - `ChangelogItemConfiguration.cs` (if standalone; may be configured as owned entity within `ChangelogEntryConfiguration` — check the source)
- [ ] **Step 3:** Copy repositories, update namespaces and DbContext references:
  - `AnnouncementRepository.cs` (implements `IAnnouncementRepository`)
  - `AnnouncementDismissalRepository.cs` (implements `IAnnouncementDismissalRepository`)
  - `ChangelogRepository.cs` (implements `IChangelogRepository`)
- [ ] **Step 4:** Update `AnnouncementsModuleExtensions.cs` — register DbContext, repositories
- [ ] **Step 5:** Verify build: `dotnet build src/Modules/Announcements/Foundry.Announcements.Infrastructure/`
- [ ] **Step 6:** Commit

```bash
git add src/Modules/Announcements/Foundry.Announcements.Infrastructure/
git commit -m "feat(announcements): move infrastructure layer from Communications"
```

### Task 5.4: Move Announcements Api layer

- [ ] **Step 1:** Copy contracts and controllers (AdminAnnouncementsController, AnnouncementsController, AdminChangelogController, ChangelogController), update namespaces
- [ ] **Step 2:** Verify build: `dotnet build src/Modules/Announcements/Foundry.Announcements.Api/`
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Foundry.Announcements.Api/
git commit -m "feat(announcements): move API layer from Communications"
```

### Task 5.5: Move Announcements tests

- [ ] **Step 1:** Copy test files (domain, application, infrastructure, API), update namespaces
- [ ] **Step 2:** Verify tests compile and run: `dotnet test tests/Modules/Announcements/Foundry.Announcements.Tests/`
- [ ] **Step 3:** Commit

```bash
git add tests/Modules/Announcements/
git commit -m "test(announcements): move tests from Communications"
```

---

## Chunk 6: Reactive Notification Handlers

Create the new reactive event handlers in Notifications.Application. These handlers subscribe to domain events from other modules and own all notification logic.

### Task 6.1: Create UserRegisteredNotificationHandler

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/UserRegisteredNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/UserRegisteredNotificationHandlerTests.cs`

- [ ] **Step 1:** Write the failing test:

```csharp
namespace Foundry.Notifications.Tests.Application.EventHandlers;

public sealed class UserRegisteredNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldSendWelcomeEmailAndCreateNotification()
    {
        // Arrange: UserRegisteredEvent with user data
        // Assert: IEmailService.SendAsync called with welcome email
        // Assert: INotificationRepository.AddAsync called
        // Assert: INotificationService.SendToUserAsync called
    }

    [Fact]
    public async Task HandleAsync_WhenEmailPreferenceDisabled_ShouldSkipEmail()
    {
        // Arrange: preference checker returns disabled for email
        // Assert: IEmailService.SendAsync NOT called
        // Assert: in-app notification still created
    }
}
```

- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement `UserRegisteredNotificationHandler` — consumes `UserRegisteredEvent`, checks email preferences, renders welcome email template, sends via SMTP, creates in-app notification, publishes `EmailSentEvent` and `NotificationCreatedEvent`
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive UserRegisteredNotificationHandler"
```

### Task 6.2: Create PasswordResetNotificationHandler

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/PasswordResetNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/PasswordResetNotificationHandlerTests.cs`

- [ ] **Step 1:** Write failing test — handler consumes `PasswordResetRequestedEvent`, sends reset email WITHOUT checking preferences (security-critical), publishes `EmailSentEvent`
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement handler
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive PasswordResetNotificationHandler"
```

### Task 6.3: Create InquirySubmittedNotificationHandler

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquirySubmittedNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/InquirySubmittedNotificationHandlerTests.cs`

- [ ] **Step 1:** Write failing test — handler consumes `InquirySubmittedEvent`, sends admin email (to `event.AdminEmail`), sends submitter confirmation email, publishes `EmailSentEvent` after each
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement handler — reads `AdminEmail` from event (not from configuration)
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive InquirySubmittedNotificationHandler"
```

### Task 6.4: Create AnnouncementPublishedNotificationHandler

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/AnnouncementPublishedNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/AnnouncementPublishedNotificationHandlerTests.cs`

- [ ] **Step 1:** Write failing test — handler consumes `AnnouncementPublishedEvent`, only processes pinned or Alert-type announcements. For each user in `TargetUserIds`:
  - Creates in-app `Notification` entity, saves, pushes via SignalR (`INotificationService`), publishes `NotificationCreatedEvent`
  - Sends push notification via `IPushProviderFactory`, publishes `PushSentEvent`
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement handler — iterates `event.TargetUserIds`, for each user: creates in-app notification + publishes `NotificationCreatedEvent`, sends push notification + publishes `PushSentEvent`
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive AnnouncementPublishedNotificationHandler"
```

### Task 6.5: Create MessageSentNotificationHandler

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/MessageSentNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/MessageSentNotificationHandlerTests.cs`

- [ ] **Step 1:** Write failing test — handler consumes `MessageSentIntegrationEvent`. For each participant in `event.ParticipantIds` (sender already excluded by Messaging):
  - Checks in-app preferences, creates `Notification` entity, pushes via SignalR (`INotificationService`), publishes `NotificationCreatedEvent`
  - Checks push preferences, sends push notification via `IPushProviderFactory`, publishes `PushSentEvent`
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement handler
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive MessageSentNotificationHandler"
```

### Task 6.6: Create InvoiceOverdueNotificationHandler (NEW)

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InvoiceOverdueNotificationHandler.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/Application/EventHandlers/InvoiceOverdueNotificationHandlerTests.cs`

- [ ] **Step 1:** Write failing test — handler consumes `InvoiceOverdueEvent` from Billing (`Foundry.Shared.Contracts.Billing.Events` — already available via the `Foundry.Shared.Contracts` project reference in Notifications.Application). Sends overdue email to `event.UserEmail` (the Billing module populates this with the relevant user's email — may be tenant admin or invoice owner depending on Billing's logic). Publishes `EmailSentEvent` after delivery.
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement handler
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add reactive InvoiceOverdueNotificationHandler"
```

---

## Chunk 7: Database Migrations

Create EF Core initial migrations for the three new schemas. Fresh database — no data migration.

### Task 7.1: Create Notifications schema migration

- [ ] **Step 1:** Generate initial migration:

```bash
dotnet ef migrations add InitialNotificationsSchema \
    --project src/Modules/Notifications/Foundry.Notifications.Infrastructure \
    --startup-project src/Foundry.Api \
    --context NotificationsDbContext
```

- [ ] **Step 2:** Review the generated migration to verify it creates all expected tables in the `notifications` schema. Specifically verify that `TenantSettings` and `UserSettings` tables are included — these are shared keyed-settings infrastructure tables that reside in the notifications schema via `ApplySettingsConfigurations()` in the DbContext.
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Notifications/Foundry.Notifications.Infrastructure/Migrations/
git commit -m "feat(notifications): add initial database migration"
```

### Task 7.2: Create Messaging schema migration

- [ ] **Step 1:** Generate migration for MessagingDbContext (same pattern)
- [ ] **Step 2:** Verify tables: Conversations, Participants, Messages in `messaging` schema
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Messaging/Foundry.Messaging.Infrastructure/Migrations/
git commit -m "feat(messaging): add initial database migration"
```

### Task 7.3: Create Announcements schema migration

- [ ] **Step 1:** Generate migration for AnnouncementsDbContext
- [ ] **Step 2:** Verify tables: Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems in `announcements` schema
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Foundry.Announcements.Infrastructure/Migrations/
git commit -m "feat(announcements): add initial database migration"
```

---

## Chunk 8: Wiring, Configuration & Cleanup

Wire the new modules into the application, update configuration, delete old Communications module, update architecture tests.

### Task 8.1: Update Foundry.Api.csproj

**Files:**
- Modify: `src/Foundry.Api/Foundry.Api.csproj`

- [ ] **Step 1:** Remove Communications project references:

```xml
<!-- Remove -->
<ProjectReference Include="..\Modules\Communications\Foundry.Communications.Api\Foundry.Communications.Api.csproj" />
<ProjectReference Include="..\Modules\Communications\Foundry.Communications.Infrastructure\Foundry.Communications.Infrastructure.csproj" />
```

- [ ] **Step 2:** Add new module project references:

```xml
<!-- Add -->
<ProjectReference Include="..\Modules\Notifications\Foundry.Notifications.Api\Foundry.Notifications.Api.csproj" />
<ProjectReference Include="..\Modules\Notifications\Foundry.Notifications.Infrastructure\Foundry.Notifications.Infrastructure.csproj" />
<ProjectReference Include="..\Modules\Messaging\Foundry.Messaging.Api\Foundry.Messaging.Api.csproj" />
<ProjectReference Include="..\Modules\Messaging\Foundry.Messaging.Infrastructure\Foundry.Messaging.Infrastructure.csproj" />
<ProjectReference Include="..\Modules\Announcements\Foundry.Announcements.Api\Foundry.Announcements.Api.csproj" />
<ProjectReference Include="..\Modules\Announcements\Foundry.Announcements.Infrastructure\Foundry.Announcements.Infrastructure.csproj" />
```

- [ ] **Step 3:** Commit

```bash
git add src/Foundry.Api/Foundry.Api.csproj
git commit -m "chore: update Foundry.Api project references for new modules"
```

### Task 8.2: Update FoundryModules.cs

**Files:**
- Modify: `src/Foundry.Api/FoundryModules.cs`

- [ ] **Step 1:** Replace Communications `using` with new module usings:

```csharp
// Remove
using Foundry.Communications.Infrastructure.Extensions;

// Add
using Foundry.Notifications.Infrastructure.Extensions;
using Foundry.Messaging.Infrastructure.Extensions;
using Foundry.Announcements.Infrastructure.Extensions;
```

- [ ] **Step 2:** Replace Communications registration with three new registrations in `AddFoundryModules`:

```csharp
if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
    services.AddNotificationsModule(configuration);

if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
    services.AddMessagingModule(configuration);

if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
    services.AddAnnouncementsModule(configuration);
```

- [ ] **Step 3:** Replace Communications initialization in `InitializeFoundryModulesAsync`:

```csharp
if (await featureManager.IsEnabledAsync("Modules.Notifications"))
    await app.InitializeNotificationsModuleAsync();

if (await featureManager.IsEnabledAsync("Modules.Messaging"))
    await app.InitializeMessagingModuleAsync();

if (await featureManager.IsEnabledAsync("Modules.Announcements"))
    await app.InitializeAnnouncementsModuleAsync();
```

- [ ] **Step 4:** Commit

```bash
git add src/Foundry.Api/FoundryModules.cs
git commit -m "feat: wire Notifications, Messaging, Announcements modules in FoundryModules"
```

### Task 8.3: Update Program.cs

**Files:**
- Modify: `src/Foundry.Api/Program.cs`

- [ ] **Step 1:** Update Hangfire job registration — change `using Foundry.Communications.Infrastructure.Jobs` to `using Foundry.Notifications.Infrastructure.Jobs`
- [ ] **Step 2:** Verify `RetryFailedEmailsJob` class name is unchanged (it is, just the namespace changed)
- [ ] **Step 3:** Verify that `RetryFailedEmailsJob` is registered in DI — it was registered via `services.AddScoped<RetryFailedEmailsJob>()` in `CommunicationsModuleExtensions`. Confirm that `NotificationsModuleExtensions` includes the same registration (it should from Task 3.3 Step 6).
- [ ] **Step 4:** Commit

```bash
git add src/Foundry.Api/Program.cs
git commit -m "chore: update Program.cs job imports for Notifications module"
```

### Task 8.4: Update appsettings.json

**Files:**
- Modify: `src/Foundry.Api/appsettings.json`
- Modify: `src/Foundry.Api/appsettings.Development.json` (if exists)
- Modify: `src/Foundry.Api/appsettings.Testing.json` (if exists)
- Modify: `src/Foundry.Api/appsettings.Production.json` (if exists)
- Modify: `src/Foundry.Api/appsettings.Staging.json` (if exists)

- [ ] **Step 1:** In `FeatureManagement` section, replace `"Modules.Communications": true` with:

```json
"Modules.Notifications": true,
"Modules.Messaging": true,
"Modules.Announcements": true
```

- [ ] **Step 2:** If there is a legacy `"Foundry": { "Modules": { "Communications": ... } }` section, remove the `Communications` entry and add the three new module entries.

- [ ] **Step 3:** Rename the top-level `"Communications": { "Email": { "Provider": "Smtp" } }` config section to `"Notifications"` to match the new settings namespace.

- [ ] **Step 4:** Check ALL appsettings files (`appsettings.json`, `appsettings.Development.json`, `appsettings.Testing.json`, `appsettings.Production.json`, `appsettings.Staging.json`) for any `Communications` or `Modules.Communications` entries and update them.

- [ ] **Step 5:** Commit

```bash
git add src/Foundry.Api/appsettings*.json
git commit -m "chore: update feature flags and config for new module structure"
```

### Task 8.5: Update architecture tests

**IMPORTANT:** This must be done BEFORE deleting the Communications module (Task 8.6), because the architecture test .csproj references Communications projects.

**Files:**
- Modify: `tests/Foundry.Architecture.Tests/Foundry.Architecture.Tests.csproj`
- Modify: `tests/Foundry.Architecture.Tests/Modules/ModuleToggleTests.cs`
- Modify: `tests/Foundry.Architecture.Tests/ModuleRegistrationTests.cs`
- Modify: `tests/Foundry.Architecture.Tests/MultiTenancyArchitectureTests.cs`

- [ ] **Step 1:** Update `Foundry.Architecture.Tests.csproj` — remove the four `ProjectReference` entries for `Foundry.Communications.*` projects and add references to the three new modules' projects (Domain, Application, Infrastructure, Api for each — 12 new references).

- [ ] **Step 2:** In `ModuleToggleTests.cs`:
  - Replace `CommunicationsDbContext` references with `NotificationsDbContext`, `MessagingDbContext`, `AnnouncementsDbContext`
  - Update feature flag entries: replace `"Modules.Communications"` with three separate flag entries
  - Update `using` statements

- [ ] **Step 3:** In `ModuleRegistrationTests.cs`:
  - Update `_modulesWithDbContext` array: replace `"Communications"` with `"Notifications"`, `"Messaging"`, `"Announcements"`
  - Replace `[InlineData("Communications")]` on `Module_ShouldProvide_AddModuleExtensionMethod` with three `[InlineData]` entries: `[InlineData("Notifications")]`, `[InlineData("Messaging")]`, `[InlineData("Announcements")]`
  - Replace `[InlineData("Communications")]` on `Module_ShouldProvide_InitializeModuleExtensionMethod` with the same three entries

  **Note:** `AllDiscoveredModules_ShouldBeRegistered_InFoundryModules` auto-discovers via DLL scanning and does NOT need manual updates — the new `Foundry.*.Domain.dll` files will be found automatically.

- [ ] **Step 4:** In `MultiTenancyArchitectureTests.cs`:
  - Update `_tenantAwareModules` array: replace `"Communications"` with `"Notifications"`, `"Messaging"`, `"Announcements"`

- [ ] **Step 5:** Verify build: `dotnet build tests/Foundry.Architecture.Tests/`
- [ ] **Step 6:** Commit

```bash
git add tests/Foundry.Architecture.Tests/
git commit -m "test: update architecture tests for new module structure"
```

### Task 8.6: Delete old Communications module

**Files:**
- Delete: entire `src/Modules/Communications/` directory
- Delete: entire `tests/Modules/Communications/` directory

**Note:** If Communications was already removed from the solution in Task 1.4 Step 3, skip Step 1 here.

- [ ] **Step 1:** Remove Communications projects from solution (if not already done):

```bash
dotnet sln Foundry.sln remove \
    src/Modules/Communications/Foundry.Communications.Domain/Foundry.Communications.Domain.csproj \
    src/Modules/Communications/Foundry.Communications.Application/Foundry.Communications.Application.csproj \
    src/Modules/Communications/Foundry.Communications.Infrastructure/Foundry.Communications.Infrastructure.csproj \
    src/Modules/Communications/Foundry.Communications.Api/Foundry.Communications.Api.csproj \
    tests/Modules/Communications/Foundry.Communications.Tests/Foundry.Communications.Tests.csproj
```

- [ ] **Step 2:** Delete the directories:

```bash
rm -rf src/Modules/Communications/
rm -rf tests/Modules/Communications/
```

- [ ] **Step 3:** Verify full build: `dotnet build`
- [ ] **Step 4:** Commit

```bash
git add -A
git commit -m "chore: remove old Communications module"
```

### Task 8.7: Fix remaining cross-codebase references

- [ ] **Step 1:** Search for any remaining `Foundry.Communications` references:

```bash
grep -r "Foundry.Communications" src/ tests/ --include="*.cs" --include="*.csproj"
```

- [ ] **Step 2:** Fix any remaining references
- [ ] **Step 3:** Search for `Communications` in config/docs:

```bash
grep -r "Communications" src/ tests/ --include="*.json" --include="*.yaml"
```

- [ ] **Step 4:** Update any relevant references
- [ ] **Step 5:** Commit if changes needed

```bash
git add -A
git commit -m "chore: fix remaining Communications references across codebase"
```

---

## Chunk 9: Verification

Full build, test run, and final push.

### Task 9.1: Full build verification

- [ ] **Step 1:** Clean and rebuild:

```bash
dotnet clean
dotnet build
```

Expected: 0 errors, 0 warnings (TreatWarningsAsErrors is enabled)

- [ ] **Step 2:** Run all tests:

```bash
dotnet test
```

Expected: All tests pass

- [ ] **Step 3:** Run module-specific tests:

```bash
dotnet test tests/Modules/Notifications/Foundry.Notifications.Tests/
dotnet test tests/Modules/Messaging/Foundry.Messaging.Tests/
dotnet test tests/Modules/Announcements/Foundry.Announcements.Tests/
```

- [ ] **Step 4:** Run architecture tests:

```bash
dotnet test tests/Foundry.Architecture.Tests/
```

- [ ] **Step 5:** Final sweep for stale references:

```bash
grep -r "Foundry.Communications" src/ tests/ --include="*.cs" --include="*.csproj" --include="*.json"
grep -r "Communications" src/Foundry.Api/ --include="*.json"
```

Expected: No matches (excluding docs/ which may legitimately reference Communications for historical context)

- [ ] **Step 6:** Commit any fixes

### Task 9.2: Final push

- [ ] **Step 1:** Check git status: `git status`
- [ ] **Step 2:** Pull and rebase: `git pull --rebase`
- [ ] **Step 3:** Push: `git push`

---

## Task Dependency Map

```
Chunk 1 (Contracts) ──→ Chunk 2 (Scaffolding) ──→ Chunk 3 (Notifications)
                                                ──→ Chunk 4 (Messaging)      ──→ Chunk 6 (Reactive Handlers)
                                                ──→ Chunk 5 (Announcements)
                                                                                    ↓
                                                                              Chunk 7 (Migrations)
                                                                                    ↓
                                                                              Chunk 8 (Wiring & Cleanup)
                                                                                    ↓
                                                                              Chunk 9 (Verification)
```

**Parallelizable:** Chunks 3, 4, and 5 can run in parallel (independent module moves). Chunk 6 depends on Chunks 3, 4, and 5. Chunk 2 tasks (2.1, 2.2, 2.3) can run in parallel.

---

## Summary

| Chunk | Tasks | Key Changes |
|-------|-------|-------------|
| 1. Contracts | 4 | Delete Send*RequestedEvent, move events to new namespaces, add SmsSentEvent/PushSentEvent, enrich events |
| 2. Scaffolding | 3 | 15 new .csproj files, stub DbContexts and ModuleExtensions |
| 3. Notifications | 5 | Move domain, application, infrastructure, API, tests from Communications |
| 4. Messaging | 5 | Move domain, application, infrastructure, API, tests from Communications |
| 5. Announcements | 5 | Move domain, application, infrastructure, API, tests from Communications |
| 6. Reactive Handlers | 6 | New TDD handlers: UserRegistered, PasswordReset, InquirySubmitted, AnnouncementPublished, MessageSent, InvoiceOverdue |
| 7. Migrations | 3 | Standard EF Core initial migrations per module |
| 8. Wiring & Cleanup | 7 | Update FoundryModules, Program.cs, appsettings, delete Communications, update arch tests |
| 9. Verification | 2 | Full build + test + push |
