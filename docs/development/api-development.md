# API Development Guide

This guide covers how to build APIs in Wallow, from controller patterns to error handling.

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
- Handlers return `Result<T>` instead of throwing exceptions for expected failures
- FluentValidation validates commands before handlers execute
- Global exception handling catches unexpected errors
- Routes are versioned: `api/v{version:apiVersion}/{module}/{resource}`

## Controller Patterns

### Basic Controller Structure

```csharp
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inquiries/submissions")]
[Authorize]
[Tags("Submissions")]
[Produces("application/json")]
[Consumes("application/json")]
public class SubmissionsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{
    // ... endpoints
}
```

### Standard Attributes

Every controller should include:

| Attribute | Purpose |
|-----------|---------|
| `[ApiController]` | Enables automatic model validation and binding |
| `[ApiVersion(1)]` | API version number |
| `[Route("api/v{version:apiVersion}/{module}/{resource}")]` | Versioned RESTful route pattern |
| `[Authorize]` | Requires authentication (JWT or API key) |
| `[Tags("...")]` | OpenAPI grouping for Scalar documentation |
| `[Produces("application/json")]` | Response content type |
| `[Consumes("application/json")]` | Request content type |

### Route Conventions

```
GET    /api/v1/{module}/{resources}              List all
GET    /api/v1/{module}/{resources}/{id}         Get by ID
POST   /api/v1/{module}/{resources}              Create
PUT    /api/v1/{module}/{resources}/{id}         Update
DELETE /api/v1/{module}/{resources}/{id}         Delete
POST   /api/v1/{module}/{resources}/{id}/action  Custom action
```

Examples from the codebase:
- `GET /api/v1/inquiries/submissions` - List submissions
- `GET /api/v1/inquiries/submissions/{id}` - Get submission by ID
- `POST /api/v1/inquiries/submissions/{id}/close` - Close a submission
- `GET /api/v1/notifications/notifications` - Get user notifications

### Injecting Dependencies

Controllers use primary constructors and inject:
- `IMessageBus` -- Wolverine mediator for commands/queries
- `ICurrentUserService` -- Current user ID and context from JWT
- Domain services directly when CQRS is not used (Identity module pattern)

```csharp
// Standard pattern: primary constructor with Wolverine + user service
public class SubmissionsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{
    // ...
}
```

### Accessing Current User

Use `ICurrentUserService` (injected via primary constructor) to access the current user. Never use raw `FindFirst` / `FindFirstValue` on `ClaimsPrincipal`.

```csharp
Guid? userId = currentUserService.GetCurrentUserId();
```

For claim access outside controllers, use the extension methods in `ClaimsPrincipalExtensions` from `Wallow.Shared.Kernel.Extensions` (e.g., `GetUserId()`, `GetEmail()`, `GetRoles()`).

### ProducesResponseType Attributes

Document all possible response types for OpenAPI:

```csharp
/// <summary>
/// Create a new submission.
/// </summary>
[HttpPost]
[ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Create(
    [FromBody] CreateSubmissionRequest request,
    CancellationToken ct)
{
    // ...
}

/// <summary>
/// Get a specific submission by ID.
/// </summary>
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
{
    // ...
}
```

## Request/Response Contracts

### Location

Contracts live in the Api layer:
```
src/Modules/{Module}/Wallow.{Module}.Api/
├── Contracts/
│   ├── Requests/
│   │   ├── CreateItemRequest.cs
│   │   └── UpdateItemRequest.cs
│   └── Responses/         (optional - for complex responses)
│       └── ItemResponse.cs
└── Controllers/
```

Some modules organize by feature:
```
Contracts/
├── Submissions/
│   ├── CreateSubmissionRequest.cs
│   ├── UpdateSubmissionRequest.cs
│   └── SubmissionResponse.cs
└── Forms/
    └── CreateFormRequest.cs
```

### Request Records

Use `record` types for immutable request contracts:

```csharp
namespace Wallow.Inquiries.Api.Contracts.Submissions;

public sealed record CreateSubmissionRequest(
    string Subject,
    string Body,
    string? ContactEmail);
```

### Response Records

For simple responses, return DTOs directly. For complex API-specific shapes, create response records:

```csharp
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

| Type | Layer | Purpose |
|------|-------|---------|
| **DTO** | Application | Internal data transfer, used by handlers |
| **Response** | Api | API contract, may differ from DTO |

When they match, return DTOs directly. When they differ (naming, structure, or additional fields), map DTO to Response:

```csharp
// In controller - map DTO to response
private static SubmissionResponse ToSubmissionResponse(SubmissionDto dto) => new(
    dto.Id,
    dto.UserId,
    dto.Subject,
    dto.Body,
    dto.Status,
    dto.ContactEmail,
    dto.CreatedAt,
    dto.UpdatedAt);
```

## Commands and Queries

### Command Structure

Commands are immutable records in the Application layer:

```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/CreateSubmission/CreateSubmissionCommand.cs
namespace Wallow.Inquiries.Application.Commands.CreateSubmission;

public sealed record CreateSubmissionCommand(
    Guid UserId,
    string Subject,
    string Body,
    string? ContactEmail = null);
```

### Handler Structure

Handlers use primary constructor injection and return `Result<T>`:

```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/CreateSubmission/CreateSubmissionHandler.cs
namespace Wallow.Inquiries.Application.Commands.CreateSubmission;

public sealed class CreateSubmissionHandler(
    ISubmissionRepository submissionRepository,
    IMessageBus messageBus)
{
    public async Task<Result<SubmissionDto>> Handle(
        CreateSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        // Create aggregate
        var submission = Submission.Create(
            command.UserId,
            command.Subject,
            command.Body,
            command.ContactEmail);

        // Persist
        submissionRepository.Add(submission);
        await submissionRepository.SaveChangesAsync(cancellationToken);

        // Publish events
        await messageBus.PublishAsync(new AuditEntryRequestedEvent { ... });

        // Return DTO
        return Result.Success(submission.ToDto());
    }
}
```

### Controller to Handler Flow

```csharp
[HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateSubmissionRequest request,
    CancellationToken cancellationToken)
{
    Guid? userId = currentUserService.GetCurrentUserId();

    // 1. Map request to command
    var command = new CreateSubmissionCommand(
        userId!.Value,
        request.Subject,
        request.Body,
        request.ContactEmail);

    // 2. Send to handler via Wolverine
    var result = await bus.InvokeAsync<Result<SubmissionDto>>(command, cancellationToken);

    // 3. Map result to HTTP response
    return result.Map(ToSubmissionResponse)
        .ToCreatedResult($"/api/inquiries/submissions/{result.Value?.Id}");
}
```

## Result Pattern

### Result Types

Wallow uses the Result pattern for expected failures instead of exceptions.

```csharp
// Non-generic for void operations
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success();
    public static Result Failure(Error error);
}

// Generic for operations returning values
public class Result<TValue> : Result
{
    public TValue Value { get; }  // Throws if IsFailure

    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper);
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder);
}
```

### Error Types

```csharp
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    // Factory methods
    public static Error NotFound(string entity, object id);
    public static Error Validation(string message);
    public static Error Validation(string code, string message);
    public static Error Conflict(string message);
    public static Error Unauthorized(string message = "Unauthorized access");
    public static Error Forbidden(string message = "Access denied");
}
```

### Creating Results in Handlers

```csharp
// Success with value
return Result.Success(submission.ToDto());

// Failure with error
return Result.Failure<SubmissionDto>(
    Error.NotFound("Submission", submissionId));

return Result.Failure<SubmissionDto>(
    Error.Validation("Subject must not be empty"));

return Result.Failure<SubmissionDto>(
    Error.Conflict($"Submission '{subject}' already exists"));
```

### Result Extensions

`ResultExtensions` in `Wallow.Shared.Api` maps Results to HTTP responses for all modules:

```csharp
// src/Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs
namespace Wallow.Shared.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToCreatedResult<T>(this Result<T> result, string location)
    {
        if (result.IsSuccess)
            return new CreatedResult(location, result.Value);

        return ToErrorResult(result.Error);
    }

    private static IActionResult ToErrorResult(Error error)
    {
        var statusCode = error.Code switch
        {
            _ when error.Code.EndsWith(".NotFound") => StatusCodes.Status404NotFound,
            _ when error.Code.StartsWith("Validation") => StatusCodes.Status400BadRequest,
            _ when error.Code.StartsWith("Unauthorized") => StatusCodes.Status401Unauthorized,
            _ when error.Code.StartsWith("Forbidden") => StatusCodes.Status403Forbidden,
            _ when error.Code.StartsWith("Conflict") => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Detail = error.Message,
            Extensions = { ["code"] = error.Code }
        };

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
```

### Using Map for Transformations

```csharp
// Transform DTO to response before converting to action result
var result = await bus.InvokeAsync<Result<SubmissionDto>>(command, cancellationToken);

return result.Map(ToSubmissionResponse)
    .ToCreatedResult($"/api/inquiries/submissions/{result.Value?.Id}");

// Chain multiple transformations
return result
    .Map(submissions => submissions.Select(ToSubmissionResponse).ToList())
    .Map(responses => (IReadOnlyList<SubmissionResponse>)responses)
    .ToActionResult();
```

## Validation

### FluentValidation Setup

Wolverine automatically validates commands using FluentValidation middleware:

```csharp
// In Program.cs
builder.Host.UseWolverine(opts =>
{
    opts.UseFluentValidation();  // Validates commands before handlers
});
```

Each module registers its validators:

```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Extensions/ApplicationExtensions.cs
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
src/Modules/Inquiries/Wallow.Inquiries.Application/
└── Commands/
    └── CreateSubmission/
        ├── CreateSubmissionCommand.cs
        ├── CreateSubmissionHandler.cs
        └── CreateSubmissionValidator.cs
```

### Writing Validators

```csharp
using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.CreateSubmission;

public sealed class CreateSubmissionValidator : AbstractValidator<CreateSubmissionCommand>
{
    public CreateSubmissionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(200).WithMessage("Subject must not exceed 200 characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .MaximumLength(5000).WithMessage("Body must not exceed 5000 characters");
    }
}
```

### Validation Failure Response

When validation fails, Wolverine returns a `400 Bad Request` with validation errors:

```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "errors": {
        "Subject": ["Subject is required"],
        "Body": ["Body is required"]
    }
}
```

## Error Handling

### Global Exception Handler

Unexpected exceptions are caught by `GlobalExceptionHandler` (`src/Wallow.Api/Middleware/GlobalExceptionHandler.cs`), which implements `IExceptionHandler`. It logs the error, maps the exception type to an HTTP status code, and returns a Problem Details response.

### Exception to Status Code Mapping

| Exception Type | HTTP Status | Title |
|----------------|-------------|-------|
| `EntityNotFoundException` | 404 | Resource Not Found |
| `BusinessRuleException` | 422 | Business Rule Violation |
| `UnauthorizedAccessException` | 401 | Unauthorized |
| `ArgumentException` | 400 | Bad Request |
| Other exceptions | 500 | Internal Server Error |

### Problem Details Format (RFC 7807)

All error responses use Problem Details:

```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
    "title": "Resource Not Found",
    "status": 404,
    "detail": "Submission with ID '123' was not found",
    "instance": "/errors/00-abc123",
    "traceId": "00-abc123",
    "code": "Submission.NotFound"
}
```

### When to Throw vs Return Result

| Scenario | Approach |
|----------|----------|
| Entity not found (expected) | Return `Result.Failure(Error.NotFound(...))` |
| Validation failure (expected) | Return `Result.Failure(Error.Validation(...))` |
| Business rule violation (expected) | Return `Result.Failure(...)` or throw `BusinessRuleException` |
| Programming error (unexpected) | Throw exception (caught by GlobalExceptionHandler) |
| External service failure (unexpected) | Let exception propagate or wrap and rethrow |

## Authentication and Authorization

### Controller-Level Authorization

```csharp
[ApiController]
[Route("api/identity/users")]
[Authorize]  // Requires any authenticated user
public class UsersController : ControllerBase
{
    // ...
}
```

### Permission-Based Authorization

Use `[HasPermission]` for fine-grained access control:

```csharp
using Wallow.Shared.Kernel.Identity.Authorization;

[HttpGet]
[HasPermission(PermissionType.UsersRead)]
public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(...)

[HttpPost]
[HasPermission(PermissionType.UsersCreate)]
public async Task<ActionResult> CreateUser(CreateUserRequest request, ...)

[HttpPost("{userId:guid}/roles")]
[HasPermission(PermissionType.RolesUpdate)]
public async Task<ActionResult> AssignRole(Guid userId, ...)
```

### Available Permissions

Permissions are defined as string constants in `PermissionType` (`Wallow.Shared.Kernel.Identity.Authorization`):

```csharp
public static class PermissionType
{
    public const string UsersRead = "UsersRead";
    public const string UsersCreate = "UsersCreate";
    public const string InquiriesRead = "InquiriesRead";
    public const string InquiriesWrite = "InquiriesWrite";
    public const string AdminAccess = "AdminAccess";
    // ... more permissions
}
```

### How Authorization Works

1. **JWT contains roles** - OpenIddict issues JWT with role claims
2. **Permission expansion** - `PermissionExpansionMiddleware` maps roles to permissions
3. **Authorization check** - `PermissionAuthorizationHandler` checks permission claims

```
JWT: { "roles": ["admin"] }
    ↓
PermissionExpansionMiddleware: admin → [UsersRead, UsersCreate, UsersUpdate, ...]
    ↓
[HasPermission(UsersCreate)] checks for "UsersCreate" claim
```

### Accessing Tenant Context

For multi-tenant operations, inject `ITenantContext` via the primary constructor. The tenant is resolved automatically from the JWT by `TenantResolutionMiddleware`.

## Adding a New Endpoint

### Checklist

1. **Define the contract** (if needed)
   ```
   src/Modules/{Module}/Wallow.{Module}.Api/Contracts/{Feature}/{Name}Request.cs
   ```

2. **Create the command/query**
   ```
   src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Command.cs
   ```

3. **Create the validator** (for commands)
   ```
   src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Validator.cs
   ```

4. **Create the handler**
   ```
   src/Modules/{Module}/Wallow.{Module}.Application/Commands/{Name}/{Name}Handler.cs
   ```

5. **Add the endpoint to controller**
   - XML doc comment
   - HTTP method attribute
   - ProducesResponseType attributes
   - Map request to command
   - Invoke via `bus.InvokeAsync`
   - Map result to response

### Example: Adding "Close Submission" Endpoint

**1. No request contract needed** (ID comes from route)

**2. Command:**
```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/CloseSubmission/CloseSubmissionCommand.cs
namespace Wallow.Inquiries.Application.Commands.CloseSubmission;

public sealed record CloseSubmissionCommand(Guid SubmissionId, Guid ClosedBy);
```

**3. Validator:**
```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/CloseSubmission/CloseSubmissionValidator.cs
public sealed class CloseSubmissionValidator : AbstractValidator<CloseSubmissionCommand>
{
    public CloseSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ClosedBy).NotEmpty();
    }
}
```

**4. Handler:**
```csharp
// src/Modules/Inquiries/Wallow.Inquiries.Application/Commands/CloseSubmission/CloseSubmissionHandler.cs
public sealed class CloseSubmissionHandler(ISubmissionRepository repo)
{
    public async Task<Result> Handle(CloseSubmissionCommand command, CancellationToken ct)
    {
        var submission = await repo.GetByIdAsync(command.SubmissionId, ct);
        if (submission is null)
            return Result.Failure(Error.NotFound("Submission", command.SubmissionId));

        submission.Close(command.ClosedBy);
        await repo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**5. Controller endpoint:**
```csharp
/// <summary>
/// Close a submission.
/// </summary>
[HttpPost("{id:guid}/close")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Close(Guid id, CancellationToken ct)
{
    var command = new CloseSubmissionCommand(id, currentUserService.GetCurrentUserId()!.Value);
    var result = await bus.InvokeAsync<Result>(command, ct);

    if (result.IsSuccess)
        return NoContent();

    return result.ToActionResult();
}
```

## File Upload Endpoints

For file uploads, use `multipart/form-data`:

```csharp
/// <summary>
/// Upload a file.
/// </summary>
[HttpPost("upload")]
[RequestSizeLimit(100 * 1024 * 1024)] // 100MB
[Consumes("multipart/form-data")]
[ProducesResponseType(typeof(UploadResponse), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Upload(
    IFormFile file,
    [FromForm] string bucket,
    [FromForm] string? path = null,
    CancellationToken cancellationToken = default)
{
    if (file.Length == 0)
    {
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = "File is empty"
        });
    }

    await using var stream = file.OpenReadStream();

    var command = new UploadFileCommand(
        tenantContext.TenantId.Value,
        currentUserService.GetCurrentUserId()!.Value,
        bucket,
        file.FileName,
        file.ContentType,
        stream,
        file.Length,
        path);

    var result = await bus.InvokeAsync<Result<UploadResult>>(command, cancellationToken);

    return result.Map(ToUploadResponse)
        .ToCreatedResult($"/api/storage/files/{result.Value?.FileId}");
}
```

## Common Patterns

### Pagination

```csharp
[HttpGet]
[ProducesResponseType(typeof(PagedResult<SubmissionResponse>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken ct = default)
{
    Result<PagedResult<SubmissionDto>> result = await bus.InvokeAsync<Result<PagedResult<SubmissionDto>>>(
        new GetAllSubmissionsQuery(skip, take), ct);

    return result.ToActionResult();
}
```

### Nested Resources

```csharp
// GET /api/v1/inquiries/submissions/{id}/comments
[HttpGet("{id:guid}/comments")]
[ProducesResponseType(typeof(IReadOnlyList<SubmissionCommentDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetComments(
    Guid id,
    CancellationToken ct = default)
{
    var query = new GetSubmissionCommentsQuery(id);
    var result = await bus.InvokeAsync<Result<IReadOnlyList<SubmissionCommentDto>>>(query, ct);
    return result.ToActionResult();
}
```

### Action Endpoints (RPC-style)

For operations that don't fit REST:

```csharp
// POST /api/v1/inquiries/submissions/{id}/close
[HttpPost("{id:guid}/close")]
[ProducesResponseType(typeof(SubmissionResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Close(Guid id, CancellationToken ct)
{
    var command = new CloseSubmissionCommand(id, currentUserService.GetCurrentUserId()!.Value);
    var result = await bus.InvokeAsync<Result<SubmissionDto>>(command, ct);
    return result.Map(ToSubmissionResponse).ToActionResult();
}
```

### Redirect Responses

```csharp
// Download redirects to presigned URL
[HttpGet("files/{id:guid}/download")]
[ProducesResponseType(StatusCodes.Status302Found)]
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    var result = await bus.InvokeAsync<Result<PresignedUrlResult>>(
        new GetPresignedUrlQuery(tenantContext.TenantId.Value, id), ct);

    if (result.IsFailure)
        return result.ToActionResult();

    return Redirect(result.Value!.Url);
}
```

## Testing Endpoints

See `docs/development/testing.md` for comprehensive testing patterns. Quick overview:

```csharp
public class SubmissionsControllerTests : WallowIntegrationTestBase
{
    [Fact]
    public async Task Create_ReturnsCreated_WithValidRequest()
    {
        // Arrange
        var request = new CreateSubmissionRequest("Help with account", "I cannot log in.", null);

        // Act
        var response = await Client.PostAsJsonAsync("/api/inquiries/submissions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var submission = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        submission.Should().NotBeNull();
        submission!.Subject.Should().Be("Help with account");
    }
}
```

## Related Documentation

- [Developer guide](../getting-started/developer-guide.md) -- Overall development workflow
- [Testing guide](testing.md) -- Testing patterns and fixtures
- [Module creation guide](../architecture/module-creation.md) -- Creating new modules
- [Authorization guide](../architecture/authorization.md) -- Permission system details
