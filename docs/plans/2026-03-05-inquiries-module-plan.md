# Inquiries Module Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a new Inquiries module for capturing public business inquiries with status workflow, email notifications, and spam protection.

**Architecture:** New Clean Architecture module (Domain → Application → Infrastructure → Api) following existing Wallow patterns. Not tenant-scoped. Publishes `InquirySubmittedEvent` to RabbitMQ; Communications module consumes it to send emails via `SendEmailRequestedEvent`.

**Tech Stack:** .NET 10, EF Core (PostgreSQL `inquiries` schema), Wolverine (CQRS + messaging), FluentValidation, Valkey (rate limiting)

---

### Task 1: Create project structure and solution references

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Wallow.Inquiries.Domain.csproj`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Wallow.Inquiries.Application.csproj`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Wallow.Inquiries.Infrastructure.csproj`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/Wallow.Inquiries.Api.csproj`
- Modify: `src/Wallow.Api/Wallow.Api.csproj`
- Modify: `Wallow.sln`

**Step 1: Create the four project directories and .csproj files**

`Wallow.Inquiries.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Inquiries.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
```

`Wallow.Inquiries.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Inquiries.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Inquiries.Domain\Wallow.Inquiries.Domain.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
</Project>
```

`Wallow.Inquiries.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Inquiries.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="StackExchange.Redis" />
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Inquiries.Domain\Wallow.Inquiries.Domain.csproj" />
    <ProjectReference Include="..\Wallow.Inquiries.Application\Wallow.Inquiries.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

`Wallow.Inquiries.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Inquiries.Api</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Inquiries.Application\Wallow.Inquiries.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Api\Wallow.Shared.Api.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Add projects to solution**

Run:
```bash
dotnet sln add src/Modules/Inquiries/Wallow.Inquiries.Domain/Wallow.Inquiries.Domain.csproj
dotnet sln add src/Modules/Inquiries/Wallow.Inquiries.Application/Wallow.Inquiries.Application.csproj
dotnet sln add src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Wallow.Inquiries.Infrastructure.csproj
dotnet sln add src/Modules/Inquiries/Wallow.Inquiries.Api/Wallow.Inquiries.Api.csproj
```

**Step 3: Add project references to Wallow.Api.csproj**

Add to the `<!-- Module Api projects -->` section:
```xml
<ProjectReference Include="..\Modules\Inquiries\Wallow.Inquiries.Api\Wallow.Inquiries.Api.csproj" />
```

Add to the `<!-- Module Infrastructure projects -->` section:
```xml
<ProjectReference Include="..\Modules\Inquiries\Wallow.Inquiries.Infrastructure\Wallow.Inquiries.Infrastructure.csproj" />
```

**Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(inquiries): scaffold module project structure"
```

---

### Task 2: Domain layer — Enums, Identity, Events, Entity

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/ProjectType.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/BudgetRange.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/Timeline.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/InquiryStatus.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Identity/InquiryId.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Events/InquirySubmittedDomainEvent.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Events/InquiryStatusChangedDomainEvent.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Exceptions/InvalidInquiryStatusTransitionException.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Domain/Entities/Inquiry.cs`

**Step 1: Create enums**

`ProjectType.cs`:
```csharp
namespace Wallow.Inquiries.Domain.Enums;

public enum ProjectType
{
    WebApplication,
    MobileApplication,
    ApiIntegration,
    Consulting,
    Other
}
```

`BudgetRange.cs`:
```csharp
namespace Wallow.Inquiries.Domain.Enums;

public enum BudgetRange
{
    Under5K,
    From5KTo15K,
    From15KTo50K,
    Over50K,
    NotSure
}
```

`Timeline.cs`:
```csharp
namespace Wallow.Inquiries.Domain.Enums;

public enum Timeline
{
    Asap,
    OneToThreeMonths,
    ThreeToSixMonths,
    SixPlusMonths,
    Flexible
}
```

`InquiryStatus.cs`:
```csharp
namespace Wallow.Inquiries.Domain.Enums;

public enum InquiryStatus
{
    New,
    Reviewed,
    Contacted,
    Closed
}
```

**Step 2: Create strongly-typed ID**

`InquiryId.cs`:
```csharp
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Inquiries.Domain.Identity;

public readonly record struct InquiryId(Guid Value) : IStronglyTypedId<InquiryId>
{
    public static InquiryId Create(Guid value) => new(value);
    public static InquiryId New() => new(Guid.NewGuid());
}
```

**Step 3: Create domain events**

`InquirySubmittedDomainEvent.cs`:
```csharp
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Events;

public sealed record InquirySubmittedDomainEvent(
    Guid InquiryId,
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message) : DomainEvent;
```

`InquiryStatusChangedDomainEvent.cs`:
```csharp
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Events;

public sealed record InquiryStatusChangedDomainEvent(
    Guid InquiryId,
    string OldStatus,
    string NewStatus) : DomainEvent;
```

**Step 4: Create domain exception**

`InvalidInquiryStatusTransitionException.cs`:
```csharp
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Exceptions;

public sealed class InvalidInquiryStatusTransitionException : BusinessRuleException
{
    public InvalidInquiryStatusTransitionException(string from, string to)
        : base("Inquiries.InvalidStatusTransition", $"Cannot transition from {from} to {to}")
    {
    }
}
```

**Step 5: Create Inquiry aggregate root**

`Inquiry.cs`:
```csharp
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Exceptions;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Entities;

public sealed class Inquiry : AggregateRoot<InquiryId>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Company { get; private set; }
    public ProjectType ProjectType { get; private set; }
    public BudgetRange BudgetRange { get; private set; }
    public Timeline Timeline { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public InquiryStatus Status { get; private set; }
    public string SubmitterIpAddress { get; private set; } = string.Empty;

    private Inquiry() { } // EF Core

    public static Inquiry Create(
        string name,
        string email,
        string? company,
        ProjectType projectType,
        BudgetRange budgetRange,
        Timeline timeline,
        string message,
        string submitterIpAddress,
        TimeProvider timeProvider)
    {
        Inquiry inquiry = new()
        {
            Id = InquiryId.New(),
            Name = name,
            Email = email,
            Company = company,
            ProjectType = projectType,
            BudgetRange = budgetRange,
            Timeline = timeline,
            Message = message,
            Status = InquiryStatus.New,
            SubmitterIpAddress = submitterIpAddress
        };

        inquiry.SetCreated(timeProvider.GetUtcNow());

        inquiry.RaiseDomainEvent(new InquirySubmittedDomainEvent(
            inquiry.Id.Value,
            name,
            email,
            company,
            projectType.ToString(),
            budgetRange.ToString(),
            timeline.ToString(),
            message));

        return inquiry;
    }

    public void TransitionTo(InquiryStatus newStatus, TimeProvider timeProvider)
    {
        if (!IsValidTransition(Status, newStatus))
        {
            throw new InvalidInquiryStatusTransitionException(Status.ToString(), newStatus.ToString());
        }

        InquiryStatus oldStatus = Status;
        Status = newStatus;
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new InquiryStatusChangedDomainEvent(
            Id.Value,
            oldStatus.ToString(),
            newStatus.ToString()));
    }

    private static bool IsValidTransition(InquiryStatus current, InquiryStatus target)
    {
        return (current, target) switch
        {
            (InquiryStatus.New, InquiryStatus.Reviewed) => true,
            (InquiryStatus.Reviewed, InquiryStatus.Contacted) => true,
            (InquiryStatus.Contacted, InquiryStatus.Closed) => true,
            _ => false
        };
    }
}
```

**Step 6: Verify build**

Run: `dotnet build src/Modules/Inquiries/Wallow.Inquiries.Domain`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add domain layer with Inquiry aggregate root"
```

---

### Task 3: Shared Contracts — Integration events

**Files:**
- Create: `src/Shared/Wallow.Shared.Contracts/Inquiries/Events/InquirySubmittedEvent.cs`
- Create: `src/Shared/Wallow.Shared.Contracts/Inquiries/Events/InquiryStatusChangedEvent.cs`

**Step 1: Create integration events**

`InquirySubmittedEvent.cs`:
```csharp
namespace Wallow.Shared.Contracts.Inquiries.Events;

public sealed record InquirySubmittedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Company { get; init; }
    public required string ProjectType { get; init; }
    public required string BudgetRange { get; init; }
    public required string Timeline { get; init; }
    public required string Message { get; init; }
}
```

`InquiryStatusChangedEvent.cs`:
```csharp
namespace Wallow.Shared.Contracts.Inquiries.Events;

public sealed record InquiryStatusChangedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required string OldStatus { get; init; }
    public required string NewStatus { get; init; }
}
```

**Step 2: Verify build**

Run: `dotnet build src/Shared/Wallow.Shared.Contracts`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add integration event contracts"
```

---

### Task 4: Application layer — Repository interface, DTOs, Commands, Queries, Validators, Event handlers

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Interfaces/IInquiryRepository.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Interfaces/IRateLimitService.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/DTOs/InquiryDto.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Mappings/InquiryMappings.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryCommand.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryHandler.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryValidator.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusCommand.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusHandler.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/UpdateInquiryStatus/UpdateInquiryStatusValidator.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiries/GetInquiriesQuery.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiries/GetInquiriesHandler.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryById/GetInquiryByIdQuery.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Queries/GetInquiryById/GetInquiryByIdHandler.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/EventHandlers/InquirySubmittedDomainEventHandler.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Application/Extensions/ApplicationExtensions.cs`

**Step 1: Create interfaces**

`IInquiryRepository.cs`:
```csharp
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Interfaces;

public interface IInquiryRepository
{
    Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inquiry>> GetAllAsync(InquiryStatus? statusFilter = null, CancellationToken cancellationToken = default);
    void Add(Inquiry inquiry);
    void Update(Inquiry inquiry);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`IRateLimitService.cs`:
```csharp
namespace Wallow.Inquiries.Application.Interfaces;

public interface IRateLimitService
{
    Task<bool> IsRateLimitedAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task RecordSubmissionAsync(string ipAddress, CancellationToken cancellationToken = default);
}
```

**Step 2: Create DTO and mappings**

`InquiryDto.cs`:
```csharp
namespace Wallow.Inquiries.Application.DTOs;

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
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
```

`InquiryMappings.cs`:
```csharp
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Domain.Entities;

namespace Wallow.Inquiries.Application.Mappings;

public static class InquiryMappings
{
    public static InquiryDto ToDto(this Inquiry inquiry) => new(
        inquiry.Id.Value,
        inquiry.Name,
        inquiry.Email,
        inquiry.Company,
        inquiry.ProjectType.ToString(),
        inquiry.BudgetRange.ToString(),
        inquiry.Timeline.ToString(),
        inquiry.Message,
        inquiry.Status.ToString(),
        inquiry.CreatedAt,
        inquiry.UpdatedAt);
}
```

**Step 3: Create SubmitInquiry command, handler, and validator**

`SubmitInquiryCommand.cs`:
```csharp
using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed record SubmitInquiryCommand(
    string Name,
    string Email,
    string? Company,
    ProjectType ProjectType,
    BudgetRange BudgetRange,
    Timeline Timeline,
    string Message,
    string SubmitterIpAddress,
    string? Honeypot);
```

`SubmitInquiryHandler.cs`:
```csharp
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed class SubmitInquiryHandler(
    IInquiryRepository inquiryRepository,
    IRateLimitService rateLimitService,
    TimeProvider timeProvider)
{
    public async Task<Result<InquiryDto>> Handle(
        SubmitInquiryCommand command,
        CancellationToken cancellationToken)
    {
        // Honeypot check — silently reject bots
        if (!string.IsNullOrEmpty(command.Honeypot))
        {
            return Result.Success(CreateDummyDto());
        }

        // Rate limit check
        bool isLimited = await rateLimitService.IsRateLimitedAsync(
            command.SubmitterIpAddress, cancellationToken);

        if (isLimited)
        {
            return Result.Failure<InquiryDto>(
                Error.Validation("Inquiries.RateLimited", "Too many submissions. Please try again later."));
        }

        Inquiry inquiry = Inquiry.Create(
            command.Name,
            command.Email,
            command.Company,
            command.ProjectType,
            command.BudgetRange,
            command.Timeline,
            command.Message,
            command.SubmitterIpAddress,
            timeProvider);

        inquiryRepository.Add(inquiry);
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        await rateLimitService.RecordSubmissionAsync(
            command.SubmitterIpAddress, cancellationToken);

        return Result.Success(inquiry.ToDto());
    }

    private static InquiryDto CreateDummyDto() => new(
        Guid.NewGuid(), "received", "received@example.com", null,
        "Other", "NotSure", "Flexible", "Thank you", "New",
        DateTimeOffset.UtcNow, null);
}
```

`SubmitInquiryValidator.cs`:
```csharp
using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed class SubmitInquiryValidator : AbstractValidator<SubmitInquiryCommand>
{
    public SubmitInquiryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required");

        RuleFor(x => x.Company)
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MinimumLength(10).WithMessage("Message must be at least 10 characters")
            .MaximumLength(5000).WithMessage("Message must not exceed 5000 characters");

        RuleFor(x => x.ProjectType).IsInEnum();
        RuleFor(x => x.BudgetRange).IsInEnum();
        RuleFor(x => x.Timeline).IsInEnum();
    }
}
```

**Step 4: Create UpdateInquiryStatus command, handler, and validator**

`UpdateInquiryStatusCommand.cs`:
```csharp
using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed record UpdateInquiryStatusCommand(
    Guid InquiryId,
    InquiryStatus NewStatus);
```

`UpdateInquiryStatusHandler.cs`:
```csharp
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed class UpdateInquiryStatusHandler(
    IInquiryRepository inquiryRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<InquiryDto>> Handle(
        UpdateInquiryStatusCommand command,
        CancellationToken cancellationToken)
    {
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(
            InquiryId.Create(command.InquiryId), cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryDto>(
                Error.NotFound($"Inquiry '{command.InquiryId}' not found"));
        }

        inquiry.TransitionTo(command.NewStatus, timeProvider);

        inquiryRepository.Update(inquiry);
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
```

`UpdateInquiryStatusValidator.cs`:
```csharp
using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed class UpdateInquiryStatusValidator : AbstractValidator<UpdateInquiryStatusCommand>
{
    public UpdateInquiryStatusValidator()
    {
        RuleFor(x => x.InquiryId)
            .NotEmpty().WithMessage("Inquiry ID is required");

        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
```

**Step 5: Create queries**

`GetInquiriesQuery.cs`:
```csharp
using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Application.Queries.GetInquiries;

public sealed record GetInquiriesQuery(InquiryStatus? StatusFilter = null);
```

`GetInquiriesHandler.cs`:
```csharp
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetInquiries;

public sealed class GetInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetAllAsync(
            query.StatusFilter, cancellationToken);

        IReadOnlyList<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success(dtos);
    }
}
```

`GetInquiryByIdQuery.cs`:
```csharp
namespace Wallow.Inquiries.Application.Queries.GetInquiryById;

public sealed record GetInquiryByIdQuery(Guid Id);
```

`GetInquiryByIdHandler.cs`:
```csharp
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetInquiryById;

public sealed class GetInquiryByIdHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<InquiryDto>> Handle(
        GetInquiryByIdQuery query,
        CancellationToken cancellationToken)
    {
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(
            InquiryId.Create(query.Id), cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryDto>(
                Error.NotFound($"Inquiry '{query.Id}' not found"));
        }

        return Result.Success(inquiry.ToDto());
    }
}
```

**Step 6: Create domain event handler (bridges to integration event)**

`InquirySubmittedDomainEventHandler.cs`:
```csharp
using Wallow.Inquiries.Domain.Events;
using Wallow.Shared.Contracts.Inquiries.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Inquiries.Application.EventHandlers;

public sealed partial class InquirySubmittedDomainEventHandler
{
    public static async Task HandleAsync(
        InquirySubmittedDomainEvent domainEvent,
        IMessageBus bus,
        ILogger<InquirySubmittedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingEvent(logger, domainEvent.InquiryId);

        await bus.PublishAsync(new InquirySubmittedEvent
        {
            InquiryId = domainEvent.InquiryId,
            Name = domainEvent.Name,
            Email = domainEvent.Email,
            Company = domainEvent.Company,
            ProjectType = domainEvent.ProjectType,
            BudgetRange = domainEvent.BudgetRange,
            Timeline = domainEvent.Timeline,
            Message = domainEvent.Message
        });

        LogPublishedEvent(logger, domainEvent.InquiryId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling InquirySubmittedDomainEvent for Inquiry {InquiryId}")]
    private static partial void LogHandlingEvent(ILogger logger, Guid inquiryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published InquirySubmittedEvent for Inquiry {InquiryId}")]
    private static partial void LogPublishedEvent(ILogger logger, Guid inquiryId);
}
```

**Step 7: Create application extensions**

`ApplicationExtensions.cs`:
```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Inquiries.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddInquiriesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

**Step 8: Verify build**

Run: `dotnet build src/Modules/Inquiries/Wallow.Inquiries.Application`
Expected: Build succeeds

**Step 9: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add application layer with commands, queries, and event handlers"
```

---

### Task 5: Infrastructure layer — DbContext, Configuration, Repository, Rate Limiting, Extensions

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/InquiriesDbContext.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/InquiriesDbContextFactory.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/Configurations/InquiryConfiguration.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Persistence/Repositories/InquiryRepository.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Services/ValkeyRateLimitService.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesInfrastructureExtensions.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs`

**Step 1: Create DbContext**

`InquiriesDbContext.cs` — Note: NOT tenant-aware since inquiries are public, not tenant-scoped:
```csharp
using Wallow.Inquiries.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContext : DbContext
{
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    public InquiriesDbContext(DbContextOptions<InquiriesDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inquiries");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);
    }
}
```

`InquiriesDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Inquiries.Infrastructure.Persistence;

public class InquiriesDbContextFactory : IDesignTimeDbContextFactory<InquiriesDbContext>
{
    public InquiriesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InquiriesDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql("Host=localhost;Database=wallow;Username=postgres;Password=postgres");
        return new InquiriesDbContext(optionsBuilder.Options);
    }
}
```

**Step 2: Create entity configuration**

`InquiryConfiguration.cs`:
```csharp
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Inquiries.Infrastructure.Persistence.Configurations;

public sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.ToTable("inquiries");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(new StronglyTypedIdConverter<InquiryId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(i => i.Company)
            .HasColumnName("company")
            .HasMaxLength(200);

        builder.Property(i => i.ProjectType)
            .HasColumnName("project_type")
            .IsRequired();

        builder.Property(i => i.BudgetRange)
            .HasColumnName("budget_range")
            .IsRequired();

        builder.Property(i => i.Timeline)
            .HasColumnName("timeline")
            .IsRequired();

        builder.Property(i => i.Message)
            .HasColumnName("message")
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(i => i.SubmitterIpAddress)
            .HasColumnName("submitter_ip_address")
            .HasMaxLength(45)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(i => i.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(i => i.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Ignore(i => i.DomainEvents);

        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);
        builder.HasIndex(i => i.Email);
    }
}
```

**Step 3: Create repository**

`InquiryRepository.cs`:
```csharp
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Inquiries.Infrastructure.Persistence.Repositories;

public sealed class InquiryRepository : IInquiryRepository
{
    private readonly InquiriesDbContext _context;

    public InquiryRepository(InquiriesDbContext context)
    {
        _context = context;
    }

    public Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default)
    {
        return _context.Inquiries
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Inquiry>> GetAllAsync(
        InquiryStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Inquiry> query = _context.Inquiries.AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(i => i.Status == statusFilter.Value);
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(Inquiry inquiry)
    {
        _context.Inquiries.Add(inquiry);
    }

    public void Update(Inquiry inquiry)
    {
        _context.Inquiries.Update(inquiry);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

**Step 4: Create rate limit service**

`ValkeyRateLimitService.cs`:
```csharp
using Wallow.Inquiries.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Wallow.Inquiries.Infrastructure.Services;

public sealed class ValkeyRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly int _maxSubmissions;
    private readonly TimeSpan _window;

    public ValkeyRateLimitService(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _redis = redis;
        _maxSubmissions = configuration.GetValue("Inquiries:RateLimit:MaxSubmissions", 3);
        _window = TimeSpan.FromMinutes(configuration.GetValue("Inquiries:RateLimit:WindowMinutes", 60));
    }

    public async Task<bool> IsRateLimitedAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"inquiry:ratelimit:{ipAddress}";
        RedisValue count = await db.StringGetAsync(key);

        if (count.IsNullOrEmpty)
        {
            return false;
        }

        return (int)count >= _maxSubmissions;
    }

    public async Task RecordSubmissionAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        IDatabase db = _redis.GetDatabase();
        string key = $"inquiry:ratelimit:{ipAddress}";

        long newCount = await db.StringIncrementAsync(key);
        if (newCount == 1)
        {
            await db.KeyExpireAsync(key, _window);
        }
    }
}
```

**Step 5: Create infrastructure extensions**

`InquiriesInfrastructureExtensions.cs`:
```csharp
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence.Repositories;
using Wallow.Inquiries.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static class InquiriesInfrastructureExtensions
{
    public static IServiceCollection AddInquiriesInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InquiriesDbContext>((sp, options) =>
        {
            string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
        });

        services.AddScoped<IInquiryRepository, InquiryRepository>();
        services.AddScoped<IRateLimitService, ValkeyRateLimitService>();

        return services;
    }
}
```

`InquiriesModuleExtensions.cs`:
```csharp
using Wallow.Inquiries.Application.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static partial class InquiriesModuleExtensions
{
    public static IServiceCollection AddInquiriesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddInquiriesApplication();
        services.AddInquiriesInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeInquiriesModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            InquiriesDbContext db = scope.ServiceProvider.GetRequiredService<InquiriesDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("InquiriesModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inquiries module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
```

**Step 6: Verify build**

Run: `dotnet build src/Modules/Inquiries/Wallow.Inquiries.Infrastructure`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add infrastructure layer with persistence and rate limiting"
```

---

### Task 6: API layer — Controller, Contracts, Module registration

**Files:**
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/SubmitInquiryRequest.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/InquiryResponse.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/UpdateInquiryStatusRequest.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs`
- Create: `src/Modules/Inquiries/Wallow.Inquiries.Api/InquiriesModule.cs`

**Step 1: Create request/response contracts**

`SubmitInquiryRequest.cs`:
```csharp
namespace Wallow.Inquiries.Api.Contracts;

public sealed record SubmitInquiryRequest(
    string Name,
    string Email,
    string? Company,
    int ProjectType,
    int BudgetRange,
    int Timeline,
    string Message,
    string? Honeypot = null);
```

`InquiryResponse.cs`:
```csharp
namespace Wallow.Inquiries.Api.Contracts;

public sealed record InquiryResponse(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
```

`UpdateInquiryStatusRequest.cs`:
```csharp
namespace Wallow.Inquiries.Api.Contracts;

public sealed record UpdateInquiryStatusRequest(int NewStatus);
```

**Step 2: Create controller**

`InquiriesController.cs`:
```csharp
using Asp.Versioning;
using Wallow.Inquiries.Api.Contracts;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Queries.GetInquiries;
using Wallow.Inquiries.Application.Queries.GetInquiryById;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Inquiries.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inquiries")]
[Tags("Inquiries")]
[Produces("application/json")]
[Consumes("application/json")]
public class InquiriesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public InquiriesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Submit a new business inquiry (public endpoint).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitInquiryRequest request,
        CancellationToken cancellationToken)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        SubmitInquiryCommand command = new(
            request.Name,
            request.Email,
            request.Company,
            (ProjectType)request.ProjectType,
            (BudgetRange)request.BudgetRange,
            (Timeline)request.Timeline,
            request.Message,
            ipAddress,
            request.Honeypot);

        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToInquiryResponse)
            .ToCreatedResult($"/api/v1/inquiries/{result.Value.Id}");
    }

    /// <summary>
    /// Get all inquiries (authenticated, admin only).
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] InquiryStatus? status,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<InquiryDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetInquiriesQuery(status), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Get a specific inquiry by ID (authenticated, admin only).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    /// <summary>
    /// Update inquiry status (authenticated, admin only).
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInquiryStatusRequest request,
        CancellationToken cancellationToken)
    {
        UpdateInquiryStatusCommand command = new(id, (InquiryStatus)request.NewStatus);

        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    private static InquiryResponse ToInquiryResponse(InquiryDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Email,
        dto.Company,
        dto.ProjectType,
        dto.BudgetRange,
        dto.Timeline,
        dto.Message,
        dto.Status,
        dto.CreatedAt,
        dto.UpdatedAt);
}
```

**Step 3: Create module registration**

`InquiriesModule.cs` — follows the `IModuleRegistration` auto-discovery pattern:
```csharp
using System.Reflection;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Infrastructure.Extensions;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Kernel.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Wallow.Inquiries.Api;

public sealed class InquiriesModule : IModuleRegistration
{
    public static string ModuleName => "Inquiries";

    public static Assembly? HandlerAssembly =>
        typeof(SubmitInquiryCommand).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddInquiriesModule(configuration);
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        await app.InitializeInquiriesModuleAsync();
    }

    public static void ConfigureMessaging(WolverineOptions options)
    {
        options.PublishMessage<InquirySubmittedEvent>().ToRabbitExchange("inquiries-events");
        options.PublishMessage<InquiryStatusChangedEvent>().ToRabbitExchange("inquiries-events");
        options.ListenToRabbitQueue("inquiries-inbox");
    }
}
```

**Step 4: Verify full solution build**

Run: `dotnet build`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add API layer with controller and module registration"
```

---

### Task 7: Communications module — Email notification handler

**Files:**
- Create: `src/Modules/Communications/Wallow.Communications.Application/EventHandlers/InquirySubmittedEventHandler.cs`

**Step 1: Create the event handler in Communications module**

This handler subscribes to `InquirySubmittedEvent` and publishes two `SendEmailRequestedEvent` messages — one to admin, one to submitter.

`InquirySubmittedEventHandler.cs`:
```csharp
using Wallow.Shared.Contracts.Communications.Email.Events;
using Wallow.Shared.Contracts.Inquiries.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Communications.Application.EventHandlers;

public sealed partial class InquirySubmittedEventHandler
{
    public static async Task HandleAsync(
        InquirySubmittedEvent evt,
        IMessageBus bus,
        IConfiguration configuration,
        ILogger<InquirySubmittedEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingEvent(logger, evt.InquiryId);

        string adminEmail = configuration.GetValue("Inquiries:AdminEmail", "admin@wallow.dev")!;

        // Send admin notification
        await bus.PublishAsync(new SendEmailRequestedEvent
        {
            TenantId = Guid.Empty,
            To = adminEmail,
            Subject = $"New Business Inquiry from {evt.Name}",
            Body = $"""
                New business inquiry received:

                Name: {evt.Name}
                Email: {evt.Email}
                Company: {evt.Company ?? "Not specified"}
                Project Type: {evt.ProjectType}
                Budget Range: {evt.BudgetRange}
                Timeline: {evt.Timeline}

                Message:
                {evt.Message}
                """,
            SourceModule = "Inquiries",
            CorrelationId = evt.InquiryId
        });

        // Send confirmation to submitter
        await bus.PublishAsync(new SendEmailRequestedEvent
        {
            TenantId = Guid.Empty,
            To = evt.Email,
            Subject = "Thanks for reaching out!",
            Body = $"""
                Hi {evt.Name},

                Thanks for your inquiry! We've received your message and will get back to you shortly.

                Here's a summary of what you submitted:
                - Project Type: {evt.ProjectType}
                - Budget Range: {evt.BudgetRange}
                - Timeline: {evt.Timeline}

                We look forward to working with you!

                Best regards,
                The Wallow Team
                """,
            SourceModule = "Inquiries",
            CorrelationId = evt.InquiryId
        });

        LogPublishedEmails(logger, evt.InquiryId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling InquirySubmittedEvent for Inquiry {InquiryId}")]
    private static partial void LogHandlingEvent(ILogger logger, Guid inquiryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published email notifications for Inquiry {InquiryId}")]
    private static partial void LogPublishedEmails(ILogger logger, Guid inquiryId);
}
```

**Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(communications): add handler for inquiry submission email notifications"
```

---

### Task 8: EF Core migration

**Step 1: Create the initial migration**

Run:
```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/Inquiries/Wallow.Inquiries.Infrastructure \
    --startup-project src/Wallow.Api \
    --context InquiriesDbContext
```

Expected: Migration files created in `src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Migrations/`

**Step 2: Verify migration applies**

Run: `dotnet build`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(inquiries): add EF Core initial migration"
```

---

### Task 9: Verify end-to-end — build, test, run

**Step 1: Full solution build**

Run: `dotnet build`
Expected: Build succeeds with zero errors

**Step 2: Run existing tests (ensure no regressions)**

Run: `dotnet test`
Expected: All existing tests pass

**Step 3: Commit any remaining changes**

```bash
git add -A && git commit -m "chore(inquiries): finalize module integration"
```
