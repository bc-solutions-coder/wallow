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
[Route("api/v{version:apiVersion}/billing/invoices")]
[Authorize]
[Tags("Invoices")]
[Produces("application/json")]
[Consumes("application/json")]
public class InvoicesController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
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
- `GET /api/v1/billing/invoices` - List invoices
- `GET /api/v1/billing/invoices/{id}` - Get invoice by ID
- `POST /api/v1/billing/invoices/{id}/issue` - Issue an invoice
- `GET /api/v1/notifications/notifications` - Get user notifications

### Injecting Dependencies

Controllers use primary constructors and inject:
- `IMessageBus` -- Wolverine mediator for commands/queries
- `ICurrentUserService` -- Current user ID and context from JWT
- Domain services directly when CQRS is not used (Identity module pattern)

```csharp
// Standard pattern: primary constructor with Wolverine + user service
public class InvoicesController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
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
    Guid? userId = currentUserService.GetCurrentUserId();

    // 1. Map request to command
    var command = new CreateInvoiceCommand(
        userId!.Value,
        request.InvoiceNumber,
        request.Currency,
        request.DueDate);

    // 2. Send to handler via Wolverine
    var result = await bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

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
var result = await bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

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
    public const string BillingRead = "BillingRead";
    public const string InvoicesRead = "InvoicesRead";
    public const string InvoicesWrite = "InvoicesWrite";
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
    var command = new ArchiveInvoiceCommand(id, currentUserService.GetCurrentUserId()!.Value);
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
[ProducesResponseType(typeof(PagedResult<InvoiceResponse>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAll(
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken ct = default)
{
    Result<PagedResult<InvoiceDto>> result = await bus.InvokeAsync<Result<PagedResult<InvoiceDto>>>(
        new GetAllInvoicesQuery(skip, take), ct);

    return result.ToActionResult();
}
```

### Nested Resources

```csharp
// GET /api/v1/billing/invoices/{id}/line-items
[HttpGet("{id:guid}/line-items")]
[ProducesResponseType(typeof(IReadOnlyList<InvoiceLineItemDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetLineItems(
    Guid id,
    CancellationToken ct = default)
{
    var query = new GetInvoiceLineItemsQuery(id);
    var result = await bus.InvokeAsync<Result<IReadOnlyList<InvoiceLineItemDto>>>(query, ct);
    return result.ToActionResult();
}
```

### Action Endpoints (RPC-style)

For operations that don't fit REST:

```csharp
// POST /api/v1/billing/invoices/{id}/issue
[HttpPost("{id:guid}/issue")]
[ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
{
    var command = new IssueInvoiceCommand(id, currentUserService.GetCurrentUserId()!.Value);
    var result = await bus.InvokeAsync<Result<InvoiceDto>>(command, ct);
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
public class InvoicesControllerTests : WallowIntegrationTestBase
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

- [Developer guide](../getting-started/developer-guide.md) -- Overall development workflow
- [Testing guide](testing.md) -- Testing patterns and fixtures
- [Module creation guide](../architecture/module-creation.md) -- Creating new modules
- [Authorization guide](../architecture/authorization.md) -- Permission system details
