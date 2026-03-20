# Push Notifications & User Notification Settings Design

## Overview

Add modular push notification support to the Communications module with three interchangeable providers: Firebase Cloud Messaging (FCM), Apple Push Notification service (APNs), and Web Push (VAPID). Uses message-based fan-out via Wolverine/RabbitMQ for per-device delivery. Supports per-tenant provider configuration with encrypted credentials and app-level defaults as fallback.

Also adds a unified user notification settings API so users can control which channels and notification types they receive, replacing scattered per-channel preference endpoints. Settings are owned by the Communications module (not centralized in Configuration).

## Domain Layer (`Channels/Push/`)

### Entities

**`DeviceRegistration`** — aggregate root, tenant-scoped
- `DeviceRegistrationId` (strongly-typed ID)
- `TenantId`, `UserId`
- `Platform` (`PushPlatform` enum)
- `DeviceToken` (string)
- `DeviceName` (string, optional — e.g. "John's iPhone")
- `LastUsedAt` (DateTime)
- Lifecycle: `Create()`, `UpdateToken()`, `UpdateLastUsed()`

**`PushMessage`** — aggregate root, tenant-scoped (one per device delivery)
- `PushMessageId` (strongly-typed ID)
- `TenantId`, `UserId`, `DeviceRegistrationId`
- `Title`, `Body`, `Data` (string? — JSON payload)
- `Platform` (`PushPlatform` enum — denormalized for query convenience)
- `Status` (`PushStatus` enum)
- `FailureReason` (string?), `RetryCount` (int)
- `SentAt` (DateTime?)
- Lifecycle: `Create()` → `MarkAsSent()` / `MarkAsFailed()` → `ResetForRetry()`, `CanRetry()`

**`TenantPushConfiguration`** — aggregate root, tenant-scoped
- `TenantPushConfigurationId` (strongly-typed ID)
- `TenantId`, `Platform` (`PushPlatform` enum)
- `EncryptedCredentials` (string — encrypted JSON blob)
- `IsEnabled` (bool)
- One record per tenant per platform

### Enums

- **`PushPlatform`**: `Fcm = 0`, `Apns = 1`, `WebPush = 2`
- **`PushStatus`**: `Pending = 0`, `Sent = 1`, `Failed = 2`

### Domain Events

- `PushSentDomainEvent(PushMessageId)`
- `PushFailedDomainEvent(PushMessageId, string Reason)`

### Identity Types

- `PushMessageId` : `IStronglyTypedId<PushMessageId>`
- `DeviceRegistrationId` : `IStronglyTypedId<DeviceRegistrationId>`
- `TenantPushConfigurationId` : `IStronglyTypedId<TenantPushConfigurationId>`

## Application Layer (`Channels/Push/`)

### Interfaces

**`IPushProvider`**
```csharp
public readonly record struct PushDeliveryResult(bool Success, string? MessageId, string? ErrorMessage);

public record PushDeliveryRequest(
    string DeviceToken,
    PushPlatform Platform,
    string Title,
    string Body,
    string? Data);

public interface IPushProvider
{
    PushPlatform Platform { get; }
    Task<PushDeliveryResult> SendAsync(PushDeliveryRequest request, CancellationToken ct = default);
}
```

**`IPushProviderFactory`**
```csharp
public interface IPushProviderFactory
{
    IPushProvider GetProvider(TenantId tenantId, PushPlatform platform);
}
```

Resolves the correct provider instance with tenant-specific or default credentials.

**Repositories:**
- `IPushMessageRepository` — standard repo for `PushMessage`
- `IDeviceRegistrationRepository` — CRUD + `GetByUserIdAsync(UserId)`, `GetByIdAsync(DeviceRegistrationId)`
- `ITenantPushConfigurationRepository` — `GetByTenantAndPlatformAsync()`, `GetByTenantAsync()`

### Commands

**`SendPushNotificationCommand`** — `UserId`, `Title`, `Body`, `Data?`
Handler:
1. Check `ChannelPreference` for `ChannelType.Push` — if user opted out, skip
2. Look up all `DeviceRegistration` records for the user
3. For each device: persist a `PushMessage` (status=Pending), publish `DeliverPushCommand` to the bus

**`DeliverPushCommand`** — `PushMessageId`, `DeviceToken`, `Platform`, `Title`, `Body`, `Data?`, `TenantId`
Handler:
1. Resolve provider via `IPushProviderFactory` using `TenantId` + `Platform`
2. Call `SendAsync`
3. Update `PushMessage` to `Sent` or `Failed`

**`RegisterDeviceCommand`** — `Platform`, `DeviceToken`, `DeviceName?`
Handler: Creates `DeviceRegistration`, or updates token if same user+platform+device exists

**`UpdateDeviceCommand`** — `DeviceRegistrationId`, `DeviceToken?`, `DeviceName?`
Handler: Updates the registration fields, bumps `LastUsedAt`

**`RemoveDeviceCommand`** — `DeviceRegistrationId`
Handler: Deletes the registration

**`SetTenantPushConfigurationCommand`** — `Platform`, `Credentials` (unencrypted settings object), `IsEnabled`
Handler: Encrypts credentials via Data Protection API, upserts `TenantPushConfiguration`

**`RemoveTenantPushConfigurationCommand`** — `Platform`
Handler: Deletes the tenant's config for that platform, falls back to app-level defaults

### Queries

- `GetUserDevicesQuery` — returns all devices for the current user
- `GetTenantPushConfigurationQuery` — returns configured platforms for the tenant (admin-only, credentials redacted)

### Event Handler

**`SendPushRequestedEventHandler`** — consumes `SendPushRequestedEvent` from `Shared.Contracts`, dispatches `SendPushNotificationCommand`

### Validators

FluentValidation for each command (device token not empty, title not empty, platform is valid, etc.)

## Infrastructure Layer

### Providers

All implement `IPushProvider`. Each is constructed with its settings (resolved per-tenant at runtime by the factory).

**`FcmPushProvider`** — Firebase Cloud Messaging HTTP v1 API
- Uses `HttpClient` with Google service account JWT auth
- Settings: `ProjectId`, `ServiceAccountKeyJson`

**`ApnsPushProvider`** — Apple APNs HTTP/2 API
- Uses `HttpClient` with JWT token auth (ES256)
- Settings: `TeamId`, `KeyId`, `PrivateKey`, `BundleId`, `UseProduction`

**`WebPushPushProvider`** — Web Push via VAPID (RFC 8030)
- Uses `HttpClient` with VAPID auth headers
- Settings: `VapidPublicKey`, `VapidPrivateKey`, `Subject`

**`LogPushProvider`** — Logs push details at Information level, returns success. Used when no provider is configured for a platform.

### `PushProviderFactory`

Implements `IPushProviderFactory`:
1. Check `TenantPushConfiguration` for tenant + platform → decrypt credentials → construct provider
2. If not found, check `appsettings.json` for platform defaults → construct provider
3. If neither, return `LogPushProvider`

Caches constructed providers per-tenant+platform for the request scope.

### Encryption

Uses **ASP.NET Core Data Protection API** for encrypting/decrypting tenant credentials:
- `IDataProtector` with purpose string `"TenantPushCredentials"`
- Encrypt on write (`SetTenantPushConfigurationCommand` handler)
- Decrypt on read (`PushProviderFactory` when constructing providers)
- Key storage configured per environment (filesystem for dev, database or vault for production)

### Settings Classes (app-level defaults from `appsettings.json`)

```csharp
public sealed class FcmSettings
{
    public bool Enabled { get; init; }
    public string ProjectId { get; init; }
    public string ServiceAccountKeyPath { get; init; }
}

public sealed class ApnsSettings
{
    public bool Enabled { get; init; }
    public string TeamId { get; init; }
    public string KeyId { get; init; }
    public string PrivateKeyPath { get; init; }
    public string BundleId { get; init; }
    public bool UseProduction { get; init; }
}

public sealed class WebPushSettings
{
    public bool Enabled { get; init; }
    public string VapidPublicKey { get; init; }
    public string VapidPrivateKey { get; init; }
    public string Subject { get; init; }
}
```

### `appsettings.json` structure

```json
{
  "Communications": {
    "Push": {
      "Fcm": {
        "Enabled": true,
        "ProjectId": "my-project",
        "ServiceAccountKeyPath": "/secrets/fcm-key.json"
      },
      "Apns": {
        "Enabled": false,
        "TeamId": "",
        "KeyId": "",
        "PrivateKeyPath": "",
        "BundleId": "",
        "UseProduction": false
      },
      "WebPush": {
        "Enabled": true,
        "VapidPublicKey": "",
        "VapidPrivateKey": "",
        "Subject": "mailto:admin@wallow.dev"
      }
    }
  }
}
```

### DI Registration (in `CommunicationsModuleExtensions`)

- Configure `FcmSettings`, `ApnsSettings`, `WebPushSettings` from config sections
- Register `IPushProviderFactory` as scoped
- Register `LogPushProvider` as singleton (fallback)
- Register `HttpClient` for each enabled provider with resilience handlers
- Register push repositories

### EF Core

- `DeviceRegistration`, `PushMessage`, `TenantPushConfiguration` configurations added to `CommunicationsDbContext`
- Migration for new tables in the `communications` schema

## API Layer

### `PushDevicesController`

- `POST /api/v1/push/devices` — register a device
- `GET /api/v1/push/devices` — list current user's devices
- `PUT /api/v1/push/devices/{id}` — update token or name
- `DELETE /api/v1/push/devices/{id}` — remove a device

### `PushConfigurationController` (admin-only)

- `POST /api/v1/push/configuration` — set provider config for a platform
- `GET /api/v1/push/configuration` — list configured platforms (credentials redacted)
- `PUT /api/v1/push/configuration/{platform}` — update provider config
- `DELETE /api/v1/push/configuration/{platform}` — remove, falls back to defaults

### Shared.Contracts

Add `SendPushRequestedEvent`:
```csharp
public sealed record SendPushRequestedEvent(
    Guid UserId,
    string Title,
    string Body,
    string? Data = null) : IntegrationEvent;
```

## Delivery Flow

```
Module publishes SendPushRequestedEvent
    → SendPushRequestedEventHandler (consumes from RabbitMQ)
        → SendPushNotificationCommand
            → Check ChannelPreference (Push enabled for user?)
            → Load DeviceRegistrations for user
            → For each device:
                → Persist PushMessage (Pending)
                → Publish DeliverPushCommand to bus
                    → PushProviderFactory resolves provider (tenant config → app defaults → LogPushProvider)
                    → Provider.SendAsync()
                    → Update PushMessage (Sent/Failed)
```

## User Notification Settings

### Problem

The existing `ChannelPreference` entity already supports `UserId + ChannelType + NotificationType + IsEnabled`, but:
- There's only an `EmailPreferencesController` — no unified API across channels
- No global channel toggle (e.g., "disable all push" without toggling every notification type)
- Settings are moving out of the centralized Configuration module into the modules that own them

### Design

**Global channel toggle convention:** A `ChannelPreference` record with `NotificationType = "*"` acts as the global kill switch for that channel. Delivery handlers check this first.

**Preference check order (all delivery handlers):**
1. Is there a `ChannelPreference` with `NotificationType = "*"` for this channel? If `IsEnabled = false` → skip delivery
2. Is there a `ChannelPreference` for this specific `NotificationType`? If `IsEnabled = false` → skip delivery
3. No preference record exists → deliver (opt-out model, enabled by default)

### API: `UserNotificationSettingsController`

Replaces scattered per-channel endpoints. All endpoints operate on the current authenticated user.

- `GET /api/v1/notifications/settings` — returns all channel preferences grouped by channel type. Response shape:
```json
{
  "channels": [
    {
      "channelType": "Email",
      "isEnabled": true,
      "notificationTypes": [
        { "notificationType": "InvoiceReady", "isEnabled": true },
        { "notificationType": "WelcomeEmail", "isEnabled": false }
      ]
    },
    {
      "channelType": "Push",
      "isEnabled": true,
      "notificationTypes": []
    },
    {
      "channelType": "Sms",
      "isEnabled": false,
      "notificationTypes": []
    }
  ]
}
```

- `PUT /api/v1/notifications/settings/channels/{channelType}` — toggle an entire channel on/off
```json
{ "isEnabled": false }
```
Sets or updates the `NotificationType = "*"` record for that channel.

- `PUT /api/v1/notifications/settings/channels/{channelType}/{notificationType}` — toggle a specific notification type
```json
{ "isEnabled": false }
```

### Application Layer Changes

**New query:** `GetUserNotificationSettingsQuery(Guid UserId)` — loads all `ChannelPreference` records for the user, groups by channel, and includes the global toggle status.

**New command:** `SetChannelEnabledCommand(Guid UserId, ChannelType ChannelType, bool IsEnabled)` — upserts the `NotificationType = "*"` record.

The existing `SetChannelPreferenceCommand` already handles the per-notification-type toggle.

**New repository method:** `IChannelPreferenceRepository.GetGlobalPreferenceAsync(Guid userId, ChannelType channelType)` — fetches the `"*"` record for quick checks in delivery handlers.

### Delivery Handler Integration

All delivery handlers (email, SMS, push, in-app) need to check preferences before sending. Add a shared service:

```csharp
public interface INotificationPreferenceChecker
{
    Task<bool> IsChannelEnabledForUserAsync(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        CancellationToken ct = default);
}
```

Implementation checks global toggle first, then specific notification type. Returns `true` if no preference records exist (opt-out model).

Used by `SendPushNotificationCommand` handler, `SendSmsHandler`, `SendEmailHandler`, etc.

### Remove EmailPreferencesController

Delete `EmailPreferencesController` and its associated request/response contracts. The `UserNotificationSettingsController` fully replaces it — same underlying `ChannelPreference` table, unified API surface. No migration needed since the data model is unchanged.
