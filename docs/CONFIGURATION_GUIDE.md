# Configuration Guide

This guide explains how to configure Foundry modules using the Options Pattern and environment-specific settings.

## Overview

Foundry uses the **Microsoft.Extensions.Options pattern** for type-safe configuration. Each module defines its own Options class that binds to a section in `appsettings.json`. This provides:

- **Type safety** - Compile-time checking instead of magic strings
- **Testability** - Easy to mock `IOptions<T>` in unit tests
- **Documentation** - The class itself documents available settings
- **Validation** - Can add validation attributes or custom validators

## Configuration Reference

This section documents all configuration sections used by Foundry. See the "Quick Start" section below for how to create your own module configuration.

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=foundry;Username=foundry;Password=foundry",
    "Redis": "localhost:6379,abortConnect=false",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  }
}
```

| Key | Description | Environment Variable |
|-----|-------------|---------------------|
| `DefaultConnection` | PostgreSQL connection string | `ConnectionStrings__DefaultConnection` |
| `Redis` | Redis/Valkey connection string for caching and SignalR backplane | `ConnectionStrings__Redis` |
| `RabbitMq` | RabbitMQ AMQP URI for Wolverine messaging | `ConnectionStrings__RabbitMq` |

### RabbitMQ

```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Host` | `localhost` | RabbitMQ server hostname |
| `Port` | `5672` | RabbitMQ AMQP port |
| `Username` | `guest` | RabbitMQ username |
| `Password` | `guest` | RabbitMQ password |

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

### Keycloak (Authentication)

```json
{
  "Keycloak": {
    "realm": "foundry",
    "auth-server-url": "http://localhost:8080/",
    "ssl-required": "none",
    "resource": "foundry-api",
    "credentials": {
      "secret": "foundry-api-secret"
    },
    "verify-token-audience": true,
    "confidential-port": 0
  },
  "KeycloakAdmin": {
    "realm": "foundry",
    "auth-server-url": "http://localhost:8080/",
    "resource": "foundry-api",
    "credentials": {
      "secret": "foundry-api-secret"
    }
  }
}
```

| Key | Description |
|-----|-------------|
| `realm` | Keycloak realm name |
| `auth-server-url` | Keycloak server URL (include trailing slash) |
| `ssl-required` | SSL requirement: `none`, `external`, or `all` |
| `resource` | Client ID in Keycloak |
| `credentials.secret` | Client secret from Keycloak |
| `verify-token-audience` | Whether to verify the `aud` claim in JWT |

**Environment-specific values:**
- **Development**: `http://localhost:8080/`, `ssl-required: none`
- **Production**: `https://keycloak.yourdomain.com/`, `ssl-required: external`

### SMTP (Email)

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "DefaultFromAddress": "noreply@foundry.local",
    "DefaultFromName": "Foundry",
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
| `DefaultFromAddress` | `noreply@foundry.local` | Default sender email address |
| `DefaultFromName` | `Foundry` | Default sender display name |
| `MaxRetries` | `3` | Number of retry attempts on failure |
| `TimeoutSeconds` | `30` | SMTP operation timeout |

**Local development**: Use Mailpit at `localhost:1025` (no auth, no SSL). View emails at `http://localhost:8025`.

### OpenTelemetry (Observability)

```json
{
  "OpenTelemetry": {
    "EnableLogging": true,
    "ServiceName": "Foundry",
    "OtlpEndpoint": "http://localhost:4318",
    "OtlpGrpcEndpoint": "http://localhost:4317"
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `EnableLogging` | `false` | Enable OpenTelemetry logging export |
| `ServiceName` | `Foundry` | Service name for traces and metrics |
| `OtlpEndpoint` | `http://localhost:4318` | OTLP HTTP endpoint |
| `OtlpGrpcEndpoint` | `http://localhost:4317` | OTLP gRPC endpoint (used for traces/metrics) |

**Note**: The application currently uses `OtlpGrpcEndpoint` for exporting traces and metrics.

### Storage Module

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/foundry/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "foundry-files",
      "UsePathStyle": true,
      "Region": "us-east-1"
    }
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `Provider` | `Local` | Storage provider: `Local` or `S3` |
| `Local.BasePath` | `/var/foundry/storage` | Local filesystem path for file storage |
| `Local.BaseUrl` | `null` | Base URL for serving files (optional) |
| `S3.Endpoint` | - | S3-compatible endpoint URL |
| `S3.AccessKey` | - | S3 access key |
| `S3.SecretKey` | - | S3 secret key |
| `S3.BucketName` | - | S3 bucket name |
| `S3.UsePathStyle` | `true` | Use path-style URLs (required for MinIO) |
| `S3.Region` | `us-east-1` | S3 region |

---

## Quick Start

### 1. Create an Options Class

Create a class in your module's Infrastructure layer:

```csharp
// src/Modules/YourModule/Foundry.YourModule.Infrastructure/Configuration/YourModuleOptions.cs
namespace Foundry.YourModule.Infrastructure.Configuration;

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
// src/Modules/YourModule/Foundry.YourModule.Infrastructure/Extensions/YourModuleExtensions.cs
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
    "DefaultConnection": "Host=localhost;Port=5432;Database=foundry;Username=foundry;Password=foundry",
    "Redis": "localhost:6379,abortConnect=false",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  },
  "Keycloak": {
    "realm": "foundry",
    "auth-server-url": "https://keycloak.yourdomain.com/",
    "ssl-required": "external",
    "resource": "foundry-api",
    "credentials": {
      "secret": "REPLACE_IN_PRODUCTION"
    }
  },
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/foundry/storage"
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
  "Keycloak": {
    "auth-server-url": "http://localhost:8080/",
    "ssl-required": "none",
    "credentials": {
      "secret": "foundry-api-secret"
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
    "DefaultConnection": "Host=postgres;Port=5432;Database=foundry;Username=OVERRIDE_VIA_ENV_VAR;Password=OVERRIDE_VIA_ENV_VAR"
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Username": "OVERRIDE_VIA_ENV_VAR",
    "Password": "OVERRIDE_VIA_ENV_VAR"
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
      "Endpoint": "https://s3.amazonaws.com",
      "BucketName": "foundry-production"
    }
  }
}
```

## Environment Variables

Environment variables override all JSON configuration. Use double underscores (`__`) for nested keys:

```bash
# Connection strings
export ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=foundry;Username=user;Password=pass"
export ConnectionStrings__Redis="redis-server:6379,password=secret"
export ConnectionStrings__RabbitMq="amqp://user:pass@rabbitmq:5672"

# RabbitMQ
export RabbitMQ__Host="rabbitmq"
export RabbitMQ__Username="foundry"
export RabbitMQ__Password="secret"

# Keycloak
export Keycloak__auth-server-url="https://keycloak.example.com/"
export Keycloak__credentials__secret="your-client-secret"

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
export OpenTelemetry__ServiceName="Foundry"
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
    image: foundry-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=foundry;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      - ConnectionStrings__Redis=valkey:6379
      - ConnectionStrings__RabbitMq=amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672
      - RabbitMQ__Host=rabbitmq
      - RabbitMQ__Username=${RABBITMQ_USER}
      - RabbitMQ__Password=${RABBITMQ_PASSWORD}
      - Keycloak__auth-server-url=http://keycloak:8080/
      - Keycloak__credentials__secret=${KEYCLOAK_CLIENT_SECRET}
      - Storage__Provider=S3
      - Storage__S3__AccessKey=${AWS_ACCESS_KEY}
      - Storage__S3__SecretKey=${AWS_SECRET_KEY}
```

```yaml
# Kubernetes ConfigMap/Secret
apiVersion: v1
kind: ConfigMap
metadata:
  name: foundry-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  RabbitMQ__Host: "rabbitmq"
  Keycloak__auth-server-url: "http://keycloak:8080/"
  Storage__Provider: "S3"
  OpenTelemetry__OtlpGrpcEndpoint: "http://otel-collector:4317"
---
apiVersion: v1
kind: Secret
metadata:
  name: foundry-secrets
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=foundry;Username=user;Password=secret"
  ConnectionStrings__Redis: "redis:6379,password=secret"
  RabbitMQ__Username: "foundry"
  RabbitMQ__Password: "secret"
  Keycloak__credentials__secret: "your-client-secret"
  Storage__S3__AccessKey: "AKIAIOSFODNN7EXAMPLE"
  Storage__S3__SecretKey: "your-secret-key"
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
| PostgreSQL | 5432 | Primary database | `foundry` / `foundry` |
| RabbitMQ | 5672, 15672 | Message broker | `guest` / `guest` |
| Valkey | 6379 | Cache and SignalR backplane | N/A |
| Keycloak | 8080 | Identity provider | `admin` / `admin` |
| Mailpit | 1025, 8025 | Email testing (SMTP: 1025, UI: 8025) | N/A |
| Grafana LGTM | 3000, 4317, 4318 | Observability (Grafana: 3000, OTLP gRPC: 4317, OTLP HTTP: 4318) | `admin` / `admin` |

Docker environment variables are configured in `docker/.env` (copy from `docker/.env.example` and set your own values):

```env
COMPOSE_PROJECT_NAME=foundry
POSTGRES_USER=foundry
POSTGRES_PASSWORD=CHANGE_ME
POSTGRES_DB=foundry
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=CHANGE_ME
KEYCLOAK_ADMIN=admin
KEYCLOAK_ADMIN_PASSWORD=CHANGE_ME
```

## User Secrets (Development Only)

For sensitive configuration during development, use User Secrets to keep credentials out of source control:

```bash
# Initialize user secrets (one-time)
cd src/Foundry.Api
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

## Real Examples in Foundry

### Storage Module

```csharp
// StorageOptions.cs
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Local;
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = "/var/foundry/storage";
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
    "Provider": "Local",
    "Local": {
      "BasePath": "/var/foundry/storage",
      "BaseUrl": "http://localhost:5000"
    },
    "S3": {
      "Endpoint": "http://localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "foundry-files"
    }
  }
}
```

### Notifications Module (Email/SMTP)

```csharp
// SmtpSettings.cs (in Foundry.Notifications.Infrastructure)
public sealed class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string DefaultFromAddress { get; set; } = "noreply@foundry.local";
    public string DefaultFromName { get; set; } = "Foundry";
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
    "DefaultFromAddress": "noreply@foundry.local",
    "DefaultFromName": "Foundry",
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
| Options classes | `src/Modules/{Module}/Foundry.{Module}.Infrastructure/Configuration/` |
| Module registration | `src/Modules/{Module}/Foundry.{Module}.Infrastructure/Extensions/` |
| Base configuration | `src/Foundry.Api/appsettings.json` |
| Environment overrides | `src/Foundry.Api/appsettings.{Environment}.json` |

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
