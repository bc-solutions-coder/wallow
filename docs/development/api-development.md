# API Development Guide

This guide covers how to build APIs in Wallow, from controller patterns to error handling.
Every example is taken from the Inquiries module (`api/src/Modules/Inquiries/`) unless noted,
so you can open the real file next to the excerpt.

## Overview

Wallow APIs follow a consistent architecture:

```
HTTP Request
    ↓
Controller (receives request, extracts user/tenant context)
    ↓
Command/Query (immutable record)
    ↓
Wolverine Handler (business logic, returns Result<T>)
    ↓
Result Extensions (maps Result to HTTP response)
    ↓
HTTP Response (JSON or ProblemDetails)
```

**Key principles:**

- Controllers are thin -- they delegate to Wolverine handlers immediately
- Commands/queries are immutable records
- Handlers are normally **`public sealed class`es** taking dependencies through a primary
  constructor; Wolverine also discovers **static** handlers (mostly event handlers). Either
  shape is auto-discovered — no DI registration is required.
- Handlers return `Result<T>` instead of throwing exceptions for expected failures
- FluentValidation validates commands before handlers execute
- Global exception handling catches unexpected errors
- Routes are versioned: `v{version:apiVersion}/{resource}`

## Controller Patterns

### Basic Controller Structure

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Api/Controllers/InquiriesController.cs
namespace Wallow.Inquiries.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/inquiries")]
[Authorize]
[Tags("Inquiries")]
[Produces("application/json")]
[Consumes("application/json")]
public partial class InquiriesController(
    IMessageBus bus,
    ITenantContext tenantContext,
    ILogger<InquiriesController> logger) : ControllerBase
{
    // ... endpoints
}
```

The class is `partial` because the `[LoggerMessage]` source generator emits the other half.
See [Logging](#logging) below.

### Standard Attributes

Every controller should include:

| Attribute                                     | Purpose                                        |
| --------------------------------------------- | ---------------------------------------------- |
| `[ApiController]`                             | Enables automatic model validation and binding |
| `[ApiVersion(1)]`                             | API version number                             |
| `[Route("v{version:apiVersion}/{resource}")]` | Versioned RESTful route pattern                |
| `[Authorize]`                                 | Requires authentication (JWT or API key)       |
| `[Tags("...")]`                               | OpenAPI grouping for Scalar documentation      |
| `[Produces("application/json")]`              | Response content type                          |
| `[Consumes("application/json")]`              | Request content type                           |

### Route Conventions

Route templates start at the version segment. There is **no `api/` prefix** in any controller:

```
GET    /v1/{resources}              List all
GET    /v1/{resources}/{id}         Get by ID
POST   /v1/{resources}              Create
PUT    /v1/{resources}/{id}         Update
PATCH  /v1/{resources}/{id}/{field} Partial update
DELETE /v1/{resources}/{id}         Delete
POST   /v1/{resources}/{id}/action  Custom action
```

> **About the `/api` prefix.** Some deployments front the API with a reverse proxy that routes
> on a path prefix. That prefix is **opt-in and external**: `Wallow.Api` reads the `PathBase`
> configuration key at startup (`api/src/Wallow.Api/Program.cs`) and calls `UsePathBase` only
> when it is set, so the proxy can mount the whole API under a prefix. The controllers
> themselves never declare one -- when you call the API directly on port 5001, the paths below
> are exactly what you request.

Examples from the codebase:

- `GET /v1/inquiries` -- list inquiries (optionally filtered by `?status=`)
- `GET /v1/inquiries/{id}` -- get one inquiry by ID
- `PATCH /v1/inquiries/{id}/status` -- move an inquiry to the next status
- `GET /v1/inquiries/{id}/comments` -- comments on an inquiry
- `GET /v1/notifications` -- current user's notifications
- `POST /v1/storage/upload` -- upload a file

### Injecting Dependencies

Controllers use primary constructors and inject:

- `IMessageBus` -- Wolverine mediator for commands/queries
- `ITenantContext` -- resolved tenant for the current request
- `ILogger<T>` -- required by the `[LoggerMessage]` source generator
- `ICurrentUserService` -- when the endpoint needs the user ID as a `Guid`
- Domain services directly when CQRS is not used (the Branding and ApiKeys modules)

```csharp
public partial class InquiriesController(
    IMessageBus bus,
    ITenantContext tenantContext,
    ILogger<InquiriesController> logger) : ControllerBase
```

### Accessing Current User

Never use raw `FindFirst` / `FindFirstValue` on `ClaimsPrincipal`. Two supported approaches:

**1. `ClaimsPrincipalExtensions` on `User`** (`Wallow.Shared.Kernel.Extensions`) -- returns the
raw claim values:

```csharp
string? userId = User.GetUserId();
string authorName = User.GetDisplayName() ?? "Unknown";
bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);
```

`InquiriesController` also uses `GetClientId()` to tell service accounts apart from humans:

```csharp
private string? ExtractSubmitterId()
{
    string? azp = User.GetClientId();
    if (azp is not null && azp.StartsWith("sa-", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    return User.GetUserId();
}
```

**2. `ICurrentUserService`** (`Wallow.Shared.Kernel.Services`) -- injected, returns `Guid?`:

```csharp
Guid? userId = currentUserService.GetCurrentUserId();
if (userId is null)
{
    return this.Problem(SharedErrors.Unauthenticated);
}
```

If a claim you need has no extension method, add one to `ClaimsPrincipalExtensions` rather than
reaching for `FindFirst`.

### Logging

Controllers never call `logger.LogInformation(...)` directly. Declare `private partial void`
methods with `[LoggerMessage]` at the bottom of the class and call those:

```csharp
if (result.IsSuccess)
{
    LogInquiryCreated(result.Value.Id, submitterId, tenantContext.TenantId.Value);
}

// ... at the bottom of the partial class:

[LoggerMessage(
    Level = LogLevel.Information,
    Message = "Inquiry {InquiryId} created by user {SubmitterId} under tenant {TenantId}")]
private partial void LogInquiryCreated(Guid inquiryId, string? submitterId, Guid tenantId);

[LoggerMessage(
    Level = LogLevel.Warning,
    Message = "Inquiry {InquiryId} not found for user {UserId} under tenant {TenantId}")]
private partial void LogInquiryNotFound(Guid inquiryId, string? userId, Guid tenantId);
```

### ProducesResponseType Attributes

Declare the success shape and any failure the action produces beyond the shared set:

```csharp
[HttpPost]
[HasPermission(PermissionType.InquiriesWrite)]
[ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> Submit(
    [FromBody] SubmitInquiryRequest request,
    CancellationToken cancellationToken)
{
    // ...
}
```

`ProblemResponsesConvention` (`Wallow.Shared.Api.Problems`) adds 400, 401, 403, 404, 429 and
500 to every action, so those need no attribute. An action that binds input gets its 400 as
`HttpValidationProblemDetails`; one that binds nothing gets plain `ProblemDetails`. Declare a
400 yourself only when the convention's choice would be wrong, and then always with a type:
`[ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]`.
A bare `[ProducesResponseType(StatusCodes.Status400BadRequest)]` documents a plain
`ProblemDetails` 400 and hides the validation shape the action actually sends. Statuses outside
the shared six that an action can answer (a 422 business rule, a 409 conflict) still need their
own typed declaration. Error responses are documented as `application/problem+json`, whatever the
formatter list says, because that is what the wire carries.

## Request/Response Contracts

### Location

Contracts live in the Api layer. Small modules keep them flat:

```
api/src/Modules/Inquiries/Wallow.Inquiries.Api/
├── Contracts/
│   ├── SubmitInquiryRequest.cs
│   ├── UpdateInquiryStatusRequest.cs
│   ├── AddInquiryCommentRequest.cs
│   ├── InquiryResponse.cs
│   └── InquiryCommentResponse.cs
└── Controllers/
    └── InquiriesController.cs
```

Larger modules split by direction (Identity, Storage, Announcements) or by feature area
(Notifications):

```
Wallow.Identity.Api/Contracts/
├── Requests/
│   └── CreateUserRequest.cs
└── Responses/
    └── CurrentUserResponse.cs
```

### Request Records

Use `record` types for immutable request contracts:

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/SubmitInquiryRequest.cs
namespace Wallow.Inquiries.Api.Contracts;

public sealed record SubmitInquiryRequest(
    string Name,
    string Email,
    string Phone,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
```

Note what is **not** here: `SubmitterId` and `SubmitterIpAddress`. Caller-supplied identity is
never trusted -- the controller derives the submitter from the JWT.

### Response Records

For simple responses, use a positional record:

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/InquiryResponse.cs
namespace Wallow.Inquiries.Api.Contracts;

public sealed record InquiryResponse(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? Company,
    string? SubmitterId,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

For shapes assembled from claims rather than a DTO, an init-only record works better:

```csharp
// api/src/Modules/Identity/Wallow.Identity.Api/Contracts/Responses/CurrentUserResponse.cs
namespace Wallow.Identity.Api.Contracts.Responses;

public record CurrentUserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
```

### DTOs vs Response Contracts

| Type         | Layer       | Purpose                                  |
| ------------ | ----------- | ---------------------------------------- |
| **DTO**      | Application | Internal data transfer, used by handlers |
| **Response** | Api         | API contract, may differ from DTO        |

`InquiryDto` and `InquiryResponse` deliberately differ: the DTO carries
`SubmitterIpAddress` (an internal audit field that must not leak) and uses `DateTimeOffset`.
The controller maps between them with a private static method:

```csharp
private static InquiryResponse ToInquiryResponse(InquiryDto dto) => new(
    dto.Id,
    dto.Name,
    dto.Email,
    dto.Phone,
    dto.Company,
    dto.SubmitterId,
    dto.ProjectType,
    dto.BudgetRange,
    dto.Timeline,
    dto.Message,
    dto.Status,
    dto.CreatedAt.UtcDateTime,
    dto.UpdatedAt ?? dto.CreatedAt.UtcDateTime);
```

When DTO and response are identical, return the DTO directly instead of writing a mapper.

## Commands and Queries

### Command Structure

Commands are immutable records in the Application layer:

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryCommand.cs
namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed record SubmitInquiryCommand(
    string Name,
    string Email,
    string Phone,
    string? Company,
    string? SubmitterId,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
```

### Handler Structure

A handler is normally a **`public sealed class`** taking dependencies through a primary
constructor, with a `Handle`/`HandleAsync` method; Wolverine also discovers **static** classes
whose static `HandleAsync` receives dependencies as method parameters. Either way there is no
interface to implement and no DI registration to write. The Inquiries command handlers shown
below use the static shape — a deliberate local exception in that module, kept truthful here
because the examples are real files:

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryHandler.cs
namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

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
            command.Phone,
            command.Company,
            command.SubmitterId,
            command.ProjectType,
            command.BudgetRange,
            command.Timeline,
            command.Message,
            string.Empty,
            timeProvider);

        await inquiryRepository.AddAsync(inquiry, cancellationToken);
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
```

Three conventions worth copying:

- **State changes go through the aggregate.** `Inquiry.Create(...)` raises
  `InquirySubmittedDomainEvent`; the handler never sets fields itself.
- **`TimeProvider` is injected**, never `DateTime.UtcNow` -- that is what makes the domain
  tests deterministic.
- **The handler does not publish integration events.** The domain event raised by the
  aggregate is bridged in `InquirySubmittedDomainEventHandler`, which publishes
  `Shared.Contracts.Inquiries.Events.InquirySubmittedEvent` over `IMessageBus`. Keeping the
  bridge separate is what lets other modules react without referencing Inquiries.

A handler that must look up state first returns a failure `Result` rather than throwing:

```csharp
// .../Commands/UpdateInquiryStatus/UpdateInquiryStatusHandler.cs
public static class UpdateInquiryStatusHandler
{
    public static async Task<Result<InquiryDto>> HandleAsync(
        UpdateInquiryStatusCommand command,
        IInquiryRepository inquiryRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        InquiryId inquiryId = InquiryId.Create(command.InquiryId);
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(inquiryId, cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryDto>(InquiriesErrors.InquiryNotFound);
        }

        inquiry.TransitionTo(command.NewStatus, timeProvider);
        await inquiryRepository.UpdateAsync(inquiry, cancellationToken);
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
```

### Query Handlers

Queries follow the same shape. `GetInquiriesHandler` is written as an instance class with a
`Handle` method and primary-constructor injection — the repo norm:

```csharp
// .../Queries/GetInquiries/GetInquiriesQuery.cs
public sealed record GetInquiriesQuery(InquiryStatus? Status = null);

// .../Queries/GetInquiries/GetInquiriesHandler.cs
public sealed class GetInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = query.Status is not null
            ? await inquiryRepository.GetByStatusAsync(query.Status.Value, cancellationToken)
            : await inquiryRepository.GetAllAsync(cancellationToken);

        List<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
```

Prefer the sealed-class primary-constructor form for new code; both are discovered
identically, and the static shape remains in use mostly for event handlers (and Inquiries'
command handlers, as noted above).

### Controller to Handler Flow

```csharp
[HttpPost]
[HasPermission(PermissionType.InquiriesWrite)]
[ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> Submit(
    [FromBody] SubmitInquiryRequest request,
    CancellationToken cancellationToken)
{
    string? submitterId = ExtractSubmitterId();

    // 1. Map request to command, filling identity from the token
    SubmitInquiryCommand command = new(
        request.Name,
        request.Email,
        request.Phone,
        request.Company,
        submitterId,
        request.ProjectType,
        request.BudgetRange,
        request.Timeline,
        request.Message);

    // 2. Send to the handler via Wolverine
    Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

    if (result.IsSuccess)
    {
        LogInquiryCreated(result.Value.Id, submitterId, tenantContext.TenantId.Value);
    }

    // 3. Map DTO to response, then Result to HTTP
    return result.Map(ToInquiryResponse).ToActionResult();
}
```

## Result Pattern

### Result Types

Wallow uses the Result pattern for expected failures instead of exceptions
(`Wallow.Shared.Kernel.Results`).

```csharp
// Non-generic for void operations
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success();
    public static Result Failure(Error error);

    public static Result<TValue> Success<TValue>(TValue value);
    public static Result<TValue> Failure<TValue>(Error error);
}

// Generic for operations returning values
public class Result<TValue> : Result
{
    public TValue Value { get; }  // Throws if IsFailure

    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper);
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder);
}
```

### Errors and the Error-Code Catalog

Every failure the API reports carries a **code**, a **kind**, and a **message**. The code is a
stable, machine-readable identifier the frontends narrow on; the kind decides the HTTP status;
the message is the human-readable `detail`. All three come from a **catalog entry**
(`Wallow.Shared.Kernel.Errors`):

```csharp
public sealed record ErrorCatalogEntry(string Code, ErrorKind Kind, string DefaultMessage);

public enum ErrorKind
{
    Validation,        // 400
    Unauthenticated,   // 401
    Forbidden,         // 403
    NotFound,          // 404
    MethodNotAllowed,  // 405
    Conflict,          // 409
    BusinessRule,      // 422
    RateLimited,       // 429
    Failure,           // 500
    Unavailable        // 503
}
```

`Error` and the domain exceptions are constructed **only** from an entry, with an optional
message override for the cases where the sentence must name a value. There is no bare-string
constructor and no code-prefix parsing anywhere:

```csharp
public sealed record Error(ErrorCatalogEntry entry, string? message = null)
{
    public string Code { get; }        // entry.Code
    public ErrorKind Kind { get; }     // entry.Kind
    public string Message { get; }     // message ?? entry.DefaultMessage
}

// Result has matching shortcuts
Result.Failure(entry);
Result.Failure<InquiryDto>(entry, "Inquiry 'abc' is already closed");
```

#### Catalog ownership

- **Each module owns exactly one static catalog** at
  `Wallow.<Module>.Domain/Errors/<Module>Errors.cs`: a `public static class` whose members are
  `public static readonly ErrorCatalogEntry` fields. Every code has exactly one owner; the
  aggregate is resolved when the host starts, so two catalogs declaring the same code fail
  startup in every environment.
- **`Wallow.Shared.Api` owns `SettingsErrors`** (`Settings.SystemKeyBlocked`,
  `Settings.UnknownKey`): the setting-key checks are one condition whichever module's settings
  endpoint reaches them, and `SettingKeyValidationResult.ToResult(key)` answers with them.
- **The shared kernel holds only the nine status-generic entries** in `SharedErrors`:
  `Validation.Failed`, `Auth.Unauthenticated`, `Auth.Forbidden`, `Http.NotFound`,
  `Http.MethodNotAllowed`, `Http.ClientError`, `RateLimit.Exceeded`, `Setup.Required`,
  `Server.Error`. Anything a module means specifically goes in the module's catalog, never here.
- **`Add<Module>Module` registers the catalog** as its first statement:
  `services.AddErrorCatalog(typeof(InquiriesErrors));`. The call validates the catalog eagerly
  and contributes it to the `ErrorCatalog` singleton the API aggregates.

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Domain/Errors/InquiriesErrors.cs
public static class InquiriesErrors
{
    public static readonly ErrorCatalogEntry InquiryNotFound = new(
        "Inquiry.NotFound", ErrorKind.NotFound, "Inquiry not found");

    public static readonly ErrorCatalogEntry InvalidStatusTransition = new(
        "Inquiries.InvalidStatusTransition", ErrorKind.BusinessRule,
        "The inquiry cannot move to that status");
}
```

#### Naming rule

A code is **dotted PascalCase `Area.Reason`** — one dot, both halves PascalCase, no
underscores, no digits-first segments. `ErrorCatalogEntry` rejects anything else at
construction, so a malformed code fails the module's tests rather than reaching a client.
`Area` is the aggregate or concern (`Inquiry`, `Bucket`, `Identity`, `Auth`, `Mfa`),
`Reason` says what went wrong (`NotFound`, `AlreadyExists`, `LastOwner`). Pick the kind by the
HTTP status the client should see, not by the layer that detected the problem.

#### OpenAPI export

The API emits the aggregated catalog into the v1 document as
`components.schemas.ErrorCode`: a string enum of every registered code, with each entry's
default sentence in `x-enum-descriptions`. Every `*ProblemDetails` schema references it from
`code`, gains `traceId`, loses `instance`, and requires `type`, `title`, `status`, `code` and
`traceId`, so the generated SDK types `code` as a union of known codes and the always-present
members as non-optional. A backend integration test asserts the enum equals the catalog, and
the committed `packages/sdk/openapi/v1.json` is diffed against the emitted document in CI —
adding an entry means regenerating the snapshot and the SDK client.

### Creating Results in Handlers

```csharp
// Success with value
return Result.Success(inquiry.ToDto());

// Failure straight from the catalog entry (default sentence)
return Result.Failure<InquiryDto>(InquiriesErrors.InquiryNotFound);

// Failure with an overriding message that names the offending value
return Result.Failure<FileDto>(
    StorageErrors.FileTooLarge, $"File size {size} exceeds the {limit} byte limit");

// A status-generic failure with no module-specific meaning
return Result.Failure(SharedErrors.ValidationFailed, "Name is required");
```

### Result Extensions

`ResultExtensions` in `Wallow.Shared.Api` maps Results to HTTP responses for all modules:

```csharp
// api/src/Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs
namespace Wallow.Shared.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result);
    public static IActionResult ToActionResult<T>(this Result<T> result);
    public static IActionResult ToCreatedResult<T>(this Result<T> result, string location);
    public static IActionResult ToCreatedResult<T>(
        this Result<T> result,
        string actionName,
        string controllerName,
        Func<T, object> routeValuesFactory);
    public static IActionResult ToNoContentResult(this Result result);

    private static ProblemResult ToErrorResult(Error error) =>
        new(error.Kind.ToHttpStatusCode(), error.Code, error.Message);
}
```

A failed `Result` becomes a `ProblemResult` (`Wallow.Shared.Api.Problems`): the status from
the entry's kind, the entry's code, and the message as `detail`. When it executes, the
framework's `ProblemDetailsFactory` builds the body and runs the shared customizer, so the
wire shape is the one every other failure gets (see [Problem Details Format](#problem-details-format)).

The status is derived from the entry's kind alone; the title is always the status's reason
phrase:

| `ErrorKind`        | Status | Title                 |
| ------------------ | ------ | --------------------- |
| `Validation`       | 400    | Bad Request           |
| `Unauthenticated`  | 401    | Unauthorized          |
| `Forbidden`        | 403    | Forbidden             |
| `NotFound`         | 404    | Not Found             |
| `MethodNotAllowed` | 405    | Method Not Allowed    |
| `Conflict`         | 409    | Conflict              |
| `BusinessRule`     | 422    | Unprocessable Entity  |
| `RateLimited`      | 429    | Too Many Requests     |
| `Failure`          | 500    | Internal Server Error |
| `Unavailable`      | 503    | Service Unavailable   |

#### Failing from a controller

A controller that decides a failure itself, rather than relaying a `Result`, uses the same
path. Never construct a `ProblemDetails` or call the bare `Problem(statusCode:, title:, ...)`
family — an architecture test (`ProblemWriterGuardTests`) fails the build on either:

```csharp
// A catalogued failure, with the entry's default sentence or a sentence naming the value
return this.Problem(ApiKeysErrors.ApiKeyNotFound);
return this.Problem(ApiKeysErrors.LimitReached,
    $"You have reached the maximum of {maxPerUser} API keys per user.");

// A validation failure the model binder could not see: name the field with nameof, and the
// customizer camel-cases it on the wire ("name", "branding.displayName")
ModelState.AddModelError(nameof(request.Name), "API key name is required.");
return ValidationProblem(ModelState);
```

Middleware, which has no controller, writes through `IProblemDetailsService` with the
`TryWriteProblemAsync(entry, detail?)` and `TryWriteValidationProblemAsync(errors)` helpers in
`ProblemDetailsServiceExtensions`.

### Using Map for Transformations

```csharp
// Transform DTO to response before converting to an action result
Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

return result.Map(ToInquiryResponse).ToActionResult();

// Map a collection, casting to the interface the endpoint advertises
return result.Map(inquiries =>
    (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
    .ToActionResult();
```

## Validation

### FluentValidation Setup

Wolverine validates commands using FluentValidation middleware before the handler runs:

```csharp
// api/src/Wallow.Api/Program.cs
builder.Host.UseWolverine(opts =>
{
    // FluentValidation middleware — validates commands before handlers.
    // ExplicitRegistration is REQUIRED, not a preference: see below.
    opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
});
```

**`RegistrationBehavior.ExplicitRegistration` is load-bearing.** Each module already registers its
own validators (`AddXApplication` → `AddValidatorsFromAssembly`), so Wolverine must not also scan
for them: its scan appends registrations with a plain `IServiceCollection.Add`, leaving two
`IValidator<T>` entries per command. Two registrations flip `FluentValidationPolicy` from
`ExecuteOne(IValidator<T>)` to `ExecuteMany(IEnumerable<IValidator<T>>)`, and that enumerable is
service-located from the root provider — which throws
_"Cannot resolve scoped service 'IEnumerable<IValidator&lt;T&gt;>' from root provider"_ under
Development scope validation. Drop the argument and the app fails to start.

Each module registers its validators by assembly scan:

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Application/Extensions/ApplicationExtensions.cs
public static class ApplicationExtensions
{
    public static IServiceCollection AddInquiriesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

### Validator File Organization

Validators live alongside their commands:

```
api/src/Modules/Inquiries/Wallow.Inquiries.Application/
└── Commands/
    └── SubmitInquiry/
        ├── SubmitInquiryCommand.cs
        ├── SubmitInquiryHandler.cs
        └── SubmitInquiryValidator.cs
```

### Writing Validators

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/SubmitInquiry/SubmitInquiryValidator.cs
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
            .EmailAddress().WithMessage("A valid email address is required")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(5000).WithMessage("Message must not exceed 5000 characters");
    }
}
```

Strongly-typed IDs need `Must` rather than `NotEmpty`, since the record wrapper is never null:

```csharp
// .../Commands/AddInquiryComment/AddInquiryCommentValidator.cs
RuleFor(x => x.InquiryId)
    .Must(id => id.Value != Guid.Empty).WithMessage("Inquiry ID is required");
```

### Validation Failure Response

A failing validator throws `FluentValidation.ValidationException`, which `GlobalExceptionHandler`
turns into the same `400 Bad Request` body the model binder produces for an invalid request.
`code` is always `Validation.Failed`, `detail` is one fixed sentence, and the field messages
live under `errors` as a dictionary keyed by the camelCase member path — dots preserved, so
`Branding.DisplayName` becomes `branding.displayName`:

```json
{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request is invalid.",
  "code": "Validation.Failed",
  "traceId": "00-abc123",
  "errors": {
    "name": ["Name is required"],
    "branding.displayName": ["Display name is required"]
  }
}
```

`errors` appears only on validation problems; a 400 with a different `code` (`Http.ClientError`
for a malformed request, or a module's `Validation`-kind entry) carries no `errors` member.

## Error Handling

### Global Exception Handler

Unexpected exceptions are caught by `GlobalExceptionHandler`
(`api/src/Wallow.Api/Middleware/GlobalExceptionHandler.cs`), which implements `IExceptionHandler`.
It logs the error through a `[LoggerMessage]` method, maps the exception type to a status and
code, and writes the body through `IProblemDetailsService` so it is customized like every other
problem. A 5xx `detail` is always the one generic sentence, in every environment; only in
Development does the body also carry an `exception` member with the type, message and stack.

### Exception to Status Code Mapping

A `DomainException` (`EntityNotFoundException`, `BusinessRuleException`,
`ForbiddenAccessException`, and module-specific subclasses) carries a catalog entry, so its
status comes from the entry's kind exactly as a failed `Result` does; the response `code` is
the entry's code, and one entry renders identically whichever way it surfaced. Framework and
runtime exceptions map by type:

| Exception Type                                | HTTP Status | `code`                       |
| --------------------------------------------- | ----------- | ---------------------------- |
| `DomainException` with kind `NotFound`        | 404         | the entry's code             |
| `DomainException` with kind `BusinessRule`    | 422         | the entry's code             |
| `DomainException` with kind `Forbidden`       | 403         | the entry's code             |
| `DomainException` with kind `Validation`      | 400         | the entry's code             |
| `ValidationException` (FluentValidation)      | 400         | `Validation.Failed` + errors |
| `UnauthorizedAccessException`                 | 401         | `Auth.Unauthenticated`       |
| `ArgumentException` / `ArgumentNullException` | 400         | `Http.ClientError`           |
| `OperationCanceledException`, client gone     | 499         | no body                      |
| `BadHttpRequestException` (4xx)               | its status  | `Http.ClientError`           |
| Other exceptions                              | 500         | `Server.Error`               |

Client cancellations are logged at Information and explicitly **not** marked as a failed span,
so an abandoned request does not show up as an error in tracing. A cancellation whose client is
still connected is a server-side fault and takes the 500 row.

### Problem Details Format

Every error response — a failed `Result`, a thrown exception, an invalid model, a 404 for an
unmatched route, a 401/403 from authentication, a 429 from the rate limiter, a 503 from the
setup gate — is `application/problem+json` with one shape:

```json
{
  "type": "about:blank",
  "title": "Not Found",
  "status": 404,
  "detail": "Inquiry not found",
  "code": "Inquiry.NotFound",
  "traceId": "00-abc123"
}
```

- `type`, `title`, `status`, `code`, `traceId` are always present. `type` is `about:blank`
  and `title` is the status's reason phrase; the meaning lives in `code`.
- `detail` is a user-safe sentence on every 4xx and one fixed generic sentence on every 5xx,
  in every environment. Nothing from an exception, a store, or a driver reaches it.
- `errors` appears only on validation problems (see
  [Validation Failure Response](#validation-failure-response)).
- `exception` appears only in Development, only on 5xx.
- `instance`, `api` and `version` are never written.

One place produces that shape: `ProblemContract.Customize` in `Wallow.Shared.Api.Problems`,
registered as the `ProblemDetailsOptions.CustomizeProblemDetails` hook. It runs for every
problem the framework's `ProblemDetailsFactory` or `IProblemDetailsService` builds, which is
why product code must reach the body only through `this.Problem(entry)`,
`ValidationProblem(ModelState)`, `result.ToActionResult()`, or the `TryWriteProblemAsync`
helpers — never a hand-built `ProblemDetails` or a direct `WriteAsJsonAsync`. The customizer
fills a missing `code` from the status-generic `SharedErrors` table (`Http.NotFound`,
`Auth.Unauthenticated`, …), so even a problem the framework raised on its own carries one.

### When to Throw vs Return Result

| Scenario                              | Approach                                                                            |
| ------------------------------------- | ----------------------------------------------------------------------------------- |
| Entity not found (expected)           | Return `Result.Failure(<Module>Errors.<Entity>NotFound)`                            |
| Validation failure (expected)         | Return `Result.Failure(<Module>Errors.<Reason>, message)` with a `Validation` entry |
| Invalid domain state transition       | Throw from the aggregate (e.g. `InvalidInquiryStatusTransitionException`)           |
| Business rule violation (expected)    | Return `Result.Failure(entry)` or throw `BusinessRuleException(entry)`              |
| Programming error (unexpected)        | Throw exception (caught by GlobalExceptionHandler)                                  |
| External service failure (unexpected) | Let exception propagate or wrap and rethrow                                         |

## Authentication and Authorization

### Controller-Level Authorization

```csharp
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/inquiries")]
[Authorize]  // Requires any authenticated caller
public partial class InquiriesController : ControllerBase
{
    // ...
}
```

### Permission-Based Authorization

Use `[HasPermission]` for fine-grained access control:

```csharp
using Wallow.Shared.Kernel.Identity.Authorization;

[HttpPost]
[HasPermission(PermissionType.InquiriesWrite)]
public async Task<IActionResult> Submit(...)

[HttpGet]
[HasPermission(PermissionType.InquiriesRead)]
public async Task<IActionResult> GetAll(...)

[HttpPatch("{id:guid}/status")]
[HasPermission(PermissionType.InquiriesWrite)]
public async Task<IActionResult> UpdateStatus(...)
```

Some endpoints are deliberately left without `[HasPermission]` because they serve _both_ staff
and the person who filed the inquiry. Those check the permission in code and fall back to
ownership:

```csharp
bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);

if (!hasReadPermission)
{
    string? submitterId = ExtractSubmitterId();
    if (submitterId is null || result.Value.SubmitterId != submitterId)
    {
        LogInquiryAccessDenied(id, userId, submitterId, result.Value.SubmitterId);
        return NotFound();
    }
}
```

Returning `NotFound()` rather than `Forbid()` is intentional: it does not reveal that the
record exists.

### Available Permissions

Permissions are string constants in `PermissionType`
(`Wallow.Shared.Kernel.Identity.Authorization`):

```csharp
public static class PermissionType
{
    public const string UsersRead = "UsersRead";
    public const string UsersCreate = "UsersCreate";
    public const string RolesUpdate = "RolesUpdate";
    public const string StorageRead = "StorageRead";
    public const string StorageWrite = "StorageWrite";
    public const string InquiriesRead = "InquiriesRead";
    public const string InquiriesWrite = "InquiriesWrite";
    public const string AdminAccess = "AdminAccess";
    // ... more permissions
}
```

### How Authorization Works

1. **JWT contains roles** -- OpenIddict issues a JWT with role claims
2. **Permission expansion** -- `PermissionExpansionMiddleware` maps roles to permission claims
3. **Authorization check** -- `PermissionAuthorizationHandler` checks the permission claim

```
JWT: { "roles": ["admin"] }
    ↓
PermissionExpansionMiddleware: admin → [UsersRead, UsersCreate, UsersUpdate, ...]
    ↓
[HasPermission(UsersCreate)] checks for "UsersCreate" claim
```

### Accessing Tenant Context

For multi-tenant operations, inject `ITenantContext` via the primary constructor and read
`tenantContext.TenantId.Value`. The tenant is resolved automatically from the JWT by
`TenantResolutionMiddleware`, and each module's `DbContext` applies a tenant query filter, so
handlers do not filter by tenant themselves.

## Adding a New Endpoint

### Checklist

1. **Define the contract** (if the endpoint takes a body)

   ```
   api/src/Modules/{Module}/Wallow.{Module}.Api/Contracts/{Name}Request.cs
   ```

2. **Create the command/query**

   ```
   api/src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Command.cs
   ```

3. **Create the validator** (for commands)

   ```
   api/src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Validator.cs
   ```

4. **Create the handler**

   ```
   api/src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Handler.cs
   ```

5. **Add the endpoint to the controller**
   - HTTP method attribute and `[HasPermission]`
   - `ProducesResponseType` attributes
   - Map request to command, filling identity from the token
   - Invoke via `bus.InvokeAsync`
   - Map result to response

6. **Add tests** -- validator, handler, and controller (see [Testing Endpoints](#testing-endpoints))

### Worked Example: "Add Comment to Inquiry"

This is the real `POST /v1/inquiries/{id}/comments` vertical, top to bottom.

**1. Request contract:**

```csharp
// api/src/Modules/Inquiries/Wallow.Inquiries.Api/Contracts/AddInquiryCommentRequest.cs
namespace Wallow.Inquiries.Api.Contracts;

public sealed record AddInquiryCommentRequest(
    string Content,
    bool IsInternal);
```

**2. Command** -- note it carries the author, which the request does not:

```csharp
// .../Application/Commands/AddInquiryComment/AddInquiryCommentCommand.cs
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public sealed record AddInquiryCommentCommand(
    InquiryId InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal);
```

**3. Validator:**

```csharp
// .../Application/Commands/AddInquiryComment/AddInquiryCommentValidator.cs
using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public sealed class AddInquiryCommentValidator : AbstractValidator<AddInquiryCommentCommand>
{
    public AddInquiryCommentValidator()
    {
        RuleFor(x => x.InquiryId)
            .Must(id => id.Value != Guid.Empty).WithMessage("Inquiry ID is required");

        RuleFor(x => x.AuthorId)
            .NotEmpty().WithMessage("Author ID is required");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required")
            .MaximumLength(200).WithMessage("Author name must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required")
            .MaximumLength(5000).WithMessage("Content must not exceed 5000 characters");
    }
}
```

**4. Handler** -- returns the new strongly-typed ID, not a full DTO:

```csharp
// .../Application/Commands/AddInquiryComment/AddInquiryCommentHandler.cs
namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public static class AddInquiryCommentHandler
{
    public static async Task<Result<InquiryCommentId>> HandleAsync(
        AddInquiryCommentCommand command,
        IInquiryCommentRepository commentRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        InquiryComment comment = InquiryComment.Create(
            command.InquiryId,
            command.AuthorId,
            command.AuthorName,
            command.Content,
            command.IsInternal,
            timeProvider);

        await commentRepository.AddAsync(comment, cancellationToken);
        await commentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(comment.Id);
    }
}
```

**5. Controller endpoint:**

```csharp
[HttpPost("{id:guid}/comments")]
[HasPermission(PermissionType.InquiriesWrite)]
[ProducesResponseType(StatusCodes.Status201Created)]
public async Task<IActionResult> AddComment(
    Guid id,
    [FromBody] AddInquiryCommentRequest request,
    CancellationToken cancellationToken)
{
    string authorId = User.GetUserId() ?? string.Empty;
    string authorName = User.GetDisplayName() ?? "Unknown";

    AddInquiryCommentCommand command = new(
        InquiryId.Create(id),
        authorId,
        authorName,
        request.Content,
        request.IsInternal);

    Result<InquiryCommentId> result = await bus.InvokeAsync<Result<InquiryCommentId>>(command, cancellationToken);

    if (result.IsFailure)
    {
        return result.ToActionResult();
    }

    return Created($"{id}/comments/{result.Value.Value}", new { Id = result.Value.Value });
}
```

## File Upload Endpoints

For file uploads, use `multipart/form-data`. From
`api/src/Modules/Storage/Wallow.Storage.Api/Controllers/StorageController.cs`:

```csharp
/// <summary>
/// Upload a file.
/// </summary>
[HttpPost("upload")]
[HasPermission(PermissionType.StorageWrite)]
[EnableRateLimiting("upload")]
[RequestSizeLimit(100 * 1024 * 1024)] // 100MB
[Consumes("multipart/form-data")]
[ProducesResponseType(typeof(UploadResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Upload(
    IFormFile file,
    [FromForm] string bucket,
    [FromForm] string? path = null,
    [FromForm] bool isPublic = false,
    CancellationToken cancellationToken = default)
{
    if (file.Length == 0)
    {
        ModelState.AddModelError(nameof(file), "The uploaded file is empty.");
        return ValidationProblem(ModelState);
    }

    Guid? userId = currentUserService.GetCurrentUserId();
    if (userId is null)
    {
        return this.Problem(SharedErrors.Unauthenticated);
    }

    await using Stream stream = file.OpenReadStream();

    UploadFileCommand command = new(
        tenantContext.TenantId.Value,
        userId.Value,
        bucket,
        file.FileName,
        file.ContentType,
        stream,
        file.Length,
        path,
        isPublic);

    Result<UploadResult> result = await bus.InvokeAsync<Result<UploadResult>>(command, cancellationToken);

    if (!result.IsSuccess)
    {
        return result.ToActionResult();
    }

    return result.Map(ToUploadResponse)
        .ToCreatedResult($"/v1/storage/files/{result.Value.FileId}");
}
```

Note that the `Location` header passed to `ToCreatedResult` uses the same prefix-free path the
controllers declare.

## Common Patterns

### Pagination

`PagedResult<T>` (`Wallow.Shared.Kernel.Pagination`) carries items plus `TotalCount`, `Page`,
and `PageSize`, and computes `TotalPages` / `HasNextPage` / `HasPreviousPage`. Page the DTOs in
the query handler, then map items to responses in the controller:

```csharp
// GET /v1/storage/files
[HttpGet("files")]
[HasPermission(PermissionType.StorageRead)]
[ProducesResponseType(typeof(PagedResult<FileMetadataResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> ListFiles(
    [FromQuery] string bucket,
    [FromQuery] string? path = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
{
    Result<PagedResult<StoredFileDto>> result = await bus.InvokeAsync<Result<PagedResult<StoredFileDto>>>(
        new GetFilesByBucketQuery(bucket, path, page, pageSize), cancellationToken);

    return result.Map(paged => new PagedResult<FileMetadataResponse>(
            paged.Items.Select(ToFileMetadataResponse).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize))
        .ToActionResult();
}
```

### Filtering by Query String

Parse loosely-typed query values in the controller and hand the domain enum to the query:

```csharp
// GET /v1/inquiries?status=Reviewed
[HttpGet]
[HasPermission(PermissionType.InquiriesRead)]
[ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(
    [FromQuery] string? status,
    CancellationToken cancellationToken)
{
    InquiryStatus? parsedStatus = null;
    if (!string.IsNullOrWhiteSpace(status)
        && Enum.TryParse<InquiryStatus>(status, ignoreCase: true, out InquiryStatus parsed))
    {
        parsedStatus = parsed;
    }

    Result<IReadOnlyList<InquiryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
        new GetInquiriesQuery(parsedStatus), cancellationToken);

    return result.Map(inquiries =>
        (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
        .ToActionResult();
}
```

### Nested Resources

```csharp
// GET /v1/inquiries/{id}/comments
[HttpGet("{id:guid}/comments")]
[ProducesResponseType(typeof(IReadOnlyList<InquiryCommentResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
{
    bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);
    bool includeInternal = hasReadPermission;

    IReadOnlyList<InquiryCommentDto> comments = await bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(
        new GetInquiryCommentsQuery(InquiryId.Create(id), includeInternal), cancellationToken);

    IReadOnlyList<InquiryCommentResponse> response = comments
        .Select(ToInquiryCommentResponse)
        .ToList();

    return Ok(response);
}
```

### Action Endpoints (RPC-style)

For operations that do not fit REST -- here a guarded state transition:

```csharp
// PATCH /v1/inquiries/{id}/status
[HttpPatch("{id:guid}/status")]
[HasPermission(PermissionType.InquiriesWrite)]
[ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> UpdateStatus(
    Guid id,
    [FromBody] UpdateInquiryStatusRequest request,
    CancellationToken cancellationToken)
{
    if (!Enum.TryParse<InquiryStatus>(request.NewStatus, ignoreCase: true, out InquiryStatus newStatus))
    {
        ModelState.AddModelError(nameof(request.NewStatus), $"Invalid status value: '{request.NewStatus}'.");
        return ValidationProblem(ModelState);
    }

    Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
        new UpdateInquiryStatusCommand(id, newStatus), cancellationToken);

    return result.Map(ToInquiryResponse).ToActionResult();
}
```

### Redirect Responses

```csharp
// GET /v1/storage/files/{id}/download — redirects to a presigned URL
[HttpGet("files/{id:guid}/download")]
[HasPermission(PermissionType.StorageRead)]
[ProducesResponseType(StatusCodes.Status302Found)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
{
    Result<PresignedUrlResult> result = await bus.InvokeAsync<Result<PresignedUrlResult>>(
        new GetPresignedUrlQuery(id), cancellationToken);

    if (result.IsFailure)
    {
        return result.ToActionResult();
    }

    return Redirect(result.Value.Url);
}
```

## Testing Endpoints

See [testing.md](testing.md) for the full picture; the E2E layer is covered in
[testing-e2e.md](testing-e2e.md). Run the backend suite with `./scripts/run-tests.sh inquiries`.

Each layer of the vertical gets its own test class. **Validators** use FluentValidation's
`TestHelper`:

```csharp
// api/tests/Modules/Inquiries/Wallow.Inquiries.Tests/Application/Commands/SubmitInquiry/SubmitInquiryValidatorTests.cs
public class SubmitInquiryValidatorTests
{
    private readonly SubmitInquiryValidator _validator = new();

    private static SubmitInquiryCommand Valid() =>
        new("John Doe", "john@example.com", "555-0100", "Acme", null,
            "Web Application", "$10k - $50k", "3 months", "We need help building our platform.");

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        SubmitInquiryCommand command = Valid() with { Email = "not-an-email" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
```

**Handlers** need no host and no container either — the test constructs an instance handler
(or, as with Inquiries' static handlers here, calls the static method directly) with an
NSubstitute repository:

```csharp
// .../Application/Commands/SubmitInquiry/SubmitInquiryHandlerTests.cs
[Fact]
public async Task HandleAsync_WithValidData_ReturnsSuccessWithDto()
{
    IInquiryRepository repo = Substitute.For<IInquiryRepository>();
    SubmitInquiryCommand command = BuildCommand();

    Result<InquiryDto> result = await SubmitInquiryHandler.HandleAsync(
        command, repo, TimeProvider.System, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    result.Value.Email.Should().Be(command.Email);
    await repo.Received(1).AddAsync(Arg.Any<Inquiry>(), Arg.Any<CancellationToken>());
}
```

**Controllers** are tested against a substituted `IMessageBus`, which keeps the assertions on
mapping and status codes rather than on business logic:

```csharp
// .../Api/Controllers/InquiriesControllerTests.cs
[Fact]
public async Task Submit_WithValidRequest_ReturnsOk()
{
    InquiryDto dto = CreateDto();
    _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<SubmitInquiryCommand>(), Arg.Any<CancellationToken>())
        .Returns(Result.Success(dto));

    SubmitInquiryRequest request = new(
        "John Doe", "john@example.com", "555-0100", "Acme",
        "Website", "$10k", "3 months", "We need a website.");

    IActionResult result = await _controller.Submit(request, CancellationToken.None);

    OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
    InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
    response.Email.Should().Be("john@example.com");
}
```

Authorization branches deserve their own tests -- `InquiriesControllerAuthTests` seeds a
`ClaimsPrincipal` with (and without) `InquiriesRead` to prove that a non-owner gets a 404 from
`GetById`.

## Related Documentation

- [Developer guide](../getting-started/developer-guide.md) -- Overall development workflow
- [Testing guide](testing.md) -- Testing patterns and fixtures
- [E2E testing](testing-e2e.md) -- Playwright suites for the frontends
- [Module creation guide](../architecture/module-creation.md) -- Creating new modules
- [Authorization guide](../architecture/authorization.md) -- Permission system details
