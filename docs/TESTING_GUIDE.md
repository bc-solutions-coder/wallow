# Testing Guide

This guide covers testing practices, patterns, and conventions for the Wallow platform.

## 1. Overview

### Test Frameworks

| Package | Purpose |
|---------|---------|
| xUnit | Test framework |
| FluentAssertions | Fluent assertion library |
| NSubstitute | Mocking library |
| Testcontainers | Docker-based integration testing |
| NetArchTest | Architecture rule validation |
| Bogus | Fake data generation |
| FluentValidation.TestHelper | Validator testing utilities |

### Test Categories

| Category | Purpose | Infrastructure |
|----------|---------|----------------|
| **Unit Tests** | Test individual components in isolation | No external dependencies |
| **Integration Tests** | Test API endpoints and cross-cutting concerns | Real PostgreSQL, RabbitMQ, Redis via Testcontainers |
| **Architecture Tests** | Enforce architectural rules | Reflection-based, no runtime dependencies |
| **Messaging Tests** | Test event publish/consume flows | Real RabbitMQ via Testcontainers |

## 2. Test Project Structure

### Location Conventions

```
tests/
├── Wallow.Tests.Common/           # Shared test infrastructure
│   ├── Factories/                  # WebApplicationFactory implementations
│   ├── Fixtures/                   # Test fixtures (Database, RabbitMQ, Redis)
│   ├── Helpers/                    # Test utilities and extensions
│   └── Builders/                   # Test data builders
├── Wallow.Api.Tests/              # API integration tests
├── Wallow.Architecture.Tests/     # Architecture enforcement tests
├── Wallow.Messaging.IntegrationTests/ # Messaging integration tests
├── Wallow.Shared.Kernel.Tests/    # Shared kernel unit tests
├── Wallow.Shared.Infrastructure.Tests/ # Shared infrastructure tests
└── Modules/
    └── {Module}/
        └── Wallow.{Module}.Tests/ # Module-specific tests (single project per module)
            ├── Domain/             # Domain entity tests
            ├── Application/        # Command/query handler and validator tests
            └── Infrastructure/     # Repository and query tests
```

### Project Setup

Test projects inherit common settings from `tests/Directory.Build.props`:

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>

  <PropertyGroup>
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  </PropertyGroup>
</Project>
```

### Module Test Project Configuration

Example test project file (`Wallow.Billing.Tests.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Wallow.Billing.Application.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Bogus" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\..\src\Modules\Billing\Wallow.Billing.Application\Wallow.Billing.Application.csproj" />
    <ProjectReference Include="..\..\..\..\src\Modules\Billing\Wallow.Billing.Domain\Wallow.Billing.Domain.csproj" />
    <ProjectReference Include="..\..\..\Wallow.Tests.Common\Wallow.Tests.Common.csproj" />
  </ItemGroup>
</Project>
```

## 3. Unit Tests

### Test Naming Convention

Use the pattern: `Method_Scenario_ExpectedResult`

```csharp
public class CreateInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesInvoice()
    {
        // ...
    }

    [Fact]
    public async Task Handle_WithDuplicateInvoiceNumber_ReturnsFailure()
    {
        // ...
    }
}
```

### Handler Testing with NSubstitute

Test command/query handlers by mocking their dependencies:

```csharp
public class CreateInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly CreateInvoiceHandler _handler;

    public CreateInvoiceHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _handler = new CreateInvoiceHandler(_repository, _messageBus);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesInvoice()
    {
        // Arrange
        var command = new CreateInvoiceCommand(
            UserId: Guid.NewGuid(),
            InvoiceNumber: "INV-001",
            Currency: "USD",
            DueDate: DateTime.UtcNow.AddDays(30));

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.InvoiceNumber.Should().Be("INV-001");

        _repository.Received(1).Add(Arg.Any<Invoice>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WithDuplicateInvoiceNumber_ReturnsFailure()
    {
        // Arrange
        var command = new CreateInvoiceCommand(
            UserId: Guid.NewGuid(),
            InvoiceNumber: "INV-001",
            Currency: "USD",
            DueDate: null);

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");

        _repository.DidNotReceive().Add(Arg.Any<Invoice>());
    }
}
```

### Validator Testing

Use FluentValidation's `TestValidate` extension:

```csharp
public class CreateInvoiceValidatorTests
{
    private readonly CreateInvoiceValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        var command = new CreateInvoiceCommand(
            Guid.Empty,
            "INV-001",
            "USD",
            null
        );

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U")]
    public void Should_Have_Error_When_Currency_Is_Not_3_Characters(string currency)
    {
        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            currency,
            null
        );

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency must be a 3-letter ISO code");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            "USD",
            DateTime.UtcNow.AddDays(30)
        );

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

### Domain Entity Testing

Test domain entities focus on behavior and domain events:

```csharp
public class InvoiceCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInvoiceInDraftStatus()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid createdBy = Guid.NewGuid();

        // Act
        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", createdBy);

        // Assert
        invoice.Should().NotBeNull();
        invoice.UserId.Should().Be(userId);
        invoice.InvoiceNumber.Should().Be("INV-001");
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldRaiseInvoiceCreatedDomainEvent()
    {
        // Act
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-002", "USD", Guid.NewGuid());

        // Assert
        invoice.DomainEvents.Should().ContainSingle();
        invoice.DomainEvents.First().Should().BeOfType<InvoiceCreatedDomainEvent>();
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-003", "USD", Guid.NewGuid());
        invoice.ClearDomainEvents();

        // Act
        invoice.Cancel(Guid.NewGuid());

        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        invoice.DomainEvents.Should().ContainSingle();
        invoice.DomainEvents.First().Should().BeOfType<InvoiceCancelledDomainEvent>();
    }
}
```

## 4. Integration Tests

### WallowApiFactory Setup

`WallowApiFactory` extends `WebApplicationFactory<Program>` and manages Testcontainers:

```csharp
public class WallowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("wallow_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.2-management-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitMq.StartAsync(),
            _redis.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", _rabbitMq.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redis.GetConnectionString() + ",allowAdmin=true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:RabbitMq"] = _rabbitMq.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString() + ",allowAdmin=true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace authentication with test handler
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Fixed tenant context for tests
            services.AddScoped<ITenantContext>(_ => new TenantContext
            {
                TenantId = TenantId.Create(TestConstants.TestTenantId),
                TenantName = "Test Tenant",
                IsResolved = true
            });
        });
    }
}
```

### TestAuthHandler for Authentication Bypass

Tests use `TestAuthHandler` instead of real Keycloak authentication:

```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Skip auth if explicitly requested
        if (Request.Headers.TryGetValue("X-Test-Auth-Skip", out var skipHeader) && skipHeader == "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Check for Authorization header or SignalR access_token
        var hasAuthHeader = Request.Headers.ContainsKey("Authorization");
        var hasAccessToken = Request.Query.ContainsKey("access_token");

        if (!hasAuthHeader && !hasAccessToken)
        {
            return Task.FromResult(AuthenticateResult.Fail("No authorization token provided"));
        }

        // Parse user ID and roles from test headers or token
        string userId;
        string[] roles;

        if (Request.Headers.TryGetValue("X-Test-User-Id", out var userIdHeader))
        {
            userId = userIdHeader.ToString();
            roles = Request.Headers.TryGetValue("X-Test-Roles", out var rolesHeader)
                ? rolesHeader.ToString().Split(',')
                : new[] { "admin" };
        }
        else
        {
            // Parse from test token or use defaults
            userId = TestConstants.AdminUserId.ToString();
            roles = new[] { "admin" };
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, $"{userId}@test.com"),
            new("organization", TestConstants.TestOrgId.ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

### HttpClient Usage Patterns

```csharp
public class HealthCheckTests : IAsyncLifetime
{
    private WallowApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WallowApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Health_Endpoint_Returns_Status()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var healthReport = await response.Content.ReadFromJsonAsync<HealthResponse>();
        healthReport.Should().NotBeNull();
        healthReport!.Status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
    }
}
```

### Authentication in HttpClient

Use the `WithAuth` extension method:

```csharp
public static class HttpClientExtensions
{
    public static HttpClient WithAuth(this HttpClient client, string userId, string[]? roles = null)
    {
        var token = JwtTokenHelper.GenerateToken(userId, roles: roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

// Usage
var client = _factory.CreateClient().WithAuth("user-123", new[] { "admin" });
```

### SignalR Integration Testing

```csharp
public class RealtimeHubIntegrationTests : IAsyncLifetime
{
    private WallowApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WallowApiFactory();
        await _factory.InitializeAsync();
        _ = _factory.Server;
    }

    [Fact]
    public async Task AuthenticatedClient_CanConnect()
    {
        await using var connection = CreateHubConnection("user-1");

        await connection.StartAsync();

        connection.State.Should().Be(HubConnectionState.Connected);

        await connection.StopAsync();
    }

    [Fact]
    public async Task Client_ReceivesNotification()
    {
        const string userId = "user-notif";
        await using var connection = CreateHubConnection(userId);
        var tcs = new TaskCompletionSource<RealtimeEnvelope>();

        connection.On<RealtimeEnvelope>("ReceiveNotifications", envelope => tcs.TrySetResult(envelope));
        await connection.StartAsync();
        await Task.Delay(500); // Allow LongPolling cycle to establish

        var dispatcher = _factory.Services.GetRequiredService<IRealtimeDispatcher>();
        await dispatcher.SendToUserAsync(userId, RealtimeEnvelope.Create("Notifications", "TaskAssigned", new { TaskId = 42 }));

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        received.Module.Should().Be("Notifications");
        received.Type.Should().Be("TaskAssigned");
    }

    private HubConnection CreateHubConnection(string userId)
    {
        var token = JwtTokenHelper.GenerateToken(userId);

        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/realtime", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
```

## 5. Architecture Tests

Architecture tests use NetArchTest to enforce design rules at compile time.

### Clean Architecture Layer Dependencies

```csharp
public class CleanArchitectureTests
{
    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (var moduleName in TestConstants.AllModules)
        {
            yield return new object[] { moduleName };
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void DomainLayer_ShouldNotDependOn_ApplicationLayer(string moduleName)
    {
        var domainAssembly = Assembly.Load($"Wallow.{moduleName}.Domain");

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn($"Wallow.{moduleName}.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain layer in {moduleName} module should not depend on Application layer. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void DomainLayer_ShouldNotDependOn_EntityFramework(string moduleName)
    {
        var domainAssembly = Assembly.Load($"Wallow.{moduleName}.Domain");

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void Entities_ShouldBeSealed(string moduleName)
    {
        var domainAssembly = Assembly.Load($"Wallow.{moduleName}.Domain");

        var result = Types.InAssembly(domainAssembly)
            .That()
            .ResideInNamespace($"Wallow.{moduleName}.Domain.Entities")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

### Module Isolation Tests

```csharp
public class ModuleIsolationTests
{
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];

    public static IEnumerable<object[]> GetModulePairs()
    {
        foreach (var sourceModule in TestConstants.AllModules)
        {
            foreach (var targetModule in TestConstants.AllModules)
            {
                if (sourceModule == targetModule) continue;

                foreach (var sourceLayer in Layers)
                {
                    foreach (var targetLayer in Layers)
                    {
                        yield return new object[] { sourceModule, sourceLayer, targetModule, targetLayer };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetModulePairs))]
    public void Module_ShouldNotReference_OtherModule(
        string sourceModule, string sourceLayer, string targetModule, string targetLayer)
    {
        var sourceAssembly = Assembly.Load($"Wallow.{sourceModule}.{sourceLayer}");

        var result = Types.InAssembly(sourceAssembly)
            .ShouldNot()
            .HaveDependencyOn($"Wallow.{targetModule}")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{sourceModule}.{sourceLayer} should not reference {targetModule}.{targetLayer}. " +
            $"Modules must communicate only via Shared.Contracts events.");
    }
}
```

### Adding New Modules to Architecture Tests

When creating a new module, add it to `TestConstants.AllModules`:

```csharp
// tests/Wallow.Architecture.Tests/TestConstants.cs
internal static class TestConstants
{
    // AllModules is populated dynamically at runtime by scanning for Wallow.*.Domain.dll files.
    // No manual registration is needed when adding a new module.
    public static readonly string[] AllModules = Directory
        .GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Wallow.*.Domain.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => name is not null)
        .Select(name => name!.Split('.')[1])
        .Distinct()
        .Order()
        .ToArray();

    public static readonly string[] EventSourcedModules =
    [
        // Add here if a module uses event sourcing
    ];
}
```

## 6. Test Fixtures and Factories

### ICollectionFixture for Shared State (Preferred)

Use `ICollectionFixture<T>` (not `IClassFixture<T>`) when multiple test classes share expensive setup such as containers. `IClassFixture` creates a new fixture instance per test class, which means a new set of containers per class — very slow for integration tests.

```csharp
[CollectionDefinition(nameof(BillingTestCollection))]
public class BillingTestCollection : ICollectionFixture<WallowApiFactory> { }

[Collection(nameof(BillingTestCollection))]
public class InvoiceTests : BillingIntegrationTestBase
{
    public InvoiceTests(WallowApiFactory factory) : base(factory) { }
}
```

For messaging tests, the fixture implements `ICollectionFixture` similarly:

```csharp
public class CrossModuleEventPropagationTests : ICollectionFixture<MessagingTestFixture>, IAsyncLifetime
{
    private readonly MessagingTestFixture _fixture;

    public CrossModuleEventPropagationTests(MessagingTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _fixture.GetCrossModuleTracker().Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DataRequestCreated_Should_Be_Consumable_By_Modules()
    {
        var bus = _fixture.GetMessageBus();
        // ...
    }
}
```

### IAsyncLifetime for Container Lifecycle

Implement `IAsyncLifetime` for async setup/teardown:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

### Test Data Builders

Use fluent builders for complex test data:

```csharp
public class InvoiceBuilder
{
    private Guid _userId = Guid.NewGuid();
    private string _invoiceNumber = $"INV-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    private string _currency = "USD";
    private readonly List<(string Description, decimal Amount, int Quantity)> _lineItems = [];
    private bool _issued;
    private bool _paid;

    public InvoiceBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public InvoiceBuilder WithInvoiceNumber(string number)
    {
        _invoiceNumber = number;
        return this;
    }

    public InvoiceBuilder WithLineItem(string description, decimal amount, int quantity = 1)
    {
        _lineItems.Add((description, amount, quantity));
        return this;
    }

    public InvoiceBuilder AsIssued()
    {
        _issued = true;
        return this;
    }

    public InvoiceBuilder AsPaid()
    {
        _issued = true;
        _paid = true;
        return this;
    }

    public Invoice Build()
    {
        var invoice = Invoice.Create(_userId, _invoiceNumber, _currency, Guid.NewGuid(), null);

        foreach (var (description, amount, quantity) in _lineItems)
        {
            invoice.AddLineItem(description, Money.Create(amount, _currency), quantity, Guid.NewGuid());
        }

        if (_issued)
        {
            if (!_lineItems.Any())
            {
                invoice.AddLineItem("Default Item", Money.Create(100, _currency), 1, Guid.NewGuid());
            }
            invoice.Issue(Guid.NewGuid());
        }

        if (_paid)
        {
            invoice.MarkAsPaid(Guid.NewGuid(), Guid.NewGuid());
        }

        invoice.ClearDomainEvents();
        return invoice;
    }

    public static InvoiceBuilder Create() => new();
}

// Usage
var invoice = InvoiceBuilder.Create()
    .WithInvoiceNumber("INV-001")
    .WithLineItem("Service", 100m)
    .AsIssued()
    .Build();
```

## 7. Messaging Tests

### MessagingTestFixture Setup

```csharp
public class MessagingTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private readonly RedisContainer _redisContainer;
    private readonly MessageTracker _messageTracker = new();
    private readonly CrossModuleEventTracker _crossModuleTracker = new();

    public MessagingTestFixture()
    {
        _postgresContainer = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("wallow_test")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4.2-management-alpine")
            .Build();

        _redisContainer = new RedisBuilder("valkey/valkey:8-alpine")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMessageTracker>(_messageTracker);
            services.AddSingleton<ICrossModuleEventTracker>(_crossModuleTracker);
            services.AddSingleton<MessageWaiter>();
        });

        // Enable test handler discovery
        builder.UseSetting("Wolverine:TestAssembly", typeof(MessagingTestFixture).Assembly.FullName);
    }

    public IMessageBus GetMessageBus() => Services.GetRequiredService<IMessageBus>();
    public MessageWaiter GetMessageWaiter() => Services.GetRequiredService<MessageWaiter>();
    public IMessageTracker GetMessageTracker() => _messageTracker;
}
```

### Event Publish/Consume Verification

```csharp
[Fact]
public async Task DataRequestCreated_Should_Be_Consumable_By_Modules()
{
    var bus = _fixture.GetMessageBus();
    var waiter = _fixture.GetMessageWaiter();
    var tracker = _fixture.GetCrossModuleTracker();

    var dataRequestEvent = new DataRequestCreatedEvent
    {
        RequestId = Guid.NewGuid(),
        RequestType = "Erasure",
        TenantId = Guid.NewGuid(),
        SubjectUserId = Guid.NewGuid()
    };

    await bus.PublishAsync(dataRequestEvent);

    await waiter.WaitForCrossModuleHandlersAsync(
        "DataRequestCreated",
        dataRequestEvent.EventId,
        expectedHandlerCount: 1,
        timeoutMs: 15000);

    var handlers = tracker.GetExecutedHandlers("DataRequestCreated", dataRequestEvent.EventId);
    handlers.Should().Contain("Test");
}
```

### Test Event Handlers

```csharp
public class TestEventHandler
{
    private readonly ILogger<TestEventHandler> _logger;
    private readonly IMessageTracker _tracker;

    public TestEventHandler(ILogger<TestEventHandler> logger, IMessageTracker tracker)
    {
        _logger = logger;
        _tracker = tracker;
    }

    public Task Handle(TestEvent @event)
    {
        _tracker.RecordEvent(@event);
        _logger.LogInformation("Processed TestEvent: {Message}", @event.Message);
        return Task.CompletedTask;
    }

    public Task Handle(TestEventThatFails @event)
    {
        _tracker.IncrementAttempt(@event.EventId);
        var attempts = _tracker.GetAttemptCount(@event.EventId);

        if (attempts < @event.FailAfterAttempts)
        {
            throw new InvalidOperationException($"Simulated failure on attempt {attempts}");
        }

        return Task.CompletedTask;
    }
}
```

## 8. Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test tests/Modules/Billing/Wallow.Billing.Tests

# Run tests matching a filter
dotnet test --filter "FullyQualifiedName~CreateInvoice"

# Run tests by category/trait
dotnet test --filter "Category=Unit"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### CI/CD Integration

Tests run automatically in CI pipelines. The `Directory.Build.props` configures Coverlet for code coverage:

```xml
<PropertyGroup>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
</PropertyGroup>
```

### Parallel Execution

xUnit runs tests in parallel by default. Use `[Collection]` to group tests that cannot run in parallel:

```csharp
[Collection("Database")]
public class DatabaseTests
{
    // Tests in same collection run sequentially
}
```

## 9. Event Sourcing and Saga Tests (Removed)

The event-sourced modules and Wolverine Sagas were removed during the module simplification. The platform currently has 8 modules: Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries, and Showcases. There is no `MartenFixture` in `Wallow.Tests.Common`.

## 10. Bogus Test Data Generation

Use Bogus for realistic fake data in tests.

### Static Test Data Helper

```csharp
using Bogus;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Tests.TestData;

public static class BillingTestData
{
    private static readonly Faker Faker = new();

    public static Guid UserId() => Faker.Random.Guid();
    public static string InvoiceNumber() => $"INV-{Faker.Random.AlphaNumeric(8).ToUpper()}";
    public static string Currency() => Faker.PickRandom("USD", "EUR", "GBP");
    public static decimal Amount() => Faker.Finance.Amount(10, 10000);
    public static string Description() => Faker.Commerce.ProductName();

    public static TenantId TenantId() => Shared.Kernel.Identity.TenantId.Create(Faker.Random.Guid());
}
```

## 11. End-to-End Tests

Test complete user journeys through the API. End-to-end tests exercise the full HTTP pipeline including authentication, middleware, routing, and database persistence.

```csharp
[Collection(nameof(WallowTestCollection))]
public class InvoiceWorkflowTests : BillingIntegrationTestBase
{
    public InvoiceWorkflowTests(WallowApiFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateAndIssueInvoice_FullWorkflow()
    {
        // Create invoice
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/api/billing/invoices",
            new { UserId = Guid.NewGuid(), InvoiceNumber = "INV-E2E-001", Currency = "USD" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Add line item
        InvoiceDto invoice = (await createResponse.Content.ReadFromJsonAsync<InvoiceDto>())!;
        HttpResponseMessage lineItemResponse = await Client.PostAsJsonAsync(
            $"/api/billing/invoices/{invoice.Id}/line-items",
            new { Description = "Service", UnitPrice = 100m, Quantity = 2 });
        lineItemResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Issue invoice
        HttpResponseMessage issueResponse = await Client.PostAsync(
            $"/api/billing/invoices/{invoice.Id}/issue", null);
        issueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## 12. Code Coverage

### Coverage Configuration

The `tests/coverage.runsettings` file configures code coverage:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[*]*.Migrations.*,[*]*Program,[*]*Startup</Exclude>
          <ExcludeByFile>**/Migrations/**/*.cs</ExcludeByFile>
          <IncludeTestAssembly>false</IncludeTestAssembly>
          <SingleHit>false</SingleHit>
          <UseSourceLink>true</UseSourceLink>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Running Coverage

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --settings tests/coverage.runsettings

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
    -reports:"**/coverage.cobertura.xml" \
    -targetdir:"coverage-report" \
    -reporttypes:Html

# Open report
open coverage-report/index.html
```

## 13. Best Practices

### Test Isolation

- Each test should be independent and not rely on other tests' state
- Use `IAsyncLifetime` to reset state between tests
- Clear domain events after entity setup: `entity.ClearDomainEvents()`

### Avoid Testing Implementation Details

```csharp
// Bad: Testing internal state
var handler = new CreateInvoiceHandler(...);
handler._someInternalField.Should().Be(expected);

// Good: Testing behavior through public interface
var result = await handler.Handle(command, CancellationToken.None);
result.IsSuccess.Should().BeTrue();
```

### When to Mock vs Real Dependencies

| Use Mocks | Use Real Dependencies |
|-----------|----------------------|
| Unit tests for handlers | Integration tests |
| Expensive external services | Database operations in integration tests |
| Non-deterministic behavior (time, randomness) | Messaging tests |
| Third-party APIs | Architecture tests |

### Test Data Management

- Use builders for complex entities
- Use Bogus for random but realistic data
- Use fixed test constants for IDs that need to be referenced across tests

```csharp
public static class TestConstants
{
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid TestOrgId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000100");
}
```

### Container Image Versions

Keep Testcontainer images aligned with `docker-compose.yml`:

- PostgreSQL: `postgres:18-alpine`
- RabbitMQ: `rabbitmq:4.2-management-alpine`
- Redis/Valkey: `valkey/valkey:8-alpine`

### Constraints

- **Never use real Keycloak in tests** - Always use `TestAuthHandler`
- **Never hardcode connection strings** - Testcontainers provide dynamic strings
- **Architecture tests must pass on every build** - Fix violations, don't delete tests
- **Integration tests should use `WallowApiFactory`** - Unit tests should not
