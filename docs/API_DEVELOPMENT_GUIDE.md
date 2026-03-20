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
- Controllers are thin - they delegate to Wolverine handlers immediately
- Commands/queries are immutable records
- Handlers return `Result<T>` instead of throwing exceptions for expected failures
- FluentValidation validates commands before handlers execute
- Global exception handling catches unexpected errors

## Controller Patterns

### Basic Controller Structure

```csharp
using Wallow.Billing.Api.Contracts.Invoices;
using Wallow.Billing.Api.Extensions;
using Wallow.Billing.Application.Commands.CreateInvoice;
using Wallow.Billing.Application.DTOs;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[Route("api/billing/invoices")]
[Authorize]
[Tags("Invoices")]
[Produces("application/json")]
[Consumes("application/json")]
public class InvoicesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public InvoicesController(IMessageBus bus)
    {
        _bus = bus;
    }

    // ... endpoints
}
```

### Standard Attributes

Every controller should include:

| Attribute | Purpose |
|-----------|---------|
| `[ApiController]` | Enables automatic model validation and binding |
| `[Route("api/{module}/{resource}")]` | RESTful route pattern |
| `[Authorize]` | Requires authentication (JWT or API key) |
| `[Tags("...")]` | OpenAPI grouping for Scalar documentation |
| `[Produces("application/json")]` | Response content type |
| `[Consumes("application/json")]` | Request content type |

### Route Conventions

```
GET    /api/{module}/{resources}              List all
GET    /api/{module}/{resources}/{id}         Get by ID
POST   /api/{module}/{resources}              Create
PUT    /api/{module}/{resources}/{id}         Update
DELETE /api/{module}/{resources}/{id}         Delete
POST   /api/{module}/{resources}/{id}/action  Custom action
```

Examples from the codebase:
- `GET /api/billing/invoices` - List invoices
- `GET /api/billing/invoices/{id}` - Get invoice by ID
- `POST /api/billing/invoices/{id}/issue` - Issue an invoice
- `GET /api/communications/notifications` - Get user notifications

### Injecting Dependencies

Controllers inject:
- `IMessageBus` - Wolverine mediator for commands/queries
- `ITenantContext` - Current tenant from JWT (optional)
- Domain services directly when CQRS is not used (Identity module pattern)

```csharp
// Standard pattern: Use Wolverine for CQRS
public class InvoicesController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ITenantContext _tenantContext;

    public InvoicesController(IMessageBus bus, ITenantContext tenantContext)
    {
        _bus = bus;
        _tenantContext = tenantContext;
    }
}

// Exception: Identity module calls Keycloak services directly
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _keycloakAdmin;

    public UsersController(IUserManagementService keycloakAdmin)
    {
        _keycloakAdmin = keycloakAdmin;
    }
}
```

### Accessing Current User

Extract user ID and name from JWT claims:

```csharp
private Guid GetCurrentUserId()
{
    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
        return userId;

    return Guid.Empty;
}

private (Guid UserId, string UserName) GetCurrentUser()
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;
    var userName = User.FindFirst(ClaimTypes.Name)?.Value
        ?? User.FindFirst("name")?.Value
        ?? "Unknown User";

    if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
        return (userId, userName);

    return (Guid.Empty, userName);
}
```

### ProducesResponseType Attributes

Document all possible response types for OpenAPI:

```csharp
/// <summary>
/// Create a new invoice.
/// </summary>
[HttpPost]
[ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Create(
    [FromBody] CreateInvoiceRequest request,
    CancellationToken ct)
{
    // ...
}

/// <summary>
/// Get a specific invoice by ID.
/// </summary>
[HttpGet("{id:guid}")]
[ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
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
├── Invoices/
│   ├── CreateInvoiceRequest.cs
│   ├── AddLineItemRequest.cs
│   └── InvoiceResponse.cs
└── Payments/
    └── ProcessPaymentRequest.cs
```

### Request Records

Use `record` types for immutable request contracts:

```csharp
namespace Wallow.Billing.Api.Contracts.Invoices;

public sealed record CreateInvoiceRequest(
    string InvoiceNumber,
    string Currency,
    DateTime? DueDate);
```

```csharp
namespace Wallow.Billing.Api.Contracts.Invoices;

public sealed record CreateInvoiceRequest(
    string InvoiceNumber,
    string Currency,
    DateTime? DueDate);
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
private static InvoiceResponse ToInvoiceResponse(InvoiceDto dto) => new(
    dto.Id,
    dto.UserId,
    dto.InvoiceNumber,
    dto.Status,
    dto.TotalAmount,
    dto.Currency,
    dto.DueDate,
    dto.PaidAt,
    dto.CreatedAt,
    dto.UpdatedAt,
    dto.LineItems.Select(ToLineItemResponse).ToList());
```

## Commands and Queries

### Command Structure

Commands are immutable records in the Application layer:

```csharp
// src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice/CreateInvoiceCommand.cs
namespace Wallow.Billing.Application.Commands.CreateInvoice;

public sealed record CreateInvoiceCommand(
    Guid UserId,
    string InvoiceNumber,
    string Currency,
    DateTime? DueDate,
    Dictionary<string, object>? CustomFields = null);
```

### Handler Structure

Handlers use primary constructor injection and return `Result<T>`:

```csharp
// src/Modules/Billing/Wallow.Billing.Application/Commands/CreateInvoice/CreateInvoiceHandler.cs
namespace Wallow.Billing.Application.Commands.CreateInvoice;

public sealed class CreateInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    IMessageBus messageBus)
{
    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        // Validation/business rules
        var exists = await invoiceRepository.ExistsByInvoiceNumberAsync(
            command.InvoiceNumber, cancellationToken);

        if (exists)
        {
            return Result.Failure<InvoiceDto>(
                Error.Conflict($"Invoice '{command.InvoiceNumber}' already exists"));
        }

        // Create aggregate
        var invoice = Invoice.Create(
            command.UserId,
            command.InvoiceNumber,
            command.Currency,
            command.UserId,
            command.DueDate,
            command.CustomFields);

        // Persist
        invoiceRepository.Add(invoice);
        await invoiceRepository.SaveChangesAsync(cancellationToken);

        // Publish events
        await messageBus.PublishAsync(new AuditEntryRequestedEvent { ... });

        // Return DTO
        return Result.Success(invoice.ToDto());
    }
}
```

### Controller to Handler Flow

```csharp
[HttpPost]
public async Task<IActionResult> Create(
    [FromBody] CreateInvoiceRequest request,
    CancellationToken cancellationToken)
{
    var userId = GetCurrentUserId();

    // 1. Map request to command
    var command = new CreateInvoiceCommand(
        userId,
        request.InvoiceNumber,
        request.Currency,
        request.DueDate);

    // 2. Send to handler via Wolverine
    var result = await _bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

    // 3. Map result to HTTP response
    return result.Map(ToInvoiceResponse)
        .ToCreatedResult($"/api/billing/invoices/{result.Value?.Id}");
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
return Result.Success(invoice.ToDto());

// Failure with error
return Result.Failure<InvoiceDto>(
    Error.NotFound("Invoice", invoiceId));

return Result.Failure<InvoiceDto>(
    Error.Validation("Currency must be a 3-letter ISO code"));

return Result.Failure<InvoiceDto>(
    Error.Conflict($"Invoice '{number}' already exists"));
```

### Result Extensions

Each module has a `ResultExtensions.cs` that maps Results to HTTP responses:

```csharp
// src/Modules/Billing/Wallow.Billing.Api/Extensions/ResultExtensions.cs
namespace Wallow.Billing.Api.Extensions;

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
var result = await _bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

return result.Map(ToInvoiceResponse)
    .ToCreatedResult($"/api/billing/invoices/{result.Value?.Id}");

// Chain multiple transformations
return result
    .Map(invoices => invoices.Select(ToInvoiceResponse).ToList())
    .Map(responses => (IReadOnlyList<InvoiceResponse>)responses)
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
// src/Modules/Billing/Wallow.Billing.Application/Extensions/ApplicationExtensions.cs
public static class ApplicationExtensions
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

### Validator File Organization

Validators live alongside their commands:

```
src/Modules/Billing/Wallow.Billing.Application/
└── Commands/
    └── CreateInvoice/
        ├── CreateInvoiceCommand.cs
        ├── CreateInvoiceHandler.cs
        └── CreateInvoiceValidator.cs
```

### Writing Validators

```csharp
using FluentValidation;

namespace Wallow.Billing.Application.Commands.CreateInvoice;

public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.InvoiceNumber)
            .NotEmpty().WithMessage("Invoice number is required")
            .MaximumLength(50).WithMessage("Invoice number must not exceed 50 characters");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code");
    }
}
```

### Common Validation Rules

```csharp
// Required field
RuleFor(x => x.Name)
    .NotEmpty().WithMessage("Name is required");

// String length
RuleFor(x => x.Description)
    .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters");

// Email format
RuleFor(x => x.Email)
    .NotEmpty().WithMessage("Email is required")
    .EmailAddress().WithMessage("Email must be a valid email address");

// Enum values
RuleFor(x => x.Status)
    .IsInEnum().WithMessage("Invalid status value");

// GUID not empty
RuleFor(x => x.EntityId)
    .NotEmpty().WithMessage("EntityId is required");

// Custom validation
RuleFor(x => x.StartDate)
    .LessThan(x => x.EndDate)
    .WithMessage("Start date must be before end date");
```

### Validation Failure Response

When validation fails, Wolverine returns a `400 Bad Request` with validation errors:

```json
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "errors": {
        "InvoiceNumber": ["Invoice number is required"],
        "Currency": ["Currency must be a 3-letter ISO code"]
    }
}
```

## Error Handling

### Global Exception Handler

Unexpected exceptions are caught by `GlobalExceptionHandler`:

```csharp
// src/Wallow.Api/Middleware/GlobalExceptionHandler.cs
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        _logger.LogError(exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
            traceId, httpContext.Request.Path);

        var problemDetails = CreateProblemDetails(exception, traceId);
        httpContext.Response.StatusCode = problemDetails.Status ?? 500;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

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
    "detail": "Invoice with ID '123' was not found",
    "instance": "/errors/00-abc123",
    "traceId": "00-abc123",
    "code": "Invoice.NotFound"
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
using Wallow.Identity.Api.Authorization;
using Wallow.Identity.Domain.Enums;

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

Permissions are defined in `PermissionType` enum:

```csharp
public enum PermissionType
{
    // User management
    UsersRead = 100,
    UsersCreate = 101,
    UsersUpdate = 102,
    UsersDelete = 103,

    // Role management
    RolesRead = 200,
    RolesCreate = 201,
    RolesUpdate = 202,
    RolesDelete = 203,

    // Billing
    BillingRead = 500,
    BillingManage = 501,
    InvoicesRead = 502,
    InvoicesWrite = 503,

    // Admin
    AdminAccess = 900,
    SystemSettings = 901,

    // ... more permissions
}
```

### How Authorization Works

1. **JWT contains roles** - Keycloak issues JWT with role claims
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

For multi-tenant operations:

```csharp
public class StorageController : ControllerBase
{
    private readonly ITenantContext _tenantContext;

    public StorageController(IMessageBus bus, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(...)
    {
        var command = new UploadFileCommand(
            _tenantContext.TenantId.Value,  // Current tenant from JWT
            GetCurrentUserId(),
            ...);
    }
}
```

## Adding a New Endpoint

### Checklist

1. **Define the contract** (if needed)
   ```
   src/Modules/{Module}/Wallow.{Module}.Api/Contracts/Requests/{Name}Request.cs
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
   - Invoke via `_bus.InvokeAsync`
   - Map result to response

### Example: Adding "Archive Invoice" Endpoint

**1. No request contract needed** (ID comes from route)

**2. Command:**
```csharp
// src/Modules/Billing/Wallow.Billing.Application/Commands/ArchiveInvoice/ArchiveInvoiceCommand.cs
namespace Wallow.Billing.Application.Commands.ArchiveInvoice;

public sealed record ArchiveInvoiceCommand(Guid InvoiceId, Guid ArchivedBy);
```

**3. Validator:**
```csharp
// src/Modules/Billing/Wallow.Billing.Application/Commands/ArchiveInvoice/ArchiveInvoiceValidator.cs
public sealed class ArchiveInvoiceValidator : AbstractValidator<ArchiveInvoiceCommand>
{
    public ArchiveInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.ArchivedBy).NotEmpty();
    }
}
```

**4. Handler:**
```csharp
// src/Modules/Billing/Wallow.Billing.Application/Commands/ArchiveInvoice/ArchiveInvoiceHandler.cs
public sealed class ArchiveInvoiceHandler(IInvoiceRepository repo)
{
    public async Task<Result> Handle(ArchiveInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await repo.GetByIdAsync(command.InvoiceId, ct);
        if (invoice is null)
            return Result.Failure(Error.NotFound("Invoice", command.InvoiceId));

        invoice.Archive(command.ArchivedBy);
        await repo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

**5. Controller endpoint:**
```csharp
/// <summary>
/// Archive an invoice.
/// </summary>
[HttpPost("{id:guid}/archive")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
{
    var command = new ArchiveInvoiceCommand(id, GetCurrentUserId());
    var result = await _bus.InvokeAsync<Result>(command, ct);

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
        _tenantContext.TenantId.Value,
        GetCurrentUserId(),
        bucket,
        file.FileName,
        file.ContentType,
        stream,
        file.Length,
        path);

    var result = await _bus.InvokeAsync<Result<UploadResult>>(command, cancellationToken);

    return result.Map(ToUploadResponse)
        .ToCreatedResult($"/api/storage/files/{result.Value?.FileId}");
}
```

## Common Patterns

### Pagination

```csharp
[HttpGet]
[ProducesResponseType(typeof(PagedResult<InvoiceDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(
    [FromQuery] string? search = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    var query = new GetInvoicesQuery(
        _tenantContext.TenantId.Value,
        search,
        page,
        pageSize);

    var result = await _bus.InvokeAsync<Result<PagedResult<InvoiceDto>>>(query, ct);
    return result.ToActionResult();
}
```

### Nested Resources

```csharp
// GET /api/billing/invoices/{id}/line-items
[HttpGet("{id:guid}/line-items")]
[ProducesResponseType(typeof(IReadOnlyList<InvoiceLineItemDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetLineItems(
    Guid id,
    CancellationToken ct = default)
{
    var query = new GetInvoiceLineItemsQuery(id);
    var result = await _bus.InvokeAsync<Result<IReadOnlyList<InvoiceLineItemDto>>>(query, ct);
    return result.ToActionResult();
}
```

### Action Endpoints (RPC-style)

For operations that don't fit REST:

```csharp
// POST /api/billing/invoices/{id}/issue
[HttpPost("{id:guid}/issue")]
[ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
{
    var command = new IssueInvoiceCommand(id, GetCurrentUserId());
    var result = await _bus.InvokeAsync<Result<InvoiceDto>>(command, ct);
    return result.Map(ToInvoiceResponse).ToActionResult();
}
```

### Redirect Responses

```csharp
// Download redirects to presigned URL
[HttpGet("files/{id:guid}/download")]
[ProducesResponseType(StatusCodes.Status302Found)]
public async Task<IActionResult> Download(Guid id, CancellationToken ct)
{
    var result = await _bus.InvokeAsync<Result<PresignedUrlResult>>(
        new GetPresignedUrlQuery(_tenantContext.TenantId.Value, id), ct);

    if (result.IsFailure)
        return result.ToActionResult();

    return Redirect(result.Value!.Url);
}
```

## Testing Endpoints

See `docs/TESTING_GUIDE.md` for comprehensive testing patterns. Quick overview:

```csharp
public class InvoicesControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task Create_ReturnsCreated_WithValidRequest()
    {
        // Arrange
        var request = new CreateInvoiceRequest("INV-001", "USD", DateTime.UtcNow.AddDays(30));

        // Act
        var response = await Client.PostAsJsonAsync("/api/billing/invoices", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
        invoice!.InvoiceNumber.Should().Be("INV-001");
    }
}
```

## Related Documentation

- `docs/DEVELOPER_GUIDE.md` - Overall development workflow
- `docs/TESTING_GUIDE.md` - Testing patterns and fixtures
- `docs/MODULE_CREATION_GUIDE.md` - Creating new modules
- `docs/AUTHORIZATION.md` - Permission system details
