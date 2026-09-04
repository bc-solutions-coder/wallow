using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wallow.Inquiries.Domain.Errors;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Contracts.Setup;
using Wallow.Shared.Kernel.Errors;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// The failure sweep: one probe per way a request can fail, each asserting the unified problem
/// contract on the wire. Every body is <c>application/problem+json</c> with <c>type</c>
/// <c>about:blank</c>, a reason-phrase <c>title</c>, <c>status</c>, a catalogued <c>code</c>,
/// <c>traceId</c>, and a <c>detail</c> that is user-safe on 4xx and the one fixed sentence on 5xx;
/// <c>errors</c> appears only on validation problems; <c>instance</c>, <c>api</c> and
/// <c>version</c> never appear. The API sends X-Content-Type-Options: nosniff, so an error with
/// no body would make a browser navigation download a zero-byte file — the body is load-bearing.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ErrorContractTests : IDisposable
{
    private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    private static readonly string[] _neverPresent = ["instance", "api", "version", ProblemContract.ExceptionMember];

    private readonly WallowApiFactory _factory;
    private readonly WebApplicationFactory<Program> _withProbe;
    private readonly HttpClient _client;
    private readonly HttpClient _probeClient;
    private readonly ErrorCatalog _catalog;

    public ErrorContractTests(WallowApiFactory factory)
    {
        _factory = factory;
        _withProbe = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddControllers().AddApplicationPart(typeof(FailureProbeController).Assembly)));
        _client = factory.CreateClient();
        _probeClient = _withProbe.CreateClient();
        _catalog = factory.Services.GetRequiredService<ErrorCatalog>();
    }

    public void Dispose()
    {
        _probeClient.Dispose();
        _client.Dispose();
        _withProbe.Dispose();
    }

    [Fact]
    public async Task Unmatched_Path_Returns_404_Problem_Not_A_Challenge()
    {
        // Browsers send Accept: text/html,...,*/* on navigations; the writer ignores Accept anyway.
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/identity/setup");
        request.Headers.TryAddWithoutValidation("Accept", BrowserAccept);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemAsync(response, 404, SharedErrors.NotFound.Code);
    }

    [Fact]
    public async Task Wrong_Method_Returns_405_Problem()
    {
        // Authenticated: the framework's synthesized 405 endpoint carries no AllowAnonymous, so
        // an anonymous wrong-method request is challenged to 401 before the 405 is reached.
        using HttpRequestMessage request = new(HttpMethod.Delete, "/v1/identity/setup/status");
        AsAdmin(request);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        await AssertProblemAsync(response, 405, SharedErrors.MethodNotAllowed.Code);
    }

    [Fact]
    public async Task Unauthenticated_Protected_Endpoint_Returns_401_Problem()
    {
        HttpResponseMessage response = await _client.GetAsync("/v1/identity/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, SharedErrors.Unauthenticated.Code);
    }

    [Fact]
    public async Task Unauthenticated_Browser_Navigation_Gets_A_Body_Not_A_Download()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/v1/identity/users/me");
        request.Headers.TryAddWithoutValidation("Accept", BrowserAccept);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AssertProblemAsync(response, 401, SharedErrors.Unauthenticated.Code);
    }

    [Fact]
    public async Task Organizationless_Caller_On_A_Tenant_Endpoint_Returns_403_Problem()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/v1/identity/users");
        AsAdmin(request);
        request.Headers.TryAddWithoutValidation("X-Test-No-Organization", "true");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement body = await AssertProblemAsync(response, 403, SharedErrors.Forbidden.Code);
        body.GetProperty("detail").GetString().Should().Contain("organization");
    }

    [Fact]
    public async Task Automatic_Model_Validation_Returns_400_With_CamelCase_Errors()
    {
        HttpResponseMessage response = await _probeClient.PostAsJsonAsync(
            $"{FailureProbeController.ProbePath}/validate",
            new { branding = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement body = await AssertProblemAsync(
            response, 400, SharedErrors.ValidationFailed.Code, expectErrors: true);
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ValidationFailed.DefaultMessage);
        JsonElement errors = body.GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["name", "branding.displayName"]);
        errors.GetProperty("name").ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetProperty("name").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FluentValidation_Failure_Returns_400_With_CamelCase_Dotted_Errors()
    {
        HttpResponseMessage response = await _probeClient.PostAsync(
            $"{FailureProbeController.ProbePath}/fluent", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement body = await AssertProblemAsync(
            response, 400, SharedErrors.ValidationFailed.Code, expectErrors: true);
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ValidationFailed.DefaultMessage);
        JsonElement errors = body.GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(["branding.displayName", "name"]);
        errors.GetProperty("branding.displayName").EnumerateArray().Select(message => message.GetString())
            .Should().Equal("Display name is required.", "Display name must be shorter.");
    }

    [Fact]
    public async Task Failed_Result_Returns_The_Catalogued_Status_And_Code()
    {
        HttpResponseMessage response = await _probeClient.GetAsync(
            $"{FailureProbeController.ProbePath}/business-rule");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement body = await AssertProblemAsync(response, 422, InquiriesErrors.InvalidStatusTransition.Code);
        body.GetProperty("detail").GetString().Should().Be(InquiriesErrors.InvalidStatusTransition.DefaultMessage);
    }

    [Fact]
    public async Task Rate_Limited_Request_Returns_429_Problem()
    {
        using WebApplicationFactory<Program> tightened = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Registration:PermitLimit", "1"));
        using HttpClient client = tightened.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        // An empty body fails model validation, so the window is spent without creating anything.
        using HttpResponseMessage allowed = await client.PostAsJsonAsync("/v1/identity/organizations", new { });
        allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        HttpResponseMessage response = await client.PostAsJsonAsync("/v1/identity/organizations", new { });

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Contains("Retry-After").Should().BeTrue();
        await AssertProblemAsync(response, 429, SharedErrors.RateLimitExceeded.Code);
    }

    [Fact]
    public async Task Setup_Required_Returns_503_Problem_With_The_Generic_Detail()
    {
        using WebApplicationFactory<Program> setupRequired = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISetupStatusProvider>();
                services.AddSingleton<ISetupStatusProvider, SetupRequiredProvider>();
            }));
        using HttpClient client = setupRequired.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/v1/identity/users/me");
        AsAdmin(request);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        JsonElement body = await AssertProblemAsync(response, 503, SharedErrors.SetupRequired.Code);
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ServerError.DefaultMessage);
    }

    [Fact]
    public async Task Unhandled_Exception_Returns_500_With_The_Generic_Detail_And_No_Exception()
    {
        HttpResponseMessage response = await _probeClient.GetAsync($"{FailureProbeController.ProbePath}/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        JsonElement body = await AssertProblemAsync(response, 500, SharedErrors.ServerError.Code);
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ServerError.DefaultMessage);
        body.ToString().Should().NotContain("internal detail");
    }

    private static void AsAdmin(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer test-token");
        request.Headers.TryAddWithoutValidation("X-Test-User-Id", TestConstants.AdminUserId.ToString());
        request.Headers.TryAddWithoutValidation("X-Test-Roles", "admin");
    }

    /// <summary>
    /// Asserts the always-present members and the never-present ones, that <c>code</c> is the one
    /// expected and catalogued (the drift check: a code on the wire the catalog does not list is
    /// a bug wherever it was written), and returns the body for probe-specific assertions.
    /// </summary>
    private async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response,
        int expectedStatus,
        string expectedCode,
        bool expectErrors = false)
    {
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemContract.ContentType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Object);

        body.GetProperty("type").GetString().Should().Be(ProblemContract.BlankType);
        body.GetProperty("title").GetString().Should().Be(ProblemContract.TitleFor(expectedStatus));
        body.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.GetProperty(ProblemContract.CodeMember).GetString().Should().Be(expectedCode);
        body.GetProperty(ProblemContract.TraceIdMember).GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();

        _catalog.Entries.Select(entry => entry.Code).Should().Contain(
            expectedCode, "every code on the wire must be catalogued");

        foreach (string member in _neverPresent)
        {
            body.TryGetProperty(member, out _).Should().BeFalse("{0} is not part of the contract", member);
        }

        body.TryGetProperty("errors", out JsonElement errors).Should().Be(expectErrors);
        if (expectErrors)
        {
            errors.ValueKind.Should().Be(JsonValueKind.Object);
        }

        return body;
    }

    private sealed class SetupRequiredProvider : ISetupStatusProvider
    {
        public Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
