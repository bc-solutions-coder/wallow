# Push Notifications & User Notification Settings Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add modular push notification support (FCM, APNs, Web Push) with message-based fan-out, per-tenant encrypted provider configuration, and a unified user notification settings API to the Communications module.

**Architecture:** Follows existing Communications module patterns — domain aggregates with strongly-typed IDs, application-layer CQRS with Wolverine, infrastructure providers behind interfaces, FluentValidation, and EF Core persistence. Push delivery fans out via Wolverine messages (one per device), not loops. Tenant push credentials are encrypted via ASP.NET Core Data Protection API.

**Tech Stack:** .NET 10, EF Core, Wolverine, RabbitMQ, FluentValidation, ASP.NET Core Data Protection, PostgreSQL (`communications` schema)

**Design doc:** `docs/plans/2026-03-10-push-notifications-design.md`

---

## Phase 1: Push Domain Layer

Foundation entities, enums, value objects, events, and IDs for the push channel.

### Task 1.1: Push Enums and Identity Types

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Enums/PushPlatform.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Enums/PushStatus.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/PushMessageId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/DeviceRegistrationId.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/TenantPushConfigurationId.cs`

**Step 1: Create PushPlatform enum**

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Enums/PushPlatform.cs
namespace Wallow.Communications.Domain.Channels.Push.Enums;

public enum PushPlatform
{
    Fcm = 0,
    Apns = 1,
    WebPush = 2
}
```

**Step 2: Create PushStatus enum**

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Enums/PushStatus.cs
namespace Wallow.Communications.Domain.Channels.Push.Enums;

public enum PushStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}
```

**Step 3: Create strongly-typed IDs**

Follow the pattern from `SmsMessageId`:

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/PushMessageId.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Domain.Channels.Push.Identity;

public readonly record struct PushMessageId(Guid Value) : IStronglyTypedId<PushMessageId>
{
    public static PushMessageId Create(Guid value) => new(value);
    public static PushMessageId New() => new(Guid.NewGuid());
}
```

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/DeviceRegistrationId.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Domain.Channels.Push.Identity;

public readonly record struct DeviceRegistrationId(Guid Value) : IStronglyTypedId<DeviceRegistrationId>
{
    public static DeviceRegistrationId Create(Guid value) => new(value);
    public static DeviceRegistrationId New() => new(Guid.NewGuid());
}
```

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Identity/TenantPushConfigurationId.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Domain.Channels.Push.Identity;

public readonly record struct TenantPushConfigurationId(Guid Value) : IStronglyTypedId<TenantPushConfigurationId>
{
    public static TenantPushConfigurationId Create(Guid value) => new(value);
    public static TenantPushConfigurationId New() => new(Guid.NewGuid());
}
```

**Step 4: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/
git commit -m "feat(communications): add push notification enums and identity types"
```

---

### Task 1.2: Push Domain Events

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Events/PushSentDomainEvent.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Events/PushFailedDomainEvent.cs`

**Step 1: Create domain events**

Follow the pattern from `SmsSentDomainEvent` / `SmsFailedDomainEvent`:

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Events/PushSentDomainEvent.cs
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Communications.Domain.Channels.Push.Events;

public sealed record PushSentDomainEvent(
    PushMessageId MessageId) : DomainEvent;
```

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Events/PushFailedDomainEvent.cs
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using JetBrains.Annotations;

namespace Wallow.Communications.Domain.Channels.Push.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record PushFailedDomainEvent(
    PushMessageId MessageId,
    string Reason) : DomainEvent;
```

**Step 2: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Events/
git commit -m "feat(communications): add push domain events"
```

---

### Task 1.3: DeviceRegistration Entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/DeviceRegistration.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/DeviceRegistrationTests.cs`

**Step 1: Write tests**

```csharp
// tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/DeviceRegistrationTests.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Tests.Domain.Channels.Push;

public class DeviceRegistrationCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsDeviceRegistration()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();

        DeviceRegistration device = DeviceRegistration.Create(
            tenantId, userId, PushPlatform.Fcm, "fcm-token-123", "John's Phone", TimeProvider.System);

        device.TenantId.Should().Be(tenantId);
        device.UserId.Should().Be(userId);
        device.Platform.Should().Be(PushPlatform.Fcm);
        device.DeviceToken.Should().Be("fcm-token-123");
        device.DeviceName.Should().Be("John's Phone");
        device.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullDeviceName_Succeeds()
    {
        DeviceRegistration device = DeviceRegistration.Create(
            TenantId.New(), Guid.NewGuid(), PushPlatform.WebPush, "web-push-token", null, TimeProvider.System);

        device.DeviceName.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyToken_ThrowsArgumentException()
    {
        Action act = () => DeviceRegistration.Create(
            TenantId.New(), Guid.NewGuid(), PushPlatform.Fcm, "", null, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}

public class DeviceRegistrationUpdateTests
{
    [Fact]
    public void UpdateToken_ChangesTokenAndBumpsLastUsedAt()
    {
        DeviceRegistration device = CreateTestDevice();
        DateTime originalLastUsed = device.LastUsedAt;

        device.UpdateToken("new-token-456", TimeProvider.System);

        device.DeviceToken.Should().Be("new-token-456");
        device.LastUsedAt.Should().BeOnOrAfter(originalLastUsed);
    }

    [Fact]
    public void UpdateLastUsed_BumpsTimestamp()
    {
        DeviceRegistration device = CreateTestDevice();
        DateTime originalLastUsed = device.LastUsedAt;

        device.UpdateLastUsed(TimeProvider.System);

        device.LastUsedAt.Should().BeOnOrAfter(originalLastUsed);
    }

    private static DeviceRegistration CreateTestDevice() =>
        DeviceRegistration.Create(TenantId.New(), Guid.NewGuid(), PushPlatform.Fcm, "token-123", "Test Phone", TimeProvider.System);
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests --filter "FullyQualifiedName~DeviceRegistration" --no-restore
```

Expected: Compilation error — `DeviceRegistration` does not exist.

**Step 3: Implement DeviceRegistration entity**

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/DeviceRegistration.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Communications.Domain.Channels.Push.Entities;

public sealed class DeviceRegistration : AggregateRoot<DeviceRegistrationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public PushPlatform Platform { get; private set; }
    public string DeviceToken { get; private set; } = null!;
    public string? DeviceName { get; private set; }
    public DateTime LastUsedAt { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private DeviceRegistration() { } // EF Core

    private DeviceRegistration(
        TenantId tenantId,
        Guid userId,
        PushPlatform platform,
        string deviceToken,
        string? deviceName,
        TimeProvider timeProvider)
        : base(DeviceRegistrationId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        Platform = platform;
        DeviceToken = deviceToken;
        DeviceName = deviceName;
        LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static DeviceRegistration Create(
        TenantId tenantId,
        Guid userId,
        PushPlatform platform,
        string deviceToken,
        string? deviceName,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceToken);
        return new DeviceRegistration(tenantId, userId, platform, deviceToken, deviceName, timeProvider);
    }

    public void UpdateToken(string newToken, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newToken);
        DeviceToken = newToken;
        LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void UpdateLastUsed(TimeProvider timeProvider)
    {
        LastUsedAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests --filter "FullyQualifiedName~DeviceRegistration" --no-restore
```

**Step 5: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/DeviceRegistration.cs tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/DeviceRegistrationTests.cs
git commit -m "feat(communications): add DeviceRegistration entity with tests"
```

---

### Task 1.4: PushMessage Entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/PushMessage.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/PushMessageTests.cs`

**Step 1: Write tests**

```csharp
// tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/PushMessageTests.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Communications.Domain.Channels.Push.Events;
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Tests.Domain.Channels.Push;

public class PushMessageCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsPushMessageInPendingStatus()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        DeviceRegistrationId deviceId = DeviceRegistrationId.New();

        PushMessage message = PushMessage.Create(
            tenantId, userId, deviceId, PushPlatform.Fcm, "Title", "Body", null, TimeProvider.System);

        message.TenantId.Should().Be(tenantId);
        message.UserId.Should().Be(userId);
        message.DeviceRegistrationId.Should().Be(deviceId);
        message.Platform.Should().Be(PushPlatform.Fcm);
        message.Title.Should().Be("Title");
        message.Body.Should().Be("Body");
        message.Data.Should().BeNull();
        message.Status.Should().Be(PushStatus.Pending);
        message.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithData_StoresJsonPayload()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), Guid.NewGuid(), DeviceRegistrationId.New(),
            PushPlatform.Apns, "Title", "Body", "{\"key\":\"value\"}", TimeProvider.System);

        message.Data.Should().Be("{\"key\":\"value\"}");
    }
}

public class PushMessageSendingTests
{
    [Fact]
    public void MarkAsSent_ChangesStatusToSent()
    {
        PushMessage message = CreateTestMessage();

        message.MarkAsSent(TimeProvider.System);

        message.Status.Should().Be(PushStatus.Sent);
        message.SentAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsSent_RaisesPushSentDomainEvent()
    {
        PushMessage message = CreateTestMessage();

        message.MarkAsSent(TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushSentDomainEvent>()
            .Which.MessageId.Should().Be(message.Id);
    }

    [Fact]
    public void MarkAsFailed_ChangesStatusToFailedAndIncrementsRetryCount()
    {
        PushMessage message = CreateTestMessage();

        message.MarkAsFailed("Provider timeout", TimeProvider.System);

        message.Status.Should().Be(PushStatus.Failed);
        message.FailureReason.Should().Be("Provider timeout");
        message.RetryCount.Should().Be(1);
    }

    [Fact]
    public void MarkAsFailed_RaisesPushFailedDomainEvent()
    {
        PushMessage message = CreateTestMessage();

        message.MarkAsFailed("Provider timeout", TimeProvider.System);

        message.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PushFailedDomainEvent>()
            .Which.Reason.Should().Be("Provider timeout");
    }

    private static PushMessage CreateTestMessage() =>
        PushMessage.Create(TenantId.New(), Guid.NewGuid(), DeviceRegistrationId.New(),
            PushPlatform.Fcm, "Test Title", "Test Body", null, TimeProvider.System);
}

public class PushMessageRetryTests
{
    [Fact]
    public void ResetForRetry_ChangesStatusToPending()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), Guid.NewGuid(), DeviceRegistrationId.New(),
            PushPlatform.Fcm, "Title", "Body", null, TimeProvider.System);
        message.MarkAsFailed("Error", TimeProvider.System);

        message.ResetForRetry(TimeProvider.System);

        message.Status.Should().Be(PushStatus.Pending);
    }

    [Fact]
    public void CanRetry_WithinLimit_ReturnsTrue()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), Guid.NewGuid(), DeviceRegistrationId.New(),
            PushPlatform.Fcm, "Title", "Body", null, TimeProvider.System);
        message.MarkAsFailed("Error", TimeProvider.System);

        message.CanRetry().Should().BeTrue();
    }

    [Fact]
    public void CanRetry_AtLimit_ReturnsFalse()
    {
        PushMessage message = PushMessage.Create(
            TenantId.New(), Guid.NewGuid(), DeviceRegistrationId.New(),
            PushPlatform.Fcm, "Title", "Body", null, TimeProvider.System);
        message.MarkAsFailed("Error 1", TimeProvider.System);
        message.MarkAsFailed("Error 2", TimeProvider.System);
        message.MarkAsFailed("Error 3", TimeProvider.System);

        message.CanRetry().Should().BeFalse();
    }
}
```

**Step 2: Implement PushMessage entity**

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/PushMessage.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Communications.Domain.Channels.Push.Events;
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Communications.Domain.Channels.Push.Entities;

public sealed class PushMessage : AggregateRoot<PushMessageId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public DeviceRegistrationId DeviceRegistrationId { get; private set; }
    public PushPlatform Platform { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? Data { get; private set; }
    public PushStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public int RetryCount { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private PushMessage() { } // EF Core

    private PushMessage(
        TenantId tenantId,
        Guid userId,
        DeviceRegistrationId deviceRegistrationId,
        PushPlatform platform,
        string title,
        string body,
        string? data,
        TimeProvider timeProvider)
        : base(PushMessageId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        DeviceRegistrationId = deviceRegistrationId;
        Platform = platform;
        Title = title;
        Body = body;
        Data = data;
        Status = PushStatus.Pending;
        RetryCount = 0;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static PushMessage Create(
        TenantId tenantId,
        Guid userId,
        DeviceRegistrationId deviceRegistrationId,
        PushPlatform platform,
        string title,
        string body,
        string? data,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        return new PushMessage(tenantId, userId, deviceRegistrationId, platform, title, body, data, timeProvider);
    }

    public void MarkAsSent(TimeProvider timeProvider)
    {
        Status = PushStatus.Sent;
        SentAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow());
        RaiseDomainEvent(new PushSentDomainEvent(Id));
    }

    public void MarkAsFailed(string reason, TimeProvider timeProvider)
    {
        Status = PushStatus.Failed;
        FailureReason = reason;
        RetryCount++;
        SetUpdated(timeProvider.GetUtcNow());
        RaiseDomainEvent(new PushFailedDomainEvent(Id, reason));
    }

    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = PushStatus.Pending;
        FailureReason = null;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public bool CanRetry(int maxRetries = 3) => RetryCount < maxRetries;
}
```

**Step 3: Run tests**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests --filter "FullyQualifiedName~PushMessage" --no-restore
```

**Step 4: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/PushMessage.cs tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/PushMessageTests.cs
git commit -m "feat(communications): add PushMessage entity with tests"
```

---

### Task 1.5: TenantPushConfiguration Entity

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/TenantPushConfiguration.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/TenantPushConfigurationTests.cs`

**Step 1: Write tests**

```csharp
// tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/TenantPushConfigurationTests.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Tests.Domain.Channels.Push;

public class TenantPushConfigurationTests
{
    [Fact]
    public void Create_WithValidData_ReturnsEnabledConfiguration()
    {
        TenantId tenantId = TenantId.New();
        string encrypted = "encrypted-credentials-blob";

        TenantPushConfiguration config = TenantPushConfiguration.Create(
            tenantId, PushPlatform.Fcm, encrypted, TimeProvider.System);

        config.TenantId.Should().Be(tenantId);
        config.Platform.Should().Be(PushPlatform.Fcm);
        config.EncryptedCredentials.Should().Be(encrypted);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Apns, "creds", TimeProvider.System);

        config.Disable(TimeProvider.System);

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_SetsIsEnabledToTrue()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.Apns, "creds", TimeProvider.System);
        config.Disable(TimeProvider.System);

        config.Enable(TimeProvider.System);

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateCredentials_ChangesEncryptedCredentials()
    {
        TenantPushConfiguration config = TenantPushConfiguration.Create(
            TenantId.New(), PushPlatform.WebPush, "old-creds", TimeProvider.System);

        config.UpdateCredentials("new-creds", TimeProvider.System);

        config.EncryptedCredentials.Should().Be("new-creds");
    }
}
```

**Step 2: Implement TenantPushConfiguration entity**

```csharp
// src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/TenantPushConfiguration.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Communications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Communications.Domain.Channels.Push.Entities;

public sealed class TenantPushConfiguration : AggregateRoot<TenantPushConfigurationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public PushPlatform Platform { get; private set; }
    public string EncryptedCredentials { get; private set; } = null!;
    public bool IsEnabled { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private TenantPushConfiguration() { } // EF Core

    private TenantPushConfiguration(
        TenantId tenantId,
        PushPlatform platform,
        string encryptedCredentials,
        TimeProvider timeProvider)
        : base(TenantPushConfigurationId.New())
    {
        TenantId = tenantId;
        Platform = platform;
        EncryptedCredentials = encryptedCredentials;
        IsEnabled = true;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static TenantPushConfiguration Create(
        TenantId tenantId,
        PushPlatform platform,
        string encryptedCredentials,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        return new TenantPushConfiguration(tenantId, platform, encryptedCredentials, timeProvider);
    }

    public void UpdateCredentials(string encryptedCredentials, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedCredentials);
        EncryptedCredentials = encryptedCredentials;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Enable(TimeProvider timeProvider)
    {
        IsEnabled = true;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Disable(TimeProvider timeProvider)
    {
        IsEnabled = false;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
```

**Step 3: Run tests**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests --filter "FullyQualifiedName~TenantPushConfiguration" --no-restore
```

**Step 4: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Domain/Channels/Push/Entities/TenantPushConfiguration.cs tests/Modules/Communications/Wallow.Communications.Tests/Domain/Channels/Push/TenantPushConfigurationTests.cs
git commit -m "feat(communications): add TenantPushConfiguration entity with tests"
```

---

## Phase 2: Push Application Layer

Interfaces, commands, queries, handlers, validators, and the shared preference checker.

### Task 2.1: Push Interfaces and DTOs

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushProviderFactory.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushMessageRepository.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IDeviceRegistrationRepository.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/ITenantPushConfigurationRepository.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/DTOs/DeviceRegistrationDto.cs`

**Step 1: Create IPushProvider interface**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushProvider.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using JetBrains.Annotations;

namespace Wallow.Communications.Application.Channels.Push.Interfaces;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
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
    Task<PushDeliveryResult> SendAsync(PushDeliveryRequest request, CancellationToken cancellationToken = default);
}
```

**Step 2: Create IPushProviderFactory**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushProviderFactory.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Application.Channels.Push.Interfaces;

public interface IPushProviderFactory
{
    Task<IPushProvider> GetProviderAsync(TenantId tenantId, PushPlatform platform, CancellationToken cancellationToken = default);
}
```

**Step 3: Create repository interfaces**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushMessageRepository.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Identity;

namespace Wallow.Communications.Application.Channels.Push.Interfaces;

public interface IPushMessageRepository
{
    void Add(PushMessage message);
    Task<PushMessage?> GetByIdAsync(PushMessageId id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IDeviceRegistrationRepository.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Communications.Domain.Channels.Push.Identity;

namespace Wallow.Communications.Application.Channels.Push.Interfaces;

public interface IDeviceRegistrationRepository
{
    void Add(DeviceRegistration device);
    void Remove(DeviceRegistration device);
    Task<DeviceRegistration?> GetByIdAsync(DeviceRegistrationId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceRegistration>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DeviceRegistration?> GetByUserAndTokenAsync(Guid userId, PushPlatform platform, string deviceToken, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/ITenantPushConfigurationRepository.cs
using Wallow.Communications.Domain.Channels.Push.Entities;
using Wallow.Communications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Application.Channels.Push.Interfaces;

public interface ITenantPushConfigurationRepository
{
    void Add(TenantPushConfiguration config);
    void Remove(TenantPushConfiguration config);
    Task<TenantPushConfiguration?> GetByTenantAndPlatformAsync(TenantId tenantId, PushPlatform platform, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantPushConfiguration>> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Step 4: Create DeviceRegistrationDto**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Channels/Push/DTOs/DeviceRegistrationDto.cs
using Wallow.Communications.Domain.Channels.Push.Enums;
using JetBrains.Annotations;

namespace Wallow.Communications.Application.Channels.Push.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DeviceRegistrationDto(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string DeviceToken,
    string? DeviceName,
    DateTime LastUsedAt,
    DateTime CreatedAt);
```

**Step 5: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Application/Channels/Push/
git commit -m "feat(communications): add push interfaces and DTOs"
```

---

### Task 2.2: INotificationPreferenceChecker Service

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/Interfaces/INotificationPreferenceChecker.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/Services/NotificationPreferenceChecker.cs`
- Modify: `src/Modules/Communications/Wallow.Communications.Application/Preferences/Interfaces/IChannelPreferenceRepository.cs` — add `GetGlobalPreferenceAsync`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Preferences/NotificationPreferenceCheckerTests.cs`

**Step 1: Add repository method to IChannelPreferenceRepository**

Add to the existing interface:

```csharp
Task<ChannelPreference?> GetGlobalPreferenceAsync(Guid userId, ChannelType channelType, CancellationToken cancellationToken = default);
```

This fetches the `NotificationType = "*"` record.

**Step 2: Create INotificationPreferenceChecker**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Preferences/Interfaces/INotificationPreferenceChecker.cs
using Wallow.Communications.Domain.Preferences;

namespace Wallow.Communications.Application.Preferences.Interfaces;

public interface INotificationPreferenceChecker
{
    Task<bool> IsChannelEnabledForUserAsync(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        CancellationToken cancellationToken = default);
}
```

**Step 3: Implement NotificationPreferenceChecker**

```csharp
// src/Modules/Communications/Wallow.Communications.Application/Preferences/Services/NotificationPreferenceChecker.cs
using Wallow.Communications.Application.Preferences.Interfaces;
using Wallow.Communications.Domain.Preferences;
using Wallow.Communications.Domain.Preferences.Entities;

namespace Wallow.Communications.Application.Preferences.Services;

public sealed class NotificationPreferenceChecker(IChannelPreferenceRepository preferenceRepository) : INotificationPreferenceChecker
{
    public const string GlobalNotificationType = "*";

    public async Task<bool> IsChannelEnabledForUserAsync(
        Guid userId,
        ChannelType channelType,
        string notificationType,
        CancellationToken cancellationToken = default)
    {
        // Check global channel toggle first
        ChannelPreference? globalPref = await preferenceRepository.GetGlobalPreferenceAsync(
            userId, channelType, cancellationToken);

        if (globalPref is { IsEnabled: false })
        {
            return false;
        }

        // Check specific notification type
        ChannelPreference? typePref = await preferenceRepository.GetByUserChannelAndNotificationTypeAsync(
            userId, channelType, notificationType, cancellationToken);

        if (typePref is { IsEnabled: false })
        {
            return false;
        }

        // No preference = enabled by default (opt-out model)
        return true;
    }
}
```

**Step 4: Write tests**

```csharp
// tests/Modules/Communications/Wallow.Communications.Tests/Application/Preferences/NotificationPreferenceCheckerTests.cs
using Wallow.Communications.Application.Preferences.Interfaces;
using Wallow.Communications.Application.Preferences.Services;
using Wallow.Communications.Domain.Preferences;
using Wallow.Communications.Domain.Preferences.Entities;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Communications.Tests.Application.Preferences;

public class NotificationPreferenceCheckerTests
{
    private readonly IChannelPreferenceRepository _repo = Substitute.For<IChannelPreferenceRepository>();
    private readonly NotificationPreferenceChecker _checker;

    public NotificationPreferenceCheckerTests()
    {
        _checker = new NotificationPreferenceChecker(_repo);
    }

    [Fact]
    public async Task IsChannelEnabledForUserAsync_NoPreferences_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();

        bool result = await _checker.IsChannelEnabledForUserAsync(userId, ChannelType.Push, "InvoiceReady");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChannelEnabledForUserAsync_GlobalDisabled_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Push, "*", TimeProvider.System, false);
        _repo.GetGlobalPreferenceAsync(userId, ChannelType.Push, Arg.Any<CancellationToken>())
            .Returns(globalPref);

        bool result = await _checker.IsChannelEnabledForUserAsync(userId, ChannelType.Push, "InvoiceReady");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledForUserAsync_GlobalEnabled_SpecificDisabled_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Push, "*", TimeProvider.System, true);
        ChannelPreference typePref = ChannelPreference.Create(userId, ChannelType.Push, "InvoiceReady", TimeProvider.System, false);

        _repo.GetGlobalPreferenceAsync(userId, ChannelType.Push, Arg.Any<CancellationToken>())
            .Returns(globalPref);
        _repo.GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Push, "InvoiceReady", Arg.Any<CancellationToken>())
            .Returns(typePref);

        bool result = await _checker.IsChannelEnabledForUserAsync(userId, ChannelType.Push, "InvoiceReady");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChannelEnabledForUserAsync_GlobalEnabled_SpecificEnabled_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();
        ChannelPreference globalPref = ChannelPreference.Create(userId, ChannelType.Push, "*", TimeProvider.System, true);
        ChannelPreference typePref = ChannelPreference.Create(userId, ChannelType.Push, "InvoiceReady", TimeProvider.System, true);

        _repo.GetGlobalPreferenceAsync(userId, ChannelType.Push, Arg.Any<CancellationToken>())
            .Returns(globalPref);
        _repo.GetByUserChannelAndNotificationTypeAsync(userId, ChannelType.Push, "InvoiceReady", Arg.Any<CancellationToken>())
            .Returns(typePref);

        bool result = await _checker.IsChannelEnabledForUserAsync(userId, ChannelType.Push, "InvoiceReady");

        result.Should().BeTrue();
    }
}
```

**Step 5: Run tests**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests --filter "FullyQualifiedName~NotificationPreferenceChecker" --no-restore
```

**Step 6: Commit**

```bash
git add src/Modules/Communications/Wallow.Communications.Application/Preferences/ tests/Modules/Communications/Wallow.Communications.Tests/Application/Preferences/
git commit -m "feat(communications): add INotificationPreferenceChecker with global toggle support"
```

---

### Task 2.3: Device Registration Commands and Handlers

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceValidator.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/UpdateDevice/UpdateDeviceCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/UpdateDevice/UpdateDeviceHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RemoveDevice/RemoveDeviceCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RemoveDevice/RemoveDeviceHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Queries/GetUserDevices/GetUserDevicesQuery.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/RegisterDeviceHandlerTests.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/RegisterDeviceValidatorTests.cs`

Commands, handlers, validators, and query for device CRUD. Follow SendSms patterns exactly (Wolverine handler classes with primary constructors, FluentValidation).

**Commit message:** `feat(communications): add device registration commands and queries`

---

### Task 2.4: Push Notification Commands and Handlers

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/SendPushNotification/SendPushNotificationCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/SendPushNotification/SendPushNotificationHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/SendPushNotification/SendPushNotificationValidator.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/DeliverPush/DeliverPushCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/DeliverPush/DeliverPushHandler.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/SendPushNotificationHandlerTests.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/DeliverPushHandlerTests.cs`

**Key behavior of SendPushNotificationHandler:**
1. Check `INotificationPreferenceChecker.IsChannelEnabledForUserAsync(userId, ChannelType.Push, notificationType)`
2. If disabled, return success (silently skip)
3. Load all `DeviceRegistration` records for the user
4. For each device: create `PushMessage` (Pending), save, publish `DeliverPushCommand` to bus via `IMessageBus.PublishAsync`

**Key behavior of DeliverPushHandler:**
1. Load `PushMessage` by ID
2. Resolve provider via `IPushProviderFactory.GetProviderAsync(tenantId, platform)`
3. Call `provider.SendAsync()`
4. Update `PushMessage` to Sent or Failed
5. Save

**Commit message:** `feat(communications): add push notification send and deliver commands`

---

### Task 2.5: SendPushRequestedEvent in Shared.Contracts

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Communications/Push/Events/SendPushRequestedEvent.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/EventHandlers/SendPushRequestedEventHandler.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/SendPushRequestedEventHandlerTests.cs`

**SendPushRequestedEvent** — follows `SendSmsRequestedEvent` pattern:

```csharp
// src/Shared/Wallow.Shared.Contracts/Communications/Push/Events/SendPushRequestedEvent.cs
namespace Wallow.Shared.Contracts.Communications.Push.Events;

public sealed record SendPushRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? Data { get; init; }
    public string? NotificationType { get; init; }
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
}
```

**Handler** — follows `SendSmsRequestedEventHandler` pattern (static partial class with LoggerMessage).

**Commit message:** `feat(communications): add SendPushRequestedEvent and handler`

---

### Task 2.6: Tenant Push Configuration Commands

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/SetTenantPushConfiguration/SetTenantPushConfigurationCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/SetTenantPushConfiguration/SetTenantPushConfigurationHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RemoveTenantPushConfiguration/RemoveTenantPushConfigurationCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Commands/RemoveTenantPushConfiguration/RemoveTenantPushConfigurationHandler.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Queries/GetTenantPushConfiguration/GetTenantPushConfigurationQuery.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/DTOs/TenantPushConfigurationDto.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Channels/Push/Interfaces/IPushCredentialEncryptor.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Channels/Push/SetTenantPushConfigurationHandlerTests.cs`

**IPushCredentialEncryptor** — Application-layer interface, Infrastructure implements with Data Protection:

```csharp
public interface IPushCredentialEncryptor
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
```

**Commit message:** `feat(communications): add tenant push configuration commands`

---

## Phase 3: Push Infrastructure Layer

EF Core persistence, provider implementations, DI registration.

### Task 3.1: EF Core Configurations and Migration

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/DeviceRegistrationConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/PushMessageConfiguration.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Configurations/TenantPushConfigurationConfiguration.cs`
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/CommunicationsDbContext.cs` — add DbSets

Follow `SmsMessageConfiguration` and `ChannelPreferenceConfiguration` patterns exactly. Tables: `device_registrations`, `push_messages`, `tenant_push_configurations`. All in `communications` schema.

Key indexes:
- `device_registrations`: `(tenant_id, user_id)`, unique on `(tenant_id, user_id, platform, device_token)`
- `push_messages`: `(tenant_id)`, `(status)`, `(user_id)`
- `tenant_push_configurations`: unique on `(tenant_id, platform)`

Run migration:
```bash
dotnet ef migrations add AddPushNotificationTables \
    --project src/Modules/Communications/Wallow.Communications.Infrastructure \
    --startup-project src/Wallow.Api \
    --context CommunicationsDbContext
```

**Commit message:** `feat(communications): add push notification EF Core configurations and migration`

---

### Task 3.2: Push Repositories

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/PushMessageRepository.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/DeviceRegistrationRepository.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/TenantPushConfigurationRepository.cs`
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/ChannelPreferenceRepository.cs` — add `GetGlobalPreferenceAsync`

Follow existing repository patterns (inject `CommunicationsDbContext`, implement interface methods).

**Commit message:** `feat(communications): add push notification repositories`

---

### Task 3.3: Push Provider Implementations

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/LogPushProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/FcmPushProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/ApnsPushProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/WebPushProvider.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/PushProviderFactory.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/PushCredentialEncryptor.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/FcmSettings.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/ApnsSettings.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/Push/WebPushSettings.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/Services/Push/LogPushProviderTests.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/Services/Push/PushProviderFactoryTests.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Infrastructure/Services/Push/PushCredentialEncryptorTests.cs`

**LogPushProvider** — follows `NullSmsProvider` pattern with `ILogger<LogPushProvider>` and `[LoggerMessage]` source gen.

**PushProviderFactory** — implements `IPushProviderFactory`:
1. Check `ITenantPushConfigurationRepository` for tenant+platform
2. If found and enabled, decrypt credentials via `IPushCredentialEncryptor`, construct provider
3. If not found, check `IOptions<FcmSettings>`/`IOptions<ApnsSettings>`/`IOptions<WebPushSettings>` from config
4. If config not enabled, return `LogPushProvider`

**PushCredentialEncryptor** — wraps `IDataProtector` with purpose `"TenantPushCredentials"`.

**FcmPushProvider, ApnsPushProvider, WebPushProvider** — each uses `HttpClient` with `ILogger` and `[LoggerMessage]`. Initial implementations can be structured but call the real APIs. Follow `TwilioSmsProvider` pattern (partial class for log messages).

**Commit message:** `feat(communications): add push providers, factory, and credential encryptor`

---

### Task 3.4: DI Registration

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs` — add push services

Add to `AddCommunicationsServices`:
- Configure settings: `services.Configure<FcmSettings>(configuration.GetSection("Communications:Push:Fcm"))` etc.
- Register repositories: `IPushMessageRepository`, `IDeviceRegistrationRepository`, `ITenantPushConfigurationRepository`
- Register `IPushProviderFactory` as scoped
- Register `IPushCredentialEncryptor` as singleton
- Register `INotificationPreferenceChecker` as scoped
- Register `LogPushProvider` as singleton
- Register `HttpClient` for each provider with `AddWallowResilienceHandler`
- Add Data Protection: `services.AddDataProtection()`

Add to `AddCommunicationsPersistence`:
- Push repositories

**Commit message:** `feat(communications): register push notification services in DI`

---

### Task 3.5: appsettings.json Configuration

**Files:**
- Modify: `src/Wallow.Api/appsettings.json` — add `Communications:Push` section
- Modify: `src/Wallow.Api/appsettings.Development.json` — all providers disabled, LogPushProvider by default

**Commit message:** `chore(communications): add push notification configuration sections`

---

## Phase 4: Push API Layer

Controllers, request/response contracts, permissions.

### Task 4.1: Push API Contracts

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Push/Requests/RegisterDeviceRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Push/Requests/UpdateDeviceRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Push/Requests/SetPushConfigurationRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Push/Responses/DeviceRegistrationResponse.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Push/Responses/TenantPushConfigurationResponse.cs`

**Commit message:** `feat(communications): add push API contracts`

---

### Task 4.2: PushDevicesController

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Controllers/PushDevicesController.cs`
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` — add `PushDeviceManage`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Api/Controllers/PushDevicesControllerTests.cs`

Follow `EmailPreferencesController` pattern. Endpoints:
- `POST /api/v1/push/devices`
- `GET /api/v1/push/devices`
- `PUT /api/v1/push/devices/{id}`
- `DELETE /api/v1/push/devices/{id}`

All authorized, all use `ICurrentUserService` for user context.

**Commit message:** `feat(communications): add PushDevicesController`

---

### Task 4.3: PushConfigurationController (Admin)

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Controllers/PushConfigurationController.cs`
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` — add `PushConfigurationManage`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Api/Controllers/PushConfigurationControllerTests.cs`

Admin-only endpoints:
- `POST /api/v1/push/configuration`
- `GET /api/v1/push/configuration`
- `PUT /api/v1/push/configuration/{platform}`
- `DELETE /api/v1/push/configuration/{platform}`

Credentials in response are always redacted (return `"***"` instead of actual values).

**Commit message:** `feat(communications): add PushConfigurationController`

---

## Phase 5: User Notification Settings

Unified settings API replacing `EmailPreferencesController`.

### Task 5.1: User Notification Settings Commands and Queries

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/Commands/SetChannelEnabledCommand.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/Queries/GetUserNotificationSettingsQuery.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/DTOs/UserNotificationSettingsDto.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Application/Preferences/DTOs/ChannelSettingsDto.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Preferences/SetChannelEnabledHandlerTests.cs`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Application/Preferences/GetUserNotificationSettingsHandlerTests.cs`

**SetChannelEnabledCommand** — upserts the `NotificationType = "*"` record for a channel.

**GetUserNotificationSettingsQuery** — loads all preferences for user, groups by channel, includes global toggle status.

**Commit message:** `feat(communications): add unified notification settings commands and queries`

---

### Task 5.2: UserNotificationSettingsController

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Api/Controllers/UserNotificationSettingsController.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Preferences/Requests/SetChannelEnabledRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Preferences/Requests/SetNotificationTypePreferenceRequest.cs`
- Create: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Preferences/Responses/UserNotificationSettingsResponse.cs`
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` — add `NotificationSettingsManage`
- Test: `tests/Modules/Communications/Wallow.Communications.Tests/Api/Controllers/UserNotificationSettingsControllerTests.cs`

Endpoints:
- `GET /api/v1/notifications/settings`
- `PUT /api/v1/notifications/settings/channels/{channelType}`
- `PUT /api/v1/notifications/settings/channels/{channelType}/{notificationType}`

**Commit message:** `feat(communications): add UserNotificationSettingsController`

---

### Task 5.3: Remove EmailPreferencesController

**Files:**
- Delete: `src/Modules/Communications/Wallow.Communications.Api/Controllers/EmailPreferencesController.cs`
- Delete: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Email/Enums/ApiNotificationType.cs`
- Delete: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Email/Requests/UpdateEmailPreferenceRequest.cs`
- Delete: `src/Modules/Communications/Wallow.Communications.Api/Contracts/Email/Responses/EmailPreferenceResponse.cs`
- Delete: `src/Modules/Communications/Wallow.Communications.Api/Mappings/EnumMappings.cs`
- Delete: `tests/Modules/Communications/Wallow.Communications.Tests/Api/Controllers/EmailPreferencesControllerTests.cs`
- Delete: `tests/Modules/Communications/Wallow.Communications.Tests/Api/Mappings/EnumMappingsTests.cs`
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` — remove `EmailPreferenceManage` if no longer referenced

Check for any references to removed types before deleting.

**Commit message:** `refactor(communications): remove EmailPreferencesController in favor of unified settings`

---

## Phase 6: Integration and Verification

### Task 6.1: Wire Existing Handlers to Preference Checker

**Files:**
- Modify: `src/Modules/Communications/Wallow.Communications.Application/Channels/Email/Commands/SendEmail/SendEmailHandler.cs` — add preference check
- Modify: `src/Modules/Communications/Wallow.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsHandler.cs` — add preference check

Add `INotificationPreferenceChecker` to handler constructors. Check before sending. If disabled, return success silently.

**Commit message:** `feat(communications): integrate preference checker into email and SMS handlers`

---

### Task 6.2: Build Verification and Final Tests

**Step 1: Build the solution**

```bash
dotnet build
```

Fix any compilation errors.

**Step 2: Run all Communications tests**

```bash
dotnet test tests/Modules/Communications/Wallow.Communications.Tests
```

**Step 3: Run architecture tests**

```bash
dotnet test tests/Wallow.Architecture.Tests
```

Fix any architecture violations (module isolation, naming conventions, etc.).

**Step 4: Final commit**

```bash
git add -A
git commit -m "test(communications): verify push notifications build and pass all tests"
```
