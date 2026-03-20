# Showcases Module Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a global Showcases module for admin-managed portfolio entries with anonymous read access.

**Architecture:** New Clean Architecture module (Domain -> Application -> Infrastructure -> Api). Not tenant-scoped — inherits from `DbContext` directly, not `TenantAwareDbContext`. Admin role manages entries via `ShowcasesManage` permission; public reads require no authentication.

**Tech Stack:** .NET 10, EF Core (PostgreSQL `showcases` schema), Wolverine (CQRS), FluentValidation

---

### Task 1: Create project structure and solution references

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Domain/Wallow.Showcases.Domain.csproj`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Wallow.Showcases.Application.csproj`
- Create: `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Wallow.Showcases.Infrastructure.csproj`
- Create: `src/Modules/Showcases/Wallow.Showcases.Api/Wallow.Showcases.Api.csproj`
- Modify: `src/Wallow.Api/Wallow.Api.csproj`
- Modify: `Wallow.sln`

**Step 1: Create the four project directories and .csproj files**

`Wallow.Showcases.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Showcases.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  </ItemGroup>
</Project>
```

`Wallow.Showcases.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Showcases.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Showcases.Domain\Wallow.Showcases.Domain.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
</Project>
```

`Wallow.Showcases.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Showcases.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="WolverineFx" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Showcases.Domain\Wallow.Showcases.Domain.csproj" />
    <ProjectReference Include="..\Wallow.Showcases.Application\Wallow.Showcases.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

`Wallow.Showcases.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Showcases.Api</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Wallow.Showcases.Application\Wallow.Showcases.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Api\Wallow.Shared.Api.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Add projects to solution**

Run:
```bash
dotnet sln add src/Modules/Showcases/Wallow.Showcases.Domain/Wallow.Showcases.Domain.csproj
dotnet sln add src/Modules/Showcases/Wallow.Showcases.Application/Wallow.Showcases.Application.csproj
dotnet sln add src/Modules/Showcases/Wallow.Showcases.Infrastructure/Wallow.Showcases.Infrastructure.csproj
dotnet sln add src/Modules/Showcases/Wallow.Showcases.Api/Wallow.Showcases.Api.csproj
```

**Step 3: Add project references to `src/Wallow.Api/Wallow.Api.csproj`**

Add to the `<!-- Module Api projects -->` section:
```xml
<ProjectReference Include="..\Modules\Showcases\Wallow.Showcases.Api\Wallow.Showcases.Api.csproj" />
```

Add to the `<!-- Module Infrastructure projects -->` section:
```xml
<ProjectReference Include="..\Modules\Showcases\Wallow.Showcases.Infrastructure\Wallow.Showcases.Infrastructure.csproj" />
```

**Step 4: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(showcases): scaffold module project structure"
```

---

### Task 2: Domain layer — Enum, Identity, Entity

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Domain/Enums/ShowcaseCategory.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Domain/Identity/ShowcaseId.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Domain/Entities/Showcase.cs`

**Step 1: Create the ShowcaseCategory enum**

`ShowcaseCategory.cs`:
```csharp
namespace Wallow.Showcases.Domain.Enums;

public enum ShowcaseCategory
{
    WebApp,
    Api,
    Mobile,
    Library,
    Tool
}
```

**Step 2: Create the strongly-typed ID**

`ShowcaseId.cs`:
```csharp
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Showcases.Domain.Identity;

public readonly record struct ShowcaseId(Guid Value) : IStronglyTypedId<ShowcaseId>
{
    public static ShowcaseId Create(Guid value) => new(value);
    public static ShowcaseId New() => new(Guid.NewGuid());
}
```

**Step 3: Create the Showcase aggregate root**

`Showcase.cs`:
```csharp
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Showcases.Domain.Entities;

public sealed class Showcase : AuditableEntity<ShowcaseId>
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ShowcaseCategory Category { get; private set; }
    public string? DemoUrl { get; private set; }
    public string? GitHubUrl { get; private set; }
    public string? VideoUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public int DisplayOrder { get; private set; }

    private Showcase() { } // EF Core

    public static Showcase Create(
        string title,
        string description,
        ShowcaseCategory category,
        string? demoUrl,
        string? gitHubUrl,
        string? videoUrl,
        string? thumbnailUrl,
        List<string> tags,
        int displayOrder,
        TimeProvider timeProvider)
    {
        Showcase showcase = new()
        {
            Id = ShowcaseId.New(),
            Title = title,
            Description = description,
            Category = category,
            DemoUrl = demoUrl,
            GitHubUrl = gitHubUrl,
            VideoUrl = videoUrl,
            ThumbnailUrl = thumbnailUrl,
            Tags = tags,
            DisplayOrder = displayOrder
        };

        showcase.SetCreated(timeProvider.GetUtcNow());

        return showcase;
    }

    public void Update(
        string title,
        string description,
        ShowcaseCategory category,
        string? demoUrl,
        string? gitHubUrl,
        string? videoUrl,
        string? thumbnailUrl,
        List<string> tags,
        int displayOrder,
        TimeProvider timeProvider)
    {
        Title = title;
        Description = description;
        Category = category;
        DemoUrl = demoUrl;
        GitHubUrl = gitHubUrl;
        VideoUrl = videoUrl;
        ThumbnailUrl = thumbnailUrl;
        Tags = tags;
        DisplayOrder = displayOrder;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
```

**Notes:**
- Uses `AuditableEntity<ShowcaseId>` (not `AggregateRoot`) — no domain events needed for simple CRUD.
- No `ITenantScoped` — this entity is global.
- `SetCreated` and `SetUpdated` come from `AuditableEntity` base class.

**Step 4: Verify build**

Run: `dotnet build src/Modules/Showcases/Wallow.Showcases.Domain`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(showcases): add domain layer with Showcase entity"
```

---

### Task 3: Application layer — Repository interface, DTOs, Mappings

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Interfaces/IShowcaseRepository.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/DTOs/ShowcaseDto.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Mappings/ShowcaseMappings.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Extensions/ApplicationExtensions.cs`

**Step 1: Create the repository interface**

`IShowcaseRepository.cs`:
```csharp
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;

namespace Wallow.Showcases.Application.Interfaces;

public interface IShowcaseRepository
{
    Task<Showcase?> GetByIdAsync(ShowcaseId id, CancellationToken cancellationToken = default);
    Task<List<Showcase>> GetAllAsync(CancellationToken cancellationToken = default);
    void Add(Showcase showcase);
    void Remove(Showcase showcase);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Step 2: Create the DTO**

`ShowcaseDto.cs`:
```csharp
namespace Wallow.Showcases.Application.DTOs;

public sealed record ShowcaseDto(
    Guid Id,
    string Title,
    string Description,
    string Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string> Tags,
    int DisplayOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

**Step 3: Create the mapping extension**

`ShowcaseMappings.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Domain.Entities;

namespace Wallow.Showcases.Application.Mappings;

public static class ShowcaseMappings
{
    public static ShowcaseDto ToDto(this Showcase showcase)
    {
        return new ShowcaseDto(
            showcase.Id.Value,
            showcase.Title,
            showcase.Description,
            showcase.Category.ToString(),
            showcase.DemoUrl,
            showcase.GitHubUrl,
            showcase.VideoUrl,
            showcase.ThumbnailUrl,
            showcase.Tags,
            showcase.DisplayOrder,
            showcase.CreatedAt,
            showcase.UpdatedAt);
    }
}
```

**Step 4: Create the application extensions**

`ApplicationExtensions.cs`:
```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Showcases.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddShowcasesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

**Step 5: Verify build**

Run: `dotnet build src/Modules/Showcases/Wallow.Showcases.Application`
Expected: Build succeeds

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(showcases): add application layer with DTOs, repository interface, and mappings"
```

---

### Task 4: Application layer — Commands (Create, Update, Delete)

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/CreateShowcase/CreateShowcaseCommand.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/CreateShowcase/CreateShowcaseValidator.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/CreateShowcase/CreateShowcaseHandler.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/UpdateShowcase/UpdateShowcaseCommand.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/UpdateShowcase/UpdateShowcaseValidator.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/UpdateShowcase/UpdateShowcaseHandler.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/DeleteShowcase/DeleteShowcaseCommand.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Commands/DeleteShowcase/DeleteShowcaseHandler.cs`

**Step 1: Create CreateShowcase command, validator, and handler**

`CreateShowcaseCommand.cs`:
```csharp
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Application.Commands.CreateShowcase;

public sealed record CreateShowcaseCommand(
    string Title,
    string Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    string? ThumbnailUrl,
    List<string> Tags,
    int DisplayOrder);
```

`CreateShowcaseValidator.cs`:
```csharp
using FluentValidation;

namespace Wallow.Showcases.Application.Commands.CreateShowcase;

public sealed class CreateShowcaseValidator : AbstractValidator<CreateShowcaseCommand>
{
    public CreateShowcaseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid category");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.DemoUrl)
                     || !string.IsNullOrWhiteSpace(x.GitHubUrl)
                     || !string.IsNullOrWhiteSpace(x.VideoUrl))
            .WithMessage("At least one URL (Demo, GitHub, or Video) is required");

        RuleFor(x => x.DemoUrl)
            .Must(BeAValidUrl).WithMessage("Demo URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.DemoUrl));

        RuleFor(x => x.GitHubUrl)
            .Must(BeAValidUrl).WithMessage("GitHub URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.GitHubUrl));

        RuleFor(x => x.VideoUrl)
            .Must(BeAValidUrl).WithMessage("Video URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));

        RuleFor(x => x.ThumbnailUrl)
            .Must(BeAValidUrl).WithMessage("Thumbnail URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.ThumbnailUrl));
    }

    private static bool BeAValidUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? result)
            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
```

`CreateShowcaseHandler.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Mappings;
using Wallow.Showcases.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Application.Commands.CreateShowcase;

public sealed class CreateShowcaseHandler(
    IShowcaseRepository showcaseRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<ShowcaseDto>> Handle(
        CreateShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        Showcase showcase = Showcase.Create(
            command.Title,
            command.Description,
            command.Category,
            command.DemoUrl,
            command.GitHubUrl,
            command.VideoUrl,
            command.ThumbnailUrl,
            command.Tags,
            command.DisplayOrder,
            timeProvider);

        showcaseRepository.Add(showcase);
        await showcaseRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(showcase.ToDto());
    }
}
```

**Step 2: Create UpdateShowcase command, validator, and handler**

`UpdateShowcaseCommand.cs`:
```csharp
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Application.Commands.UpdateShowcase;

public sealed record UpdateShowcaseCommand(
    Guid Id,
    string Title,
    string Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    string? ThumbnailUrl,
    List<string> Tags,
    int DisplayOrder);
```

`UpdateShowcaseValidator.cs`:
```csharp
using FluentValidation;

namespace Wallow.Showcases.Application.Commands.UpdateShowcase;

public sealed class UpdateShowcaseValidator : AbstractValidator<UpdateShowcaseCommand>
{
    public UpdateShowcaseValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Showcase ID is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid category");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.DemoUrl)
                     || !string.IsNullOrWhiteSpace(x.GitHubUrl)
                     || !string.IsNullOrWhiteSpace(x.VideoUrl))
            .WithMessage("At least one URL (Demo, GitHub, or Video) is required");

        RuleFor(x => x.DemoUrl)
            .Must(BeAValidUrl).WithMessage("Demo URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.DemoUrl));

        RuleFor(x => x.GitHubUrl)
            .Must(BeAValidUrl).WithMessage("GitHub URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.GitHubUrl));

        RuleFor(x => x.VideoUrl)
            .Must(BeAValidUrl).WithMessage("Video URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));

        RuleFor(x => x.ThumbnailUrl)
            .Must(BeAValidUrl).WithMessage("Thumbnail URL must be a valid URL")
            .When(x => !string.IsNullOrWhiteSpace(x.ThumbnailUrl));
    }

    private static bool BeAValidUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? result)
            && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
```

`UpdateShowcaseHandler.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Mappings;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Application.Commands.UpdateShowcase;

public sealed class UpdateShowcaseHandler(
    IShowcaseRepository showcaseRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<ShowcaseDto>> Handle(
        UpdateShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        ShowcaseId id = ShowcaseId.Create(command.Id);
        Showcase? showcase = await showcaseRepository.GetByIdAsync(id, cancellationToken);

        if (showcase is null)
        {
            return Result.Failure<ShowcaseDto>(Error.NotFound("Showcase", command.Id.ToString()));
        }

        showcase.Update(
            command.Title,
            command.Description,
            command.Category,
            command.DemoUrl,
            command.GitHubUrl,
            command.VideoUrl,
            command.ThumbnailUrl,
            command.Tags,
            command.DisplayOrder,
            timeProvider);

        await showcaseRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(showcase.ToDto());
    }
}
```

**Step 3: Create DeleteShowcase command and handler**

`DeleteShowcaseCommand.cs`:
```csharp
namespace Wallow.Showcases.Application.Commands.DeleteShowcase;

public sealed record DeleteShowcaseCommand(Guid Id);
```

`DeleteShowcaseHandler.cs`:
```csharp
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Application.Commands.DeleteShowcase;

public sealed class DeleteShowcaseHandler(IShowcaseRepository showcaseRepository)
{
    public async Task<Result> Handle(
        DeleteShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        ShowcaseId id = ShowcaseId.Create(command.Id);
        Showcase? showcase = await showcaseRepository.GetByIdAsync(id, cancellationToken);

        if (showcase is null)
        {
            return Result.Failure(Error.NotFound("Showcase", command.Id.ToString()));
        }

        showcaseRepository.Remove(showcase);
        await showcaseRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
```

**Step 4: Verify build**

Run: `dotnet build src/Modules/Showcases/Wallow.Showcases.Application`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(showcases): add CQRS command handlers for create, update, delete"
```

---

### Task 5: Application layer — Queries (GetShowcase, ListShowcases)

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcase/GetShowcaseQuery.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Queries/GetShowcase/GetShowcaseHandler.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Queries/ListShowcases/ListShowcasesQuery.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Application/Queries/ListShowcases/ListShowcasesHandler.cs`

**Step 1: Create GetShowcase query and handler**

`GetShowcaseQuery.cs`:
```csharp
namespace Wallow.Showcases.Application.Queries.GetShowcase;

public sealed record GetShowcaseQuery(Guid Id);
```

`GetShowcaseHandler.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Mappings;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Application.Queries.GetShowcase;

public sealed class GetShowcaseHandler(IShowcaseRepository showcaseRepository)
{
    public async Task<Result<ShowcaseDto>> Handle(
        GetShowcaseQuery query,
        CancellationToken cancellationToken)
    {
        ShowcaseId id = ShowcaseId.Create(query.Id);
        Showcase? showcase = await showcaseRepository.GetByIdAsync(id, cancellationToken);

        if (showcase is null)
        {
            return Result.Failure<ShowcaseDto>(Error.NotFound("Showcase", query.Id.ToString()));
        }

        return Result.Success(showcase.ToDto());
    }
}
```

**Step 2: Create ListShowcases query and handler**

`ListShowcasesQuery.cs`:
```csharp
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Application.Queries.ListShowcases;

public sealed record ListShowcasesQuery(ShowcaseCategory? Category = null, string? Tag = null);
```

`ListShowcasesHandler.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Mappings;
using Wallow.Showcases.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Application.Queries.ListShowcases;

public sealed class ListShowcasesHandler(IShowcaseRepository showcaseRepository)
{
    public async Task<Result<List<ShowcaseDto>>> Handle(
        ListShowcasesQuery query,
        CancellationToken cancellationToken)
    {
        List<Showcase> showcases = await showcaseRepository.GetAllAsync(cancellationToken);

        IEnumerable<Showcase> filtered = showcases.AsEnumerable();

        if (query.Category.HasValue)
        {
            filtered = filtered.Where(s => s.Category == query.Category.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            filtered = filtered.Where(s => s.Tags.Contains(query.Tag, StringComparer.OrdinalIgnoreCase));
        }

        List<ShowcaseDto> result = filtered
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.CreatedAt)
            .Select(s => s.ToDto())
            .ToList();

        return Result.Success(result);
    }
}
```

**Step 3: Verify build**

Run: `dotnet build src/Modules/Showcases/Wallow.Showcases.Application`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(showcases): add query handlers for get and list showcases"
```

---

### Task 6: Infrastructure layer — DbContext, Entity Configuration, Repository

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/ShowcasesDbContext.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/Configurations/ShowcaseConfiguration.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Persistence/Repositories/ShowcaseRepository.cs`

**Step 1: Create the DbContext**

`ShowcasesDbContext.cs`:
```csharp
using Wallow.Showcases.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Showcases.Infrastructure.Persistence;

public sealed class ShowcasesDbContext : DbContext
{
    public DbSet<Showcase> Showcases => Set<Showcase>();

    public ShowcasesDbContext(DbContextOptions<ShowcasesDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("showcases");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShowcasesDbContext).Assembly);
    }
}
```

**Note:** Inherits from plain `DbContext`, not `TenantAwareDbContext` — no tenant filtering, no `ITenantContext` dependency.

**Step 2: Create the entity configuration**

`ShowcaseConfiguration.cs`:
```csharp
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.Showcases.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseConfiguration : IEntityTypeConfiguration<Showcase>
{
    public void Configure(EntityTypeBuilder<Showcase> builder)
    {
        builder.ToTable("showcases");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(new StronglyTypedIdConverter<ShowcaseId>())
            .HasColumnName("id");

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(s => s.Description)
            .IsRequired()
            .HasColumnName("description");

        builder.Property(s => s.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("category");

        builder.Property(s => s.DemoUrl)
            .HasMaxLength(2048)
            .HasColumnName("demo_url");

        builder.Property(s => s.GitHubUrl)
            .HasMaxLength(2048)
            .HasColumnName("github_url");

        builder.Property(s => s.VideoUrl)
            .HasMaxLength(2048)
            .HasColumnName("video_url");

        builder.Property(s => s.ThumbnailUrl)
            .HasMaxLength(2048)
            .HasColumnName("thumbnail_url");

        builder.Property(s => s.Tags)
            .HasColumnType("text[]")
            .HasColumnName("tags");

        builder.Property(s => s.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Ignore(s => s.CreatedBy);
        builder.Ignore(s => s.UpdatedBy);

        builder.HasIndex(s => s.DisplayOrder)
            .HasDatabaseName("ix_showcases_display_order");

        builder.HasIndex(s => s.Category)
            .HasDatabaseName("ix_showcases_category");
    }
}
```

**Step 3: Create the repository**

`ShowcaseRepository.cs`:
```csharp
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Showcases.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseRepository : IShowcaseRepository
{
    private static readonly Func<ShowcasesDbContext, ShowcaseId, CancellationToken, Task<Showcase?>> _getByIdQuery =
        EF.CompileAsyncQuery(
            (ShowcasesDbContext ctx, ShowcaseId id, CancellationToken _) =>
                ctx.Showcases.AsTracking().FirstOrDefault(s => s.Id == id));

    private readonly ShowcasesDbContext _context;

    public ShowcaseRepository(ShowcasesDbContext context)
    {
        _context = context;
    }

    public Task<Showcase?> GetByIdAsync(ShowcaseId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(_context, id, cancellationToken);
    }

    public Task<List<Showcase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _context.Showcases
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(Showcase showcase)
    {
        _context.Showcases.Add(showcase);
    }

    public void Remove(Showcase showcase)
    {
        _context.Showcases.Remove(showcase);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

**Step 4: Verify build**

Run: `dotnet build src/Modules/Showcases/Wallow.Showcases.Infrastructure`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(showcases): add infrastructure layer with DbContext, entity config, and repository"
```

---

### Task 7: Infrastructure layer — Module registration and extensions

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Extensions/ShowcasesModuleExtensions.cs`
- Modify: `src/Wallow.Api/WallowModules.cs`

**Step 1: Create the module extension methods**

`ShowcasesModuleExtensions.cs`:
```csharp
using Wallow.Showcases.Application.Extensions;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Infrastructure.Persistence;
using Wallow.Showcases.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wallow.Showcases.Infrastructure.Extensions;

public static class ShowcasesModuleExtensions
{
    public static IServiceCollection AddShowcasesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddShowcasesApplication();
        services.AddShowcasesPersistence(configuration);
        return services;
    }

    public static async Task InitializeShowcasesModuleAsync(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        ShowcasesDbContext db = scope.ServiceProvider.GetRequiredService<ShowcasesDbContext>();
        await db.Database.MigrateAsync();
        scope.ServiceProvider.GetRequiredService<ILogger<ShowcasesDbContext>>()
            .LogShowcasesModuleInitialized();
    }

    private static IServiceCollection AddShowcasesPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShowcasesDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "showcases");
                    npgsql.EnableRetryOnFailure(3);
                }));

        services.AddScoped<IShowcaseRepository, ShowcaseRepository>();

        return services;
    }
}

internal static partial class ShowcasesLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Showcases module initialized")]
    public static partial void LogShowcasesModuleInitialized(this ILogger logger);
}
```

**Note:** Uses `Microsoft.AspNetCore.Builder.WebApplication` — requires adding `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the Infrastructure csproj OR importing the namespace. Check if other Infrastructure projects use `WebApplication` — if they reference `Shared.Infrastructure` which already includes it, this should work. If not, add the FrameworkReference.

**Step 2: Register in WallowModules.cs**

Add `using Wallow.Showcases.Infrastructure.Extensions;` to the top.

In `AddWallowModules`, add under FEATURE MODULES section:
```csharp
if (modules.GetValue("Showcases", defaultValue: true))
{
    services.AddShowcasesModule(configuration);
}
```

In `InitializeWallowModulesAsync`, add:
```csharp
if (modules.GetValue("Showcases", defaultValue: true))
{
    await app.InitializeShowcasesModuleAsync();
}
```

**Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(showcases): add module registration and DI wiring"
```

---

### Task 8: Add ShowcasesManage permission

**Files:**
- Modify: `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs`

**Step 1: Add the permission constant**

Add to `PermissionType.cs` after the Storage section:
```csharp
// Showcases
public const string ShowcasesManage = "ShowcasesManage";
```

**Note:** No need to update `RolePermissionMapping` — the `admin` role already maps to `PermissionType.All`, which uses reflection to gather all constants. The new `ShowcasesManage` permission is automatically included.

**Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(showcases): add ShowcasesManage permission type"
```

---

### Task 9: API layer — Controller with public and admin endpoints

**Files:**
- Create: `src/Modules/Showcases/Wallow.Showcases.Api/Controllers/ShowcasesController.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Api/Contracts/Requests/CreateShowcaseRequest.cs`
- Create: `src/Modules/Showcases/Wallow.Showcases.Api/Contracts/Requests/UpdateShowcaseRequest.cs`

**Step 1: Create API request contracts**

`CreateShowcaseRequest.cs`:
```csharp
namespace Wallow.Showcases.Api.Contracts.Requests;

public sealed record CreateShowcaseRequest(
    string Title,
    string Description,
    string Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    string? ThumbnailUrl,
    List<string> Tags,
    int DisplayOrder);
```

`UpdateShowcaseRequest.cs`:
```csharp
namespace Wallow.Showcases.Api.Contracts.Requests;

public sealed record UpdateShowcaseRequest(
    string Title,
    string Description,
    string Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    string? ThumbnailUrl,
    List<string> Tags,
    int DisplayOrder);
```

**Step 2: Create the controller**

`ShowcasesController.cs`:
```csharp
using Asp.Versioning;
using Wallow.Showcases.Api.Contracts.Requests;
using Wallow.Showcases.Application.Commands.CreateShowcase;
using Wallow.Showcases.Application.Commands.DeleteShowcase;
using Wallow.Showcases.Application.Commands.UpdateShowcase;
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Queries.GetShowcase;
using Wallow.Showcases.Application.Queries.ListShowcases;
using Wallow.Showcases.Domain.Enums;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Showcases.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/showcases")]
[Tags("Showcases")]
[Produces("application/json")]
[Consumes("application/json")]
public class ShowcasesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public ShowcasesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ShowcaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] string? tag,
        CancellationToken cancellationToken)
    {
        ShowcaseCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category)
            && Enum.TryParse<ShowcaseCategory>(category, ignoreCase: true, out ShowcaseCategory cat))
        {
            parsedCategory = cat;
        }

        ListShowcasesQuery query = new(parsedCategory, tag);
        Result<List<ShowcaseDto>> result = await _bus.InvokeAsync<Result<List<ShowcaseDto>>>(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ShowcaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        GetShowcaseQuery query = new(id);
        Result<ShowcaseDto> result = await _bus.InvokeAsync<Result<ShowcaseDto>>(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(typeof(ShowcaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShowcaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ShowcaseCategory>(request.Category, ignoreCase: true, out ShowcaseCategory category))
        {
            return BadRequest(new ProblemDetails { Detail = $"Invalid category: {request.Category}" });
        }

        CreateShowcaseCommand command = new(
            request.Title,
            request.Description,
            category,
            request.DemoUrl,
            request.GitHubUrl,
            request.VideoUrl,
            request.ThumbnailUrl,
            request.Tags,
            request.DisplayOrder);

        Result<ShowcaseDto> result = await _bus.InvokeAsync<Result<ShowcaseDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.ToCreatedResult($"/api/v1/showcases/{result.Value.Id}");
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(typeof(ShowcaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateShowcaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ShowcaseCategory>(request.Category, ignoreCase: true, out ShowcaseCategory category))
        {
            return BadRequest(new ProblemDetails { Detail = $"Invalid category: {request.Category}" });
        }

        UpdateShowcaseCommand command = new(
            id,
            request.Title,
            request.Description,
            category,
            request.DemoUrl,
            request.GitHubUrl,
            request.VideoUrl,
            request.ThumbnailUrl,
            request.Tags,
            request.DisplayOrder);

        Result<ShowcaseDto> result = await _bus.InvokeAsync<Result<ShowcaseDto>>(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [HasPermission(PermissionType.ShowcasesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        DeleteShowcaseCommand command = new(id);
        Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }
}
```

**Step 3: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(showcases): add API controller with public read and admin write endpoints"
```

---

### Task 10: EF Core migration

**Step 1: Generate the initial migration**

Run:
```bash
dotnet ef migrations add InitialShowcases \
    --project src/Modules/Showcases/Wallow.Showcases.Infrastructure \
    --startup-project src/Wallow.Api \
    --context ShowcasesDbContext
```

Expected: Migration files created in `src/Modules/Showcases/Wallow.Showcases.Infrastructure/Migrations/`

**Step 2: Verify build**

Run: `dotnet build`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(showcases): add initial EF Core migration"
```

---

### Task 11: Unit tests — Domain and Application

**Files:**
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Wallow.Showcases.Tests.csproj`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Domain/ShowcaseTests.cs`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/CreateShowcaseHandlerTests.cs`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/UpdateShowcaseHandlerTests.cs`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Commands/DeleteShowcaseHandlerTests.cs`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Queries/GetShowcaseHandlerTests.cs`
- Create: `tests/Modules/Showcases/Wallow.Showcases.Tests/Application/Queries/ListShowcasesHandlerTests.cs`
- Modify: `Wallow.sln`

**Step 1: Create the test project**

`Wallow.Showcases.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Showcases.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\..\src\Modules\Showcases\Wallow.Showcases.Domain\Wallow.Showcases.Domain.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Showcases\Wallow.Showcases.Application\Wallow.Showcases.Application.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Showcases\Wallow.Showcases.Infrastructure\Wallow.Showcases.Infrastructure.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Showcases\Wallow.Showcases.Api\Wallow.Showcases.Api.csproj" />
    <ProjectReference Include="..\..\..\Wallow.Tests.Common\Wallow.Tests.Common.csproj" />
  </ItemGroup>
</Project>
```

Add to solution:
```bash
dotnet sln add tests/Modules/Showcases/Wallow.Showcases.Tests/Wallow.Showcases.Tests.csproj
```

**Step 2: Write domain tests**

`ShowcaseTests.cs`:
```csharp
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Tests.Domain;

public class ShowcaseTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void Create_SetsAllProperties()
    {
        Showcase showcase = Showcase.Create(
            "My Project",
            "A description",
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            "https://github.com/user/repo",
            "https://youtube.com/watch?v=123",
            "https://img.example.com/thumb.png",
            ["C#", ".NET"],
            1,
            _timeProvider);

        showcase.Title.Should().Be("My Project");
        showcase.Description.Should().Be("A description");
        showcase.Category.Should().Be(ShowcaseCategory.WebApp);
        showcase.DemoUrl.Should().Be("https://demo.example.com");
        showcase.GitHubUrl.Should().Be("https://github.com/user/repo");
        showcase.VideoUrl.Should().Be("https://youtube.com/watch?v=123");
        showcase.ThumbnailUrl.Should().Be("https://img.example.com/thumb.png");
        showcase.Tags.Should().BeEquivalentTo(["C#", ".NET"]);
        showcase.DisplayOrder.Should().Be(1);
        showcase.Id.Value.Should().NotBeEmpty();
        showcase.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public void Update_ChangesAllProperties()
    {
        Showcase showcase = CreateTestShowcase();

        showcase.Update(
            "Updated Title",
            "Updated description",
            ShowcaseCategory.Api,
            "https://new-demo.example.com",
            null,
            null,
            null,
            ["Go"],
            5,
            _timeProvider);

        showcase.Title.Should().Be("Updated Title");
        showcase.Description.Should().Be("Updated description");
        showcase.Category.Should().Be(ShowcaseCategory.Api);
        showcase.DemoUrl.Should().Be("https://new-demo.example.com");
        showcase.GitHubUrl.Should().BeNull();
        showcase.VideoUrl.Should().BeNull();
        showcase.ThumbnailUrl.Should().BeNull();
        showcase.Tags.Should().BeEquivalentTo(["Go"]);
        showcase.DisplayOrder.Should().Be(5);
        showcase.UpdatedAt.Should().NotBe(default);
    }

    private Showcase CreateTestShowcase()
    {
        return Showcase.Create(
            "Test",
            "Desc",
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            null,
            null,
            null,
            [],
            0,
            _timeProvider);
    }
}
```

**Step 3: Write command handler tests**

`CreateShowcaseHandlerTests.cs`:
```csharp
using Wallow.Showcases.Application.Commands.CreateShowcase;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Tests.Application.Commands;

public class CreateShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository = Substitute.For<IShowcaseRepository>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly CreateShowcaseHandler _handler;

    public CreateShowcaseHandlerTests()
    {
        _handler = new CreateShowcaseHandler(_repository, _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessWithDto()
    {
        CreateShowcaseCommand command = new(
            "Test Project",
            "Description",
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            "https://github.com/user/repo",
            null,
            null,
            ["C#"],
            0);

        Result<ShowcaseDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Test Project");
        result.Value.Category.Should().Be("WebApp");
        _repository.Received(1).Add(Arg.Any<Showcase>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

`UpdateShowcaseHandlerTests.cs`:
```csharp
using Wallow.Showcases.Application.Commands.UpdateShowcase;
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Tests.Application.Commands;

public class UpdateShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository = Substitute.For<IShowcaseRepository>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private readonly UpdateShowcaseHandler _handler;

    public UpdateShowcaseHandlerTests()
    {
        _handler = new UpdateShowcaseHandler(_repository, _timeProvider);
    }

    [Fact]
    public async Task Handle_ExistingShowcase_ReturnsSuccess()
    {
        Showcase existing = Showcase.Create("Old", "Old desc", ShowcaseCategory.WebApp,
            "https://demo.example.com", null, null, null, [], 0, _timeProvider);
        Guid id = existing.Id.Value;

        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        UpdateShowcaseCommand command = new(id, "New Title", "New desc", ShowcaseCategory.Api,
            "https://new.example.com", null, null, null, ["Go"], 1);

        Result<ShowcaseDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task Handle_NonExistentShowcase_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns((Showcase?)null);

        UpdateShowcaseCommand command = new(Guid.NewGuid(), "Title", "Desc", ShowcaseCategory.WebApp,
            "https://demo.example.com", null, null, null, [], 0);

        Result<ShowcaseDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

`DeleteShowcaseHandlerTests.cs`:
```csharp
using Wallow.Showcases.Application.Commands.DeleteShowcase;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Tests.Application.Commands;

public class DeleteShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository = Substitute.For<IShowcaseRepository>();
    private readonly DeleteShowcaseHandler _handler;

    public DeleteShowcaseHandlerTests()
    {
        _handler = new DeleteShowcaseHandler(_repository);
    }

    [Fact]
    public async Task Handle_ExistingShowcase_ReturnsSuccessAndRemoves()
    {
        Showcase existing = Showcase.Create("Test", "Desc", ShowcaseCategory.WebApp,
            "https://demo.example.com", null, null, null, [], 0, TimeProvider.System);
        Guid id = existing.Id.Value;

        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        Result result = await _handler.Handle(new DeleteShowcaseCommand(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repository.Received(1).Remove(existing);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentShowcase_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns((Showcase?)null);

        Result result = await _handler.Handle(new DeleteShowcaseCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

**Step 4: Write query handler tests**

`GetShowcaseHandlerTests.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Queries.GetShowcase;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Tests.Application.Queries;

public class GetShowcaseHandlerTests
{
    private readonly IShowcaseRepository _repository = Substitute.For<IShowcaseRepository>();
    private readonly GetShowcaseHandler _handler;

    public GetShowcaseHandlerTests()
    {
        _handler = new GetShowcaseHandler(_repository);
    }

    [Fact]
    public async Task Handle_ExistingShowcase_ReturnsDto()
    {
        Showcase existing = Showcase.Create("Test", "Desc", ShowcaseCategory.WebApp,
            "https://demo.example.com", null, null, null, ["C#"], 0, TimeProvider.System);
        Guid id = existing.Id.Value;

        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<ShowcaseDto> result = await _handler.Handle(new GetShowcaseQuery(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Test");
    }

    [Fact]
    public async Task Handle_NonExistentShowcase_ReturnsNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<ShowcaseId>(), Arg.Any<CancellationToken>())
            .Returns((Showcase?)null);

        Result<ShowcaseDto> result = await _handler.Handle(new GetShowcaseQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}
```

`ListShowcasesHandlerTests.cs`:
```csharp
using Wallow.Showcases.Application.DTOs;
using Wallow.Showcases.Application.Interfaces;
using Wallow.Showcases.Application.Queries.ListShowcases;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Showcases.Tests.Application.Queries;

public class ListShowcasesHandlerTests
{
    private readonly IShowcaseRepository _repository = Substitute.For<IShowcaseRepository>();
    private readonly ListShowcasesHandler _handler;

    public ListShowcasesHandlerTests()
    {
        _handler = new ListShowcasesHandler(_repository);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllOrdered()
    {
        List<Showcase> showcases =
        [
            Showcase.Create("B", "Desc", ShowcaseCategory.Api, "https://b.com", null, null, null, [], 2, TimeProvider.System),
            Showcase.Create("A", "Desc", ShowcaseCategory.WebApp, "https://a.com", null, null, null, [], 1, TimeProvider.System)
        ];

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(showcases);

        Result<List<ShowcaseDto>> result = await _handler.Handle(new ListShowcasesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Title.Should().Be("A"); // DisplayOrder 1 first
    }

    [Fact]
    public async Task Handle_CategoryFilter_ReturnsFiltered()
    {
        List<Showcase> showcases =
        [
            Showcase.Create("Web", "Desc", ShowcaseCategory.WebApp, "https://web.com", null, null, null, [], 0, TimeProvider.System),
            Showcase.Create("Api", "Desc", ShowcaseCategory.Api, "https://api.com", null, null, null, [], 0, TimeProvider.System)
        ];

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(showcases);

        Result<List<ShowcaseDto>> result = await _handler.Handle(
            new ListShowcasesQuery(Category: ShowcaseCategory.Api), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Be("Api");
    }

    [Fact]
    public async Task Handle_TagFilter_ReturnsFiltered()
    {
        List<Showcase> showcases =
        [
            Showcase.Create("With Tag", "Desc", ShowcaseCategory.WebApp, "https://a.com", null, null, null, ["C#", ".NET"], 0, TimeProvider.System),
            Showcase.Create("No Tag", "Desc", ShowcaseCategory.WebApp, "https://b.com", null, null, null, ["Go"], 0, TimeProvider.System)
        ];

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(showcases);

        Result<List<ShowcaseDto>> result = await _handler.Handle(
            new ListShowcasesQuery(Tag: "C#"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Be("With Tag");
    }
}
```

**Step 5: Run tests**

Run: `dotnet test tests/Modules/Showcases/Wallow.Showcases.Tests`
Expected: All tests pass

**Step 6: Commit**

```bash
git add -A && git commit -m "test(showcases): add unit tests for domain entity and all handlers"
```

---

### Task 12: Verify full build and all tests

**Step 1: Build entire solution**

Run: `dotnet build`
Expected: Build succeeds with no errors

**Step 2: Run all tests**

Run: `dotnet test`
Expected: All tests pass (existing + new Showcases tests)

**Step 3: Commit (if any fixes needed)**

```bash
git add -A && git commit -m "fix(showcases): address build/test issues"
```
