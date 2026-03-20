# Wallow.Shared.Infrastructure

Infrastructure utilities and configuration for EF Core and Marten event sourcing.

## Purpose

Provides shared infrastructure patterns used across modules: Marten document store configuration, EF Core utilities, and persistence helpers.

## Key Components

### Marten Integration (`Marten/MartenExtensions.cs`)
- `AddWallowMarten()` - Configures shared IDocumentStore
- Lazy connection resolution (supports Testcontainers)
- Event metadata enabled (correlation, causation, headers)
- Wolverine saga integration
- Auto-apply schema changes on startup
- Single document store shared by all event-sourced modules

### EF Core Utilities (`Persistence/`)
- `DictionaryValueComparer` - Enables JSONB storage for `Dictionary<string, object>` properties
- Used across modules for Metadata, Attributes, and CustomFields columns

### Module Registration (`Modules/`)
- `WallowModules.cs` - Central module registration
- Explicit listing (not auto-discovery) for transparency
- Handles service registration and database initialization

## Configuration

### Marten Setup
```csharp
builder.Services.AddWallowMarten(builder.Configuration);

// Requires appsettings.json:
{
  "ConnectionStrings": {
    "Wallow": "Host=localhost;Database=wallow;..."
  }
}
```

### EF Core JSONB Dictionary
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<CatalogItem>()
        .Property(x => x.Attributes)
        .HasConversion<DictionaryValueComparer>();
}
```

## Dependencies

**NuGet Packages:**
- Marten (8.20.0)
- WolverineFx.Marten (5.14.0)
- Npgsql.EntityFrameworkCore.PostgreSQL (10.0.0)

**Internal:**
- Wallow.Shared.Kernel

## Extension Points

- Add new Marten projections in event-sourced modules
- Use DictionaryValueComparer for custom JSONB properties
- Register modules in WallowModules.cs

## NuGet Potential

**Low** - Tightly coupled to Wallow's architecture decisions (Marten + Wolverine + PostgreSQL).
