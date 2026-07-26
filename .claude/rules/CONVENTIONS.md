## Coding Conventions

### C# general
- Always use the explicit type instead of `var`.
- **JWT claim access**: Never use raw `FindFirst`/`FindFirstValue`/`FindAll` on `ClaimsPrincipal`.
  Use `ClaimsPrincipalExtensions` from `Wallow.Shared.Kernel.Extensions`:
  - Single-value: `GetUserId()`, `GetClientId()`, `GetTenantId()`, `GetTenantName()`, `GetEmail()`,
    `GetDisplayName()`, `GetFirstName()`, `GetLastName()`, `GetAuthMethod()`, `GetTenantRegion()`, `GetPlan()`
  - Multi-value: `GetRoles()`, `GetPermissions()`, `GetScopes()` — return `IReadOnlyList<string>`
  - If a needed claim has no extension, add one to `ClaimsPrincipalExtensions` rather than using raw `FindFirst`.

### Logging — `[LoggerMessage]` source generator
Never call `logger.LogInformation(...)` or other `ILogger` extension methods directly (CA1848/CA1873).
- Mark the class `partial`; inject `ILogger<T>` via the primary constructor; add `using Microsoft.Extensions.Logging;`.
- Define `private partial void` methods with `[LoggerMessage]` attributes at the bottom of the class:

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Something happened for {EntityId} by user {UserId}")]
private partial void LogSomethingHappened(Guid entityId, string? userId);
```

### XML comments
- **Never use `--` inside XML comments** in `.csproj`, `.props`, `.targets`, or any XML file — XML forbids
  `--` within `<!-- -->` and it causes MSB4025 parse errors. Rephrase CLI flags (e.g. write "reuse existing
  build output" instead of "use --no-build").

### Pre-commit
- Run `dotnet format api/Wallow.slnx` before every commit and stage the formatting changes. Never commit
  unformatted code. (Commit message format lives in CLAUDE.md → Versioning.)

### Deleting
- Always ask for confirmation before deleting anything outside the current project.
