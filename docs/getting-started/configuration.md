# Configuration Guide

This guide explains how to configure Wallow modules using the Options Pattern and environment-specific settings.

## Overview

Wallow uses the **Microsoft.Extensions.Options pattern** for type-safe configuration. Each module defines its own Options class that binds to a section in `appsettings.json`. This provides:

- **Type safety** - Compile-time checking instead of magic strings
- **Testability** - Easy to mock `IOptions<T>` in unit tests
- **Documentation** - The class itself documents available settings
- **Validation** - Can add validation attributes or custom validators

## Configuration Reference

This section documents all configuration sections used by Wallow. See the "Quick Start" section below for how to create your own module configuration.

### Branding

Branding controls the user-facing identity of auth pages (login, register, password reset) and web layouts. Configuration is loaded from `branding.json` in the repository root.

**BrandingOptions properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AppName` | `string` | `"Wallow"` | Application name shown in page titles and headers |
| `AppIcon` | `string` | `"piggy-icon.svg"` | Icon filename (served from `wwwroot`) |
| `Tagline` | `string` | `"Wallow in it"` | Tagline shown on auth pages |
| `RepositoryUrl` | `string` | `""` | Link to source repository |
| `Theme` | `ThemeOptions` | see below | Light/dark theme color tokens |

**ThemeOptions:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultMode` | `string` | `"dark"` | Initial theme: `"light"` or `"dark"` |
| `Light` | `ThemeColorSet` | - | OKLCH color tokens for light mode |
| `Dark` | `ThemeColorSet` | - | OKLCH color tokens for dark mode |

**ThemeColorSet tokens** (all use OKLCH color format):

`Background`, `Foreground`, `Card`, `CardForeground`, `Popover`, `PopoverForeground`, `Primary`, `PrimaryForeground`, `Secondary`, `SecondaryForeground`, `Muted`, `MutedForeground`, `Accent`, `AccentForeground`, `Destructive`, `DestructiveForeground`, `Border`, `Input`, `Ring`, `Radius`

**Config keys** (environment variable format):

```bash
Branding__AppName="MyProduct"
Branding__AppIcon="my-icon.svg"
Branding__Tagline="Your tagline"
Branding__RepositoryUrl="https://github.com/your-org/your-repo"
Branding__Theme__DefaultMode="light"
Branding__Theme__Light__Primary="oklch(0.55 0.15 250)"
Branding__Theme__Dark__Primary="oklch(0.65 0.15 250)"
```

**branding.json** (located at repository root):

```json
{
  "appName": "YourProduct",
  "appIcon": "your-icon.svg",
  "tagline": "Your tagline here",
  "theme": {
    "defaultMode": "dark",
    "light": {
      "primary": "oklch(0.52 0.12 45)",
      "primaryForeground": "oklch(0.96 0.01 60)",
      "background": "oklch(0.96 0.01 60)",
      "foreground": "oklch(0.20 0.03 55)"
    },
    "dark": {
      "primary": "oklch(0.62 0.13 45)",
      "primaryForeground": "oklch(0.14 0.015 50)",
      "background": "oklch(0.16 0.02 50)",
      "foreground": "oklch(0.88 0.02 55)"
    }
  }
}
```

Both `Wallow.Auth` and `Wallow.Web` load `branding.json` at startup and bind it to `BrandingOptions`. The Auth boundary is the canonical owner; the Web boundary has a local copy of the options class. Color tokens are injected as CSS custom properties via the `BrandingTheme.razor` component.

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow",
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

| Key | Description | Environment Variable |
|-----|-------------|---------------------|
| `DefaultConnection` | PostgreSQL connection string | `ConnectionStrings__DefaultConnection` |
| `Redis` | Redis/Valkey connection string for caching and SignalR backplane | `ConnectionStrings__Redis` |

### Messaging Transport (Optional)

Wolverine uses in-memory transport by default. To enable RabbitMQ for cross-instance messaging, set `ModuleMessaging:Transport` to `RabbitMq` and add the connection string. See `docs/plans/2026-03-24-wolverine-rabbitmq-transport-design.md`.

```json
{
  "ModuleMessaging": {
    "Transport": "RabbitMq"
  },
  "ConnectionStrings": {
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  }
}
```

### CORS

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

| Key | Description |
|-----|-------------|
| `AllowedOrigins` | Array of allowed CORS origins for API requests |

### SMTP (Email)

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "DefaultFromAddress": "noreply@wallow.local",
    "DefaultFromName": "Wallow",
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Host` | `localhost` | SMTP server hostname |
| `Port` | `1025` | SMTP port (1025 for Mailpit, 587 for production) |
| `UseSsl` | `false` | Enable TLS/SSL |
| `Username` | `null` | SMTP authentication username (optional) |
| `Password` | `null` | SMTP authentication password (optional) |
| `DefaultFromAddress` | `noreply@wallow.local` | Default sender email address |
| `DefaultFromName` | `Wallow` | Default sender display name |
| `MaxRetries` | `3` | Number of retry attempts on failure |
| `TimeoutSeconds` | `30` | SMTP operation timeout |

**Local development**: Use Mailpit at `localhost:1025` (no auth, no SSL). View emails at `http://localhost:8025`.

### OpenTelemetry (Observability)

```json
{
  "OpenTelemetry": {
    "EnableLogging": true,
    "ServiceName": "Wallow",
    "OtlpEndpoint": "http://localhost:4318",
    "OtlpGrpcEndpoint": "http://localhost:4317"
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `EnableLogging` | `false` | Enable OpenTelemetry logging export |
| `ServiceName` | `Wallow` | Service name for traces and metrics |
| `OtlpEndpoint` | `http://localhost:4318` | OTLP HTTP endpoint |
| `OtlpGrpcEndpoint` | `http://localhost:4317` | OTLP gRPC endpoint (used for traces/metrics) |

**Note**: The application currently uses `OtlpGrpcEndpoint` for exporting traces and metrics.

### Storage Module

Wallow uses GarageHQ as the default S3-compatible object storage. The `S3StorageProvider` works with any S3-compatible backend (GarageHQ, AWS S3, Cloudflare R2, MinIO).

```json
{
  "Storage": {
    "Provider": "S3",
    "Local": {
      "BasePath": "/var/wallow/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "wallow",
      "SecretKey": "wallowsecretkey1234567890abcdefgh",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    },
    "ClamAv": {
      "Enabled": true,
      "Host": "clamav",
      "Port": 3310
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Provider` | `S3` | Storage provider: `Local` or `S3` |
| `Local.BasePath` | `/var/wallow/storage` | Local filesystem path for file storage |
| `Local.BaseUrl` | `null` | Base URL for serving files (optional) |
| `S3.Endpoint` | `http://localhost:3900` | S3-compatible endpoint URL (GarageHQ default) |
| `S3.AccessKey` | - | S3 access key |
| `S3.SecretKey` | - | S3 secret key |
| `S3.BucketName` | `wallow-files` | S3 bucket name |
| `S3.UsePathStyle` | `true` | Use path-style URLs (required for GarageHQ and MinIO) |
| `S3.Region` | `us-east-1` | S3 region |
| `ClamAv.Enabled` | `false` | Enable ClamAV virus scanning on file uploads |
| `ClamAv.Host` | `localhost` | ClamAV daemon hostname |
| `ClamAv.Port` | `3310` | ClamAV daemon port |

**Local development**: GarageHQ runs on `http://localhost:3900` via Docker Compose. The init script auto-creates the access key and bucket. Admin API at `http://localhost:3903`.

#### ClamAV Virus Scanning (Optional)

ClamAV virus scanning is **disabled by default**. When disabled, file uploads skip scanning entirely (a no-op scanner returns clean for all files). To enable scanning:

1. Start the ClamAV container using the Docker Compose profile:

   ```bash
   cd docker && docker compose --profile clamav up -d
   ```

2. Enable scanning in your configuration:

   ```json
   {
     "Storage": {
       "ClamAv": {
         "Enabled": true,
         "Host": "localhost",
         "Port": 3310
       }
     }
   }
   ```

   Or via environment variables:

   ```bash
   Storage__ClamAv__Enabled=true
   Storage__ClamAv__Host=localhost
   Storage__ClamAv__Port=3310
   ```

When enabled, all file uploads are scanned synchronously before storage. Infected files are rejected with a validation error. A ClamAV health check is also registered at `/health` (tagged `clamav`).

---

## Quick Start

### 1. Create an Options Class

Create a class in your module's Infrastructure layer:

```csharp
// src/Modules/YourModule/Wallow.YourModule.Infrastructure/Configuration/YourModuleOptions.cs
namespace Wallow.YourModule.Infrastructure.Configuration;

public sealed class YourModuleOptions
{
    public const string SectionName = "YourModule";

    public string ApiKey { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### 2. Register in Module Extensions

Bind the configuration section in your module's extension method:

```csharp
// src/Modules/YourModule/Wallow.YourModule.Infrastructure/Extensions/YourModuleExtensions.cs
public static IServiceCollection AddYourModuleInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind configuration section to options
    services.Configure<YourModuleOptions>(
        configuration.GetSection(YourModuleOptions.SectionName));

    // ... other registrations
    return services;
}
```

### 3. Add Configuration to appsettings.json

```json
{
  "YourModule": {
    "ApiKey": "your-api-key",
    "MaxRetries": 5,
    "Timeout": "00:00:45"
  }
}
```

### 4. Inject and Use Options

In services or controllers:

```csharp
public class YourService
{
    private readonly YourModuleOptions _options;

    public YourService(IOptions<YourModuleOptions> options)
    {
        _options = options.Value;
    }

    public async Task DoSomethingAsync()
    {
        if (_options.MaxRetries > 0)
        {
            // Use the configured value
        }
    }
}
```

In controllers:

```csharp
public class YourController : ControllerBase
{
    private readonly YourModuleOptions _options;

    public YourController(IOptions<YourModuleOptions> options)
    {
        _options = options.Value;
    }

    [HttpPost]
    public IActionResult ValidateApiKey([FromQuery] string apiKey)
    {
        if (apiKey != _options.ApiKey)
            return Unauthorized();

        return Ok();
    }
}
```

## Environment-Specific Configuration

.NET supports layered configuration files that override each other based on the environment.

### Configuration Loading Order

Configuration is loaded in this order (later sources override earlier ones):

1. `appsettings.json` - Base configuration (all environments)
2. `appsettings.{Environment}.json` - Environment-specific overrides
3. Environment variables
4. Command-line arguments
5. User secrets (Development only)

### Environment Names

| Environment | File | Usage |
|-------------|------|-------|
| Development | `appsettings.Development.json` | Local development |
| Staging | `appsettings.Staging.json` | Pre-production testing |
| Production | `appsettings.Production.json` | Live production |
| Testing | `appsettings.Testing.json` | Integration tests |

### Setting the Environment

```bash
# Via environment variable (recommended for servers)
export ASPNETCORE_ENVIRONMENT=Production

# Via command line
dotnet run --environment Production

# Via launchSettings.json (local development)
# Already configured in Properties/launchSettings.json
```

### Example: Environment-Specific Files

**appsettings.json** (base configuration):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "wallow",
      "SecretKey": "wallowsecretkey1234567890abcdefgh",
      "BucketName": "wallow-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

**appsettings.Development.json** (local dev overrides):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "OpenTelemetry": {
    "EnableLogging": true
  }
}
```

**appsettings.Production.json** (production overrides):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=wallow;Username=OVERRIDE_VIA_ENV_VAR;Password=OVERRIDE_VIA_ENV_VAR"
  },
  "Smtp": {
    "Host": "OVERRIDE_VIA_ENV_VAR",
    "Port": 587,
    "UseSsl": true,
    "Username": "OVERRIDE_VIA_ENV_VAR",
    "Password": "OVERRIDE_VIA_ENV_VAR"
  },
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Endpoint": "http://garage:3900",
      "AccessKey": "OVERRIDE_VIA_ENV_VAR",
      "SecretKey": "OVERRIDE_VIA_ENV_VAR",
      "BucketName": "wallow-files"
    }
  }
}
```

## Environment Variables

Environment variables override all JSON configuration. Use double underscores (`__`) for nested keys:

```bash
# Connection strings
export ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=wallow;Username=user;Password=pass"
export ConnectionStrings__Redis="redis-server:6379,password=secret"

# SMTP
export Smtp__Host="smtp.example.com"
export Smtp__Port="587"
export Smtp__UseSsl="true"
export Smtp__Username="smtp-user"
export Smtp__Password="smtp-password"

# Storage
export Storage__Provider="S3"
export Storage__S3__Endpoint="https://s3.amazonaws.com"
export Storage__S3__AccessKey="AKIAIOSFODNN7EXAMPLE"
export Storage__S3__SecretKey="your-secret-key"
export Storage__S3__BucketName="my-production-bucket"

# OpenTelemetry
export OpenTelemetry__ServiceName="Wallow"
export OpenTelemetry__OtlpGrpcEndpoint="http://otel-collector:4317"

# CORS
export Cors__AllowedOrigins__0="https://app.example.com"
export Cors__AllowedOrigins__1="https://admin.example.com"
```

### Docker/Kubernetes

In containerized deployments, pass configuration via environment variables:

```yaml
# docker-compose.yml
services:
  api:
    image: wallow-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=wallow;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=valkey:6379
      - Storage__Provider=S3
      - Storage__S3__Endpoint=http://garage:3900
      - Storage__S3__AccessKey=${GARAGE_ACCESS_KEY}
      - Storage__S3__SecretKey=${GARAGE_SECRET_KEY}
      - Storage__S3__BucketName=${GARAGE_BUCKET}
```

```yaml
# Kubernetes ConfigMap/Secret
apiVersion: v1
kind: ConfigMap
metadata:
  name: wallow-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  Storage__Provider: "S3"
  OpenTelemetry__OtlpGrpcEndpoint: "http://otel-collector:4317"
---
apiVersion: v1
kind: Secret
metadata:
  name: wallow-secrets
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=wallow;Username=user;Password=secret"
  ConnectionStrings__Redis: "redis:6379,password=secret"
  Storage__S3__Endpoint: "http://garage:3900"
  Storage__S3__AccessKey: "your-access-key"
  Storage__S3__SecretKey: "your-secret-key"
  Storage__S3__BucketName: "wallow-files"
```

## Local Development Infrastructure

Start infrastructure services using Docker Compose:

```bash
cd docker
docker compose up -d
```

This starts the following services:

| Service | Port | Purpose | Default Credentials |
|---------|------|---------|---------------------|
| PostgreSQL | 5432 | Primary database | `wallow` / `wallow` |
| Valkey | 6379 | Cache and SignalR backplane | See `docker/.env` |
| GarageHQ | 3900, 3903 | S3-compatible object storage (S3 API: 3900, Admin: 3903) | See `docker/.env` |
| Mailpit | 1025, 8025 | Email testing (SMTP: 1025, UI: 8025) | N/A |
| ClamAV (optional) | 3310 | Antivirus file scanning (`--profile clamav`) | N/A |
| Grafana LGTM | 3000, 4317, 4318 | Observability (Grafana: 3000, OTLP gRPC: 4317, OTLP HTTP: 4318) | `admin` / `admin` |

Docker environment variables are configured in `docker/.env` (copy from `docker/.env.example` and set your own values):

```env
COMPOSE_PROJECT_NAME=wallow
POSTGRES_USER=wallow
POSTGRES_PASSWORD=CHANGE_ME
POSTGRES_DB=wallow
VALKEY_PASSWORD=CHANGE_ME
GARAGE_KEY_NAME=wallow-dev
GARAGE_ACCESS_KEY=CHANGE_ME
GARAGE_SECRET_KEY=CHANGE_ME
GARAGE_BUCKET=wallow-files
```

## User Secrets (Development Only)

For sensitive configuration during development, use User Secrets to keep credentials out of source control:

```bash
# Initialize user secrets (one-time)
cd src/Wallow.Api
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "YourModule:ApiKey" "my-dev-api-key"
dotnet user-secrets set "Storage:S3:SecretKey" "my-s3-secret"

# List secrets
dotnet user-secrets list

# Clear all secrets
dotnet user-secrets clear
```

Secrets are stored in:
- **macOS/Linux**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`

## Real Examples in Wallow

### Storage Module

```csharp
// StorageOptions.cs
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Local;
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
    public ClamAvOptions ClamAv { get; set; } = new();
}

public sealed class ClamAvOptions
{
    public bool Enabled { get; set; }        // false by default
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
}

public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = "/var/wallow/storage";
    public string? BaseUrl { get; set; }
}

public sealed class S3StorageOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public bool UsePathStyle { get; set; } = true;
    public string Region { get; set; } = "us-east-1";
}
```

```json
// appsettings.json
{
  "Storage": {
    "Provider": "S3",
    "Local": {
      "BasePath": "/var/wallow/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:3900",
      "AccessKey": "wallow",
      "SecretKey": "wallowsecretkey1234567890abcdefgh",
      "BucketName": "wallow-files"
    }
  }
}
```

### Notifications Module (Email/SMTP)

```csharp
// SmtpSettings.cs (in Wallow.Notifications.Infrastructure)
public sealed class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string DefaultFromAddress { get; set; } = "noreply@wallow.local";
    public string DefaultFromName { get; set; } = "Wallow";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
}
```

```json
// appsettings.json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "DefaultFromAddress": "noreply@wallow.local",
    "DefaultFromName": "Wallow",
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  }
}
```

## Advanced Patterns

### Selecting Implementation at Startup

Use configuration to choose which implementation to register:

```csharp
private static IServiceCollection AddStorageProvider(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind options for DI
    services.Configure<StorageOptions>(
        configuration.GetSection(StorageOptions.SectionName));

    // Read immediately to select provider
    var storageOptions = configuration
        .GetSection(StorageOptions.SectionName)
        .Get<StorageOptions>() ?? new StorageOptions();

    switch (storageOptions.Provider)
    {
        case StorageProvider.S3:
            services.AddSingleton<IStorageProvider, S3StorageProvider>();
            break;
        case StorageProvider.Local:
        default:
            services.AddSingleton<IStorageProvider, LocalStorageProvider>();
            break;
    }

    return services;
}
```

### IOptionsSnapshot for Reloadable Configuration

Use `IOptionsSnapshot<T>` when you need configuration that reloads on change:

```csharp
public class MyService
{
    private readonly IOptionsSnapshot<YourModuleOptions> _options;

    public MyService(IOptionsSnapshot<YourModuleOptions> options)
    {
        _options = options;
    }

    public void DoWork()
    {
        // Gets current value on each request
        var currentOptions = _options.Value;
    }
}
```

### IOptionsMonitor for Background Services

Use `IOptionsMonitor<T>` in singletons or background services:

```csharp
public class BackgroundWorker : BackgroundService
{
    private readonly IOptionsMonitor<YourModuleOptions> _options;

    public BackgroundWorker(IOptionsMonitor<YourModuleOptions> options)
    {
        _options = options;

        // React to configuration changes
        _options.OnChange(newOptions =>
        {
            // Handle configuration update
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentValue = _options.CurrentValue;
            // Use current configuration
        }
    }
}
```

### Validation with Data Annotations

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class YourModuleOptions
{
    public const string SectionName = "YourModule";

    [Required]
    [MinLength(10)]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;
}

// Registration with validation
services.AddOptions<YourModuleOptions>()
    .Bind(configuration.GetSection(YourModuleOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart(); // Fail fast if invalid
```

## Best Practices

| Do | Don't |
|----|-------|
| Use Options classes with `SectionName` constants | Use magic strings like `configuration["Module:Setting"]` |
| Inject `IOptions<T>` into services | Inject `IConfiguration` directly into services |
| Set sensible defaults in Options classes | Require all settings to be configured |
| Use environment variables for secrets in production | Commit secrets to source control |
| Use User Secrets for local development secrets | Store API keys in appsettings.json |
| Validate configuration with `ValidateOnStart()` | Let the app crash with cryptic errors |
| Keep Options classes in Infrastructure layer | Put configuration in Domain layer |

## File Locations

| Purpose | Location |
|---------|----------|
| Options classes | `src/Modules/{Module}/Wallow.{Module}.Infrastructure/Configuration/` |
| Module registration | `src/Modules/{Module}/Wallow.{Module}.Infrastructure/Extensions/` |
| Base configuration | `src/Wallow.Api/appsettings.json` |
| Environment overrides | `src/Wallow.Api/appsettings.{Environment}.json` |

## Troubleshooting

### Configuration Not Loading

1. Check the section name matches exactly (case-sensitive)
2. Verify the JSON structure matches the Options class hierarchy
3. Check environment variable naming: `Section__Property`

### Options Are Null

Ensure you're registering with `services.Configure<T>()` before the service is resolved:

```csharp
// This must happen in AddYourModule(), not later
services.Configure<YourModuleOptions>(
    configuration.GetSection(YourModuleOptions.SectionName));
```

### Environment Not Being Applied

```bash
# Check current environment
echo $ASPNETCORE_ENVIRONMENT

# Verify file exists
ls -la appsettings.Production.json
```
