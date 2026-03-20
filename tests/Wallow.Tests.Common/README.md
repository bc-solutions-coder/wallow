# Wallow.Tests.Common

Shared test infrastructure for Wallow integration tests.

## Features

- **Testcontainers Fixtures**: PostgreSQL, RabbitMQ, and Redis containers
- **WebApplicationFactory**: Pre-configured API factory with test containers
- **JWT Auth Helpers**: Generate test tokens and authenticate HTTP clients
- **Reusable across all test projects**

## Usage

### Using WallowApiFactory

```csharp
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

public class MyIntegrationTests : IAsyncLifetime
{
    private WallowApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WallowApiFactory();
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetEndpoint_WithAuth_ReturnsOk()
    {
        var client = _factory.CreateClient().WithAuth("user-123");

        var response = await client.GetAsync("/api/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Using Individual Fixtures

```csharp
using Wallow.Tests.Common.Fixtures;

public class DatabaseTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public DatabaseTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public void CanConnectToDatabase()
    {
        var connectionString = _db.ConnectionString;
        // Use connection string...
    }
}
```

### Generating JWT Tokens

```csharp
using Wallow.Tests.Common.Helpers;

// Simple token
var token = JwtTokenHelper.GenerateToken("user-123");

// Token with roles
var adminToken = JwtTokenHelper.GenerateToken("admin-1", roles: new[] { "Admin", "User" });

// Token with email
var tokenWithEmail = JwtTokenHelper.GenerateToken("user-456", email: "test@example.com");
```

### Authenticating HTTP Clients

```csharp
using Wallow.Tests.Common.Helpers;

var client = factory.CreateClient();

// Authenticate as user
client.WithAuth("user-123");

// Authenticate with roles
client.WithAuth("admin-1", roles: new[] { "Admin" });

var response = await client.GetAsync("/api/protected-endpoint");
```

## Components

### Fixtures

- **DatabaseFixture**: PostgreSQL 18 container
- **RabbitMqFixture**: RabbitMQ 4.2 container
- **RedisFixture**: Valkey 8 container

All implement `IAsyncLifetime` for proper lifecycle management.

### Factories

- **WallowApiFactory**: Combines all containers + WebApplicationFactory
  - Auto-configures connection strings
  - Sets up test JWT configuration
  - Provides pre-configured HttpClient

### Helpers

- **JwtTokenHelper**: Generate valid JWT tokens for testing
- **HttpClientExtensions**: Extension methods to authenticate HttpClient instances

## Configuration

The factory uses these test configuration values:

```
ConnectionStrings:DefaultConnection = (Postgres container)
ConnectionStrings:RabbitMq = (RabbitMQ container)
ConnectionStrings:Redis = (Redis container)
Jwt:Secret = "test-jwt-secret-key-for-integration-tests-must-be-at-least-32-characters-long"
Jwt:Issuer = "Wallow.Test"
Jwt:Audience = "Wallow.Test.Client"
Jwt:AccessTokenExpiryMinutes = 60
Jwt:RefreshTokenExpiryDays = 7
```

## Dependencies

- Testcontainers 4.10.0
- xUnit
- Microsoft.AspNetCore.Mvc.Testing
- Microsoft.AspNetCore.SignalR.Client
- Microsoft.AspNetCore.Authentication.JwtBearer
- FluentAssertions
