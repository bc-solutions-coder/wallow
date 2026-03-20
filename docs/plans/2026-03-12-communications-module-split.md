# Communications Module Split — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the Communications module into three independent modules — Notifications, Messaging, and Announcements — as a single big-bang PR.

**Architecture:** Each new module follows the standard 4-project Clean Architecture pattern (Domain → Application → Infrastructure → Api) with its own DbContext, database schema, feature flag, and test project. Cross-module communication uses integration events via Shared.Contracts. The Notifications module is a pure delivery engine with no business logic about what triggers notifications.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL (schema-per-module), Wolverine (CQRS + messaging), FluentValidation, xUnit, NSubstitute, FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-12-communications-module-split-design.md`

---

## Phase 1: Shared Contracts Reorganization

Reorganize the shared contracts from `Communications/` to `Delivery/`, `Messaging/`, and `Announcements/` namespaces. Add new events and the `IsCritical` flag.

### Task 1.1: Create Delivery namespace and move email contracts

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/Email/Events/SendEmailRequestedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/Email/Events/EmailSentEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/Email/IEmailService.cs`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Email/Events/SendEmailRequestedEvent.cs`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Email/Events/EmailSentEvent.cs`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Email/IEmailService.cs`

- [ ] **Step 1:** Create `Delivery/Email/Events/SendEmailRequestedEvent.cs` — copy from `Communications/Email/Events/SendEmailRequestedEvent.cs`, change namespace to `Wallow.Shared.Contracts.Delivery.Email.Events`, add `public bool IsCritical { get; init; }` property
- [ ] **Step 2:** Create `Delivery/Email/Events/EmailSentEvent.cs` — copy from `Communications/Email/Events/EmailSentEvent.cs`, change namespace to `Wallow.Shared.Contracts.Delivery.Email.Events`
- [ ] **Step 3:** Create `Delivery/Email/IEmailService.cs` — copy from `Communications/Email/IEmailService.cs`, change namespace to `Wallow.Shared.Contracts.Delivery.Email`
- [ ] **Step 4:** Delete the old `Communications/Email/` directory
- [ ] **Step 5:** Find all `using Wallow.Shared.Contracts.Communications.Email` references across the codebase and update to `Wallow.Shared.Contracts.Delivery.Email`

Run: `dotnet build src/Shared/Wallow.Shared.Contracts/Wallow.Shared.Contracts.csproj`
Expected: Build succeeds

- [ ] **Step 6:** Commit

```bash
git add -A src/Shared/Wallow.Shared.Contracts/Delivery/Email/ src/Shared/Wallow.Shared.Contracts/Communications/Email/
git commit -m "refactor(contracts): move email contracts to Delivery namespace, add IsCritical"
```

### Task 1.2: Move SMS, Push, InApp contracts to Delivery namespace

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/Sms/Events/SendSmsRequestedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/Push/Events/SendPushRequestedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/InApp/Events/NotificationCreatedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/InApp/Events/SendNotificationRequestedEvent.cs` (NEW)
- Create: `src/Shared/Wallow.Shared.Contracts/Delivery/InApp/NotificationTypes.cs` (NEW)
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Sms/`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Push/`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Notifications/`

- [ ] **Step 1:** Create `Delivery/Sms/Events/SendSmsRequestedEvent.cs` — copy from `Communications/Sms/Events/SendSmsRequestedEvent.cs`, change namespace, add `IsCritical` property
- [ ] **Step 2:** Create `Delivery/Push/Events/SendPushRequestedEvent.cs` — copy from `Communications/Push/Events/SendPushRequestedEvent.cs`, change namespace, add `IsCritical` property
- [ ] **Step 3:** Create `Delivery/InApp/Events/NotificationCreatedEvent.cs` — copy from `Communications/Notifications/Events/NotificationCreatedEvent.cs`, change namespace to `Wallow.Shared.Contracts.Delivery.InApp.Events`
- [ ] **Step 4:** Create NEW `Delivery/InApp/Events/SendNotificationRequestedEvent.cs`:

```csharp
namespace Wallow.Shared.Contracts.Delivery.InApp.Events;

public sealed record SendNotificationRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Type { get; init; }
    public string? ActionUrl { get; init; }
    public string? SourceModule { get; init; }
    public bool IsCritical { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
```

- [ ] **Step 5:** Create NEW `Delivery/InApp/NotificationTypes.cs`:

```csharp
namespace Wallow.Shared.Contracts.Delivery.InApp;

public static class NotificationTypes
{
    public const string System = "System";
    public const string Message = "Message";
    public const string Announcement = "Announcement";
    public const string Security = "Security";
}
```

- [ ] **Step 6:** Delete old directories: `Communications/Sms/`, `Communications/Push/`, `Communications/Notifications/`
- [ ] **Step 7:** Update all `using` statements across the codebase for the moved namespaces

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 8:** Commit

```bash
git add -A
git commit -m "refactor(contracts): move SMS, Push, InApp contracts to Delivery namespace"
```

### Task 1.3: Move Messaging and Announcements contracts

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Messaging/Events/ConversationCreatedIntegrationEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Messaging/Events/MessageSentIntegrationEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Announcements/Events/AnnouncementPublishedEvent.cs`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Messaging/`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/Announcements/`
- Delete: `src/Shared/Wallow.Shared.Contracts/Communications/` (now empty)

- [ ] **Step 1:** Create `Messaging/Events/ConversationCreatedIntegrationEvent.cs` — copy, change namespace to `Wallow.Shared.Contracts.Messaging.Events`
- [ ] **Step 2:** Create `Messaging/Events/MessageSentIntegrationEvent.cs` — copy, change namespace
- [ ] **Step 3:** Create `Announcements/Events/AnnouncementPublishedEvent.cs` — copy, change namespace to `Wallow.Shared.Contracts.Announcements.Events`
- [ ] **Step 4:** Delete old directories and the now-empty `Communications/` directory
- [ ] **Step 5:** Update all `using` statements across the codebase
- [ ] **Step 6:** Run full build to verify

Run: `dotnet build`
Expected: Build succeeds

- [ ] **Step 7:** Commit

```bash
git add -A
git commit -m "refactor(contracts): move Messaging and Announcements contracts to own namespaces"
```

---

## Phase 2: Create Module Project Scaffolding

Create the 12 new source projects (4 per module) and 3 test projects. Wire them into the solution. No code yet — just the .csproj files with correct references, empty DbContexts, and ModuleExtensions stubs.

### Task 2.1: Create Notifications module projects

**Files to create:**
- `src/Modules/Notifications/Wallow.Notifications.Domain/Wallow.Notifications.Domain.csproj`
- `src/Modules/Notifications/Wallow.Notifications.Application/Wallow.Notifications.Application.csproj`
- `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Wallow.Notifications.Infrastructure.csproj`
- `src/Modules/Notifications/Wallow.Notifications.Api/Wallow.Notifications.Api.csproj`
- `tests/Modules/Notifications/Wallow.Notifications.Tests/Wallow.Notifications.Tests.csproj`

- [ ] **Step 1:** Create Domain .csproj (references: Shared.Kernel only):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Notifications.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2:** Create Application .csproj (references: Domain, Kernel, Contracts; packages: FluentValidation, Wolverine, JetBrains.Annotations):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Notifications.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Notifications.Domain\Wallow.Notifications.Domain.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="JetBrains.Annotations" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3:** Create Infrastructure .csproj (references: Domain, Application, Shared.Infrastructure; packages: Dapper, EF Core, Npgsql, Redis, Wolverine, MailKit, Polly):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Notifications.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="WolverineFx" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.Extensions.Resilience" />
    <PackageReference Include="Microsoft.AspNetCore.DataProtection" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Notifications.Domain\Wallow.Notifications.Domain.csproj" />
    <ProjectReference Include="..\Wallow.Notifications.Application\Wallow.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4:** Create Api .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Notifications.Api</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Notifications.Application\Wallow.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Api\Wallow.Shared.Api.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5:** Create Tests .csproj (references all 4 module layers + Shared.Api + Tests.Common):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Notifications.Tests</RootNamespace>
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
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Wallow.Notifications.Domain\Wallow.Notifications.Domain.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Wallow.Notifications.Application\Wallow.Notifications.Application.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Wallow.Notifications.Infrastructure\Wallow.Notifications.Infrastructure.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Notifications\Wallow.Notifications.Api\Wallow.Notifications.Api.csproj" />
    <ProjectReference Include="..\..\..\Wallow.Tests.Common\Wallow.Tests.Common.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6:** Add all 5 projects to `Wallow.sln`

Run: `dotnet sln Wallow.sln add src/Modules/Notifications/Wallow.Notifications.Domain/Wallow.Notifications.Domain.csproj src/Modules/Notifications/Wallow.Notifications.Application/Wallow.Notifications.Application.csproj src/Modules/Notifications/Wallow.Notifications.Infrastructure/Wallow.Notifications.Infrastructure.csproj src/Modules/Notifications/Wallow.Notifications.Api/Wallow.Notifications.Api.csproj tests/Modules/Notifications/Wallow.Notifications.Tests/Wallow.Notifications.Tests.csproj`

- [ ] **Step 7:** Commit

```bash
git add -A
git commit -m "chore: scaffold Notifications module projects"
```

### Task 2.2: Create Messaging module projects

**Files to create:**
- `src/Modules/Messaging/Wallow.Messaging.Domain/Wallow.Messaging.Domain.csproj`
- `src/Modules/Messaging/Wallow.Messaging.Application/Wallow.Messaging.Application.csproj`
- `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Wallow.Messaging.Infrastructure.csproj`
- `src/Modules/Messaging/Wallow.Messaging.Api/Wallow.Messaging.Api.csproj`
- `tests/Modules/Messaging/Wallow.Messaging.Tests/Wallow.Messaging.Tests.csproj`

- [ ] **Step 1:** Create Domain .csproj (same pattern as Notifications Domain, RootNamespace: `Wallow.Messaging.Domain`)
- [ ] **Step 2:** Create Application .csproj (same pattern, RootNamespace: `Wallow.Messaging.Application`; same package refs as Notifications)
- [ ] **Step 3:** Create Infrastructure .csproj (RootNamespace: `Wallow.Messaging.Infrastructure`; packages: Dapper, EF Core, Npgsql, EF Design, Wolverine — no MailKit/Polly/DataProtection needed)
- [ ] **Step 4:** Create Api .csproj (same pattern, RootNamespace: `Wallow.Messaging.Api`)
- [ ] **Step 5:** Create Tests .csproj (same pattern, references Messaging module layers)
- [ ] **Step 6:** Add to `Wallow.sln`
- [ ] **Step 7:** Commit

```bash
git add -A
git commit -m "chore: scaffold Messaging module projects"
```

### Task 2.3: Create Announcements module projects

**Files to create:**
- `src/Modules/Announcements/Wallow.Announcements.Domain/Wallow.Announcements.Domain.csproj`
- `src/Modules/Announcements/Wallow.Announcements.Application/Wallow.Announcements.Application.csproj`
- `src/Modules/Announcements/Wallow.Announcements.Infrastructure/Wallow.Announcements.Infrastructure.csproj`
- `src/Modules/Announcements/Wallow.Announcements.Api/Wallow.Announcements.Api.csproj`
- `tests/Modules/Announcements/Wallow.Announcements.Tests/Wallow.Announcements.Tests.csproj`

- [ ] **Step 1-6:** Same pattern as Messaging. RootNamespace: `Wallow.Announcements.*`. Infrastructure needs EF Core + Npgsql + Wolverine (no Dapper, no MailKit).
- [ ] **Step 7:** Commit

```bash
git add -A
git commit -m "chore: scaffold Announcements module projects"
```

---

## Phase 3: Move Notifications Module Code

Move all notification-related code (Email, SMS, Push, InApp, Preferences) from Communications to the new Notifications module. Update namespaces from `Wallow.Communications.*` to `Wallow.Notifications.*`.

**Namespace mapping:**
- `Wallow.Communications.Domain.Channels.Email.*` → `Wallow.Notifications.Domain.Channels.Email.*`
- `Wallow.Communications.Domain.Channels.InApp.*` → `Wallow.Notifications.Domain.Channels.InApp.*`
- `Wallow.Communications.Domain.Channels.Push.*` → `Wallow.Notifications.Domain.Channels.Push.*`
- `Wallow.Communications.Domain.Channels.Sms.*` → `Wallow.Notifications.Domain.Channels.Sms.*`
- `Wallow.Communications.Domain.Preferences.*` → `Wallow.Notifications.Domain.Preferences.*`
- `Wallow.Communications.Domain.Enums.NotificationType` → `Wallow.Notifications.Domain.Enums.NotificationType`
- `Wallow.Communications.Application.Channels.*` → `Wallow.Notifications.Application.Channels.*`
- `Wallow.Communications.Application.Preferences.*` → `Wallow.Notifications.Application.Preferences.*`
- `Wallow.Communications.Application.Settings.*` → `Wallow.Notifications.Application.Settings.*`
- `Wallow.Communications.Infrastructure.*` (notification-related) → `Wallow.Notifications.Infrastructure.*`
- `Wallow.Communications.Api.*` (notification-related) → `Wallow.Notifications.Api.*`

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

- [ ] **Step 1:** Copy all files listed above to corresponding paths under `src/Modules/Notifications/Wallow.Notifications.Domain/`
- [ ] **Step 2:** Find-and-replace all `namespace Wallow.Communications.Domain` → `namespace Wallow.Notifications.Domain` in the copied files
- [ ] **Step 3:** Verify build: `dotnet build src/Modules/Notifications/Wallow.Notifications.Domain/`
- [ ] **Step 4:** Commit

```bash
git add src/Modules/Notifications/Wallow.Notifications.Domain/
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
Settings/CommunicationsSettingKeys.cs → Settings/NotificationsSettingKeys.cs (rename class + key namespace)
Extensions/ApplicationExtensions.cs (create new one for Notifications)

- [ ] **Step 1:** Copy all Channels/ and Preferences/ directories to Notifications.Application
- [ ] **Step 2:** Create `Settings/NotificationsSettingKeys.cs` — copy from `CommunicationsSettingKeys.cs`, rename class to `NotificationsSettingKeys`, change key prefix from `communications` to `notifications`
- [ ] **Step 3:** Create `Extensions/ApplicationExtensions.cs` for Notifications — register validators from assembly
- [ ] **Step 4:** Find-and-replace namespaces: `Wallow.Communications.Application` → `Wallow.Notifications.Application`
- [ ] **Step 5:** Update domain references: `using Wallow.Communications.Domain` → `using Wallow.Notifications.Domain`
- [ ] **Step 6:** Verify build: `dotnet build src/Modules/Notifications/Wallow.Notifications.Application/`
- [ ] **Step 7:** Commit

```bash
git add src/Modules/Notifications/Wallow.Notifications.Application/
git commit -m "feat(notifications): move application layer from Communications"
```

### Task 3.3: Move Notifications Infrastructure layer

**Move from Communications.Infrastructure → Notifications.Infrastructure:**

Persistence/NotificationsDbContext.cs (new — based on CommunicationsDbContext, schema: `notifications`)
Persistence/Configurations/ — EmailMessage, EmailPreference, SmsMessage, SmsPreference, Notification, ChannelPreference, DeviceRegistration, PushMessage, TenantPushConfiguration configurations
Persistence/Repositories/ — EmailMessage, EmailPreference, SmsMessage, Notification, ChannelPreference, DeviceRegistration, PushMessage, TenantPushConfiguration repositories
Services/ — SmtpEmailProvider, SmtpConnectionPool, SmtpSettings, EmailProviderAdapter, SimpleEmailTemplateService, TwilioSmsProvider, TwilioSettings, NullSmsProvider, FcmPushProvider, ApnsPushProvider, WebPushPushProvider, LogPushProvider, PushProviderFactory, PushCredentialEncryptor, PushSettings, SignalRNotificationService, NotificationPreferenceChecker
Jobs/ — RetryFailedEmailsJob
Extensions/ — NotificationsModuleExtensions.cs (new — based on CommunicationsModuleExtensions)

- [ ] **Step 1:** Create `NotificationsDbContext.cs` — based on `CommunicationsDbContext`, change schema to `"notifications"`, include only notification-related DbSets (EmailMessages, SmsMessages, PushMessages, TenantPushConfigurations, DeviceRegistrations, Notifications, ChannelPreferences, EmailPreferences, SmsPreferences), update namespace
- [ ] **Step 2:** Copy all notification-related EF configurations (9 files) and update namespaces
- [ ] **Step 3:** Copy all notification-related repositories (8 files), update namespaces, change DbContext type from `CommunicationsDbContext` to `NotificationsDbContext`
- [ ] **Step 4:** Copy all notification-related services (17 files), update namespaces
- [ ] **Step 5:** Copy Jobs/RetryFailedEmailsJob.cs, update namespace and DbContext reference
- [ ] **Step 6:** Create `Extensions/NotificationsModuleExtensions.cs` — based on Communications version, wire only notification-related services, register `NotificationsDbContext`, call `AddSettings<NotificationsDbContext, NotificationsSettingKeys>("notifications")`

**Note:** The spec's two-layer email template system (Notifications wraps content in shared layout before sending) is a new behavioral change that should be implemented as a follow-up task after the module split is complete. For now, move `SendEmailRequestedEventHandler` as-is — it passes `event.Body` directly to SMTP. The layout wrapping feature adds `IEmailLayoutService` to the Notifications infrastructure that wraps content in header/footer HTML before delivery.
- [ ] **Step 7:** Verify build: `dotnet build src/Modules/Notifications/Wallow.Notifications.Infrastructure/`
- [ ] **Step 8:** Commit

```bash
git add src/Modules/Notifications/Wallow.Notifications.Infrastructure/
git commit -m "feat(notifications): move infrastructure layer from Communications"
```

### Task 3.4: Move Notifications Api layer

**Move from Communications.Api → Notifications.Api:**

Contracts/InApp/Responses/ — NotificationResponse, PagedNotificationResponse, UnreadCountResponse
Contracts/Preferences/ — SetChannelEnabledRequest, SetNotificationTypeEnabledRequest, UserNotificationSettingsResponse
Contracts/Push/ — DeviceRegistrationResponse, RegisterDeviceRequest, SendPushRequest, TenantPushConfigResponse, UpsertTenantPushConfigRequest
Controllers/ — NotificationsController, UserNotificationSettingsController, PushDevicesController, PushConfigurationController, NotificationsSettingsController (renamed from CommunicationsSettingsController)

- [ ] **Step 1:** Copy all notification-related contracts and controllers
- [ ] **Step 2:** Update namespaces from `Wallow.Communications.Api` to `Wallow.Notifications.Api`
- [ ] **Step 3:** Update `using` references to Application layer
- [ ] **Step 4:** Rename `CommunicationsSettingsController` to `NotificationsSettingsController`
- [ ] **Step 5:** Verify build: `dotnet build src/Modules/Notifications/Wallow.Notifications.Api/`
- [ ] **Step 6:** Commit

```bash
git add src/Modules/Notifications/Wallow.Notifications.Api/
git commit -m "feat(notifications): move API layer from Communications"
```

### Task 3.5: Move Notifications tests

**Move from Communications.Tests → Notifications.Tests:**

Move all test files that test notification-related code:
- Domain tests: `Channels/Email/`, `Channels/InApp/`, `Channels/Push/`, `Domain/Email/`, `Domain/Entities/Notification*`, `Domain/Channels/Sms/`, `Domain/Preferences/`, `Domain/Events/DomainEventTests.cs`, `Domain/Identity/StronglyTypedIdTests.cs`
- Application tests: `Application/Channels/Email/`, `Application/Channels/InApp/`, `Application/Channels/Sms/`, `Application/Channels/Push/` (if exists), `Application/DTOs/EmailDtoTests.cs`, `Application/Extensions/ApplicationExtensionsTests.cs`, `Application/Extensions/EmailApplicationExtensionsTests.cs`, `Application/Extensions/InAppApplicationExtensionsTests.cs`, `Application/Handlers/NotificationHandlerTests.cs`, `Application/Telemetry/`
- Infrastructure tests: `Infrastructure/Jobs/`, `Infrastructure/Persistence/CommunicationsDbContextTests.cs` (rename), `Infrastructure/Persistence/Repositories/EmailMessage*`, `Infrastructure/Persistence/Repositories/EmailPreference*`, `Infrastructure/Persistence/Repositories/Notification*`, `Infrastructure/Services/`
- API tests: `Api/Controllers/NotificationsControllerTests.cs`
- Contract tests: Notification-related parts of `Api/Contracts/RequestContractTests.cs` and `ResponseContractTests.cs`
- Integration tests: `Integration/Sms/SmsIntegrationTests.cs`

- [ ] **Step 1:** Copy test files, preserving directory structure
- [ ] **Step 2:** Update namespaces from `Wallow.Communications.Tests` to `Wallow.Notifications.Tests`
- [ ] **Step 3:** Update `using` references to the new module namespaces
- [ ] **Step 4:** Rename `CommunicationsDbContextTests` to `NotificationsDbContextTests`
- [ ] **Step 5:** Create `GlobalUsings.cs` for the test project
- [ ] **Step 6:** Verify tests compile: `dotnet build tests/Modules/Notifications/Wallow.Notifications.Tests/`
- [ ] **Step 7:** Run tests: `dotnet test tests/Modules/Notifications/Wallow.Notifications.Tests/`
- [ ] **Step 8:** Commit

```bash
git add tests/Modules/Notifications/
git commit -m "test(notifications): move tests from Communications"
```

---

## Phase 4: Move Messaging Module Code

Move all conversation/messaging code from Communications to the new Messaging module.

**Namespace mapping:**
- `Wallow.Communications.Domain.Messaging.*` → `Wallow.Messaging.Domain.*`
- `Wallow.Communications.Domain.Exceptions.ConversationException` → `Wallow.Messaging.Domain.Exceptions.ConversationException`
- `Wallow.Communications.Application.Messaging.*` → `Wallow.Messaging.Application.*`
- `Wallow.Communications.Infrastructure.Persistence.Configurations.Conversation*` → `Wallow.Messaging.Infrastructure.*`
- `Wallow.Communications.Infrastructure.Persistence.Repositories.Conversation*` → `Wallow.Messaging.Infrastructure.*`
- `Wallow.Communications.Infrastructure.Services.MessagingQueryService` → `Wallow.Messaging.Infrastructure.*`

### Task 4.1: Move Messaging Domain layer

**Files:**
- Domain/Entities/ — Conversation.cs, Message.cs, Participant.cs
- Domain/Enums/ — ConversationStatus.cs, MessageStatus.cs
- Domain/Events/ — ConversationCreatedDomainEvent.cs, MessageSentDomainEvent.cs
- Domain/Identity/ — ConversationId.cs, MessageId.cs, ParticipantId.cs
- Domain/Exceptions/ — ConversationException.cs

- [ ] **Step 1:** Copy all messaging domain files to `src/Modules/Messaging/Wallow.Messaging.Domain/`
- [ ] **Step 2:** Update namespaces from `Wallow.Communications.Domain.Messaging` to `Wallow.Messaging.Domain` (drop the `Messaging` level since it IS the module now)
- [ ] **Step 3:** Move `ConversationException.cs` to `Wallow.Messaging.Domain.Exceptions`
- [ ] **Step 4:** Verify build
- [ ] **Step 5:** Commit

```bash
git add src/Modules/Messaging/Wallow.Messaging.Domain/
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
- [ ] **Step 2:** Update namespaces from `Wallow.Communications.Application.Messaging` to `Wallow.Messaging.Application`
- [ ] **Step 3:** **Critical change:** Update `MessageSentEventHandler` — remove `INotificationService` dependency, replace with `IMessageBus.PublishAsync` to publish `SendNotificationRequestedEvent` for participant notifications
- [ ] **Step 4:** Create `Extensions/ApplicationExtensions.cs` — register validators
- [ ] **Step 5:** Verify build
- [ ] **Step 6:** Commit

```bash
git add src/Modules/Messaging/Wallow.Messaging.Application/
git commit -m "feat(messaging): move application layer from Communications"
```

### Task 4.3: Move Messaging Infrastructure layer

**Files:**
- Persistence/MessagingDbContext.cs (new — schema: `messaging`, DbSets: Conversations, Participants, Messages)
- Persistence/Configurations/ — ConversationConfiguration.cs, MessageConfiguration.cs, ParticipantConfiguration.cs
- Persistence/Repositories/ — ConversationRepository.cs
- Services/ — MessagingQueryService.cs
- Extensions/ — MessagingModuleExtensions.cs (new)

- [ ] **Step 1:** Create `MessagingDbContext.cs` — schema `"messaging"`, 3 DbSets
- [ ] **Step 2:** Copy EF configurations (3 files), update namespaces and DbContext references
- [ ] **Step 3:** Copy `ConversationRepository.cs`, update namespace and DbContext
- [ ] **Step 4:** Copy `MessagingQueryService.cs`, update namespace
- [ ] **Step 5:** Create `MessagingModuleExtensions.cs` — register DbContext, repositories, query service
- [ ] **Step 6:** Verify build
- [ ] **Step 7:** Commit

```bash
git add src/Modules/Messaging/Wallow.Messaging.Infrastructure/
git commit -m "feat(messaging): move infrastructure layer from Communications"
```

### Task 4.4: Move Messaging Api layer

**Files:**
- Contracts/Requests/ — CreateConversationRequest.cs, SendMessageRequest.cs
- Contracts/Responses/ — ConversationResponse.cs, MessagePageResponse.cs, MessageResponse.cs, UnreadCountResponse.cs
- Controllers/ — ConversationsController.cs

- [ ] **Step 1:** Copy contracts and controller
- [ ] **Step 2:** Update namespaces
- [ ] **Step 3:** Verify build
- [ ] **Step 4:** Commit

```bash
git add src/Modules/Messaging/Wallow.Messaging.Api/
git commit -m "feat(messaging): move API layer from Communications"
```

### Task 4.5: Move Messaging tests

**Move:**
- Domain: `Domain/Messaging/ConversationTests.cs`, `Domain/Messaging/MessageTests.cs`, `Domain/Messaging/ParticipantTests.cs`
- Application: `Application/Messaging/Commands/`, `Application/Messaging/EventHandlers/`
- Integration: `Integration/Messaging/ConversationIntegrationTests.cs`

- [ ] **Step 1:** Copy test files, update namespaces
- [ ] **Step 2:** Update `MessageSentEventHandlerTests` to reflect the new `IMessageBus.PublishAsync` pattern instead of `INotificationService`
- [ ] **Step 3:** Create `GlobalUsings.cs`
- [ ] **Step 4:** Verify tests compile and run
- [ ] **Step 5:** Commit

```bash
git add tests/Modules/Messaging/
git commit -m "test(messaging): move tests from Communications"
```

---

## Phase 5: Move Announcements Module Code

Move all announcement and changelog code from Communications to the new Announcements module.

**Namespace mapping:**
- `Wallow.Communications.Domain.Announcements.*` → `Wallow.Announcements.Domain.*`
- `Wallow.Communications.Application.Announcements.*` → `Wallow.Announcements.Application.*`

### Task 5.1: Move Announcements Domain layer

**Files:**
- Entities/ — Announcement.cs, AnnouncementDismissal.cs, ChangelogEntry.cs, ChangelogItem.cs
- Enums/ — AnnouncementStatus.cs, AnnouncementTarget.cs, AnnouncementType.cs, ChangeType.cs
- Identity/ — AnnouncementId.cs, AnnouncementDismissalId.cs, ChangelogEntryId.cs, ChangelogItemId.cs

- [ ] **Step 1:** Copy all files, update namespaces from `Wallow.Communications.Domain.Announcements` to `Wallow.Announcements.Domain`
- [ ] **Step 2:** Verify build
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Wallow.Announcements.Domain/
git commit -m "feat(announcements): move domain layer from Communications"
```

### Task 5.2: Move Announcements Application layer

**Files:**
- Commands/ — ArchiveAnnouncement/, CreateAnnouncement/, CreateChangelogEntry/, DismissAnnouncement/, PublishAnnouncement/, PublishChangelogEntry/, UpdateAnnouncement/
- Queries/ — GetActiveAnnouncements/, GetAllAnnouncements/, GetChangelog/, GetChangelogEntry/
- Services/ — AnnouncementTargetingService.cs
- Interfaces/ — IAnnouncementRepository.cs, IAnnouncementDismissalRepository.cs, IChangelogRepository.cs
- DTOs/ — AnnouncementDto.cs, ChangelogEntryDto.cs
- Extensions/ — ApplicationExtensions.cs (new)

- [ ] **Step 1:** Copy all files, update namespaces
- [ ] **Step 2:** Create `Extensions/ApplicationExtensions.cs`
- [ ] **Step 3:** Verify build
- [ ] **Step 4:** Commit

```bash
git add src/Modules/Announcements/Wallow.Announcements.Application/
git commit -m "feat(announcements): move application layer from Communications"
```

### Task 5.3: Move Announcements Infrastructure layer

**Files:**
- Persistence/AnnouncementsDbContext.cs (new — schema: `announcements`)
- Persistence/Configurations/ — AnnouncementConfiguration.cs, AnnouncementDismissalConfiguration.cs, ChangelogEntryConfiguration.cs, ChangelogItemConfiguration.cs
- Persistence/Repositories/ — AnnouncementRepository.cs, AnnouncementDismissalRepository.cs, ChangelogRepository.cs
- Extensions/ — AnnouncementsModuleExtensions.cs (new)

- [ ] **Step 1:** Create `AnnouncementsDbContext.cs` — schema `"announcements"`, DbSets for Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems
- [ ] **Step 2:** Copy EF configurations (4 files), update namespaces and DbContext
- [ ] **Step 3:** Copy repositories (3 files), update namespaces and DbContext
- [ ] **Step 4:** Create `AnnouncementsModuleExtensions.cs`
- [ ] **Step 5:** Verify build
- [ ] **Step 6:** Commit

```bash
git add src/Modules/Announcements/Wallow.Announcements.Infrastructure/
git commit -m "feat(announcements): move infrastructure layer from Communications"
```

### Task 5.4: Move Announcements Api layer

**Files:**
- Contracts/Responses/ — AnnouncementResponse.cs
- Controllers/ — AdminAnnouncementsController.cs, AnnouncementsController.cs, AdminChangelogController.cs, ChangelogController.cs

- [ ] **Step 1:** Copy contracts and controllers, update namespaces
- [ ] **Step 2:** Verify build
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Wallow.Announcements.Api/
git commit -m "feat(announcements): move API layer from Communications"
```

### Task 5.5: Move Announcements tests

**Move:**
- Domain: `Domain/Announcements/` (all 6 test files)
- Application: `Application/Announcements/` (all handler, query, service, validator tests)
- Application DTOs: `Application/DTOs/AnnouncementDtoTests.cs`
- Application top-level: `Application/Handlers/AnnouncementHandlerTests.cs`
- Infrastructure: `Infrastructure/Persistence/Repositories/Announcement*`, `Infrastructure/Persistence/Repositories/ChangelogRepositoryTests.cs`
- API: `Api/Controllers/AdminAnnouncementsControllerTests.cs`, `Api/Controllers/AdminChangelogControllerTests.cs`, `Api/Controllers/AnnouncementsControllerTests.cs`, `Api/Controllers/ChangelogControllerTests.cs`

- [ ] **Step 1:** Copy test files, update namespaces
- [ ] **Step 2:** Create `GlobalUsings.cs`
- [ ] **Step 3:** Verify tests compile and run
- [ ] **Step 4:** Commit

```bash
git add tests/Modules/Announcements/
git commit -m "test(announcements): move tests from Communications"
```

---

## Phase 6: Event Handler Relocation

Move domain-specific event handlers from the old Communications module to their owning modules. Create the new `SendNotificationRequestedEventHandler` in Notifications.

### Task 6.1: Create SendNotificationRequestedEventHandler in Notifications

**Files:**
- Create: `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/EventHandlers/SendNotificationRequestedEventHandler.cs`
- Create: `tests/Modules/Notifications/Wallow.Notifications.Tests/Application/Channels/InApp/EventHandlers/SendNotificationRequestedEventHandlerTests.cs`

- [ ] **Step 1:** Write the failing test for `SendNotificationRequestedEventHandler` — it should consume `SendNotificationRequestedEvent`, create a `Notification` entity, save it, and push via `INotificationService`. Test that it checks preferences (unless `IsCritical`).
- [ ] **Step 2:** Run test to verify it fails
- [ ] **Step 3:** Implement `SendNotificationRequestedEventHandler`:
  - Inject `INotificationRepository`, `INotificationPreferenceChecker`, `INotificationService`, `ILogger`
  - Check preferences if `!event.IsCritical`
  - Create `Notification` entity from event data
  - Save to repository
  - Push via `INotificationService.SendToUserAsync`
  - Log warning if `event.Type` is not in known `NotificationTypes`
- [ ] **Step 4:** Run test to verify it passes
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "feat(notifications): add SendNotificationRequestedEventHandler"
```

### Task 6.2: Move UserRegistered and PasswordReset handlers to Identity

**Files:**
- Create: `src/Modules/Identity/Wallow.Identity.Application/EventHandlers/UserRegisteredEmailHandler.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Application/EventHandlers/UserRegisteredNotificationHandler.cs`
- Create: `src/Modules/Identity/Wallow.Identity.Application/EventHandlers/PasswordResetRequestedHandler.cs`
- Delete (from old Communications): `Channels/Email/EventHandlers/UserRegisteredEventHandler.cs`, `Channels/Email/EventHandlers/PasswordResetRequestedEventHandler.cs`, `Channels/InApp/EventHandlers/UserRegisteredEventHandler.cs`

- [ ] **Step 0:** Add `WolverineFx` package reference to `src/Modules/Identity/Wallow.Identity.Application/Wallow.Identity.Application.csproj` — Identity currently has no Wolverine handlers; this is the first integration-event handler in Identity.Application
- [ ] **Step 1:** Write test for `UserRegisteredEmailHandler` — consumes `UserRegisteredEvent`, renders welcome email content, publishes `SendEmailRequestedEvent` via `IMessageBus`
- [ ] **Step 2:** Implement `UserRegisteredEmailHandler`
- [ ] **Step 3:** Write test for `UserRegisteredNotificationHandler` — consumes `UserRegisteredEvent`, publishes `SendNotificationRequestedEvent` with type `NotificationTypes.System`
- [ ] **Step 4:** Implement `UserRegisteredNotificationHandler`
- [ ] **Step 5:** Write test for `PasswordResetRequestedHandler` — consumes `PasswordResetRequestedEvent`, renders reset email content, publishes `SendEmailRequestedEvent` with `IsCritical = true`
- [ ] **Step 6:** Implement `PasswordResetRequestedHandler` — **behavioral change:** no longer checks `EmailPreference`, relies on `IsCritical` flag
- [ ] **Step 7:** Run all Identity tests: `dotnet test tests/Modules/Identity/`
- [ ] **Step 8:** Commit

```bash
git add -A
git commit -m "feat(identity): add email/notification handlers for user registration and password reset"
```

### Task 6.3: Move AnnouncementPublishedEventHandler to Announcements

**Files:**
- Create: `src/Modules/Announcements/Wallow.Announcements.Application/EventHandlers/AnnouncementPublishedNotificationHandler.cs`
- Move tests: Update `AnnouncementPublishedEventHandlerTests.cs` in Announcements.Tests

- [ ] **Step 1:** Write test — on `AnnouncementPublishedEvent` for pinned/alert announcements, publishes `SendNotificationRequestedEvent` and `SendPushRequestedEvent` via `IMessageBus` (no longer calls `INotificationService` directly)

**Note on broadcast vs per-user:** The current handler uses `INotificationService.BroadcastToTenantAsync()` but `SendNotificationRequestedEvent` requires a specific `UserId`. The handler should use the existing `AnnouncementTargetingService` to resolve the target user IDs, then publish a `SendNotificationRequestedEvent` per user. For large tenants this is a fanout — acceptable for the initial implementation; optimize in a follow-up if needed.

- [ ] **Step 2:** Implement handler — inject `AnnouncementTargetingService` to resolve target users, then loop and publish per-user events via `IMessageBus.PublishAsync`
- [ ] **Step 3:** Run tests
- [ ] **Step 4:** Commit

```bash
git add -A
git commit -m "feat(announcements): add notification handler for published announcements"
```

### Task 6.4: Move InquirySubmittedEventHandler to Inquiries

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquirySubmittedEmailHandler.cs`
- Delete (from old Communications): `EventHandlers/InquirySubmittedEventHandler.cs`

- [ ] **Step 1:** Write test — consumes `InquirySubmittedEvent`, publishes two `SendEmailRequestedEvent`s (admin notification + submitter confirmation) via `IMessageBus`
- [ ] **Step 2:** Implement handler (logic is largely the same as existing, just uses `IMessageBus` instead of direct email service)
- [ ] **Step 3:** Verify Inquiries builds and tests pass
- [ ] **Step 4:** Commit

```bash
git add -A
git commit -m "feat(inquiries): add email handler for inquiry submissions"
```

### Task 6.5: Update MessageSentEventHandler in Messaging

This was partially done in Task 4.2, but verify the handler change is complete.

- [ ] **Step 1:** Verify `MessageSentEventHandler` in Messaging.Application uses `IMessageBus.PublishAsync` for `SendNotificationRequestedEvent` and `SendPushRequestedEvent` (not `INotificationService`)
- [ ] **Step 2:** Verify `MessageSentEventHandlerTests` tests the new pattern
- [ ] **Step 3:** Run: `dotnet test tests/Modules/Messaging/`
- [ ] **Step 4:** Commit if any changes needed

---

## Phase 7: Database Migrations

Create EF Core migrations for the three new schemas. Use `ALTER TABLE SET SCHEMA` for zero-copy table moves.

### Task 7.1: Create Notifications schema migration

- [ ] **Step 1:** Generate initial migration for NotificationsDbContext:

```bash
dotnet ef migrations add InitialNotificationsSchema \
    --project src/Modules/Notifications/Wallow.Notifications.Infrastructure \
    --startup-project src/Wallow.Api \
    --context NotificationsDbContext
```

- [ ] **Step 2:** Edit the generated migration to use `ALTER TABLE SET SCHEMA` instead of `CREATE TABLE`:

```csharp
// In Up() method:
migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS notifications;");
migrationBuilder.Sql("ALTER TABLE communications.\"EmailMessages\" SET SCHEMA notifications;");
migrationBuilder.Sql("ALTER TABLE communications.\"SmsMessages\" SET SCHEMA notifications;");
// ... all notification tables
migrationBuilder.Sql("ALTER TABLE communications.\"TenantSettings\" SET SCHEMA notifications;");
migrationBuilder.Sql("ALTER TABLE communications.\"UserSettings\" SET SCHEMA notifications;");
```

- [ ] **Step 3:** Commit

```bash
git add src/Modules/Notifications/Wallow.Notifications.Infrastructure/Migrations/
git commit -m "feat(notifications): add schema migration from communications"
```

### Task 7.2: Create Messaging schema migration

- [ ] **Step 1:** Generate migration for MessagingDbContext
- [ ] **Step 2:** Edit to use `ALTER TABLE SET SCHEMA` for Conversations, Participants, Messages
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Messaging/Wallow.Messaging.Infrastructure/Migrations/
git commit -m "feat(messaging): add schema migration from communications"
```

### Task 7.3: Create Announcements schema migration

- [ ] **Step 1:** Generate migration for AnnouncementsDbContext
- [ ] **Step 2:** Edit to use `ALTER TABLE SET SCHEMA` for Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems
- [ ] **Step 3:** Commit

```bash
git add src/Modules/Announcements/Wallow.Announcements.Infrastructure/Migrations/
git commit -m "feat(announcements): add schema migration from communications"
```

---

## Phase 8: Wiring, Configuration & Cleanup

Wire the new modules into the application, update configuration, and remove the old Communications module.

### Task 8.1: Update WallowModules.cs, Wallow.Api.csproj, and Program.cs

**Files:**
- Modify: `src/Wallow.Api/WallowModules.cs`
- Modify: `src/Wallow.Api/Wallow.Api.csproj`
- Modify: `src/Wallow.Api/Program.cs`

- [ ] **Step 0:** Update `src/Wallow.Api/Wallow.Api.csproj` — remove the two Communications project references and add six new ones:

```xml
<!-- Remove -->
<ProjectReference Include="..\Modules\Communications\Wallow.Communications.Api\Wallow.Communications.Api.csproj" />
<ProjectReference Include="..\Modules\Communications\Wallow.Communications.Infrastructure\Wallow.Communications.Infrastructure.csproj" />

<!-- Add -->
<ProjectReference Include="..\Modules\Notifications\Wallow.Notifications.Api\Wallow.Notifications.Api.csproj" />
<ProjectReference Include="..\Modules\Notifications\Wallow.Notifications.Infrastructure\Wallow.Notifications.Infrastructure.csproj" />
<ProjectReference Include="..\Modules\Messaging\Wallow.Messaging.Api\Wallow.Messaging.Api.csproj" />
<ProjectReference Include="..\Modules\Messaging\Wallow.Messaging.Infrastructure\Wallow.Messaging.Infrastructure.csproj" />
<ProjectReference Include="..\Modules\Announcements\Wallow.Announcements.Api\Wallow.Announcements.Api.csproj" />
<ProjectReference Include="..\Modules\Announcements\Wallow.Announcements.Infrastructure\Wallow.Announcements.Infrastructure.csproj" />
```

- [ ] **Step 1:** Replace `Modules.Communications` registration with three new registrations:

```csharp
if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
{
    services.AddNotificationsModule(configuration);
}

if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
{
    services.AddMessagingModule(configuration);
}

if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
{
    services.AddAnnouncementsModule(configuration);
}
```

- [ ] **Step 2:** Update `InitializeWallowModulesAsync` similarly — replace `InitializeCommunicationsModuleAsync` with three new calls
- [ ] **Step 3:** Add `using` statements for new module extensions
- [ ] **Step 4:** Update `src/Wallow.Api/Program.cs` — change Hangfire job registration from `Wallow.Communications.Infrastructure.Jobs.RetryFailedEmailsJob` to `Wallow.Notifications.Infrastructure.Jobs.RetryFailedEmailsJob`. Update the `using` statement accordingly.
- [ ] **Step 5:** Verify build: `dotnet build src/Wallow.Api/`
- [ ] **Step 6:** Commit

```bash
git add src/Wallow.Api/WallowModules.cs src/Wallow.Api/Wallow.Api.csproj src/Wallow.Api/Program.cs
git commit -m "feat: wire Notifications, Messaging, Announcements modules in WallowModules"
```

### Task 8.2: Update appsettings.json

**Files:**
- Modify: `src/Wallow.Api/appsettings.json`
- Modify: `src/Wallow.Api/appsettings.Development.json` (if exists)
- Modify: `src/Wallow.Api/appsettings.Production.json` (if exists)
- Modify: `src/Wallow.Api/appsettings.Staging.json` (if exists)
- Modify: `src/Wallow.Api/appsettings.Testing.json` (if exists)

- [ ] **Step 1:** Replace `"Modules.Communications": true` with:

```json
"Modules.Notifications": true,
"Modules.Messaging": true,
"Modules.Announcements": true
```

- [ ] **Step 2:** Remove any `Wallow.Modules.Communications` entry if present in the vestigial `Wallow.Modules` section
- [ ] **Step 3:** Commit

```bash
git add src/Wallow.Api/appsettings*.json
git commit -m "chore: update feature flags for new module structure"
```

### Task 8.3: Delete old Communications module

**Files:**
- Delete: entire `src/Modules/Communications/` directory
- Delete: entire `tests/Modules/Communications/` directory
- Modify: `Wallow.sln` — remove old project references

- [ ] **Step 1:** Remove Communications projects from solution:

```bash
dotnet sln Wallow.sln remove \
    src/Modules/Communications/Wallow.Communications.Domain/Wallow.Communications.Domain.csproj \
    src/Modules/Communications/Wallow.Communications.Application/Wallow.Communications.Application.csproj \
    src/Modules/Communications/Wallow.Communications.Infrastructure/Wallow.Communications.Infrastructure.csproj \
    src/Modules/Communications/Wallow.Communications.Api/Wallow.Communications.Api.csproj \
    tests/Modules/Communications/Wallow.Communications.Tests/Wallow.Communications.Tests.csproj
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

### Task 8.4: Update any remaining cross-codebase references

- [ ] **Step 1:** Search for any remaining `Wallow.Communications` references:

```bash
grep -r "Wallow.Communications" src/ tests/ --include="*.cs" --include="*.csproj"
```

- [ ] **Step 2:** Fix any remaining references (architecture tests, shared infrastructure, etc.)
- [ ] **Step 3:** Search for `Communications` in non-code files:

```bash
grep -r "Communications" src/ tests/ --include="*.json" --include="*.md" --include="*.yaml"
```

- [ ] **Step 4:** Update any relevant config/documentation references
- [ ] **Step 5:** Commit

```bash
git add -A
git commit -m "chore: fix remaining Communications references across codebase"
```

---

## Phase 9: Verification

Full build, test run, and architecture validation.

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

- [ ] **Step 3:** Run module-specific tests to verify isolation:

```bash
dotnet test tests/Modules/Notifications/Wallow.Notifications.Tests/
dotnet test tests/Modules/Messaging/Wallow.Messaging.Tests/
dotnet test tests/Modules/Announcements/Wallow.Announcements.Tests/
```

- [ ] **Step 4:** Verify architecture tests still pass (if they exist):

```bash
dotnet test tests/Wallow.Architecture.Tests/
```

- [ ] **Step 5:** Commit any fixes

### Task 9.2: Update architecture tests

**Files:**
- Modify: `tests/Wallow.Architecture.Tests/ModuleRegistrationTests.cs`
- Modify: `tests/Wallow.Architecture.Tests/Modules/ModuleToggleTests.cs`
- Modify: `tests/Wallow.Architecture.Tests/MultiTenancyArchitectureTests.cs`

- [ ] **Step 1:** In `ModuleRegistrationTests.cs`:
  - Replace `[InlineData("Communications")]` with `[InlineData("Notifications")]`, `[InlineData("Messaging")]`, `[InlineData("Announcements")]` in both `Module_ShouldProvide_AddModuleExtensionMethod` and `Module_ShouldProvide_InitializeModuleExtensionMethod`
  - Update `_modulesWithDbContext` array: replace `"Communications"` with `"Notifications"`, `"Messaging"`, `"Announcements"`

- [ ] **Step 2:** In `ModuleToggleTests.cs`:
  - Replace `"FeatureManagement:Modules.Communications" = "true"` with three separate feature flag entries
  - Replace `typeof(CommunicationsDbContext)` checks with `typeof(NotificationsDbContext)`, `typeof(MessagingDbContext)`, `typeof(AnnouncementsDbContext)`

- [ ] **Step 3:** In `MultiTenancyArchitectureTests.cs`:
  - Replace `"Communications"` in `_tenantAwareModules` with `"Notifications"`, `"Messaging"`, `"Announcements"`

- [ ] **Step 4:** Verify: `dotnet test tests/Wallow.Architecture.Tests/`
- [ ] **Step 5:** Commit

```bash
git add tests/Wallow.Architecture.Tests/
git commit -m "test: update architecture tests for new module structure"
```

### Task 9.3: Final verification and push

- [ ] **Step 1:** Run full test suite one final time: `dotnet test`
- [ ] **Step 2:** Check git status: `git status`
- [ ] **Step 3:** Push: `git push`

---

## Task Dependency Map

```
Phase 1 (Contracts) ──→ Phase 2 (Scaffolding) ──→ Phase 3 (Notifications)
                                                ──→ Phase 4 (Messaging)      ──→ Phase 6 (Handler Relocation)
                                                ──→ Phase 5 (Announcements)
                                                                                    ↓
                                                                              Phase 7 (Migrations)
                                                                                    ↓
                                                                              Phase 8 (Wiring & Cleanup)
                                                                                    ↓
                                                                              Phase 9 (Verification)
```

**Parallelizable:** Phases 3, 4, and 5 can run in parallel (independent module moves). Phase 6 depends on all three being complete. Phase 2 tasks (2.1, 2.2, 2.3) can run in parallel.

---

## Summary

| Phase | Tasks | Est. Files Changed | Can Parallelize |
|-------|-------|--------------------|-----------------|
| 1. Contracts | 3 | ~30 | Sequential |
| 2. Scaffolding | 3 | 15 new .csproj | Yes (all 3) |
| 3. Notifications | 5 | ~120 move + rename | No (sequential layers) |
| 4. Messaging | 5 | ~30 move + rename | Yes (with Phase 3, 5) |
| 5. Announcements | 5 | ~40 move + rename | Yes (with Phase 3, 4) |
| 6. Handler Relocation | 5 | ~15 new + modify | After 3, 4, 5 |
| 7. Migrations | 3 | 6 new | Yes (all 3) |
| 8. Wiring & Cleanup | 4 | ~10 modify + delete all old | Sequential |
| 9. Verification | 3 | Few fixes | Sequential |
