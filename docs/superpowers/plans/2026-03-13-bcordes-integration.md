# bcordes.dev Foundry Integration - Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add scopes/permissions, overhaul inquiry submission for service accounts, add user inquiry views, SignalR real-time events, and inquiry comments to support the bcordes.dev frontend integration.

**Architecture:** Modular monolith with Clean Architecture per module. Changes span Shared.Kernel (permissions), Identity module (scopes/roles), Inquiries module (domain/application/infrastructure/API), Notifications module (SignalR handlers), and Shared.Contracts (integration events). All using Wolverine in-memory bus, EF Core, PostgreSQL.

**Tech Stack:** .NET 10, Wolverine, EF Core, PostgreSQL, SignalR, FluentValidation, NSubstitute for tests

**Spec:** `docs/superpowers/specs/2026-03-13-bcordes-integration-design.md`

---

## Chunk 1: Scopes, Permissions & Submission Overhaul (Required)

### Task 1: Add InquiriesRead and InquiriesWrite to PermissionType

**Files:**
- Modify: `src/Shared/Foundry.Shared.Kernel/Identity/Authorization/PermissionType.cs`

- [ ] **Step 1: Add permission constants**

After the Showcases section (line 87), add:

```csharp
// Inquiries
public const string InquiriesRead = "InquiriesRead";
public const string InquiriesWrite = "InquiriesWrite";
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Shared/Foundry.Shared.Kernel`

- [ ] **Step 3: Commit**

```bash
git add src/Shared/Foundry.Shared.Kernel/Identity/Authorization/PermissionType.cs
git commit -m "feat(shared): add InquiriesRead and InquiriesWrite permission types"
```

---

### Task 2: Add scopes to ApiScopes.ValidScopes

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Application/Constants/ApiScopes.cs`

- [ ] **Step 1: Add 3 new scopes to ValidScopes**

Add before the closing `};` on line 18:

```csharp
        "showcases.read",
        "inquiries.read",
        "inquiries.write"
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Modules/Identity/Foundry.Identity.Application`

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Application/Constants/ApiScopes.cs
git commit -m "feat(identity): add showcases.read, inquiries.read, inquiries.write to ValidScopes"
```

---

### Task 3: Add scope-to-permission mappings in PermissionExpansionMiddleware

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs`

- [ ] **Step 1: Add inquiries scope mappings**

In the `MapScopeToPermission` method, after the showcases entries, add:

```csharp
"inquiries.read" => PermissionType.InquiriesRead,
"inquiries.write" => PermissionType.InquiriesWrite,
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Modules/Identity/Foundry.Identity.Infrastructure`

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs
git commit -m "feat(identity): map inquiries scopes to permissions in middleware"
```

---

### Task 4: Add permissions to RolePermissionMapping

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/RolePermissionMapping.cs`

- [ ] **Step 1: Add InquiriesRead and InquiriesWrite to admin role**

In the `admin` array (after line 62, after `PushConfigWrite`), add:

```csharp
            PermissionType.InquiriesRead,
            PermissionType.InquiriesWrite,
```

- [ ] **Step 2: Add InquiriesRead to manager role**

In the `manager` array (after line 78, after `ShowcasesManage`), add:

```csharp
            PermissionType.InquiriesRead,
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/Identity/Foundry.Identity.Infrastructure`

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/RolePermissionMapping.cs
git commit -m "feat(identity): add inquiry permissions to admin and manager roles"
```

---

### Task 5: Add scopes to ApiScopeSeeder

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Data/ApiScopeSeeder.cs`

- [ ] **Step 1: Add 3 new scopes to GetDefaultScopes**

After the Platform scopes section (after line 75), add:

```csharp
        // Showcases scopes
        yield return ApiScope.Create("showcases.read", "Read Showcases", "Showcases",
            "Access to read showcase data", isDefault: true);

        // Inquiries scopes
        yield return ApiScope.Create("inquiries.read", "Read Inquiries", "Inquiries",
            "Access to read inquiry data");
        yield return ApiScope.Create("inquiries.write", "Submit Inquiries", "Inquiries",
            "Access to submit and manage inquiries");
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Modules/Identity/Foundry.Identity.Infrastructure`

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Identity/Foundry.Identity.Infrastructure/Data/ApiScopeSeeder.cs
git commit -m "feat(identity): seed showcases.read and inquiries scopes"
```

---

### Task 6: Update ApiScopeSeederGapTests

**Files:**
- Modify: `tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/ApiScopeSeederGapTests.cs`

- [ ] **Step 1: Update all count assertions from 11 to 14**

Update these lines:
- Line 41: `count.Should().Be(11)` → `count.Should().Be(14)`
- Line 58: `totalCount.Should().Be(11)` → `totalCount.Should().Be(14)`
- Line 82: `totalCount.Should().Be(11)` → `totalCount.Should().Be(14)`
- Line 166: `count.Should().Be(11)` → `count.Should().Be(14)`

- [ ] **Step 2: Update expected scope codes list (lines 95-107)**

Replace the collection with:

```csharp
        codes.Should().BeEquivalentTo([
            "invoices.read",
            "invoices.write",
            "payments.read",
            "payments.write",
            "subscriptions.read",
            "subscriptions.write",
            "users.read",
            "users.write",
            "notifications.read",
            "notifications.write",
            "webhooks.manage",
            "showcases.read",
            "inquiries.read",
            "inquiries.write"
        ]);
```

- [ ] **Step 3: Update expected categories (line 121)**

```csharp
        categories.Should().BeEquivalentTo(["Billing", "Identity", "Notifications", "Platform", "Showcases", "Inquiries"]);
```

- [ ] **Step 4: Rename test method**

Rename `SeedAsync_WhenEmpty_SeedsExactlyElevenScopes` to `SeedAsync_WhenEmpty_SeedsExactlyFourteenScopes`.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Modules/Identity/Foundry.Identity.Tests --filter "ApiScopeSeederGapTests"`
Expected: All tests pass

- [ ] **Step 6: Commit**

```bash
git add tests/Modules/Identity/Foundry.Identity.Tests/Infrastructure/ApiScopeSeederGapTests.cs
git commit -m "test(identity): update scope seeder tests for 14 scopes"
```

---

### Task 7: Update Inquiry domain entity with Phone and SubmitterId

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Domain/Entities/Inquiry.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Domain/Events/InquirySubmittedDomainEvent.cs`

- [ ] **Step 1: Add Phone and SubmitterId properties to Inquiry**

After `SubmitterIpAddress` (line 19), add:

```csharp
    public string? Phone { get; private set; }
    public string? SubmitterId { get; private set; }
```

- [ ] **Step 2: Update Create factory method**

Replace the `Create` method signature (lines 23-32) to add `phone` and `submitterId` parameters:

```csharp
    public static Inquiry Create(
        string name,
        string email,
        string? company,
        string projectType,
        string budgetRange,
        string timeline,
        string message,
        string submitterIpAddress,
        string? phone,
        string? submitterId,
        TimeProvider timeProvider)
```

Add inside the initializer (after line 44):

```csharp
            Phone = phone,
            SubmitterId = submitterId,
```

- [ ] **Step 3: Update InquirySubmittedDomainEvent to include Phone**

In `InquirySubmittedDomainEvent.cs`, add `string? Phone` parameter after `Message`:

```csharp
public sealed record InquirySubmittedDomainEvent(
    Guid InquiryId,
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string? Phone) : DomainEvent;
```

- [ ] **Step 4: Update RaiseDomainEvent call in Create**

Add `phone` to the domain event construction (after `message` on line 58):

```csharp
        inquiry.RaiseDomainEvent(new InquirySubmittedDomainEvent(
            inquiry.Id.Value,
            name,
            email,
            company,
            projectType,
            budgetRange,
            timeline,
            message,
            phone));
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Domain`

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Domain/
git commit -m "feat(inquiries): add Phone and SubmitterId to Inquiry entity"
```

---

### Task 8: Update SubmitInquiryCommand, handler, validator, and DTO

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryCommand.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryHandler.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryValidator.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/DTOs/InquiryDto.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Mappings/InquiryMappings.cs`

- [ ] **Step 1: Update SubmitInquiryCommand**

Replace entire file:

```csharp
namespace Foundry.Inquiries.Application.Commands.SubmitInquiry;

public sealed record SubmitInquiryCommand(
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string SubmitterIpAddress,
    string? Phone,
    string? SubmitterId);
```

- [ ] **Step 2: Update SubmitInquiryHandler**

Replace entire file - remove honeypot check and rate limiting:

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Commands.SubmitInquiry;

public static class SubmitInquiryHandler
{
    public static async Task<Result<InquiryDto>> HandleAsync(
        SubmitInquiryCommand command,
        IInquiryRepository inquiryRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Inquiry inquiry = Inquiry.Create(
            command.Name,
            command.Email,
            command.Company,
            command.ProjectType,
            command.BudgetRange,
            command.Timeline,
            command.Message,
            command.SubmitterIpAddress,
            command.Phone,
            command.SubmitterId,
            timeProvider);

        await inquiryRepository.AddAsync(inquiry, cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
```

- [ ] **Step 3: Update SubmitInquiryValidator**

Add Phone validation after the Message rule (after line 32):

```csharp
        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters")
            .When(x => x.Phone is not null);
```

- [ ] **Step 4: Update InquiryDto**

Replace entire file - add Phone and SubmitterId:

```csharp
namespace Foundry.Inquiries.Application.DTOs;

public sealed record InquiryDto(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    string SubmitterIpAddress,
    string? Phone,
    string? SubmitterId,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 5: Update InquiryMappings**

Replace entire file - map new fields:

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Domain.Entities;

namespace Foundry.Inquiries.Application.Mappings;

public static class InquiryMappings
{
    public static InquiryDto ToDto(this Inquiry inquiry)
    {
        return new InquiryDto(
            Id: inquiry.Id.Value,
            Name: inquiry.Name,
            Email: inquiry.Email,
            Company: inquiry.Company,
            ProjectType: inquiry.ProjectType,
            BudgetRange: inquiry.BudgetRange,
            Timeline: inquiry.Timeline,
            Message: inquiry.Message,
            Status: inquiry.Status.ToString(),
            SubmitterIpAddress: inquiry.SubmitterIpAddress,
            Phone: inquiry.Phone,
            SubmitterId: inquiry.SubmitterId,
            CreatedAt: inquiry.CreatedAt);
    }
}
```

- [ ] **Step 6: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Application`

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Application/
git commit -m "feat(inquiries): update command, handler, validator, DTO for new fields"
```

---

### Task 9: Update API contracts and controller

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Api/Contracts/SubmitInquiryRequest.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Api/Contracts/InquiryResponse.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Api/Controllers/InquiriesController.cs`

- [ ] **Step 1: Update SubmitInquiryRequest**

Replace entire file:

```csharp
namespace Foundry.Inquiries.Api.Contracts;

public sealed record SubmitInquiryRequest(
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
```

- [ ] **Step 2: Update InquiryResponse**

Replace entire file:

```csharp
namespace Foundry.Inquiries.Api.Contracts;

public sealed record InquiryResponse(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    string? SubmitterId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

- [ ] **Step 3: Update InquiriesController**

Replace the entire controller. Key changes:
- Remove `[AllowAnonymous]` from Submit, add `[HasPermission(PermissionType.InquiriesWrite)]`
- Map request fields directly (no hardcoded empty strings)
- Extract SubmitterId from JWT (null for service accounts)
- Add `[HasPermission(PermissionType.InquiriesRead)]` to GetAll
- Update `ToInquiryResponse` to map all fields

```csharp
using Asp.Versioning;
using Foundry.Inquiries.Api.Contracts;
using Foundry.Inquiries.Application.Commands.SubmitInquiry;
using Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Queries.GetInquiries;
using Foundry.Inquiries.Application.Queries.GetInquiryById;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Inquiries.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inquiries")]
[Authorize]
[Tags("Inquiries")]
[Produces("application/json")]
[Consumes("application/json")]
public class InquiriesController(IMessageBus bus) : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitInquiryRequest request,
        CancellationToken cancellationToken)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Service accounts (azp starts with "sa-") have no user identity
        string? submitterId = null;
        string? azp = User.FindFirst("azp")?.Value;
        if (azp is null || !azp.StartsWith("sa-", StringComparison.Ordinal))
        {
            submitterId = User.FindFirst("sub")?.Value;
        }

        SubmitInquiryCommand command = new(
            request.Name,
            request.Email,
            request.Company,
            request.ProjectType,
            request.BudgetRange,
            request.Timeline,
            request.Message,
            ipAddress,
            request.Phone,
            submitterId);

        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpGet]
    [HasPermission(PermissionType.InquiriesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        InquiryStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InquiryStatus>(status, ignoreCase: true, out InquiryStatus parsed))
        {
            parsedStatus = parsed;
        }

        Result<IReadOnlyList<InquiryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetInquiriesQuery(parsedStatus), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpPut("{id:guid}/status")]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInquiryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InquiryStatus>(request.NewStatus, ignoreCase: true, out InquiryStatus newStatus))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = $"Invalid status value: '{request.NewStatus}'"
            });
        }

        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new UpdateInquiryStatusCommand(id, newStatus), cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    private static InquiryResponse ToInquiryResponse(InquiryDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Email,
        dto.Company,
        dto.Phone,
        dto.ProjectType,
        dto.BudgetRange,
        dto.Timeline,
        dto.Message,
        dto.Status,
        dto.SubmitterId,
        dto.CreatedAt.UtcDateTime,
        dto.CreatedAt.UtcDateTime);
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Api`

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Api/
git commit -m "feat(inquiries): update API contracts and controller for service account auth"
```

---

### Task 10: Update EF Core configuration and add Phone/SubmitterId columns

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Configurations/InquiryConfiguration.cs`

- [ ] **Step 1: Add Phone and SubmitterId column configs**

After `SubmitterIpAddress` config (after line 62), add:

```csharp
        builder.Property(i => i.Phone)
            .HasColumnName("phone")
            .HasMaxLength(20);

        builder.Property(i => i.SubmitterId)
            .HasColumnName("submitter_id")
            .HasMaxLength(128);
```

After the email index (line 81), add:

```csharp
        builder.HasIndex(i => i.SubmitterId);
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Infrastructure`

- [ ] **Step 3: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Configurations/InquiryConfiguration.cs
git commit -m "feat(inquiries): add Phone and SubmitterId to EF configuration"
```

---

### Task 11: Update InquirySubmittedEvent and notification handler

**Files:**
- Modify: `src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquirySubmittedEvent.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/EventHandlers/InquirySubmittedDomainEventHandler.cs`
- Modify: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquirySubmittedNotificationHandler.cs`

- [ ] **Step 1: Update InquirySubmittedEvent - rename Subject to ProjectType, add Phone**

Replace entire file:

```csharp
namespace Foundry.Shared.Contracts.Inquiries.Events;

public sealed record InquirySubmittedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Company { get; init; }
    public string? Phone { get; init; }
    public required string ProjectType { get; init; }
    public required string Message { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public required string AdminEmail { get; init; }
}
```

- [ ] **Step 2: Update domain event handler mapping**

In `InquirySubmittedDomainEventHandler.cs`, update the PublishAsync call (lines 24-34):

```csharp
        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquirySubmittedEvent
        {
            InquiryId = domainEvent.InquiryId,
            Name = domainEvent.Name,
            Email = domainEvent.Email,
            Company = domainEvent.Company,
            Phone = domainEvent.Phone,
            ProjectType = domainEvent.ProjectType,
            Message = domainEvent.Message,
            SubmittedAt = inquiry?.CreatedAt ?? DateTime.UtcNow,
            AdminEmail = adminEmail
        });
```

- [ ] **Step 3: Update InquirySubmittedNotificationHandler**

Replace `message.Subject` with `message.ProjectType`:

```csharp
        SendEmailCommand emailCommand = new(
            To: message.AdminEmail,
            From: null,
            Subject: $"New Inquiry: {message.ProjectType}",
            Body: $"New inquiry from {message.Name} ({message.Email}): {message.Message}");
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquirySubmittedEvent.cs
git add src/Modules/Inquiries/Foundry.Inquiries.Application/EventHandlers/InquirySubmittedDomainEventHandler.cs
git add src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquirySubmittedNotificationHandler.cs
git commit -m "feat(inquiries): rename Subject to ProjectType in integration event"
```

---

### Task 12: Update existing tests for Chunk 1 changes

**Files:**
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Domain/Entities/InquiryCreateTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryHandlerTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryValidatorTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryBoundaryTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/EventHandlers/InquirySubmittedDomainEventHandlerTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Mappings/InquiryMappingsTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Domain/Entities/InquiryDomainEventTests.cs`
- Modify: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Api/Controllers/InquiriesControllerTests.cs`
- Modify: `tests/Modules/Notifications/Foundry.Notifications.Tests/EventHandlers/InquirySubmittedNotificationHandlerTests.cs`

This task requires reading each test file, understanding what it tests, and updating for the new signatures. Key changes:

- [ ] **Step 1: Read all test files listed above**

Read each file to understand current test structure.

- [ ] **Step 2: Update Inquiry.Create() calls**

All tests calling `Inquiry.Create()` need `phone` and `submitterId` parameters added. Add `phone: null, submitterId: null` (or test-specific values) after `submitterIpAddress`.

- [ ] **Step 3: Update SubmitInquiryCommand constructor calls**

Remove `HoneypotField` parameter. Add `Phone` and `SubmitterId` parameters.

- [ ] **Step 4: Update SubmitInquiryHandler test - remove IRateLimitService mock**

The handler no longer injects `IRateLimitService`. Remove the mock and update `HandleAsync` calls to not pass it.

- [ ] **Step 5: Update InquiryDto constructor calls**

Add `Phone` and `SubmitterId` parameters.

- [ ] **Step 6: Update InquirySubmittedDomainEvent constructor calls**

Add `Phone` parameter.

- [ ] **Step 7: Update InquirySubmittedNotificationHandler test**

Replace `Subject` with `ProjectType` in the test event and assertion.

- [ ] **Step 8: Run all inquiries tests**

Run: `dotnet test tests/Modules/Inquiries/Foundry.Inquiries.Tests`
Run: `dotnet test tests/Modules/Notifications/Foundry.Notifications.Tests --filter "InquirySubmitted"`
Expected: All pass

- [ ] **Step 9: Commit**

```bash
git add tests/
git commit -m "test(inquiries): update tests for new fields and removed honeypot/rate-limiting"
```

---

### Task 13: Regenerate EF Core migration

**Files:**
- Delete: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/` (all files)
- Create: new migration via EF Core CLI

- [ ] **Step 1: Delete existing migrations**

```bash
rm -rf src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/
```

- [ ] **Step 2: Create fresh InitialCreate migration**

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Inquiries/Foundry.Inquiries.Infrastructure \
    --startup-project src/Foundry.Api \
    --context InquiriesDbContext
```

- [ ] **Step 3: Verify the generated migration includes phone, submitter_id columns**

Read the generated migration file and verify it includes the new columns.

- [ ] **Step 4: Verify build**

Run: `dotnet build`

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/
git commit -m "chore(inquiries): regenerate migration with Phone and SubmitterId columns"
```

---

## Chunk 2: User Inquiry View & Read Scope (Deferred)

### Task 14: Add GetBySubmitterAsync to repository

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Interfaces/IInquiryRepository.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Repositories/InquiryRepository.cs`

- [ ] **Step 1: Add method to interface**

After `UpdateAsync` (line 11), add:

```csharp
    Task<IReadOnlyList<Inquiry>> GetBySubmitterAsync(string email, string? submitterId, InquiryStatus? status = null, CancellationToken cancellationToken = default);
```

Add the missing import at top:

```csharp
using Foundry.Inquiries.Domain.Enums;
```

- [ ] **Step 2: Implement in InquiryRepository**

Add after `UpdateAsync` method:

```csharp
    public async Task<IReadOnlyList<Inquiry>> GetBySubmitterAsync(
        string email, string? submitterId, InquiryStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Inquiry> query = context.Inquiries
            .Where(i => i.Email == email || (submitterId != null && i.SubmitterId == submitterId));

        if (status is not null)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Infrastructure`

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Application/Interfaces/IInquiryRepository.cs
git add src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Repositories/InquiryRepository.cs
git commit -m "feat(inquiries): add GetBySubmitterAsync to repository"
```

---

### Task 15: Add GetSubmittedInquiries query and handler

**Files:**
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Queries/GetSubmittedInquiries/GetSubmittedInquiriesQuery.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Queries/GetSubmittedInquiries/GetSubmittedInquiriesHandler.cs`

- [ ] **Step 1: Create query record**

```csharp
using Foundry.Inquiries.Domain.Enums;

namespace Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;

public sealed record GetSubmittedInquiriesQuery(
    string Email,
    string? SubmitterId,
    InquiryStatus? Status = null);
```

- [ ] **Step 2: Create handler**

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;

public sealed class GetSubmittedInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetSubmittedInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetBySubmitterAsync(
            query.Email, query.SubmitterId, query.Status, cancellationToken);

        List<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Application`

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Application/Queries/GetSubmittedInquiries/
git commit -m "feat(inquiries): add GetSubmittedInquiries query and handler"
```

---

### Task 16: Add GET /submitted endpoint and update GetById with ownership check

**Files:**
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Api/Controllers/InquiriesController.cs`

- [ ] **Step 1: Add GetSubmitted endpoint**

Add after the `GetAll` method, before `GetById`:

```csharp
    [HttpGet("submitted")]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmitted(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        string? email = User.FindFirst("email")?.Value;
        string? submitterId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(Array.Empty<InquiryResponse>());
        }

        InquiryStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InquiryStatus>(status, ignoreCase: true, out InquiryStatus parsed))
        {
            parsedStatus = parsed;
        }

        Result<IReadOnlyList<InquiryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetSubmittedInquiriesQuery(email, submitterId, parsedStatus), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }
```

Add the using at the top:

```csharp
using Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;
```

- [ ] **Step 2: Update GetById with ownership check**

Replace the `GetById` method with imperative authorization:

```csharp
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound();
        }

        InquiryDto dto = result.Value;

        // Admin/manager with InquiriesRead can see any inquiry
        bool hasReadPermission = User.HasClaim("permission", PermissionType.InquiriesRead);

        if (!hasReadPermission)
        {
            // Check ownership: email or submitterId match
            string? email = User.FindFirst("email")?.Value;
            string? sub = User.FindFirst("sub")?.Value;
            bool isOwner = (email is not null && dto.Email == email) ||
                           (sub is not null && dto.SubmitterId == sub);

            if (!isOwner)
            {
                return NotFound();
            }
        }

        return Ok(ToInquiryResponse(dto));
    }
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Api`

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Api/Controllers/InquiriesController.cs
git commit -m "feat(inquiries): add /submitted endpoint and ownership check on GetById"
```

---

### Task 17: Add tests for Chunk 2

**Files:**
- Create: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Queries/GetSubmittedInquiries/GetSubmittedInquiriesHandlerTests.cs`

- [ ] **Step 1: Create handler tests**

Test cases:
- Returns inquiries matching email
- Returns inquiries matching submitterId
- Filters by status when provided
- Returns empty list when no matches

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Tests.Application.Queries.GetSubmittedInquiries;

public class GetSubmittedInquiriesHandlerTests
{
    private readonly IInquiryRepository _repository = Substitute.For<IInquiryRepository>();
    private readonly GetSubmittedInquiriesHandler _handler;

    public GetSubmittedInquiriesHandlerTests()
    {
        _handler = new GetSubmittedInquiriesHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsMatchingInquiries()
    {
        Inquiry inquiry = Inquiry.Create("Test", "user@test.com", null, "Web", "Under5K", "ASAP",
            "Hello", "127.0.0.1", null, null, TimeProvider.System);
        _repository.GetBySubmitterAsync("user@test.com", null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Inquiry> { inquiry });

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(
            new GetSubmittedInquiriesQuery("user@test.com", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithNoMatches_ReturnsEmptyList()
    {
        _repository.GetBySubmitterAsync("nobody@test.com", null, null, Arg.Any<CancellationToken>())
            .Returns(new List<Inquiry>());

        Result<IReadOnlyList<InquiryDto>> result = await _handler.Handle(
            new GetSubmittedInquiriesQuery("nobody@test.com", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Modules/Inquiries/Foundry.Inquiries.Tests --filter "GetSubmittedInquiries"`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Queries/GetSubmittedInquiries/
git commit -m "test(inquiries): add GetSubmittedInquiriesHandler tests"
```

---

## Chunk 3: SignalR Events via Notifications Module (Deferred)

### Task 18: Add SignalR handlers in Notifications module

**Files:**
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquirySubmittedSignalRHandler.cs`
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquiryStatusChangedSignalRHandler.cs`

- [ ] **Step 1: Create InquirySubmittedSignalRHandler**

```csharp
using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Application.EventHandlers;

public static class InquirySubmittedSignalRHandler
{
    public static async Task Handle(
        InquirySubmittedEvent message,
        IRealtimeDispatcher dispatcher,
        ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquirySubmitted",
            new { message.InquiryId, message.Name, message.Email });

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
```

- [ ] **Step 2: Create InquiryStatusChangedSignalRHandler**

```csharp
using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Application.EventHandlers;

public static class InquiryStatusChangedSignalRHandler
{
    public static async Task Handle(
        InquiryStatusChangedEvent message,
        IRealtimeDispatcher dispatcher,
        ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryStatusUpdated",
            new { message.InquiryId, message.NewStatus });

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Modules/Notifications/Foundry.Notifications.Application`

- [ ] **Step 4: Commit**

```bash
git add src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquirySubmittedSignalRHandler.cs
git add src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquiryStatusChangedSignalRHandler.cs
git commit -m "feat(notifications): add SignalR handlers for inquiry events"
```

---

### Task 19: Add tests for SignalR handlers

**Files:**
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/EventHandlers/InquirySubmittedSignalRHandlerTests.cs`
- Create: `tests/Modules/Notifications/Foundry.Notifications.Tests/EventHandlers/InquiryStatusChangedSignalRHandlerTests.cs`

- [ ] **Step 1: Create InquirySubmittedSignalRHandler tests**

```csharp
using Foundry.Notifications.Application.EventHandlers;
using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Tests.EventHandlers;

public class InquirySubmittedSignalRHandlerTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    [Fact]
    public async Task Handle_WhenTenantResolved_DispatchesToTenant()
    {
        Guid tenantId = Guid.NewGuid();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));

        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            ProjectType = "Web App",
            Message = "Test message",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@example.com"
        };

        await InquirySubmittedSignalRHandler.Handle(@event, _dispatcher, _tenantContext);

        await _dispatcher.Received(1).SendToTenantAsync(
            tenantId,
            Arg.Is<RealtimeEnvelope>(e => e.Module == "Inquiries" && e.Type == "InquirySubmitted"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantNotResolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "Test",
            Email = "test@example.com",
            ProjectType = "Web",
            Message = "Test",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@example.com"
        };

        await InquirySubmittedSignalRHandler.Handle(@event, _dispatcher, _tenantContext);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }
}
```

- [ ] **Step 2: Create InquiryStatusChangedSignalRHandler tests**

Same pattern - test resolved tenant dispatches, unresolved tenant skips.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Modules/Notifications/Foundry.Notifications.Tests --filter "Inquiry"`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add tests/Modules/Notifications/Foundry.Notifications.Tests/EventHandlers/
git commit -m "test(notifications): add SignalR handler tests for inquiry events"
```

---

## Chunk 4: Inquiry Comments (Deferred)

### Task 20: Create InquiryComment domain (entity, ID, domain event)

**Files:**
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Domain/Identity/InquiryCommentId.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Domain/Entities/InquiryComment.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Domain/Events/InquiryCommentAddedDomainEvent.cs`

- [ ] **Step 1: Create InquiryCommentId**

```csharp
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Inquiries.Domain.Identity;

public readonly record struct InquiryCommentId(Guid Value) : IStronglyTypedId<InquiryCommentId>
{
    public static InquiryCommentId Create(Guid value) => new(value);
    public static InquiryCommentId New() => new(Guid.NewGuid());
}
```

- [ ] **Step 2: Create InquiryCommentAddedDomainEvent**

```csharp
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Inquiries.Domain.Events;

public sealed record InquiryCommentAddedDomainEvent(
    Guid InquiryId,
    Guid CommentId,
    bool IsInternal) : DomainEvent;
```

- [ ] **Step 3: Create InquiryComment entity**

```csharp
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Inquiries.Domain.Entities;

public sealed class InquiryComment : Entity<InquiryCommentId>, ITenantScoped
{
    public InquiryId InquiryId { get; private set; }
    public TenantId TenantId { get; set; }
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private InquiryComment() { } // EF Core

    public static InquiryComment Create(
        InquiryId inquiryId,
        string authorId,
        string authorName,
        string content,
        bool isInternal,
        TimeProvider timeProvider)
    {
        InquiryComment comment = new()
        {
            Id = InquiryCommentId.New(),
            InquiryId = inquiryId,
            AuthorId = authorId,
            AuthorName = authorName,
            Content = content,
            IsInternal = isInternal,
            CreatedAt = timeProvider.GetUtcNow()
        };

        comment.RaiseDomainEvent(new InquiryCommentAddedDomainEvent(
            inquiryId.Value,
            comment.Id.Value,
            isInternal));

        return comment;
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Domain`

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Domain/
git commit -m "feat(inquiries): add InquiryComment entity with domain event"
```

---

### Task 21: Add InquiryComment application layer (command, query, handler, DTO, repository interface)

**Files:**
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/AddInquiryComment/AddInquiryCommentCommand.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/AddInquiryComment/AddInquiryCommentHandler.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Commands/AddInquiryComment/AddInquiryCommentValidator.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Queries/GetInquiryComments/GetInquiryCommentsQuery.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Queries/GetInquiryComments/GetInquiryCommentsHandler.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/DTOs/InquiryCommentDto.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/Interfaces/IInquiryCommentRepository.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Application/Mappings/InquiryMappings.cs`

- [ ] **Step 1: Create InquiryCommentDto**

```csharp
namespace Foundry.Inquiries.Application.DTOs;

public sealed record InquiryCommentDto(
    Guid Id,
    Guid InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    DateTimeOffset CreatedAt);
```

- [ ] **Step 2: Create IInquiryCommentRepository**

```csharp
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Application.Interfaces;

public interface IInquiryCommentRepository
{
    Task<IReadOnlyList<InquiryComment>> GetByInquiryIdAsync(InquiryId inquiryId, CancellationToken cancellationToken = default);
    Task AddAsync(InquiryComment comment, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Create AddInquiryCommentCommand**

```csharp
namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

public sealed record AddInquiryCommentCommand(
    Guid InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal);
```

- [ ] **Step 4: Create AddInquiryCommentValidator**

```csharp
using FluentValidation;

namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

public sealed class AddInquiryCommentValidator : AbstractValidator<AddInquiryCommentCommand>
{
    public AddInquiryCommentValidator()
    {
        RuleFor(x => x.InquiryId).NotEmpty();
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(5000);
    }
}
```

- [ ] **Step 5: Create AddInquiryCommentHandler**

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

public static class AddInquiryCommentHandler
{
    public static async Task<Result<InquiryCommentDto>> HandleAsync(
        AddInquiryCommentCommand command,
        IInquiryRepository inquiryRepository,
        IInquiryCommentRepository commentRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(
            InquiryId.Create(command.InquiryId), cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryCommentDto>(Error.NotFound("Inquiry not found."));
        }

        InquiryComment comment = InquiryComment.Create(
            InquiryId.Create(command.InquiryId),
            command.AuthorId,
            command.AuthorName,
            command.Content,
            command.IsInternal,
            timeProvider);

        await commentRepository.AddAsync(comment, cancellationToken);

        return Result.Success(comment.ToCommentDto());
    }
}
```

- [ ] **Step 6: Create GetInquiryCommentsQuery**

```csharp
namespace Foundry.Inquiries.Application.Queries.GetInquiryComments;

public sealed record GetInquiryCommentsQuery(Guid InquiryId);
```

- [ ] **Step 7: Create GetInquiryCommentsHandler**

```csharp
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Queries.GetInquiryComments;

public sealed class GetInquiryCommentsHandler(IInquiryCommentRepository commentRepository)
{
    public async Task<Result<IReadOnlyList<InquiryCommentDto>>> Handle(
        GetInquiryCommentsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<InquiryComment> comments = await commentRepository.GetByInquiryIdAsync(
            InquiryId.Create(query.InquiryId), cancellationToken);

        List<InquiryCommentDto> dtos = comments.Select(c => c.ToCommentDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryCommentDto>>(dtos);
    }
}
```

- [ ] **Step 8: Add ToCommentDto mapping**

In `InquiryMappings.cs`, add:

```csharp
    public static InquiryCommentDto ToCommentDto(this InquiryComment comment)
    {
        return new InquiryCommentDto(
            Id: comment.Id.Value,
            InquiryId: comment.InquiryId.Value,
            AuthorId: comment.AuthorId,
            AuthorName: comment.AuthorName,
            Content: comment.Content,
            IsInternal: comment.IsInternal,
            CreatedAt: comment.CreatedAt);
    }
```

Add using: `using Foundry.Inquiries.Application.DTOs;` (already there for InquiryDto).

- [ ] **Step 9: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Application`

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Application/
git commit -m "feat(inquiries): add comment command, query, handler, DTO, repository interface"
```

---

### Task 22: Add InquiryComment infrastructure (EF config, repository, DbContext update, DI registration)

**Files:**
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Configurations/InquiryCommentConfiguration.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/Repositories/InquiryCommentRepository.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Persistence/InquiriesDbContext.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Extensions/InquiriesInfrastructureExtensions.cs`

- [ ] **Step 1: Create InquiryCommentConfiguration**

```csharp
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Foundry.Inquiries.Infrastructure.Persistence.Configurations;

public sealed class InquiryCommentConfiguration : IEntityTypeConfiguration<InquiryComment>
{
    public void Configure(EntityTypeBuilder<InquiryComment> builder)
    {
        builder.ToTable("inquiry_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(new StronglyTypedIdConverter<InquiryCommentId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.InquiryId)
            .HasConversion(new StronglyTypedIdConverter<InquiryId>())
            .HasColumnName("inquiry_id")
            .IsRequired();

        builder.Property(c => c.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(c => c.AuthorId)
            .HasColumnName("author_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(c => c.AuthorName)
            .HasColumnName("author_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(c => c.IsInternal)
            .HasColumnName("is_internal")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.InquiryId);
    }
}
```

- [ ] **Step 2: Create InquiryCommentRepository**

```csharp
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Infrastructure.Persistence.Repositories;

public sealed class InquiryCommentRepository(InquiriesDbContext context) : IInquiryCommentRepository
{
    public async Task<IReadOnlyList<InquiryComment>> GetByInquiryIdAsync(
        InquiryId inquiryId, CancellationToken cancellationToken = default)
    {
        return await context.InquiryComments
            .Where(c => c.InquiryId == inquiryId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(InquiryComment comment, CancellationToken cancellationToken = default)
    {
        await context.InquiryComments.AddAsync(comment, cancellationToken);
    }
}
```

- [ ] **Step 3: Update InquiriesDbContext - add InquiryComments DbSet**

After line 8, add:

```csharp
    public DbSet<InquiryComment> InquiryComments => Set<InquiryComment>();
```

- [ ] **Step 4: Register IInquiryCommentRepository in DI**

In `InquiriesInfrastructureExtensions.cs`, after line 31 (`AddScoped<IInquiryRepository, InquiryRepository>()`), add:

```csharp
        services.AddScoped<IInquiryCommentRepository, InquiryCommentRepository>();
```

Add the using:

```csharp
using Foundry.Inquiries.Infrastructure.Persistence.Repositories;
```

(Already imported via the existing InquiryRepository registration.)

- [ ] **Step 5: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Infrastructure`

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/
git commit -m "feat(inquiries): add InquiryComment EF config, repository, DbContext, DI"
```

---

### Task 23: Add InquiryCommentAddedEvent and handlers

**Files:**
- Create: `src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquiryCommentAddedEvent.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Application/EventHandlers/InquiryCommentAddedDomainEventHandler.cs`
- Create: `src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquiryCommentAddedSignalRHandler.cs`

- [ ] **Step 1: Create integration event**

```csharp
namespace Foundry.Shared.Contracts.Inquiries.Events;

public sealed record InquiryCommentAddedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required Guid CommentId { get; init; }
    public required bool IsInternal { get; init; }
    public required DateTime AddedAt { get; init; }
}
```

- [ ] **Step 2: Create domain event handler**

```csharp
using Foundry.Inquiries.Domain.Events;
using Wolverine;

namespace Foundry.Inquiries.Application.EventHandlers;

public static class InquiryCommentAddedDomainEventHandler
{
    public static async Task HandleAsync(
        InquiryCommentAddedDomainEvent domainEvent,
        IMessageBus bus,
        CancellationToken ct)
    {
        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquiryCommentAddedEvent
        {
            InquiryId = domainEvent.InquiryId,
            CommentId = domainEvent.CommentId,
            IsInternal = domainEvent.IsInternal,
            AddedAt = DateTime.UtcNow
        });
    }
}
```

- [ ] **Step 3: Create SignalR handler**

```csharp
using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Realtime;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedSignalRHandler
{
    public static async Task Handle(
        InquiryCommentAddedEvent message,
        IRealtimeDispatcher dispatcher,
        ITenantContext tenantContext)
    {
        if (!tenantContext.IsResolved)
        {
            return;
        }

        RealtimeEnvelope envelope = RealtimeEnvelope.Create(
            "Inquiries",
            "InquiryCommentAdded",
            new { message.InquiryId, message.CommentId, message.IsInternal });

        await dispatcher.SendToTenantAsync(tenantContext.TenantId.Value, envelope);
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build`

- [ ] **Step 5: Commit**

```bash
git add src/Shared/Foundry.Shared.Contracts/Inquiries/Events/InquiryCommentAddedEvent.cs
git add src/Modules/Inquiries/Foundry.Inquiries.Application/EventHandlers/InquiryCommentAddedDomainEventHandler.cs
git add src/Modules/Notifications/Foundry.Notifications.Application/EventHandlers/InquiryCommentAddedSignalRHandler.cs
git commit -m "feat(inquiries): add InquiryCommentAdded event and handlers"
```

---

### Task 24: Add comment API endpoints and contracts

**Files:**
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Api/Contracts/AddInquiryCommentRequest.cs`
- Create: `src/Modules/Inquiries/Foundry.Inquiries.Api/Contracts/InquiryCommentResponse.cs`
- Modify: `src/Modules/Inquiries/Foundry.Inquiries.Api/Controllers/InquiriesController.cs`

- [ ] **Step 1: Create AddInquiryCommentRequest**

```csharp
namespace Foundry.Inquiries.Api.Contracts;

public sealed record AddInquiryCommentRequest(
    string Content,
    bool IsInternal);
```

- [ ] **Step 2: Create InquiryCommentResponse**

```csharp
namespace Foundry.Inquiries.Api.Contracts;

public sealed record InquiryCommentResponse(
    Guid Id,
    Guid InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    DateTime CreatedAt);
```

- [ ] **Step 3: Add comment endpoints to InquiriesController**

Add after the `UpdateStatus` method:

```csharp
    [HttpPost("{id:guid}/comments")]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(typeof(InquiryCommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        Guid id,
        [FromBody] AddInquiryCommentRequest request,
        CancellationToken cancellationToken)
    {
        string authorId = User.FindFirst("sub")?.Value ?? string.Empty;
        string authorName = User.FindFirst("name")?.Value ?? "Unknown";

        AddInquiryCommentCommand command = new(id, authorId, authorName, request.Content, request.IsInternal);

        Result<InquiryCommentDto> result = await bus.InvokeAsync<Result<InquiryCommentDto>>(command, cancellationToken);

        return result.Map(ToCommentResponse).ToActionResult();
    }

    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
    {
        // Verify inquiry exists and user has access
        Result<InquiryDto> inquiryResult = await bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        if (!inquiryResult.IsSuccess)
        {
            return NotFound();
        }

        InquiryDto inquiry = inquiryResult.Value;
        bool hasReadPermission = User.HasClaim("permission", PermissionType.InquiriesRead);
        string? email = User.FindFirst("email")?.Value;
        string? sub = User.FindFirst("sub")?.Value;
        bool isOwner = (email is not null && inquiry.Email == email) ||
                       (sub is not null && inquiry.SubmitterId == sub);

        if (!hasReadPermission && !isOwner)
        {
            return NotFound();
        }

        Result<IReadOnlyList<InquiryCommentDto>> commentsResult = await bus.InvokeAsync<Result<IReadOnlyList<InquiryCommentDto>>>(
            new GetInquiryCommentsQuery(id), cancellationToken);

        return commentsResult.Map(comments =>
        {
            IEnumerable<InquiryCommentDto> filtered = hasReadPermission
                ? comments
                : comments.Where(c => !c.IsInternal);
            return (IReadOnlyList<InquiryCommentResponse>)filtered.Select(ToCommentResponse).ToList();
        }).ToActionResult();
    }

    private static InquiryCommentResponse ToCommentResponse(InquiryCommentDto dto) => new(
        dto.Id,
        dto.InquiryId,
        dto.AuthorId,
        dto.AuthorName,
        dto.Content,
        dto.IsInternal,
        dto.CreatedAt.UtcDateTime);
```

Add the required usings at the top of the controller:

```csharp
using Foundry.Inquiries.Application.Commands.AddInquiryComment;
using Foundry.Inquiries.Application.Queries.GetInquiryComments;
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Modules/Inquiries/Foundry.Inquiries.Api`

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Api/
git commit -m "feat(inquiries): add comment API endpoints and contracts"
```

---

### Task 25: Add tests for comment functionality

**Files:**
- Create: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Commands/AddInquiryComment/AddInquiryCommentHandlerTests.cs`
- Create: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Commands/AddInquiryComment/AddInquiryCommentValidatorTests.cs`
- Create: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Application/Queries/GetInquiryComments/GetInquiryCommentsHandlerTests.cs`
- Create: `tests/Modules/Inquiries/Foundry.Inquiries.Tests/Domain/Entities/InquiryCommentTests.cs`

- [ ] **Step 1: Create InquiryComment domain tests**

Test: Create sets all properties, raises domain event.

- [ ] **Step 2: Create AddInquiryCommentHandler tests**

Test: creates comment when inquiry exists, returns NotFound when inquiry doesn't exist.

- [ ] **Step 3: Create AddInquiryCommentValidator tests**

Test: required fields, max lengths.

- [ ] **Step 4: Create GetInquiryCommentsHandler tests**

Test: returns comments for inquiry.

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Modules/Inquiries/Foundry.Inquiries.Tests`
Expected: All pass

- [ ] **Step 6: Commit**

```bash
git add tests/Modules/Inquiries/Foundry.Inquiries.Tests/
git commit -m "test(inquiries): add comment domain, handler, and validator tests"
```

---

### Task 26: Regenerate migration with InquiryComment table

**Files:**
- Delete and recreate: `src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/`

- [ ] **Step 1: Delete existing migrations**

```bash
rm -rf src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/
```

- [ ] **Step 2: Create fresh migration**

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Inquiries/Foundry.Inquiries.Infrastructure \
    --startup-project src/Foundry.Api \
    --context InquiriesDbContext
```

- [ ] **Step 3: Verify migration includes inquiry_comments table and all new columns**

Read the generated migration and verify it includes: `phone`, `submitter_id`, `inquiry_comments` table.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add src/Modules/Inquiries/Foundry.Inquiries.Infrastructure/Migrations/
git commit -m "chore(inquiries): regenerate migration with all schema changes"
```

---

## Final: Full Build Verification

### Task 27: Full build and test verification

- [ ] **Step 1: Clean build**

Run: `dotnet build --no-incremental`

- [ ] **Step 2: Run all tests**

Run: `dotnet test`

- [ ] **Step 3: Verify no architecture test violations**

Run: `dotnet test tests/Foundry.Architecture.Tests`

- [ ] **Step 4: Push**

```bash
git push
```
