# Phase 2: Shared Contracts

**Scope:** `src/Shared/Foundry.Shared.Contracts/`
**Status:** Not Started
**Files:** 40 source files, 0 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Root

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Foundry.Shared.Contracts/IIntegrationEvent.cs` | Marker interface and base record for cross-module integration events with EventId and OccurredAt | `IIntegrationEvent` interface; `IntegrationEvent` abstract record with auto-generated `EventId` and `OccurredAt` | None (zero dependencies by design) | |

### Annotations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 2 | [ ] | `src/Shared/Foundry.Shared.Contracts/Annotations/UsedImplicitlyAttribute.cs` | Internal JetBrains.Annotations polyfill to suppress unused-member warnings on event properties | `UsedImplicitlyAttribute` and `ImplicitUseTargetFlags` enum; `internal sealed` to avoid dependency | None | |

### Billing/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 3 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/Events/InvoiceCreatedEvent.cs` | Integration event published when an invoice is created; consumed by Communications for notifications | Properties: InvoiceId, TenantId, UserId, UserEmail, InvoiceNumber, Amount, Currency, DueDate | Contracts.IntegrationEvent | |
| 4 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/Events/InvoiceOverdueEvent.cs` | Integration event published when an invoice becomes overdue; triggers reminder notifications | Same structure as InvoiceCreatedEvent with overdue context | Contracts.IntegrationEvent | |
| 5 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/Events/InvoicePaidEvent.cs` | Integration event published when an invoice is paid; triggers receipt notifications | Properties: InvoiceId, TenantId, PaymentId, UserId, InvoiceNumber, Amount, Currency, PaidAt | Contracts.IntegrationEvent | |
| 6 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/Events/PaymentReceivedEvent.cs` | Integration event published when a payment is received; triggers confirmation emails | Properties: PaymentId, TenantId, InvoiceId, UserId, UserEmail, Amount, Currency, PaymentMethod, PaidAt | Contracts.IntegrationEvent | |

### Billing/Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 7 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/IInvoiceQueryService.cs` | Cross-module query interface for invoice aggregates (revenue, counts, outstanding amounts) | `GetTotalRevenueAsync`, `GetCountAsync`, `GetPendingCountAsync`, `GetOutstandingAmountAsync` | None | |
| 8 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/IInvoiceReportService.cs` | Cross-module reporting interface for invoice detail rows with date range filtering | `GetInvoicesAsync` returns `IReadOnlyList<InvoiceReportRow>`; `InvoiceReportRow` record | None | |
| 9 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/IPaymentReportService.cs` | Cross-module reporting interface for payment detail rows with date range filtering | `GetPaymentsAsync` returns `IReadOnlyList<PaymentReportRow>`; `PaymentReportRow` record | None | |
| 10 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/IRevenueReportService.cs` | Cross-module reporting interface for revenue aggregation by period | `GetRevenueAsync` returns `IReadOnlyList<RevenueReportRow>` with gross/net/refunds breakdown | None | |
| 11 | [ ] | `src/Shared/Foundry.Shared.Contracts/Billing/ISubscriptionQueryService.cs` | Cross-module query for active subscription plan code by tenant | `GetActivePlanCodeAsync(Guid tenantId)` returns `string?` | None | |

### Communications/Announcements/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 12 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Announcements/Events/AnnouncementPublishedEvent.cs` | Integration event for published announcements with targeting info | Properties: AnnouncementId, TenantId, Title, Content, Type, Target, TargetValue, IsPinned | Contracts.IntegrationEvent | |

### Communications/Email/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Email/Events/EmailSentEvent.cs` | Integration event published after an email is successfully sent | Properties: EmailId, TenantId, ToAddress, Subject, TemplateName, SentAt | Contracts.IntegrationEvent | |
| 14 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Email/Events/SendEmailRequestedEvent.cs` | Integration event requesting email delivery from any module | Properties: TenantId, To, From (optional), Subject, Body, SourceModule, CorrelationId | Contracts.IntegrationEvent | |

### Communications/Email

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 15 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Email/IEmailService.cs` | Cross-module email service interface with plain and attachment-based sending | `SendAsync`, `SendWithAttachmentAsync` methods | None | |

### Communications/Messaging/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 16 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Messaging/Events/ConversationCreatedIntegrationEvent.cs` | Integration event for new conversation creation with participant list | Properties: ConversationId, ParticipantIds, CreatedAt, TenantId | Contracts.IntegrationEvent | |
| 17 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Messaging/Events/MessageSentIntegrationEvent.cs` | Integration event for sent messages within conversations | Properties: ConversationId, MessageId, SenderId, Content, SentAt, TenantId | Contracts.IntegrationEvent | |

### Communications/Notifications/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 18 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Notifications/Events/NotificationCreatedEvent.cs` | Integration event for notification creation and delivery readiness | Properties: NotificationId, TenantId, UserId, Title, Type, CreatedAt | Contracts.IntegrationEvent | |

### Communications/Sms/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 19 | [ ] | `src/Shared/Foundry.Shared.Contracts/Communications/Sms/Events/SendSmsRequestedEvent.cs` | Integration event requesting SMS delivery from any module | Properties: TenantId, To, Body, SourceModule, CorrelationId | Contracts.IntegrationEvent | |

### Identity/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 20 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/OrganizationCreatedEvent.cs` | Integration event for new organization creation in Keycloak | Properties: OrganizationId, TenantId, Name, Domain (optional) | Contracts.IntegrationEvent | |
| 21 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/OrganizationMemberAddedEvent.cs` | Integration event for user addition to an organization | Properties: OrganizationId, TenantId, UserId, Email | Contracts.IntegrationEvent | |
| 22 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/OrganizationMemberRemovedEvent.cs` | Integration event for user removal from an organization | Properties: OrganizationId, TenantId, UserId | Contracts.IntegrationEvent | |
| 23 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/PasswordResetRequestedEvent.cs` | Integration event for password reset requests triggering reset emails | Properties: UserId, TenantId, Email, ResetToken | Contracts.IntegrationEvent | |
| 24 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/UserRegisteredEvent.cs` | Integration event for new user registration triggering welcome flows | Properties: UserId, TenantId, Email, FirstName, LastName | Contracts.IntegrationEvent | |
| 25 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/Events/UserRoleChangedEvent.cs` | Integration event for user role changes triggering notifications | Properties: UserId, TenantId, Email, OldRole, NewRole | Contracts.IntegrationEvent | |

### Identity/Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 26 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/IUserQueryService.cs` | Cross-module query interface for user statistics (email lookup, counts by tenant) | `GetUserEmailAsync`, `GetNewUsersCountAsync`, `GetActiveUsersCountAsync`, `GetTotalUsersCountAsync` | None | |
| 27 | [ ] | `src/Shared/Foundry.Shared.Contracts/Identity/IUserService.cs` | Cross-module user lookup service with UserInfo DTO | `GetUserByIdAsync`, `GetUserByEmailAsync`; `UserInfo` record with Id, Email, FirstName, LastName, IsActive | None | |

### Inquiries/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 28 | [ ] | `src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquiryStatusChangedEvent.cs` | Integration event for inquiry status transitions | Properties: InquiryId, OldStatus, NewStatus, ChangedAt | Contracts.IntegrationEvent | |
| 29 | [ ] | `src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquirySubmittedEvent.cs` | Integration event for new inquiry submissions with contact details | Properties: InquiryId, Name, Email, Company, Phone, Subject, Message, SubmittedAt | Contracts.IntegrationEvent | |

### Metering/Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 30 | [ ] | `src/Shared/Foundry.Shared.Contracts/Metering/Events/QuotaThresholdReachedEvent.cs` | Integration event when tenant usage reaches a quota threshold (80%, 90%, 100%) | Properties: TenantId, MeterCode, MeterDisplayName, CurrentUsage, Limit, PercentUsed, Period | Contracts.IntegrationEvent | |
| 31 | [ ] | `src/Shared/Foundry.Shared.Contracts/Metering/Events/UsageFlushedEvent.cs` | Integration event when usage data is flushed from Valkey to PostgreSQL | Properties: FlushedAt, RecordCount | Contracts.IntegrationEvent | |

### Metering/Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Shared/Foundry.Shared.Contracts/Metering/IMeteringQueryService.cs` | Cross-module quota check interface with QuotaStatus result DTO | `CheckQuotaAsync` returns `QuotaStatus` with Used, Limit, PercentUsed, IsExceeded | None | |
| 33 | [ ] | `src/Shared/Foundry.Shared.Contracts/Metering/IUsageReportService.cs` | Cross-module usage reporting interface for tenant usage rows by date range | `GetUsageAsync` returns `IReadOnlyList<UsageReportRow>` with Date, Metric, Quantity, Unit, BillableAmount | Contracts.Annotations (UsedImplicitly) | |

### Realtime

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 34 | [ ] | `src/Shared/Foundry.Shared.Contracts/Realtime/IPresenceService.cs` | Interface for tracking user connections, page context, and online status via SignalR+Redis | `TrackConnectionAsync`, `RemoveConnectionAsync`, `SetPageContextAsync`, `GetOnlineUsersAsync`, `IsUserOnlineAsync` | None | |
| 35 | [ ] | `src/Shared/Foundry.Shared.Contracts/Realtime/IRealtimeDispatcher.cs` | Interface for dispatching real-time messages to users, groups, or entire tenants | `SendToUserAsync`, `SendToGroupAsync`, `SendToTenantAsync` with `RealtimeEnvelope` | None | |
| 36 | [ ] | `src/Shared/Foundry.Shared.Contracts/Realtime/RealtimeEnvelope.cs` | Standard wrapper record for real-time messages with type, module, payload, and correlation | `Type`, `Module`, `Payload`, `Timestamp`, `CorrelationId`; static `Create` factory | None | |
| 37 | [ ] | `src/Shared/Foundry.Shared.Contracts/Realtime/UserPresence.cs` | DTO record for user presence state including connections and current pages | `UserId`, `DisplayName`, `ConnectionIds`, `CurrentPages` | None | |

### Storage

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 38 | [ ] | `src/Shared/Foundry.Shared.Contracts/Storage/Commands/UploadFileCommand.cs` | Command record for file upload requests with tenant context and metadata | `TenantId`, `UserId`, `BucketName`, `FileName`, `ContentType`, `Content` (Stream), `SizeBytes`, `Path`, `IsPublic` | None | |
| 39 | [ ] | `src/Shared/Foundry.Shared.Contracts/Storage/IStorageProvider.cs` | Low-level storage backend interface for file I/O operations | `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `ExistsAsync`, `GetPresignedUrlAsync` | None | |
| 40 | [ ] | `src/Shared/Foundry.Shared.Contracts/Storage/UploadResult.cs` | DTO record for upload operation results with file metadata | `FileId`, `FileName`, `StorageKey`, `SizeBytes`, `ContentType`, `UploadedAt` | None | |

## Test Files

No dedicated test project for Shared.Contracts (contract types are validated through integration and architecture tests).
