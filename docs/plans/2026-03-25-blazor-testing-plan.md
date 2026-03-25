# Blazor Testing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add comprehensive testing for Wallow.Auth and Wallow.Web Blazor apps: service unit tests, bUnit component tests, and Playwright E2E tests.

**Architecture:** Four new test projects (Wallow.Web.Tests, Wallow.Auth.Component.Tests, Wallow.Web.Component.Tests, Wallow.E2E.Tests) plus expanding existing Wallow.Auth.Tests. bUnit component tests stub BlazorBlueprint components. E2E tests use docker-compose.test.yml with Playwright.

**Tech Stack:** xUnit, FluentAssertions, NSubstitute, RichardSzalay.MockHttp, bUnit, Microsoft.Playwright

**Design doc:** `docs/plans/2026-03-25-blazor-testing-design.md`

---

## Phase 1: Foundation (sequential — must complete before Phase 2)

### Task 1: Add NuGet packages to Directory.Packages.props

**Files:**
- Modify: `Directory.Packages.props`

**Step 1: Add bUnit and Playwright package versions**

Add these entries under the Testing section in `Directory.Packages.props`:

```xml
<PackageVersion Include="bunit" Version="2.0.33-preview" />
<PackageVersion Include="Microsoft.Playwright" Version="1.52.0" />
```

> **Note:** bUnit 2.x targets .NET 9+ and supports the latest Blazor features. Check NuGet for the latest stable version at implementation time. If 2.0 stable is released, use that instead.

**Step 2: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore: add bunit and playwright packages to central package management"
```

---

## Phase 2: Service Unit Tests (parallelizable — Tasks 2 and 3 are independent)

### Task 2: Expand Wallow.Auth.Tests with remaining AuthApiClient + ClientBrandingApiClient tests

**Files:**
- Modify: `tests/Wallow.Auth.Tests/Services/AuthApiClientTests.cs`
- Create: `tests/Wallow.Auth.Tests/Services/ClientBrandingApiClientTests.cs`

**Context:** The existing `AuthApiClientTests` covers Login, Register, ForgotPassword, and VerifyEmail. The `AuthApiClient` has 14 more methods that need tests. The `ClientBrandingApiClient` has 1 method with no tests.

**Step 1: Add tests for untested AuthApiClient methods**

Add these test methods to the existing `AuthApiClientTests.cs` class. Follow the exact same pattern as existing tests (MockHttp setup, call method, assert with FluentAssertions):

Methods needing tests (each needs success + failure cases):
- `ResetPasswordAsync` — POST to `{BasePath}/reset-password`
- `ValidateRedirectUriAsync` — GET to `{BasePath}/redirect-uri/validate?uri=...`, returns `bool`
- `GetExternalProvidersAsync` — GET to `{BasePath}/external-providers`, returns `List<string>`
- `GetMatchingOrganizationByDomainAsync` — GET to `api/v1/identity/organization-domains/match?email=...`, returns `string?`, has try/catch for `HttpRequestException`
- `RequestMembershipAsync` — POST to `api/v1/identity/membership-requests`, returns `bool`, has try/catch
- `SendMagicLinkAsync` — POST to `{BasePath}/passwordless/magic-link`
- `VerifyMagicLinkAsync` — GET to `{BasePath}/passwordless/magic-link/verify?token=...`
- `SendOtpAsync` — POST to `{BasePath}/passwordless/otp`
- `VerifyOtpAsync` — POST to `{BasePath}/passwordless/otp/verify` (uses same pattern as LoginAsync with string content check)
- `VerifyMfaChallengeAsync` — POST to `{BasePath}/mfa/verify` with `UseBackupCode = false`
- `UseBackupCodeAsync` — POST to `{BasePath}/mfa/verify` with `UseBackupCode = true`
- `VerifyInvitationAsync` — GET to `api/v1/identity/invitations/verify/{token}`, returns `InvitationDetailsResponse?`
- `AcceptInvitationAsync` — POST to `api/v1/identity/invitations/{token}/accept`, returns `bool`

Example for one method (follow this pattern for all):

```csharp
[Fact]
public async Task ResetPasswordAsync_ValidRequest_ReturnsSuccess()
{
    _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/reset-password")
        .Respond("application/json", """{"succeeded":true}""");

    AuthResponse result = await _sut.ResetPasswordAsync(
        new ResetPasswordRequest("test@test.com", "valid-token", "NewPassword1!"));

    result.Succeeded.Should().BeTrue();
}

[Fact]
public async Task ResetPasswordAsync_InvalidToken_ReturnsError()
{
    _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/reset-password")
        .Respond(HttpStatusCode.BadRequest, "application/json",
            """{"succeeded":false,"error":"invalid_token"}""");

    AuthResponse result = await _sut.ResetPasswordAsync(
        new ResetPasswordRequest("test@test.com", "bad-token", "NewPassword1!"));

    result.Succeeded.Should().BeFalse();
    result.Error.Should().Be("invalid_token");
}

[Fact]
public async Task ValidateRedirectUriAsync_ValidUri_ReturnsTrue()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/redirect-uri/validate*")
        .Respond("application/json", """{"allowed":true}""");

    bool result = await _sut.ValidateRedirectUriAsync("https://app.example.com/callback");

    result.Should().BeTrue();
}

[Fact]
public async Task ValidateRedirectUriAsync_InvalidUri_ReturnsFalse()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/redirect-uri/validate*")
        .Respond("application/json", """{"allowed":false}""");

    bool result = await _sut.ValidateRedirectUriAsync("https://evil.com/callback");

    result.Should().BeFalse();
}

[Fact]
public async Task GetExternalProvidersAsync_ReturnsProviders()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/external-providers")
        .Respond("application/json", """["Google","GitHub"]""");

    List<string> result = await _sut.GetExternalProvidersAsync();

    result.Should().BeEquivalentTo(["Google", "GitHub"]);
}

[Fact]
public async Task GetExternalProvidersAsync_ServerError_ReturnsEmpty()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/external-providers")
        .Respond(HttpStatusCode.InternalServerError);

    List<string> result = await _sut.GetExternalProvidersAsync();

    result.Should().BeEmpty();
}

[Fact]
public async Task GetMatchingOrganizationByDomainAsync_Match_ReturnsOrgName()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organization-domains/match*")
        .Respond("application/json", """{"orgName":"Acme Corp"}""");

    string? result = await _sut.GetMatchingOrganizationByDomainAsync("user@acme.com");

    result.Should().Be("Acme Corp");
}

[Fact]
public async Task GetMatchingOrganizationByDomainAsync_NoMatch_ReturnsNull()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organization-domains/match*")
        .Respond(HttpStatusCode.NotFound);

    string? result = await _sut.GetMatchingOrganizationByDomainAsync("user@random.com");

    result.Should().BeNull();
}

[Fact]
public async Task VerifyInvitationAsync_ValidToken_ReturnsDetails()
{
    _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/invitations/verify/*")
        .Respond("application/json", """
        {
            "id":"00000000-0000-0000-0000-000000000001",
            "email":"invited@test.com",
            "status":"pending",
            "expiresAt":"2026-04-01T00:00:00Z",
            "createdAt":"2026-03-01T00:00:00Z",
            "acceptedByUserId":null
        }
        """);

    InvitationDetailsResponse? result = await _sut.VerifyInvitationAsync("valid-token");

    result.Should().NotBeNull();
    result!.Email.Should().Be("invited@test.com");
    result.Status.Should().Be("pending");
}

[Fact]
public async Task AcceptInvitationAsync_ValidToken_ReturnsTrue()
{
    _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/invitations/*/accept")
        .Respond(HttpStatusCode.OK);

    bool result = await _sut.AcceptInvitationAsync("valid-token");

    result.Should().BeTrue();
}

[Fact]
public async Task AcceptInvitationAsync_InvalidToken_ReturnsFalse()
{
    _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/invitations/*/accept")
        .Respond(HttpStatusCode.NotFound);

    bool result = await _sut.AcceptInvitationAsync("invalid-token");

    result.Should().BeFalse();
}
```

Continue this pattern for SendMagicLinkAsync, VerifyMagicLinkAsync, SendOtpAsync, VerifyOtpAsync, VerifyMfaChallengeAsync, UseBackupCodeAsync, RequestMembershipAsync.

**Step 2: Create ClientBrandingApiClientTests**

```csharp
using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Auth.Services;

namespace Wallow.Auth.Tests.Services;

public sealed class ClientBrandingApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly ClientBrandingApiClient _sut;

    public ClientBrandingApiClientTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AuthApi").Returns(httpClient);

        _sut = new ClientBrandingApiClient(factory);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task GetBrandingAsync_ValidClientId_ReturnsBranding()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/test-client/branding")
            .Respond("application/json", """
            {
                "clientId":"test-client",
                "displayName":"Test App",
                "tagline":"A test application",
                "logoUrl":"https://example.com/logo.png",
                "themeJson":"{}"
            }
            """);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("test-client");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("test-client");
        result.DisplayName.Should().Be("Test App");
        result.Tagline.Should().Be("A test application");
        result.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public async Task GetBrandingAsync_NotFound_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/unknown-client/branding")
            .Respond(HttpStatusCode.NotFound);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("unknown-client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_ServerError_ReturnsNull()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/*/branding")
            .Respond(HttpStatusCode.InternalServerError);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("any-client");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_SpecialCharactersInClientId_EncodesCorrectly()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/client%20with%20spaces/branding")
            .Respond("application/json", """
            {"clientId":"client with spaces","displayName":"Spaced App","tagline":null,"logoUrl":null,"themeJson":null}
            """);

        ClientBrandingResponse? result = await _sut.GetBrandingAsync("client with spaces");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("client with spaces");
    }
}
```

**Step 3: Run tests**

```bash
./scripts/run-tests.sh auth
```

Expected: All tests pass.

**Step 4: Commit**

```bash
git add tests/Wallow.Auth.Tests/
git commit -m "test(auth): add remaining AuthApiClient and ClientBrandingApiClient service tests"
```

---

### Task 3: Create Wallow.Web.Tests project with service unit tests

**Files:**
- Create: `tests/Wallow.Web.Tests/Wallow.Web.Tests.csproj`
- Create: `tests/Wallow.Web.Tests/Services/AppRegistrationServiceTests.cs`
- Create: `tests/Wallow.Web.Tests/Services/OrganizationApiServiceTests.cs`
- Create: `tests/Wallow.Web.Tests/Services/InquiryServiceTests.cs`

**Context:** Wallow.Web services use `IHttpContextAccessor` to get bearer tokens via `GetTokenAsync("access_token")`. Tests must mock this. The named HTTP client is `"WallowApi"`.

**Step 1: Create test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.Web.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="RichardSzalay.MockHttp" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Wallow.Web\Wallow.Web.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create a test base class for Web service tests**

The Web services all share the same pattern: they need `IHttpClientFactory` (named `"WallowApi"`) and `IHttpContextAccessor` with a mocked `GetTokenAsync`. Create a base class to avoid repeating this setup.

Create `tests/Wallow.Web.Tests/Services/WebServiceTestBase.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using RichardSzalay.MockHttp;

namespace Wallow.Web.Tests.Services;

public abstract class WebServiceTestBase : IDisposable
{
    protected readonly MockHttpMessageHandler MockHttp = new();
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly IHttpContextAccessor HttpContextAccessor;

    protected WebServiceTestBase()
    {
        HttpClient httpClient = MockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        HttpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClientFactory.CreateClient("WallowApi").Returns(httpClient);

        // Mock IHttpContextAccessor to return a bearer token
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(AuthenticateResult.Success(
                new AuthenticationTicket(
                    new System.Security.Claims.ClaimsPrincipal(),
                    new AuthenticationProperties(new Dictionary<string, string?> { ["access_token"] = "test-token" }),
                    "TestScheme")));
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(authService);

        DefaultHttpContext httpContext = new() { RequestServices = serviceProvider };
        HttpContextAccessor = Substitute.For<IHttpContextAccessor>();
        HttpContextAccessor.HttpContext.Returns(httpContext);
    }

    public void Dispose()
    {
        MockHttp.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

**Step 3: Create AppRegistrationServiceTests**

```csharp
using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class AppRegistrationServiceTests : WebServiceTestBase
{
    private readonly AppRegistrationService _sut;

    public AppRegistrationServiceTests()
    {
        _sut = new AppRegistrationService(HttpClientFactory, HttpContextAccessor);
    }

    [Fact]
    public async Task GetAppsAsync_ReturnsApps()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps")
            .Respond("application/json", """
            [
                {"clientId":"app-1","displayName":"App One","clientType":"confidential","redirectUris":["https://app1.com/cb"],"createdAt":"2026-01-01T00:00:00Z"},
                {"clientId":"app-2","displayName":"App Two","clientType":"public","redirectUris":[],"createdAt":null}
            ]
            """);

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().HaveCount(2);
        result[0].ClientId.Should().Be("app-1");
        result[0].DisplayName.Should().Be("App One");
        result[1].ClientId.Should().Be("app-2");
    }

    [Fact]
    public async Task GetAppsAsync_ServerError_ReturnsEmpty()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps")
            .Respond(HttpStatusCode.InternalServerError);

        List<AppModel> result = await _sut.GetAppsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAppAsync_Found_ReturnsApp()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/app-1")
            .Respond("application/json", """
            {"clientId":"app-1","displayName":"App One","clientType":"confidential","redirectUris":["https://app1.com/cb"],"createdAt":"2026-01-01T00:00:00Z"}
            """);

        AppModel? result = await _sut.GetAppAsync("app-1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("app-1");
    }

    [Fact]
    public async Task GetAppAsync_NotFound_ReturnsNull()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/apps/unknown")
            .Respond(HttpStatusCode.NotFound);

        AppModel? result = await _sut.GetAppAsync("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAppAsync_Success_ReturnsResult()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/apps/register")
            .Respond("application/json", """
            {"clientId":"new-app","clientSecret":"secret-123","registrationAccessToken":"rat-456"}
            """);

        RegisterAppModel model = new("My App", "confidential", ["https://myapp.com/cb"], ["openid"]);
        RegisterAppResult result = await _sut.RegisterAppAsync(model);

        result.Success.Should().BeTrue();
        result.ClientId.Should().Be("new-app");
        result.ClientSecret.Should().Be("secret-123");
        result.RegistrationAccessToken.Should().Be("rat-456");
    }

    [Fact]
    public async Task RegisterAppAsync_BadRequest_ReturnsError()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/apps/register")
            .Respond(HttpStatusCode.BadRequest, "text/plain", "Invalid redirect URI");

        RegisterAppModel model = new("Bad App", "confidential", ["not-a-url"], ["openid"]);
        RegisterAppResult result = await _sut.RegisterAppAsync(model);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid redirect URI");
    }

    [Fact]
    public async Task UpsertBrandingAsync_Success_ReturnsTrue()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/apps/app-1/branding")
            .Respond(HttpStatusCode.OK);

        bool result = await _sut.UpsertBrandingAsync(
            "app-1", "App One", "A great app", null, null, null, null);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertBrandingAsync_WithLogo_ReturnsTrue()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/apps/app-1/branding")
            .Respond(HttpStatusCode.OK);

        using MemoryStream logoStream = new([1, 2, 3]);
        bool result = await _sut.UpsertBrandingAsync(
            "app-1", "App One", null, null, logoStream, "logo.png", "image/png");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertBrandingAsync_ServerError_ReturnsFalse()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/apps/app-1/branding")
            .Respond(HttpStatusCode.InternalServerError);

        bool result = await _sut.UpsertBrandingAsync(
            "app-1", "App One", null, null, null, null, null);

        result.Should().BeFalse();
    }
}
```

**Step 4: Create OrganizationApiServiceTests**

```csharp
using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class OrganizationApiServiceTests : WebServiceTestBase
{
    private readonly OrganizationApiService _sut;

    public OrganizationApiServiceTests()
    {
        _sut = new OrganizationApiService(HttpClientFactory, HttpContextAccessor);
    }

    [Fact]
    public async Task GetOrganizationsAsync_ReturnsOrgs()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organizations")
            .Respond("application/json", """
            [{"id":"00000000-0000-0000-0000-000000000001","name":"Acme","domain":"acme.com","memberCount":5}]
            """);

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Acme");
        result[0].MemberCount.Should().Be(5);
    }

    [Fact]
    public async Task GetOrganizationsAsync_ServerError_ReturnsEmpty()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organizations")
            .Respond(HttpStatusCode.InternalServerError);

        List<OrganizationModel> result = await _sut.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationAsync_Found_ReturnsOrg()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        MockHttp.When(HttpMethod.Get, $"http://localhost:5000/api/v1/identity/organizations/{orgId}")
            .Respond("application/json", """
            {"id":"00000000-0000-0000-0000-000000000001","name":"Acme","domain":"acme.com","memberCount":5}
            """);

        OrganizationModel? result = await _sut.GetOrganizationAsync(orgId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Acme");
    }

    [Fact]
    public async Task GetOrganizationAsync_NotFound_ReturnsNull()
    {
        Guid orgId = Guid.NewGuid();
        MockHttp.When(HttpMethod.Get, $"http://localhost:5000/api/v1/identity/organizations/{orgId}")
            .Respond(HttpStatusCode.NotFound);

        OrganizationModel? result = await _sut.GetOrganizationAsync(orgId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsMembers()
    {
        Guid orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        MockHttp.When(HttpMethod.Get, $"http://localhost:5000/api/v1/identity/organizations/{orgId}/members")
            .Respond("application/json", """
            [{"id":"00000000-0000-0000-0000-000000000002","email":"alice@acme.com","firstName":"Alice","lastName":"Smith","enabled":true,"roles":["Admin"]}]
            """);

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(orgId);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("alice@acme.com");
        result[0].Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task GetMembersAsync_ServerError_ReturnsEmpty()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/organizations/*/members")
            .Respond(HttpStatusCode.InternalServerError);

        List<OrganizationMemberModel> result = await _sut.GetMembersAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClientsByTenantAsync_ReturnsClients()
    {
        Guid tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        MockHttp.When(HttpMethod.Get, $"http://localhost:5000/api/v1/identity/clients/by-tenant/{tenantId}")
            .Respond("application/json", """
            [{"id":"1","name":"App","clientId":"app-1","clientSecret":null,"redirectUris":["https://app.com"],"postLogoutRedirectUris":[]}]
            """);

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(tenantId);

        result.Should().HaveCount(1);
        result[0].ClientId.Should().Be("app-1");
    }

    [Fact]
    public async Task GetClientsByTenantAsync_ServerError_ReturnsEmpty()
    {
        MockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/clients/by-tenant/*")
            .Respond(HttpStatusCode.InternalServerError);

        List<ClientModel> result = await _sut.GetClientsByTenantAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }
}
```

**Step 5: Create InquiryServiceTests**

```csharp
using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Tests.Services;

public sealed class InquiryServiceTests : WebServiceTestBase
{
    private readonly InquiryService _sut;

    public InquiryServiceTests()
    {
        _sut = new InquiryService(HttpClientFactory, HttpContextAccessor);
    }

    [Fact]
    public async Task SubmitInquiryAsync_Success_ReturnsTrue()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/inquiries")
            .Respond(HttpStatusCode.OK);

        InquiryModel model = new("John Doe", "john@test.com", "555-0100", "Acme",
            "Web App", "$10k-$50k", "3 months", "Need a website");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitInquiryAsync_BadRequest_ReturnsFalse()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/inquiries")
            .Respond(HttpStatusCode.BadRequest);

        InquiryModel model = new("", "", "", null, "", "", "", "");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitInquiryAsync_ServerError_ReturnsFalse()
    {
        MockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/inquiries")
            .Respond(HttpStatusCode.InternalServerError);

        InquiryModel model = new("John Doe", "john@test.com", "555-0100", null,
            "Web App", "$10k-$50k", "3 months", "Need a website");

        bool result = await _sut.SubmitInquiryAsync(model);

        result.Should().BeFalse();
    }
}
```

**Step 6: Run tests**

```bash
./scripts/run-tests.sh tests/Wallow.Web.Tests
```

Expected: All tests pass.

**Step 7: Commit**

```bash
git add tests/Wallow.Web.Tests/
git commit -m "test(web): add service unit tests for AppRegistration, Organization, and Inquiry services"
```

---

## Phase 3: bUnit Component Tests (parallelizable — Tasks 4 and 5 are independent)

### Task 4: Create Wallow.Auth.Component.Tests project with stubs and component tests

**Files:**
- Create: `tests/Wallow.Auth.Component.Tests/Wallow.Auth.Component.Tests.csproj`
- Create: `tests/Wallow.Auth.Component.Tests/_Imports.razor`
- Create: `tests/Wallow.Auth.Component.Tests/Stubs/` (stub components)
- Create: `tests/Wallow.Auth.Component.Tests/Pages/` (component tests)
- Create: `tests/Wallow.Auth.Component.Tests/Layout/` (layout tests)
- Create: `tests/Wallow.Auth.Component.Tests/Shared/` (shared component tests)

**Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <RootNamespace>Wallow.Auth.Component.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="bunit" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
    <Using Include="Bunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Wallow.Auth\Wallow.Auth.csproj" />
  </ItemGroup>

</Project>
```

> **Important:** Uses `Microsoft.NET.Sdk.Razor` so bUnit can compile and render Razor components.

**Step 2: Create `_Imports.razor`**

```razor
@using Microsoft.AspNetCore.Components.Web
@using Bunit
```

**Step 3: Create BlazorBlueprint stub components**

These go in `tests/Wallow.Auth.Component.Tests/Stubs/`. Each stub renders minimal HTML with a test-friendly structure. The stubs live under the `BlazorBlueprint.Components` namespace so they override the real components at compile time (since we don't reference the BlazorBlueprint package in the test project).

> **Key insight:** Because the test project does NOT reference `BlazorBlueprint.Components` NuGet package but DOES reference `Wallow.Auth.csproj`, the Razor components in the Auth project will look for `BbButton`, `BbInput`, etc. We provide stubs under the same namespace so bUnit resolves them.
>
> **Alternative if namespace resolution fails:** Register stubs via `ctx.ComponentFactories.Add(...)` in bUnit's TestContext. bUnit 2.x supports component factories that intercept component creation. This is the safer approach.

Create `tests/Wallow.Auth.Component.Tests/Stubs/StubRegistration.cs`:

```csharp
using Bunit;

namespace Wallow.Auth.Component.Tests.Stubs;

public static class StubRegistration
{
    public static void AddBlazorBlueprintStubs(this TestContext ctx)
    {
        // bUnit component factories catch any component from the BlazorBlueprint namespace
        // and render a simple <div> with the component name as a data attribute
        ctx.ComponentFactories.Add(
            type => type.Namespace?.StartsWith("BlazorBlueprint", StringComparison.Ordinal) == true,
            (type, parameters) =>
            {
                // Renders a simple stub that passes through ChildContent
                return new StubBlazorBlueprintComponent(type.Name);
            });
    }
}
```

> **Note:** The exact bUnit 2.x API for component factories may differ. At implementation time, check the bUnit docs for the correct factory registration pattern. The concept is: intercept BlazorBlueprint component creation and replace with a lightweight stub that renders `ChildContent`.

**If component factories prove too complex**, the simpler fallback is to create individual Razor stub files:

Create `tests/Wallow.Auth.Component.Tests/Stubs/BbButton.razor` (and similar for each component):

```razor
@namespace BlazorBlueprint.Components

<button data-bb="button" type="@Type" disabled="@Disabled" class="@Class" @onclick="OnClick">
    @ChildContent
</button>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Type { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public string? Variant { get; set; }
    [Parameter] public string? Href { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
```

Create similar stubs for: `BbInput`, `BbCard`, `BbCardHeader`, `BbCardTitle`, `BbCardDescription`, `BbCardContent`, `BbCardFooter`, `BbAlert`, `BbAlertDescription`, `BbLabel`, `BbCheckbox`, `BbSeparator`, `BbProgress`.

Each stub should:
- Live in `@namespace BlazorBlueprint.Components`
- Accept all `[Parameter]` properties the real component has (check imports in Auth pages)
- Render minimal HTML with a `data-bb="componentname"` attribute for test selectors
- Pass through `ChildContent`
- Use `[Parameter(CaptureUnmatchedValues = true)]` for any extra attributes

**Step 4: Create a test base class**

Create `tests/Wallow.Auth.Component.Tests/AuthComponentTestBase.cs`:

```csharp
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests;

public abstract class AuthComponentTestBase : TestContext
{
    protected readonly IAuthApiClient MockAuthClient;
    protected readonly IClientBrandingClient MockBrandingClient;
    protected readonly BrandingOptions TestBranding;

    protected AuthComponentTestBase()
    {
        MockAuthClient = Substitute.For<IAuthApiClient>();
        MockBrandingClient = Substitute.For<IClientBrandingClient>();
        TestBranding = new BrandingOptions { AppName = "TestApp" };

        // Register services that Auth pages inject
        Services.AddSingleton(MockAuthClient);
        Services.AddSingleton(MockBrandingClient);
        Services.AddSingleton(TestBranding);

        // Mock IConfiguration
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiBaseUrl"] = "http://localhost:5001"
            })
            .Build();
        Services.AddSingleton(config);

        // Mock IHttpClientFactory (used directly by Register page)
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("AuthApi").Returns(new HttpClient { BaseAddress = new Uri("http://localhost:5000") });
        Services.AddSingleton(httpClientFactory);

        // Default mock behavior: return empty providers list
        MockAuthClient.GetExternalProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([]);
    }
}
```

**Step 5: Create Login page tests**

Create `tests/Wallow.Auth.Component.Tests/Pages/LoginTests.cs`:

```csharp
using Bunit;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Models;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class LoginTests : AuthComponentTestBase
{
    [Fact]
    public void Renders_SignInForm()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>();

        cut.Markup.Should().Contain("Sign in to your account");
        cut.Markup.Should().Contain("email");
        cut.Markup.Should().Contain("password");
    }

    [Fact]
    public void Renders_TabsForPasswordMagicLinkAndOtp()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>();

        cut.Markup.Should().Contain("Password");
        cut.Markup.Should().Contain("Magic Link");
        cut.Markup.Should().Contain("OTP");
    }

    [Fact]
    public void Renders_ForgotPasswordLink()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>();

        cut.Markup.Should().Contain("Forgot password?");
        cut.Markup.Should().Contain("/forgot-password");
    }

    [Fact]
    public void Renders_RegisterLink()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>();

        cut.Markup.Should().Contain("Don't have an account?");
        cut.Markup.Should().Contain("/register");
    }

    [Fact]
    public async Task Login_EmptyFields_ShowsValidationError()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>();

        // Submit the form with empty fields
        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.TriggerEventAsync("onsubmit", new EventArgs()));

        cut.Markup.Should().Contain("Please enter your email and password");
    }

    [Fact]
    public async Task Login_InvalidCredentials_ShowsError()
    {
        MockAuthClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "invalid_credentials"));

        IRenderedComponent<Login> cut = RenderComponent<Login>();

        // Fill in fields and submit — exact mechanism depends on stub implementation
        // This test verifies the error mapping logic
        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() =>
        {
            // Simulate filled-in form by directly invoking the handler
            // bUnit's exact form interaction depends on stub component rendering
        });

        // Verify the auth client was called (or test error display logic separately)
    }

    [Fact]
    public async Task Login_Success_ShowsSignedInMessage()
    {
        MockAuthClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));

        IRenderedComponent<Login> cut = RenderComponent<Login>();

        // After successful login without ReturnUrl, should show "You are now signed in"
        // Exact interaction depends on bUnit form submission with stubs
    }

    [Fact]
    public async Task Login_MfaRequired_NavigatesToMfaChallenge()
    {
        MockAuthClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(
                Succeeded: true,
                MfaChallengeRequired: true,
                MfaChallengeToken: "mfa-token",
                MfaMethod: "totp"));

        IRenderedComponent<Login> cut = RenderComponent<Login>();

        // Verify navigation to /mfa/challenge with token and method params
        NavigationManager navMan = Services.GetRequiredService<NavigationManager>();
        // Assert navMan.Uri contains "/mfa/challenge"
    }

    [Fact]
    public void Login_WithErrorQueryParam_ShowsErrorMessage()
    {
        IRenderedComponent<Login> cut = RenderComponent<Login>(
            p => p.Add(c => c.Error, "external_login_failed"));

        cut.Markup.Should().Contain("External sign-in failed");
    }

    [Fact]
    public void Login_WithExternalProviders_ShowsProviderButtons()
    {
        MockAuthClient.GetExternalProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(["Google", "GitHub"]);

        IRenderedComponent<Login> cut = RenderComponent<Login>();

        cut.Markup.Should().Contain("Google");
        cut.Markup.Should().Contain("GitHub");
        cut.Markup.Should().Contain("Or continue with");
    }
}
```

> **Note:** bUnit form interaction with stubbed components requires careful implementation. The exact approach to filling inputs and submitting forms depends on how the stubs render. At implementation time, you may need to:
> 1. Use `cut.Find("input").Change("value")` for stub inputs that render `<input>`
> 2. Use `cut.Find("form").Submit()` for form submission
> 3. Or test the component's code-behind methods directly via `cut.InvokeAsync()`

**Step 6: Create tests for remaining Auth pages**

Follow the same pattern for each page. Key test assertions per page:

**ForgotPasswordTests.cs:**
- Renders email input and submit button
- Empty email shows validation error
- Success shows confirmation message
- Error shows error message

**ResetPasswordTests.cs:**
- Renders password fields
- Missing token handled gracefully
- Password mismatch validation
- Success navigates to login

**RegisterTests.cs:**
- Renders all form fields (email, password, confirm, checkboxes)
- Empty email validation
- Password mismatch validation
- Terms not accepted validation
- Privacy not accepted validation
- Passwordless toggle hides password fields
- Success navigates to verify email
- Success with org match shows membership suggestion
- Email taken error displayed

**VerifyEmailTests.cs / VerifyEmailConfirmTests.cs:**
- Renders appropriate messaging
- Token handling

**MfaChallengeTests.cs:**
- Renders code input
- Empty code validation
- Success navigates appropriately
- Error shows message

**MfaEnrollTests.cs:**
- Renders enrollment UI

**InvitationLandingTests.cs:**
- Loads invitation details
- Accept/decline buttons

**AcceptTermsTests.cs:**
- Checkbox required before submit

**LogoutTests.cs:**
- Basic render

**Static pages (Terms, Privacy, Home, Error):**
- Basic render tests — verify they render without errors and contain expected content

**AuthLayoutTests.cs:**
- Branding options reflected (AppName, logo)

**MfaEnrollmentBannerTests.cs:**
- Visible=true shows banner
- Visible=false hides banner
- Grace deadline displayed

**Step 7: Run tests**

```bash
./scripts/run-tests.sh tests/Wallow.Auth.Component.Tests
```

Expected: All tests pass.

**Step 8: Commit**

```bash
git add tests/Wallow.Auth.Component.Tests/
git commit -m "test(auth): add bunit component tests for all Auth pages and layouts"
```

---

### Task 5: Create Wallow.Web.Component.Tests project with stubs and component tests

**Files:**
- Create: `tests/Wallow.Web.Component.Tests/Wallow.Web.Component.Tests.csproj`
- Create: `tests/Wallow.Web.Component.Tests/_Imports.razor`
- Create: `tests/Wallow.Web.Component.Tests/Stubs/` (duplicate stubs from Auth)
- Create: `tests/Wallow.Web.Component.Tests/Pages/` (component tests)
- Create: `tests/Wallow.Web.Component.Tests/Layout/` (layout tests)

**Step 1: Create project file**

Same structure as Auth component tests but references `Wallow.Web.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <RootNamespace>Wallow.Web.Component.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="bunit" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="NSubstitute" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="NSubstitute" />
    <Using Include="Bunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Wallow.Web\Wallow.Web.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: Create `_Imports.razor`**

```razor
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Authorization
@using Bunit
@using Bunit.TestDoubles
```

**Step 3: Copy BlazorBlueprint stubs from Auth component tests**

Duplicate the stubs from Task 4. Same files, same content.

**Step 4: Create Web test base class**

Create `tests/Wallow.Web.Component.Tests/WebComponentTestBase.cs`:

```csharp
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Configuration;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests;

public abstract class WebComponentTestBase : TestContext
{
    protected readonly IAppRegistrationService MockAppService;
    protected readonly IOrganizationApiService MockOrgService;
    protected readonly IInquiryService MockInquiryService;
    protected readonly BrandingOptions TestBranding;

    protected WebComponentTestBase()
    {
        MockAppService = Substitute.For<IAppRegistrationService>();
        MockOrgService = Substitute.For<IOrganizationApiService>();
        MockInquiryService = Substitute.For<IInquiryService>();
        TestBranding = new BrandingOptions { AppName = "TestApp" };

        Services.AddSingleton(MockAppService);
        Services.AddSingleton(MockOrgService);
        Services.AddSingleton(MockInquiryService);
        Services.AddSingleton(TestBranding);

        // Add fake auth state for [Authorize] pages
        this.AddTestAuthorization()
            .SetAuthorized("testuser@test.com")
            .SetRoles("Admin");
    }
}
```

**Step 5: Create Apps page tests**

Create `tests/Wallow.Web.Component.Tests/Pages/AppsTests.cs`:

```csharp
using Bunit;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Models;

namespace Wallow.Web.Component.Tests.Pages;

public sealed class AppsTests : WebComponentTestBase
{
    [Fact]
    public void Renders_PageTitle()
    {
        MockAppService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns([]);

        IRenderedComponent<Apps> cut = RenderComponent<Apps>();

        cut.Markup.Should().Contain("My Apps");
    }

    [Fact]
    public void NoApps_ShowsEmptyState()
    {
        MockAppService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns([]);

        IRenderedComponent<Apps> cut = RenderComponent<Apps>();

        cut.Markup.Should().Contain("No apps yet");
        cut.Markup.Should().Contain("Register your first app");
    }

    [Fact]
    public void WithApps_ShowsTable()
    {
        MockAppService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns([
            new AppModel("app-1", "My App", "confidential", ["https://app.com"], DateTimeOffset.Parse("2026-01-15T00:00:00Z")),
            new AppModel("app-2", "Other App", "public", [], null)
        ]);

        IRenderedComponent<Apps> cut = RenderComponent<Apps>();

        cut.Markup.Should().Contain("My App");
        cut.Markup.Should().Contain("app-1");
        cut.Markup.Should().Contain("confidential");
        cut.Markup.Should().Contain("Other App");
        cut.Markup.Should().Contain("app-2");
    }

    [Fact]
    public void WithApps_ShowsRegisterNewAppLink()
    {
        MockAppService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns([]);

        IRenderedComponent<Apps> cut = RenderComponent<Apps>();

        cut.Markup.Should().Contain("/dashboard/apps/register");
    }
}
```

**Step 6: Create tests for remaining Web pages**

Follow the same pattern:

**RegisterAppTests.cs:**
- Renders form fields (display name, client type, redirect URIs, scopes)
- Validation errors on invalid input
- Successful submission shows result with client ID and secret
- Failed submission shows error

**InquiriesTests.cs:**
- Renders inquiry list
- Empty state handling

**OrganizationsTests.cs:**
- Lists organizations
- Empty state

**OrganizationDetailTests.cs:**
- Loads org by ID, shows details and member list
- Not found handling

**SettingsTests.cs:**
- Basic render test

**HomeTests.cs:**
- Basic render test

**DashboardLayoutTests.cs:**
- Sidebar navigation links present
- "My Apps", "Organizations", "Inquiries", "Settings" nav items

**PublicLayoutTests.cs:**
- Renders without auth

**RedirectToLoginTests.cs:**
- Triggers navigation to login URL

**Step 7: Run tests**

```bash
./scripts/run-tests.sh tests/Wallow.Web.Component.Tests
```

Expected: All tests pass.

**Step 8: Commit**

```bash
git add tests/Wallow.Web.Component.Tests/
git commit -m "test(web): add bunit component tests for all Web pages and layouts"
```

---

## Phase 4: Test Script Updates

### Task 6: Update run-tests.sh with new shorthands

**Files:**
- Modify: `scripts/run-tests.sh`

**Step 1: Add new module shorthands**

Add these cases to the `resolve_filter()` function in `scripts/run-tests.sh`:

```bash
auth-components) echo "$REPO_ROOT/tests/Wallow.Auth.Component.Tests" ;;
web)            echo "$REPO_ROOT/tests/Wallow.Web.Tests" ;;
web-components) echo "$REPO_ROOT/tests/Wallow.Web.Component.Tests" ;;
e2e)            echo "$REPO_ROOT/tests/Wallow.E2E.Tests" ;;
```

**Step 2: Add E2E exclusion filter for default runs**

When no module filter is specified (running all tests), add `--filter "Category!=E2E"` to exclude E2E tests:

```bash
if [[ -z "$PROJECT_PATH" ]]; then
    CMD+=(--filter "Category!=E2E")
fi
```

Add this after the `CMD` array is built but before execution.

**Step 3: Run the script to verify it works**

```bash
./scripts/run-tests.sh auth-components
./scripts/run-tests.sh web
./scripts/run-tests.sh web-components
```

**Step 4: Commit**

```bash
git add scripts/run-tests.sh
git commit -m "chore: add auth-components, web, web-components, and e2e shorthands to test script"
```

---

## Phase 5: E2E Tests (depends on all above)

### Task 7: Create Wallow.E2E.Tests project skeleton

**Files:**
- Create: `tests/Wallow.E2E.Tests/Wallow.E2E.Tests.csproj`
- Create: `tests/Wallow.E2E.Tests/Fixtures/DockerComposeFixture.cs`
- Create: `tests/Wallow.E2E.Tests/Fixtures/PlaywrightFixture.cs`
- Create: `tests/Wallow.E2E.Tests/PageObjects/LoginPage.cs`
- Create: `tests/Wallow.E2E.Tests/PageObjects/RegisterPage.cs`
- Create: `tests/Wallow.E2E.Tests/PageObjects/DashboardPage.cs`
- Create: `tests/Wallow.E2E.Tests/Flows/AuthFlowTests.cs`

**Step 1: Create project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.E2E.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Microsoft.Playwright" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
  </ItemGroup>

</Project>
```

**Step 2: Create DockerComposeFixture**

```csharp
using System.Diagnostics;

namespace Wallow.E2E.Tests.Fixtures;

public sealed class DockerComposeFixture : IAsyncLifetime
{
    private const string ComposeFile = "docker/docker-compose.test.yml";
    private static readonly string RepoRoot = FindRepoRoot();

    public string AuthBaseUrl { get; } = "http://localhost:5001";
    public string WebBaseUrl { get; } = "http://localhost:5002";
    public string ApiBaseUrl { get; } = "http://localhost:5000";
    public string MailpitApiUrl { get; } = "http://localhost:8025/api/v1";

    public async Task InitializeAsync()
    {
        // Check if containers are already running (local dev scenario)
        ProcessStartInfo checkInfo = new("docker", "compose -f " + ComposeFile + " ps --status running -q")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true
        };

        using Process? checkProcess = Process.Start(checkInfo);
        string output = await checkProcess!.StandardOutput.ReadToEndAsync();
        await checkProcess.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(output))
        {
            // Containers already running — skip startup
            return;
        }

        ProcessStartInfo startInfo = new("docker", $"compose -f {ComposeFile} up -d --wait")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? process = Process.Start(startInfo);
        await process!.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"docker compose up failed: {stderr}");
        }

        // Wait for health checks
        await WaitForHealthAsync(ApiBaseUrl + "/health", TimeSpan.FromSeconds(60));
        await WaitForHealthAsync(AuthBaseUrl, TimeSpan.FromSeconds(30));
        await WaitForHealthAsync(WebBaseUrl, TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        ProcessStartInfo stopInfo = new("docker", $"compose -f {ComposeFile} down -v")
        {
            WorkingDirectory = RepoRoot
        };

        using Process? process = Process.Start(stopInfo);
        await process!.WaitForExitAsync();
    }

    private static async Task WaitForHealthAsync(string url, TimeSpan timeout)
    {
        using HttpClient client = new();
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // Not ready yet
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Service at {url} did not become healthy within {timeout}");
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, ".git")) && dir != Path.GetPathRoot(dir))
        {
            dir = Directory.GetParent(dir)!.FullName;
        }
        return dir;
    }
}
```

**Step 3: Create PlaywrightFixture**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.Fixtures;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    public async Task<IPage> NewPageAsync()
    {
        IBrowserContext context = await Browser.NewContextAsync();
        return await context.NewPageAsync();
    }
}
```

**Step 4: Create collection definitions**

```csharp
namespace Wallow.E2E.Tests.Fixtures;

[CollectionDefinition(nameof(E2ETestCollection))]
public class E2ETestCollection : ICollectionFixture<DockerComposeFixture>, ICollectionFixture<PlaywrightFixture>
{
}
```

**Step 5: Create Page Object: LoginPage**

```csharp
using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class LoginPage(IPage page, string baseUrl)
{
    public async Task NavigateAsync()
    {
        await page.GotoAsync($"{baseUrl}/login");
    }

    public async Task FillEmailAsync(string email)
    {
        await page.FillAsync("#email", email);
    }

    public async Task FillPasswordAsync(string password)
    {
        await page.FillAsync("#password", password);
    }

    public async Task ClickSignInAsync()
    {
        await page.ClickAsync("button[type='submit']");
    }

    public async Task<string> GetErrorMessageAsync()
    {
        ILocator alert = page.Locator("[data-bb='alert'] >> text=Error, [data-bb='alertdescription']").First;
        return await alert.TextContentAsync() ?? "";
    }

    public async Task<bool> IsSignedInAsync()
    {
        return await page.Locator("text=You are now signed in").IsVisibleAsync();
    }

    public async Task LoginAsync(string email, string password)
    {
        await NavigateAsync();
        await FillEmailAsync(email);
        await FillPasswordAsync(password);
        await ClickSignInAsync();
    }
}
```

**Step 6: Create a basic auth flow test**

```csharp
using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.PageObjects;

namespace Wallow.E2E.Tests.Flows;

[Collection(nameof(E2ETestCollection))]
[Trait("Category", "E2E")]
public sealed class AuthFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright)
{
    [Fact]
    public async Task LoginPage_Loads()
    {
        IPage page = await playwright.NewPageAsync();
        LoginPage loginPage = new(page, docker.AuthBaseUrl);

        await loginPage.NavigateAsync();

        string title = await page.TitleAsync();
        title.Should().Contain("Sign In");
    }

    // Additional E2E tests: registration flow, forgot password, MFA, etc.
    // These depend on seeded test data and Mailpit integration.
    // Placeholder for future implementation.
}
```

**Step 7: Run tests**

```bash
./scripts/run-tests.sh e2e
```

Expected: Tests pass (or skip gracefully if Docker stack is not running).

**Step 8: Commit**

```bash
git add tests/Wallow.E2E.Tests/
git commit -m "test(e2e): add playwright E2E test project skeleton with docker compose fixture"
```

---

### Task 8: Create docker-compose.test.yml

**Files:**
- Create: `docker/docker-compose.test.yml`

**Step 1: Create the compose file**

```yaml
# Test-only compose file for E2E tests.
# Extends the base docker-compose.yml with API, Auth, and Web services.
name: wallow-test

include:
  - docker-compose.yml

services:
  api:
    build:
      context: ..
      dockerfile: src/Wallow.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=wallow;Username=wallow;Password=wallow
      - ConnectionStrings__Redis=valkey:6379
    depends_on:
      postgres:
        condition: service_healthy
      valkey:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 5s
      timeout: 3s
      retries: 10

  auth:
    build:
      context: ..
      dockerfile: src/Wallow.Auth/Dockerfile
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ApiBaseUrl=http://api:8080
    depends_on:
      api:
        condition: service_healthy

  web:
    build:
      context: ..
      dockerfile: src/Wallow.Web/Dockerfile
    ports:
      - "5002:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ServiceUrls__ApiUrl=http://api:8080
      - ServiceUrls__AuthUrl=http://auth:8080
    depends_on:
      api:
        condition: service_healthy
      auth:
        condition: service_started
```

> **Note:** This assumes Dockerfiles exist for the API, Auth, and Web projects. If they don't exist yet, they need to be created as a prerequisite. Check `src/Wallow.Api/Dockerfile`, `src/Wallow.Auth/Dockerfile`, `src/Wallow.Web/Dockerfile`. If missing, create standard ASP.NET Core multi-stage Dockerfiles.

**Step 2: Commit**

```bash
git add docker/docker-compose.test.yml
git commit -m "ci: add docker-compose.test.yml for E2E test environment"
```

---

## Phase 6: CI Pipeline Update

### Task 9: Update CI workflow for parallel build + E2E

**Files:**
- Modify: `.github/workflows/ci.yml` (or equivalent CI config file)

**Context:** Add parallel Docker build job alongside existing test job. Add E2E job gated on both.

**Step 1: Identify existing CI file**

Check for `.github/workflows/*.yml` files. The exact modification depends on the existing CI structure.

**Step 2: Add parallel Docker build job**

```yaml
  docker-build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build test images
        run: docker compose -f docker/docker-compose.test.yml build
```

**Step 3: Add E2E job gated on both**

```yaml
  e2e:
    needs: [test, docker-build]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Start test stack
        run: docker compose -f docker/docker-compose.test.yml up -d --wait
      - name: Install Playwright
        run: dotnet build tests/Wallow.E2E.Tests && pwsh tests/Wallow.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
      - name: Run E2E tests
        run: ./scripts/run-tests.sh e2e
      - name: Stop test stack
        if: always()
        run: docker compose -f docker/docker-compose.test.yml down -v
```

**Step 4: Commit**

```bash
git add .github/
git commit -m "ci: add parallel docker build and E2E test jobs"
```

---

## Summary of all commits

1. `chore: add bunit and playwright packages to central package management`
2. `test(auth): add remaining AuthApiClient and ClientBrandingApiClient service tests`
3. `test(web): add service unit tests for AppRegistration, Organization, and Inquiry services`
4. `test(auth): add bunit component tests for all Auth pages and layouts`
5. `test(web): add bunit component tests for all Web pages and layouts`
6. `chore: add auth-components, web, web-components, and e2e shorthands to test script`
7. `test(e2e): add playwright E2E test project skeleton with docker compose fixture`
8. `ci: add docker-compose.test.yml for E2E test environment`
9. `ci: add parallel docker build and E2E test jobs`

## Parallelization Guide

```
Task 1 (packages)
    ├── Task 2 (auth service tests) ──┐
    ├── Task 3 (web service tests) ───┤  can run in parallel
    ├── Task 4 (auth bunit tests) ────┤
    └── Task 5 (web bunit tests) ─────┘
                                       ↓
                              Task 6 (test script)
                                       ↓
                              Task 7 (E2E skeleton)
                              Task 8 (docker compose)  ← parallel
                                       ↓
                              Task 9 (CI pipeline)
```
