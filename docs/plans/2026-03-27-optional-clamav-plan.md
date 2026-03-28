# Optional ClamAV Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make ClamAV virus scanning optional and disabled by default, configurable per environment via appsettings.

**Architecture:** Add `ClamAvOptions` sub-object to `StorageOptions`, create `NoOpFileScanner` for when scanning is disabled, conditionally register scanner and health check in DI, add Docker Compose profile for ClamAV container.

**Tech Stack:** .NET 10, Microsoft.Extensions.DependencyInjection, Docker Compose profiles, xUnit/FluentAssertions/NSubstitute

---

### Task 1: Add ClamAvOptions and update StorageOptions

**Files:**
- Modify: `src/Modules/Storage/Wallow.Storage.Infrastructure/Configuration/StorageOptions.cs`

**Step 1: Update StorageOptions**

Replace the flat `ClamAvHost`/`ClamAvPort` properties with a nested `ClamAvOptions` object:

```csharp
public sealed class ClamAvOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
}
```

In `StorageOptions`, remove:
```csharp
public string ClamAvHost { get; set; } = "localhost";
public int ClamAvPort { get; set; } = 3310;
```

Add:
```csharp
public ClamAvOptions ClamAv { get; set; } = new();
```

**Step 2: Verify the build compiles**

Run: `dotnet build src/Modules/Storage/Wallow.Storage.Infrastructure`
Expected: Build errors in `ClamAvFileScanner.cs` and `StorageInfrastructureExtensions.cs` referencing the old properties. That's expected — we fix them next.

**Step 3: Commit**

```bash
git add src/Modules/Storage/Wallow.Storage.Infrastructure/Configuration/StorageOptions.cs
git commit -m "refactor(storage): replace flat ClamAv properties with nested ClamAvOptions"
```

---

### Task 2: Update ClamAvFileScanner to use new options

**Files:**
- Modify: `src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/ClamAvFileScanner.cs`

**Step 1: Update the constructor and ScanAsync**

Change references from `_options.ClamAvHost` / `_options.ClamAvPort` to `_options.ClamAv.Host` / `_options.ClamAv.Port`:

Line 28: `await client.ConnectAsync(_options.ClamAv.Host, _options.ClamAv.Port, cancellationToken);`

**Step 2: Verify the scanner compiles**

Run: `dotnet build src/Modules/Storage/Wallow.Storage.Infrastructure`
Expected: Still build errors in extensions — fixed in next task.

**Step 3: Commit**

```bash
git add src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/ClamAvFileScanner.cs
git commit -m "refactor(storage): update ClamAvFileScanner to use nested ClamAvOptions"
```

---

### Task 3: Create NoOpFileScanner

**Files:**
- Create: `src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/NoOpFileScanner.cs`
- Create: `tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/NoOpFileScannerTests.cs`

**Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Infrastructure.Scanning;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class NoOpFileScannerTests
{
    private readonly NoOpFileScanner _scanner;
    private readonly ILogger<NoOpFileScanner> _logger;

    public NoOpFileScannerTests()
    {
        _logger = Substitute.For<ILogger<NoOpFileScanner>>();
        _scanner = new NoOpFileScanner(_logger);
    }

    [Fact]
    public async Task ScanAsync_ReturnsClean()
    {
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });

        FileScanResult result = await _scanner.ScanAsync(stream, "test.txt");

        result.IsClean.Should().BeTrue();
        result.ThreatName.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_WithEmptyStream_ReturnsClean()
    {
        using MemoryStream stream = new();

        FileScanResult result = await _scanner.ScanAsync(stream, "empty.txt");

        result.IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_SupportsCancellation()
    {
        using MemoryStream stream = new(new byte[] { 1 });
        using CancellationTokenSource cts = new();

        FileScanResult result = await _scanner.ScanAsync(stream, "test.txt", cts.Token);

        result.IsClean.Should().BeTrue();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `./scripts/run-tests.sh storage`
Expected: FAIL — `NoOpFileScanner` doesn't exist yet.

**Step 3: Write the implementation**

```csharp
using Microsoft.Extensions.Logging;
using Wallow.Storage.Application.Interfaces;

namespace Wallow.Storage.Infrastructure.Scanning;

public sealed partial class NoOpFileScanner(ILogger<NoOpFileScanner> logger) : IFileScanner
{
    public Task<FileScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        LogScanSkipped(fileName);
        return Task.FromResult(FileScanResult.Clean());
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Virus scanning disabled — skipping scan for {FileName}")]
    private partial void LogScanSkipped(string fileName);
}
```

**Step 4: Run tests to verify they pass**

Run: `./scripts/run-tests.sh storage`
Expected: All NoOpFileScanner tests PASS.

**Step 5: Commit**

```bash
git add src/Modules/Storage/Wallow.Storage.Infrastructure/Scanning/NoOpFileScanner.cs tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/NoOpFileScannerTests.cs
git commit -m "feat(storage): add NoOpFileScanner for when virus scanning is disabled"
```

---

### Task 4: Update DI registration for conditional scanner + health check

**Files:**
- Modify: `src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageInfrastructureExtensions.cs`

**Step 1: Update AddStorageInfrastructure**

Replace lines 37-38:
```csharp
services.AddScoped<IFileScanner, ClamAvFileScanner>();
services.AddClamAvHealthCheck(configuration);
```

With:
```csharp
services.AddFileScanning(configuration);
```

Add a new private method:
```csharp
private static void AddFileScanning(
    this IServiceCollection services,
    IConfiguration configuration)
{
    StorageOptions storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                                    ?? new StorageOptions();

    if (storageOptions.ClamAv.Enabled)
    {
        services.AddScoped<IFileScanner, ClamAvFileScanner>();
        services.AddHealthChecks()
            .AddCheck(
                "clamav",
                new ClamAvHealthCheck(storageOptions.ClamAv.Host, storageOptions.ClamAv.Port),
                tags: ["clamav"]);
    }
    else
    {
        services.AddSingleton<IFileScanner, NoOpFileScanner>();
    }
}
```

Remove the old `AddClamAvHealthCheck` method (lines 117-129).

Update the `ClamAvHealthCheck` class — it stays the same, but is only registered when enabled.

**Step 2: Verify it compiles**

Run: `dotnet build src/Modules/Storage/Wallow.Storage.Infrastructure`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Modules/Storage/Wallow.Storage.Infrastructure/Extensions/StorageInfrastructureExtensions.cs
git commit -m "feat(storage): conditionally register ClamAV scanner and health check based on config"
```

---

### Task 5: Update StorageExtensionsTests

**Files:**
- Modify: `tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/StorageExtensionsTests.cs`

**Step 1: Update existing tests and add new ones**

Tests to update (config keys changed from `Storage:ClamAvHost` to `Storage:ClamAv:Host`):

1. `AddStorageInfrastructure_WithCustomClamAvConfig_RegistersHealthCheckWithCustomValues` — update config keys to `Storage:ClamAv:Host`, `Storage:ClamAv:Port`, `Storage:ClamAv:Enabled` = `true`
2. `AddStorageInfrastructure_BindsStorageOptions` — update config keys, assert on `options.ClamAv.Host` and `options.ClamAv.Port` instead of `options.ClamAvHost` / `options.ClamAvPort`
3. `AddStorageInfrastructure_WithNullStorageSection_DefaultsToLocalProvider` — assert `options.ClamAv.Host` is `"localhost"`, `options.ClamAv.Port` is `3310`, `options.ClamAv.Enabled` is `false`
4. `AddStorageInfrastructure_RegistersFileScanner` — this now registers `NoOpFileScanner` (default disabled). Update assertion: `scannerDescriptor!.ImplementationType.Should().Be<NoOpFileScanner>()` and `ServiceLifetime.Singleton`
5. `AddStorageInfrastructure_RegistersClamAvHealthCheck` — default is now disabled, so no health check. Update to verify health check is NOT registered by default.
6. `AddStorageInfrastructure_RegistersClamAvHealthCheckWithCorrectNameAndTags` — add `Storage:ClamAv:Enabled` = `true` to config
7. `AddStorageModule_RegistersHealthChecks` — add `Storage:ClamAv:Enabled` = `true` to config, or update assertion to expect no clamav health check when disabled

New tests to add:

```csharp
[Fact]
public void AddStorageInfrastructure_WhenClamAvEnabled_RegistersClamAvFileScanner()
{
    IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        ["Storage:ClamAv:Enabled"] = "true"
    });

    ServiceCollection services = CreateBaseServices(configuration);
    services.AddStorageInfrastructure(configuration);

    ServiceDescriptor? scannerDescriptor = services.FirstOrDefault(
        d => d.ServiceType == typeof(IFileScanner));

    scannerDescriptor.Should().NotBeNull();
    scannerDescriptor!.ImplementationType.Should().Be<ClamAvFileScanner>();
    scannerDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
}

[Fact]
public void AddStorageInfrastructure_WhenClamAvDisabled_RegistersNoOpFileScanner()
{
    IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        ["Storage:ClamAv:Enabled"] = "false"
    });

    ServiceCollection services = CreateBaseServices(configuration);
    services.AddStorageInfrastructure(configuration);

    ServiceDescriptor? scannerDescriptor = services.FirstOrDefault(
        d => d.ServiceType == typeof(IFileScanner));

    scannerDescriptor.Should().NotBeNull();
    scannerDescriptor!.ImplementationType.Should().Be<NoOpFileScanner>();
    scannerDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
}

[Fact]
public void AddStorageInfrastructure_WhenClamAvDisabled_DoesNotRegisterHealthCheck()
{
    IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        ["Storage:ClamAv:Enabled"] = "false"
    });

    ServiceCollection services = CreateBaseServices(configuration);
    services.AddStorageInfrastructure(configuration);
    ServiceProvider provider = services.BuildServiceProvider();

    HealthCheckServiceOptions healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
    HealthCheckRegistration? clamRegistration = healthOptions.Registrations
        .FirstOrDefault(r => r.Name == "clamav");

    clamRegistration.Should().BeNull();
}

[Fact]
public void AddStorageInfrastructure_WhenClamAvEnabled_RegistersHealthCheck()
{
    IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        ["Storage:ClamAv:Enabled"] = "true"
    });

    ServiceCollection services = CreateBaseServices(configuration);
    services.AddStorageInfrastructure(configuration);
    ServiceProvider provider = services.BuildServiceProvider();

    HealthCheckServiceOptions healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
    HealthCheckRegistration? clamRegistration = healthOptions.Registrations
        .FirstOrDefault(r => r.Name == "clamav");

    clamRegistration.Should().NotBeNull();
    clamRegistration!.Tags.Should().Contain("clamav");
}

[Fact]
public void AddStorageInfrastructure_DefaultConfig_DisablesClamAv()
{
    IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
    });

    ServiceCollection services = CreateBaseServices(configuration);
    services.AddStorageInfrastructure(configuration);
    ServiceProvider provider = services.BuildServiceProvider();

    StorageOptions options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

    options.ClamAv.Enabled.Should().BeFalse();
    options.ClamAv.Host.Should().Be("localhost");
    options.ClamAv.Port.Should().Be(3310);
}
```

**Step 2: Run tests to verify all pass**

Run: `./scripts/run-tests.sh storage`
Expected: All PASS.

**Step 3: Commit**

```bash
git add tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/StorageExtensionsTests.cs
git commit -m "test(storage): update extension tests for optional ClamAV configuration"
```

---

### Task 6: Update ClamAvFileScannerTests for new config shape

**Files:**
- Modify: `tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/ClamAvFileScannerTests.cs`

**Step 1: Update test setup**

The tests create `StorageOptions` with `ClamAvHost`/`ClamAvPort`. Update all instances to use the nested `ClamAv` sub-object:

Where tests set:
```csharp
options.Value.ClamAvHost = ...
options.Value.ClamAvPort = ...
```

Change to:
```csharp
options.Value.ClamAv.Host = ...
options.Value.ClamAv.Port = ...
```

Search for all references to `ClamAvHost` and `ClamAvPort` in the test file and update them.

**Step 2: Run tests**

Run: `./scripts/run-tests.sh storage`
Expected: All PASS.

**Step 3: Commit**

```bash
git add tests/Modules/Storage/Wallow.Storage.Tests/Infrastructure/ClamAvFileScannerTests.cs
git commit -m "test(storage): update ClamAvFileScanner tests for nested ClamAvOptions"
```

---

### Task 7: Docker Compose profile for ClamAV

**Files:**
- Modify: `docker/docker-compose.yml`
- Modify: `docker/docker-compose.dev.yml`

**Step 1: Add profile to clamav service in docker-compose.yml**

Add `profiles: ["clamav"]` to the clamav service (after `restart: unless-stopped`, before the next section):

```yaml
  clamav:
    image: clamav/clamav:1.5.2
    platform: linux/amd64
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-clamav
    ports:
      - "127.0.0.1:3310:3310"
    healthcheck:
      test: ["CMD", "/usr/local/bin/clamdcheck.sh"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - wallow
    restart: unless-stopped
    profiles: ["clamav"]
```

**Step 2: Remove clamav from docker-compose.dev.yml**

Remove the entire clamav section (lines 37-40) from `docker-compose.dev.yml`. When the profile is active, the base compose ports are used.

**Step 3: Commit**

```bash
git add docker/docker-compose.yml docker/docker-compose.dev.yml
git commit -m "chore(docker): move ClamAV to opt-in profile"
```

---

### Task 8: Update production env example and CLAUDE.md

**Files:**
- Modify: `deploy/.env.production.example`
- Modify: `CLAUDE.md`

**Step 1: Update .env.production.example**

Replace lines 124-126:
```
# ClamAV antivirus scanning (optional — only if running ClamAV container)
# Storage__ClamAvHost=clamav
# Storage__ClamAvPort=3310
```

With:
```
# ClamAV antivirus scanning (disabled by default — enable if running ClamAV container)
# Storage__ClamAv__Enabled=true
# Storage__ClamAv__Host=clamav
# Storage__ClamAv__Port=3310
```

**Step 2: Update CLAUDE.md**

In the commands section, update the docker compose comment to mention the clamav profile:
```
# Start infrastructure (Postgres, Valkey, GarageHQ, Mailpit, Grafana)
cd docker && docker compose up -d

# Start infrastructure with ClamAV virus scanning
cd docker && docker compose --profile clamav up -d
```

**Step 3: Commit**

```bash
git add deploy/.env.production.example CLAUDE.md
git commit -m "docs: update ClamAV configuration examples for opt-in model"
```

---

### Task 9: Run full storage test suite and verify

**Step 1: Run all storage tests**

Run: `./scripts/run-tests.sh storage`
Expected: All PASS with 0 failures.

**Step 2: Run architecture tests**

Run: `./scripts/run-tests.sh arch`
Expected: All PASS — no architecture violations.

**Step 3: Final commit if any fixups needed, then push**

```bash
git push
```
